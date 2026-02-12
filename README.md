# NumStrata – Project Documentation

> **Codename:** Salt2 (Revamp)
> **Genre:** Puzzle / Math / Strategy / Mahjong-Solitaire
> **Platform:** Mobile (Android/iOS)
> **Engine:** Unity (C#)

---

## 1. TỔNG QUAN (OVERVIEW)

**NumStrata** là game giải đố tư duy kết hợp giữa tính toán học (Arithmetic) và cơ chế xếp lớp (Layering/Stacking) của Mahjong.

### Gameplay Loop (Vòng lặp cốt lõi)
1.  **Quan sát:** Người chơi nhìn vào một bàn cờ 3D giả lập (2.5D) với các ô số nằm chồng lên nhau.
2.  **Tính toán:** Chọn các ô số (Tiles) và toán tử (Operators) để đưa vào thanh công thức (Slots).
3.  **Giải quyết:** Hoàn thành phép tính đúng (`A + B = C`) để xóa các ô đó khỏi bàn cờ.
4.  **Quản lý:** Xử lý số dư (Remainder) trên băng chuyền (Conveyor) và tính toán chiến thuật để giải phóng các ô bị khóa bên dưới.

### Mục tiêu thiết kế (Design Pillars)
-   **Deep Logic:** Dễ chơi nhưng khó thắng (Easy to learn, hard to master).
-   **Clean & Deterministic:** Không có yếu tố may rủi vô lý (trừ mode Daily Challenge có kiểm soát). Mọi màn chơi đều phải giải được.
-   **Satisfying Feel:** Cảm giác "click" đầm tay, âm thanh giòn giã, chuyển động mượt mà (Cubic Ease-In).

---

## 2. CƠ CHẾ GAMEPLAY (CORE MECHANICS)

### 2.1. Hệ thống Ô (Tiles) & Tương tác
-   **Các loại Tile:**
    -   **Số dương:** `0` đến `9`.
    -   **Số âm:** `-1` đến `-9` (Visual: Màu đỏ/tím để phân biệt với dấu trừ).
    -   **Toán tử:** `+`, `-`, `x`, `/`.
    -   **Mystery Tile [?]:**
        -   Chỉ hiện giá trị thật khi người chơi click chọn.
        -   *Logic an toàn:* Nếu Slot ngay trước nó là dấu chia `/`, thuật toán Random sẽ **loại bỏ số 0** khỏi pool kết quả (tránh lỗi chia cho 0).
-   **Quy tắc Xếp chồng (Mahjong Layering):**
    -   Các ô có thể nằm đè lên nhau.
    -   **Blocking Rule:** Một ô được coi là **Locked (Bị khóa)** nếu nó bị một ô khác ở tầng trên che khuất (dù chỉ một phần nhỏ).
    -   Người chơi chỉ được chọn các ô **Unlocked**.

### 2.2. Logic Tính toán (The Calculator)
Hệ thống tính toán dựa trên 5 Slot: `[Slot 1] [Slot 2] [Slot 3] = [Slot 4] [Slot 5]`
-   **Quy tắc phép tính:**
    -   Thực hiện phép tính từ trái qua phải.
    -   **Cho phép kết quả âm:** (Ví dụ: `2 - 9 = -7`).
    -   **Phép chia:**
        -   Không được chia cho 0 (Game chặn hoặc thua ngay).
        -   Nếu phép chia có dư (Ví dụ: `7 / 3 = 2 dư 1`): Thương số (`2`) vào Slot kết quả, Số dư (`1`) tự động bay vào **Conveyor**.

### 2.3. Băng chuyền (The Conveyor)
-   Chứa các số dư sinh ra từ phép chia hoặc các số do người chơi "Trả về" (Return).
-   **Cơ chế Hàng đợi (Queue):**
    -   Conveyor có giới hạn (ví dụ: 7 ô).
    -   Nếu đầy, các số mới sẽ vào hàng đợi ẩn. Khi người chơi dùng bớt số trên Conveyor, số từ hàng đợi sẽ trôi ra.
    -   **Fail Condition:** Nếu hàng đợi đầy và Conveyor cũng đầy mà vẫn sinh thêm số -> Game Over (Tràn bộ nhớ).

---

## 3. CHẾ ĐỘ CHƠI (GAME MODES)

### 3.1. Chế độ theo Màn (Campaign / Level Mode)
-   **Level Design:** 100+ màn chơi được thiết kế sẵn.
-   **Dữ liệu:**
    -   *Giá trị (Values):* Cố định (để đảm bảo độ cân bằng thiết kế).
    -   *Vị trí (Positions):* **Ngẫu nhiên** dựa trên các điểm spawn point cho phép.
    -   *Tiêu chí (Criteria):* Có thể có yêu cầu phụ (Vd: Dùng ít nhất 2 phép nhân).

### 3.2. Thử thách Hàng ngày (Daily Challenge)
-   **Seed System:** Dùng `DDMMYYYY` làm Seed cho bộ sinh số ngẫu nhiên (RNG).
-   **Tính năng:** Toàn bộ người chơi trên server chơi cùng một cấu hình màn chơi mỗi ngày.
-   **Thuật toán sinh:** Sử dụng **Reverse Engineering Algorithm** (Tạo kết quả trước -> tách thành phép tính -> rải tile) để đảm bảo 100% màn chơi có lời giải.

### 3.3. Chế độ Vĩnh cửu (Endless / Column Mode)
-   **Layout:** Các cột số xếp dọc.
-   **Luật trọng lực (Gravity):**
    -   Người chơi chỉ được chọn ô **dưới cùng** (hoặc ô không bị chặn) của mỗi cột.
    -   Khi ô dưới biến mất, các ô trên sẽ rơi xuống.
-   **Thử thách:** Tốc độ rơi tăng dần hoặc độ khó của số tăng dần (nhiều số âm hơn).

---

## 4. ĐIỀU KIỆN THẮNG/THUA & DEADLOCK

### 4.1. Điều kiện Thắng (Win)
-   **Campaign/Daily:** Xóa sạch toàn bộ Tile trên Board và Conveyor (Clear Board).
-   **Endless:** Không có thắng, chỉ có High Score.

### 4.2. Điều kiện Thua (Lose)
1.  **Sai phép tính:** Nhập đáp án sai so với kết quả tính toán.
2.  **Lỗi toán học:** Cố tình chia cho 0.
3.  **Deadlock (Hết nước đi):** Không còn bất kỳ tổ hợp nào giữa Board (các ô Unlocked) và Conveyor có thể tạo thành phép tính đúng.
4.  **Người chơi đầu hàng:** Bấm nút Give Up.

### 4.3. Hệ thống Tự động phát hiện (Deadlock Detector)
Để tránh việc người chơi mất thời gian vô ích:
-   **Chức năng:** Một hàm chạy ngầm `CheckForPossibleMoves()`.
-   **Trigger:** Chạy mỗi khi trạng thái Board thay đổi (sau khi ăn số, sau khi rơi).
-   **Logic:** Quét tất cả các ô khả dụng -> Thử tổ hợp 3-slot -> Nếu count = 0 -> **Báo Deadlock**.
-   **UX:** Khi phát hiện Deadlock -> Hiện Popup cảnh báo -> Gợi ý mua item **Shuffle** hoặc **Spawn Number**.

---

## 5. HỆ THỐNG KINH TẾ & TRỢ GIÚP (ECONOMY)

### 5.1. Tiền tệ
-   **Gold (G):** Kiếm được qua màn chơi, xem Ads, hoặc nạp IAP.

### 5.2. Kỹ năng bổ trợ (Helpers)
*Mỗi kỹ năng giới hạn dùng 3 lần/màn.*

| Tên Skill | Chức năng | Giá (G) |
| :--- | :--- | :--- |
| **Undo** | Quay lại 1 bước đi trước đó. | 10 |
| **Shuffle** | Tráo đổi vị trí toàn bộ các ô số (trừ ô bị khóa đặc biệt). | 30 |
| **Return** | Thu hồi các ô đang nằm trên Slot về Conveyor. | 20 |
| **Delete** | Xóa vĩnh viễn 1 ô bất kỳ (Board hoặc Conveyor). | 50 |
| **Spawn #** | **Ultimate:** Người chơi chọn 1 số cụ thể để sinh ra ngay lập tức. | 100 |

---

## 6. KIẾN TRÚC KỸ THUẬT (TECHNICAL ARCHITECTURE)

### 6.1. Design Patterns
-   **Command Pattern:** Quản lý hành động (`SelectTile`, `SubmitEquation`) để hỗ trợ Undo/Redo.
-   **Observer Pattern (Event Bus):** Hệ thống sự kiện (`OnGameWin`, `OnDeadlockDetected`, `OnGoldChange`) giúp UI tách biệt hoàn toàn khỏi Logic.
-   **Singleton:** `GameManager`, `SoundManager`.

### 6.2. Cấu trúc thư mục (Folder Structure)
```text
Assets/_Game/
├── Scripts/
│   ├── Core/           (GameManager, GameState, DeadlockDetector)
│   ├── Architecture/   (Command, EventBus)
│   ├── Gameplay/       (Tile, Slot, Board, Conveyor, Calculator)
│   ├── Data/           (LevelDataSO, GameConfigSO)
│   ├── Generators/     (LevelGenerator, ReverseSolver)
│   └── UI/             (Popups, HUD)
├── Resources/Levels/   (JSON/SO)
└── Prefabs/