using System;
using System.IO;
using UnityEngine;

namespace NumStrata.Data
{
    public class LocalDataManager : MonoBehaviour
    {
        public static LocalDataManager Instance { get; private set; }

        private string saveFilePath;
        private string saveBackupPath;
        private string saveTempPath;
        private string sessionResumeFilePath;

        private bool playerDirty;
        private bool levelCompletionHandledThisRun;

        public PlayerData CurrentPlayer { get; private set; }

        public event Action OnSyncCompleted;

        public string PlayerSaveFilePath => saveFilePath;
        public string SessionResumeFilePath => sessionResumeFilePath;

        public float SoundVolume
        {
            get => PlayerPrefs.GetFloat("SoundVolume", 1f);
            set { PlayerPrefs.SetFloat("SoundVolume", value); PlayerPrefs.Save(); }
        }

        public float MusicVolume
        {
            get => PlayerPrefs.GetFloat("MusicVolume", 1f);
            set { PlayerPrefs.SetFloat("MusicVolume", value); PlayerPrefs.Save(); }
        }

        public string Language
        {
            get => PlayerPrefs.GetString("Language", "vi");
            set { PlayerPrefs.SetString("Language", value); PlayerPrefs.Save(); }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Chỉ hủy script này nếu đã có Instance khác, 
                // KHÔNG hủy toàn bộ gameObject vì nó có thể chứa các script UI khác (GameManager)
                Destroy(this); 
                return;
            }
            Instance = this;
            
            // Nếu đây là GameManager chứa nhiều UI scripts, cân nhắc có nên DontDestroyOnLoad không.
            // Tạm thời giữ nguyên để không làm hỏng logic lưu data của bạn.
            DontDestroyOnLoad(gameObject);

            string root = Application.persistentDataPath;
            saveFilePath = Path.Combine(root, "PlayerData.json");
            saveBackupPath = Path.Combine(root, "PlayerData.bak");
            saveTempPath = Path.Combine(root, "PlayerData.json.tmp");
            sessionResumeFilePath = Path.Combine(root, "SessionResume.json");

            LoadData();
            EnsureCloudSyncManager();
        }

        private void EnsureCloudSyncManager()
        {
            if (CloudSyncManager.Instance != null) return;

            var go = new GameObject("CloudSyncManager");
            go.AddComponent<CloudSyncManager>();
            Debug.Log("[LocalDataManager] Created CloudSyncManager.");
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                FlushPlayerDataIfDirty();
            }
        }

        private void OnApplicationQuit()
        {
            FlushPlayerDataIfDirty();
        }

        public void MarkPlayerDirty()
        {
            if (CurrentPlayer == null) return;

            playerDirty = true;
            CurrentPlayer.lastModifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            CurrentPlayer.isDirtyCloud = true;
        }

        public void FlushPlayerData()
        {
            if (CurrentPlayer == null) return;

            EnsureCampaignValid(CurrentPlayer);
            WritePlayerFileAtomic();
            playerDirty = false;
            Debug.Log($"[LocalDataManager] Player data saved: {saveFilePath}");

            // Đồng bộ lên Cloud nếu flag dirty được bật
            if (CurrentPlayer.isDirtyCloud && CloudSyncManager.Instance != null)
            {
                CloudSyncManager.Instance.PushToCloud();
            }
        }

        public void UpdatePlayerFromCloud(PlayerData remotePlayer)
        {
            CurrentPlayer = remotePlayer;
            CurrentPlayer.isDirtyCloud = false;
            WritePlayerFileAtomic();
            playerDirty = false;
            Debug.Log("[LocalDataManager] Player data updated from cloud sync.");
            OnSyncCompleted?.Invoke();
        }

        public void FlushPlayerDataIfDirty()
        {
            if (playerDirty)
            {
                FlushPlayerData();
            }
        }

        [Obsolete("Use MarkPlayerDirty + FlushPlayerData at milestones.")]
        public void SaveData()
        {
            MarkPlayerDirty();
            FlushPlayerData();
        }

        public void LoadData()
        {
            if (TryLoadPlayerFromFile(saveFilePath))
            {
                Debug.Log("[LocalDataManager] Loaded player save.");
                return;
            }

            if (File.Exists(saveBackupPath) && TryLoadPlayerFromFile(saveBackupPath))
            {
                Debug.LogWarning("[LocalDataManager] Primary save failed; restored from backup.");
                MarkPlayerDirty();
                FlushPlayerData();
                return;
            }

            CreateDefaultPlayer();
            WritePlayerFileAtomic();
            playerDirty = false;
            Debug.Log("[LocalDataManager] Created new player profile.");
        }

        private bool TryLoadPlayerFromFile(string path)
        {
            if (!File.Exists(path)) return false;

            try
            {
                string json = File.ReadAllText(path);
                PlayerData loaded = JsonUtility.FromJson<PlayerData>(json);
                if (loaded == null) return false;

                MigratePlayerData(loaded);
                CurrentPlayer = loaded;
                playerDirty = false;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalDataManager] Failed to load '{path}': {ex.Message}");
                return false;
            }
        }

        private void MigratePlayerData(PlayerData data)
        {
            if (data.shield == null)
            {
                data.shield = new PlayerShield { hasShield = true };
            }

            // V0 → V1: không có "campaign" block, chỉ có campaignProgress[] legacy
            if (data.saveVersion <= 0)
            {
                data.saveVersion = SaveSchemaVersions.PlayerDataVersion;
                data.campaign = MigrateCampaignFromLegacy(data);
                Debug.Log($"[LocalDataManager] Migrated legacy save → campaign index {data.campaign.currentLevelIndex}");
            }

            if (data.campaign == null)
            {
                data.campaign = new CampaignSaveData();
            }

            if (data.daily == null)
            {
                data.daily = new DailySaveData();
            }

            CampaignSession.NormalizeCampaignPointer(data.campaign);
        }

        private static CampaignSaveData MigrateCampaignFromLegacy(PlayerData data)
        {
            var campaign = new CampaignSaveData();

            if (data.campaignProgress == null || data.campaignProgress.Count == 0)
            {
                return campaign;
            }

            int inProgressIndex = 0;
            int highestClearedIndex = 0;

            foreach (LegacyLevelProgress p in data.campaignProgress)
            {
                if (p == null || string.IsNullOrEmpty(p.levelId)) continue;

                int idx = CampaignSession.LevelIdToIndex(p.levelId);
                if (idx <= 0) continue;

                if (p.state == "in_progress" && idx > inProgressIndex)
                {
                    inProgressIndex = idx;
                }
                else if (p.state == "cleared" && idx > highestClearedIndex)
                {
                    highestClearedIndex = idx;
                }
            }

            if (inProgressIndex > 0)
            {
                campaign.currentLevelIndex = inProgressIndex;
                campaign.hasActiveRun = false; // bắt đầu mới; resume cũ không còn hợp lệ
            }
            else if (highestClearedIndex > 0)
            {
                campaign.currentLevelIndex = highestClearedIndex + 1;
                campaign.hasActiveRun = false;
            }

            campaign.currentLevelId = CampaignSession.IndexToLevelId(campaign.currentLevelIndex);
            return campaign;
        }

        private void CreateDefaultPlayer()
        {
            CurrentPlayer = new PlayerData
            {
                saveVersion = SaveSchemaVersions.PlayerDataVersion,
                playerId = Guid.NewGuid().ToString(),
                displayName = "Player_" + UnityEngine.Random.Range(1000, 9999),
                createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                lastModifiedAt = 0,
                isDirtyCloud = false,
                gold = 0,
                totalStreak = 0,
                shield = new PlayerShield { hasShield = true },
                campaign = new CampaignSaveData
                {
                    currentLevelIndex = 1,
                    currentLevelId = CampaignSession.IndexToLevelId(1),
                    hasActiveRun = false
                },
                daily = new DailySaveData()
            };
        }

        private void WritePlayerFileAtomic()
        {
            bool pretty = Debug.isDebugBuild;
            string json = JsonUtility.ToJson(CurrentPlayer, pretty);

            try
            {
                if (File.Exists(saveFilePath))
                {
                    File.Copy(saveFilePath, saveBackupPath, true);
                }

                File.WriteAllText(saveTempPath, json);
                if (File.Exists(saveFilePath))
                {
                    File.Delete(saveFilePath);
                }
                File.Move(saveTempPath, saveFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalDataManager] Atomic save failed: {ex.Message}");
                if (File.Exists(saveTempPath))
                {
                    try { File.Delete(saveTempPath); } catch { /* ignored */ }
                }
            }
        }

        private static void EnsureCampaignValid(PlayerData data)
        {
            if (data.campaign == null)
            {
                data.campaign = new CampaignSaveData();
            }
            CampaignSession.NormalizeCampaignPointer(data.campaign);
        }

        public int GetCurrentLevelIndex()
        {
            if (CurrentPlayer?.campaign == null) return 1;
            return Mathf.Max(1, CurrentPlayer.campaign.currentLevelIndex);
        }

        public string GetCurrentLevelId()
        {
            if (CurrentPlayer?.campaign == null) return CampaignSession.IndexToLevelId(1);
            EnsureCampaignValid(CurrentPlayer);
            return CurrentPlayer.campaign.currentLevelId;
        }

        public void BeginCampaignRun(string levelId)
        {
            if (CurrentPlayer == null) return;

            EnsureCampaignValid(CurrentPlayer);
            if (!string.Equals(CurrentPlayer.campaign.currentLevelId, levelId, StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"[LocalDataManager] BeginCampaignRun mismatch. Expected '{CurrentPlayer.campaign.currentLevelId}', got '{levelId}'.");
            }

            levelCompletionHandledThisRun = false;
            CurrentPlayer.campaign.hasActiveRun = true;
            MarkPlayerDirty();
            FlushPlayerData();
        }

        public void CompleteCampaignLevel()
        {
            if (CurrentPlayer == null || levelCompletionHandledThisRun) return;

            // Kiểm tra xem có đang chơi Challenge Mode không (set từ ChallengeTabManager)
            bool isChallenge = PlayerPrefs.GetInt("IsChallengeMode", 0) == 1;
            if (isChallenge)
            {
                // Logic hoàn thành Challenge theo hệ thống tích lũy (Catch-up)
                // 1. Xác định ngày hôm nay trong tuần (1=Mon, 7=Sun)
                int today = (int)DateTime.Now.DayOfWeek;
                if (today == 0) today = 7;

                // 2. Tìm ngày đầu tiên chưa hoàn thành tính từ đầu tuần cho đến hết ngày hôm nay
                int dayToMark = -1;
                for (int d = 1; d <= today; d++)
                {
                    if (!CurrentPlayer.daily.completedDays.Contains(d))
                    {
                        dayToMark = d;
                        break;
                    }
                }

                // 3. Nếu tìm thấy ngày hợp lệ (chưa chơi trong quá khứ hoặc hôm nay)
                if (dayToMark != -1)
                {
                    CurrentPlayer.daily.completedDays.Add(dayToMark);
                    CurrentPlayer.daily.completedDays.Sort(); // Sắp xếp lại danh sách ngày
                    
                    CurrentPlayer.totalStreak++;
                    CurrentPlayer.daily.currentStreakCount = CurrentPlayer.daily.completedDays.Count;
                    
                    Debug.Log($"[LocalDataManager] Challenge Day {dayToMark} marked. Streak increased! Total Streak: {CurrentPlayer.totalStreak}");
                }
                else
                {
                    Debug.Log("[LocalDataManager] All available challenge days (up to today) are already completed. Streak not increased.");
                }

                // Sau khi thắng Challenge, chuẩn bị quay về Tab Challenge
                PlayerPrefs.SetString("TargetTabName", "Challenge");
                PlayerPrefs.Save();
            }
            else
            {
                // Logic Campaign cũ
                EnsureCampaignValid(CurrentPlayer);
                CurrentPlayer.campaign.currentLevelIndex++;
                CurrentPlayer.campaign.currentLevelId = CampaignSession.IndexToLevelId(CurrentPlayer.campaign.currentLevelIndex);
                Debug.Log($"[LocalDataManager] Campaign Level Cleared! Now at {CurrentPlayer.campaign.currentLevelId}");

                // Sau khi thắng Campaign, chuẩn bị quay về Tab Home
                PlayerPrefs.SetString("TargetTabName", "Home");
                PlayerPrefs.Save();
            }

            levelCompletionHandledThisRun = true;
            CurrentPlayer.campaign.hasActiveRun = false;

            DeleteSessionResume();
            MarkPlayerDirty();
            FlushPlayerData();
        }

        public void FailCampaignLevel()
        {
            if (CurrentPlayer == null) return;

            bool isChallenge = PlayerPrefs.GetInt("IsChallengeMode", 0) == 1;
            string modeName = isChallenge ? "Challenge" : "Campaign";

            // Chuẩn bị quay về đúng Tab khi người chơi nhấn Home từ màn hình thua
            PlayerPrefs.SetString("TargetTabName", isChallenge ? "Challenge" : "Home");
            PlayerPrefs.Save();

            CurrentPlayer.campaign.hasActiveRun = false;
            DeleteSessionResume();
            MarkPlayerDirty();
            FlushPlayerData();
            Debug.Log($"[LocalDataManager] {modeName} Level failed. Retry {GetCurrentLevelId()}.");
        }

        public void AbandonCampaignRun()
        {
            if (CurrentPlayer == null) return;

            CurrentPlayer.campaign.hasActiveRun = false;
            DeleteSessionResume();
            MarkPlayerDirty();
            FlushPlayerData();
            Debug.Log($"[LocalDataManager] Run abandoned at {GetCurrentLevelId()}.");
        }

        public void SaveSessionResume(SessionResume resume)
        {
            if (resume == null || CurrentPlayer == null) return;

            string currentId = GetCurrentLevelId();
            if (!string.IsNullOrEmpty(resume.activeLevelId) &&
                !string.Equals(resume.activeLevelId, currentId, StringComparison.Ordinal))
            {
                Debug.LogWarning("[LocalDataManager] Resume level mismatch; skip save.");
                return;
            }

            resume.saveVersion = SaveSchemaVersions.SessionResumeVersion;
            resume.activeLevelId = currentId;
            resume.savedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            try
            {
                bool pretty = Debug.isDebugBuild;
                string json = JsonUtility.ToJson(resume, pretty);
                File.WriteAllText(sessionResumeFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalDataManager] SaveSessionResume failed: {ex.Message}");
            }
        }

        public SessionResume LoadSessionResume()
        {
            if (!File.Exists(sessionResumeFilePath)) return null;

            try
            {
                string json = File.ReadAllText(sessionResumeFilePath);
                SessionResume resume = JsonUtility.FromJson<SessionResume>(json);
                if (resume == null) return null;

                string currentId = GetCurrentLevelId();
                if (!string.Equals(resume.activeLevelId, currentId, StringComparison.Ordinal))
                {
                    Debug.LogWarning("[LocalDataManager] Stale resume; deleting.");
                    DeleteSessionResume();
                    return null;
                }

                return resume;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalDataManager] LoadSessionResume failed: {ex.Message}");
                DeleteSessionResume();
                return null;
            }
        }

        public void DeleteSessionResume()
        {
            if (!File.Exists(sessionResumeFilePath)) return;

            try
            {
                File.Delete(sessionResumeFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalDataManager] DeleteSessionResume failed: {ex.Message}");
            }
        }

        public void AddGold(int amount)
        {
            if (CurrentPlayer == null) return;
            CurrentPlayer.gold += amount;
            MarkPlayerDirty();
        }

        public void UseShield()
        {
            if (CurrentPlayer == null || CurrentPlayer.shield == null || !CurrentPlayer.shield.hasShield)
            {
                return;
            }

            CurrentPlayer.shield.hasShield = false;
            CurrentPlayer.shield.lastShieldConsumedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            CurrentPlayer.shield.nextShieldRegenAt = CurrentPlayer.shield.lastShieldConsumedAt + 604800;
            MarkPlayerDirty();
        }
    }
}
