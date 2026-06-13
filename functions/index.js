const { onSchedule } = require("firebase-functions/v2/scheduler");
const { onDocumentUpdated } = require("firebase-functions/v2/firestore");
const { onRequest } = require("firebase-functions/v2/https");
const admin = require("firebase-admin");

admin.initializeApp();
const db = admin.firestore();

// Hàm dùng chung để tính toán và cập nhật bảng xếp hạng
async function updateRankings(db) {
    console.log("Starting ranking update...");

    // 1. Fetch Campaign Top 50
    const campaignQuery = await db.collection("users")
        .orderBy("currentLevelIndex", "desc")
        .limit(50)
        .get();
        
    const campaignTop50 = [];
    campaignQuery.forEach(doc => {
        const data = doc.data();
        if (data.displayName && data.displayName.startsWith("Player_")) return;
        
        campaignTop50.push({
            id: doc.id,
            displayName: data.displayName || "Unknown Player",
            avatarUrl: data.avatarUrl || "",
            score: data.currentLevelIndex || 0
        });
    });

    // 2. Fetch Streak Top 50
    const streakQuery = await db.collection("users")
        .orderBy("totalStreak", "desc")
        .limit(50)
        .get();
        
    const streakTop50 = [];
    streakQuery.forEach(doc => {
        const data = doc.data();
        if (data.displayName && data.displayName.startsWith("Player_")) return;
        
        streakTop50.push({
            id: doc.id,
            displayName: data.displayName || "Unknown Player",
            avatarUrl: data.avatarUrl || "",
            score: data.totalStreak || 0
        });
    });

    // 3. Save to system/rankings
    await db.collection("system").doc("rankings").set({
        campaign: campaignTop50,
        streak: streakTop50,
        lastUpdated: admin.firestore.FieldValue.serverTimestamp()
    });

    console.log("Ranking updated successfully!");
}

// 1. Job định kỳ chạy mỗi 1 tiếng (Fallback an toàn)
exports.updateRankingCron = onSchedule("every 1 hours", async (event) => {
    try {
        await updateRankings(db);
    } catch (error) {
        console.error("Cron Error updating ranking:", error);
    }
});

// 2. Trigger Realtime chỉ chạy khi có thay đổi ảnh hưởng Top 50
exports.onUserUpdated = onDocumentUpdated("users/{userId}", async (event) => {
    const before = event.data.before.data();
    const after = event.data.after.data();

    // Lọc bỏ tài khoản chưa liên kết (tài khoản Khách)
    if (after.displayName && after.displayName.startsWith("Player_")) return;

    // Kiểm tra xem điểm có thực sự thay đổi không
    const campaignScoreChanged = before.currentLevelIndex !== after.currentLevelIndex;
    const streakScoreChanged = before.totalStreak !== after.totalStreak;

    if (!campaignScoreChanged && !streakScoreChanged) return;

    try {
        // Đọc bảng xếp hạng hiện tại để xem điểm "sàn" của Top 50
        const rankingsDoc = await db.collection("system").doc("rankings").get();
        let minCampaign = 0;
        let minStreak = 0;
        let inCampaignTop = false;
        let inStreakTop = false;

        if (rankingsDoc.exists) {
            const data = rankingsDoc.data();
            const campTop = data.campaign || [];
            const strkTop = data.streak || [];
            
            if (campTop.length >= 50) {
                minCampaign = campTop[campTop.length - 1].score;
            }
            if (strkTop.length >= 50) {
                minStreak = strkTop[strkTop.length - 1].score;
            }
            
            inCampaignTop = campTop.some(u => u.id === event.params.userId);
            inStreakTop = strkTop.some(u => u.id === event.params.userId);
        }

        // Tính toán xem người dùng có lọt vào Top 50 hoặc đang ở Top 50 mà đổi điểm không
        const shouldUpdateCampaign = campaignScoreChanged && (inCampaignTop || after.currentLevelIndex >= minCampaign);
        const shouldUpdateStreak = streakScoreChanged && (inStreakTop || after.totalStreak >= minStreak);

        // Nếu ảnh hưởng đến Top 50 thì mới gọi hàm update
        if (shouldUpdateCampaign || shouldUpdateStreak) {
            console.log(`User ${event.params.userId} score changed. Triggering realtime ranking update.`);
            await updateRankings(db);
        }
    } catch (error) {
        console.error("Trigger Error updating ranking:", error);
    }
});

// 3. Job dọn dẹp tài khoản khách (Anonymous) không hoạt động quá 30 ngày
exports.cleanupAnonymousUsers = onSchedule("every 24 hours", async (event) => {
    console.log("Starting cleanup of old anonymous users...");
    const thirtyDaysAgo = Date.now() - 30 * 24 * 60 * 60 * 1000;
    let deletedCount = 0;

    async function deleteInactiveAnonymousUsers(nextPageToken) {
        const listUsersResult = await admin.auth().listUsers(1000, nextPageToken);
        const deletePromises = [];

        listUsersResult.users.forEach((userRecord) => {
            // Kiểm tra xem có phải tài khoản khách (không có provider) và đã lâu không hoạt động
            const isAnonymous = userRecord.providerData.length === 0;
            const lastSignInTime = new Date(userRecord.metadata.lastSignInTime).getTime();
            const creationTime = new Date(userRecord.metadata.creationTime).getTime();
            const inactiveTime = lastSignInTime || creationTime;

            if (isAnonymous && inactiveTime < thirtyDaysAgo) {
                deletePromises.push(
                    admin.auth().deleteUser(userRecord.uid)
                        .then(() => {
                            deletedCount++;
                        })
                        .catch((err) => {
                            console.error(`Error deleting user ${userRecord.uid}:`, err);
                        })
                );
            }
        });

        await Promise.all(deletePromises);

        if (listUsersResult.pageToken) {
            await deleteInactiveAnonymousUsers(listUsersResult.pageToken);
        }
    }

    try {
        await deleteInactiveAnonymousUsers();
        console.log(`Cleanup finished. Deleted ${deletedCount} inactive anonymous users.`);
    } catch (error) {
        console.error("Error cleaning up anonymous users:", error);
    }
});

// 4. HTTP Trigger để kích hoạt lập tức việc cập nhật bảng xếp hạng (phục vụ Debug)
exports.triggerUpdateRankingHttp = onRequest(async (req, res) => {
    console.log("HTTP Trigger Update Rankings called.");
    try {
        await updateRankings(db);
        res.status(200).send("Rankings updated successfully via HTTP trigger!");
    } catch (error) {
        console.error("HTTP Trigger Error updating ranking:", error);
        res.status(500).send("Error updating ranking: " + error.message);
    }
});
