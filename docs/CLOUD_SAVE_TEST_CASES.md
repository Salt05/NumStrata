# Test cases — Cloud Save System

**Môi trường:** Unity Client + Firebase Console  
**Dữ liệu Cloud:** Firestore Collection `users/`

---

## Phase 5.1 — Auth & Initial Sync

### TC 5.1.1 — Anonymous Authentication
**Mục tiêu:** Người chơi tự động có ID mà không cần login.
1. Mở game lần đầu.
2. Kiểm tra log `[CloudSyncManager] Authenticated as: <UID>`.
**Pass:** Firebase Console xuất hiện 1 user mới trong phần Authentication.

### TC 5.1.2 — New Device Pull
**Mục tiêu:** Tải data từ Cloud về máy mới.
1. Máy A chơi đến Level 5, Gold 500 -> Cloud đã nhận.
2. Mở máy B (cùng UID), mới cài game.
3. Mở game.
**Pass:** UI Level hiển thị "Level 5", Gold hiển thị 500.

---

## Phase 5.2 — Conflict & Dirty Sync

### TC 5.2.1 — Basic Push (Dirty Flag)
**Mục tiêu:** Chỉ đẩy dữ liệu khi có thay đổi.
1. Chơi 1 màn, nhận Gold.
2. Thoát về Home.
**Pass:** Firebase Console cập nhật `lastModifiedAt` và `gold` ngay khi về Home.

### TC 5.2.2 — Conflict Resolution (Latest Win)
**Mục tiêu:** Ưu tiên bản save mới nhất.
1. **Offline:** Chỉnh local data lên Level 10 (lúc 10:00).
2. **Online thiết bị khác:** Chơi lên Level 12 (lúc 10:15) -> Cloud nhận Level 12.
3. Bật mạng thiết bị 1 -> Sync.
**Pass:** Thiết bị 1 tự cập nhật lên Level 12 (vì 10:15 > 10:00).

---

## Phase 5.3 — Edge Cases

### TC 5.3.1 — Loss of Internet
1. Tắt mạng, chơi thắng 1 màn.
2. Kiểm tra file local (`isDirtyCloud` phải bằng `true`).
3. Bật lại mạng.
**Pass:** Sau khi về Home, dữ liệu được đẩy lên Cloud và `isDirtyCloud` trở về `false`.
