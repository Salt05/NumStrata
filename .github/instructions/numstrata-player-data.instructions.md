---
name: numstrata-player-data
description: Dùng khi làm việc với cấu trúc dữ liệu người chơi, tiến trình (progress) và quản lý tài nguyên trong game NumStrata. Đặc biệt liên quan đến Firebase/JSON structures.
applyTo: ["**/Player*.cs", "**/Data*.cs", "**/Economy*.cs"]
---

# Hướng dẫn xử lý Player Data - NumStrata

1. **PlayerData (Hồ sơ người chơi)**:
   - Phân biệt giữa người chơi mới và người chơi đã có dữ liệu.
   - Các trường bắt buộc: `playerId`, `gold`, `totalStreak`, `streakIconId`, `shield`.
2. **Campaign Progress**:
   - Lưu trữ state: `not_started`, `in_progress`, `cleared`, `failed`.
   - Lưu trữ `attempts` và `helperUses`.
3. **Daily Challenge Progress**:
   - Tuân thủ Streak system (7 ngày/tuần).
   - Reset Streak nếu không hoàn thành đủ 7 ngày mà không có Shield bảo vệ.
4. **Đồng bộ Dữ liệu**:
   - Dùng Cloud Firestore làm nguồn chính, đảm bảo tương thích mapping dữ liệu về JSON ở client (Unity). Cập nhật Data locally trước rồi mới sync lên mạng.