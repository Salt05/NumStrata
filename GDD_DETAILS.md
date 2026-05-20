# NumStrata - Game Design Document (GDD)

> Codename: NumStrata
> Genre: Puzzle / Math / Strategy / Mahjong-Solitaire
> Platform: Mobile (Android/iOS)
> Engine: Unity (C#)
> Doc version: v1.0 (Locked Rules)

---

## 1) Tổng quan sản phẩm (Product Overview)

NumStrata là game giải đố kết hợp tính toán số học và cơ chế chồng lớp kiểu Mahjong. Người chơi không chỉ giải phép tính đúng mà còn phải quản trị các tài nguyên số hợp lý để chiến thắng.

NumStrata combines arithmetic puzzle solving with Mahjong-like layered access. The player must solve valid equations while managing number flow from conveyor.

### 1.1 Core Loop

1. Quan sát board 2.5D và xác định tile đang mở khóa.
2. Chọn số và toán tử vào 4-5 slot công thức.
3. Nộp phép tính đúng để xóa tile và giải phóng lớp phía dưới.
4. Quản lý phần dư chia và tile trả về trên conveyor.
5. Lặp cho đến khi clear board hoặc thất bại.

## 1.2 Trạng thái triển khai hiện tại (Code Status - 2026-05-20)

### Đã triển khai trong code

- **Hệ thống UI Dimmer & Kính mờ (Frosted Glass Blur)**:
  - Triển khai `UI_BackgroundBlur.shader` với thuật toán 25-tap blur dùng `GrabPass` thay vì chỉ làm tối màu nền.
  - Quản lý tập trung qua `PauseManager.Instance.ToggleDimmer()`, tự động nội suy mượt độ nhòe và độ tối bằng Coroutine, tối ưu việc không ghi đè vào Asset file (sử dụng runtime material instancing).
- **Hệ thống Phân lớp Hiển thị (Sorting Order System)**:
  - Áp dụng công thức tính Order chuẩn cho góc nhìn isometric/2.5D: `Value = ((Row-1)/2 * TotalLayer) + Layer + 1`.
  - Cơ chế **Thừa hưởng Order (Sorting Inheritance)**: Khi click lấy đi một Tile, nó sẽ truyền `sortingOrder` hiện tại cho các Tile nó đang đè bên dưới (nếu Tile đó không còn bị ai khác đè), đảm bảo hiệu ứng "lớp dưới nổi lên trên" mượt mà không bị lỗi Z-fighting.
- **Hệ thống Helper cải tiến UI (Override Sorting)**:
  - Tile mẫu trong Popup Helper được ép `sortingOrder = 1000` và gán `GraphicRaycaster` để đảm bảo luôn hiển thị trên cùng và tương tác được khi Dimmer đang bật.
  - Khi người chơi chọn Tile từ Helper, bản sao bay về Conveyor sẽ tự động hạ `sortingOrder` xuống **10**.
- **Quản lý Scene (Pause Menu)**:
  - Nút Home trong Pause Manager đã được nạp logic chuyển Scene linh hoạt qua biến `homeSceneName` (mặc định "MainMenu").
- **Vệ sinh Dự án (Project Cleanup)**:
  - Gỡ bỏ hoàn toàn bộ công cụ **Unity AI Assistant** và các Project liên quan để giảm tải tài nguyên.
  - Gỡ bỏ thư viện **Nova UI** và chuyển hướng sang sử dụng hệ thống UGUI/Canvas chuẩn của Unity kết hợp với Overlap Sorting thủ công.
- **Hệ thống đếm Tile tối ưu (TileCounter)**:
  - `TileCounter` sử dụng kiến trúc kích hoạt theo sự kiện (Event-Driven) thay vì quét vòng lặp (`Update`/`InvokeRepeating`) để tối ưu hiệu năng.
  - Giao diện TextMeshPro tự động cập nhật số lượng khi: Level load xong, Helper sinh thêm Tile, Helper xóa Tile, hoặc kết thúc giải toán.

- **Đọc dữ liệu inventory theo nhiều mảng phép tính**: `Inventory_Test.json` đã hỗ trợ schema `arrays[]`, mỗi `array` đại diện cho 1 chuỗi phép tính.
- **Tương thích ngược dữ liệu cũ**: vẫn đọc được schema cũ dùng `array` chung.
- **Parser phép tính hỗn hợp**:
  - Hỗ trợ cả dạng `a,op,b,result` (legacy) và `a,b,op,result` (dữ liệu mới).
  - Hỗ trợ nối phép sau chia dư bằng `+,b,result` và `b,+,result` (dùng carry là số dư trước đó).
- **Chia có dư**:
  - Phép chia sinh thương nguyên như luật game.
  - Khi dư `r != 0`, hệ thống sinh thêm token số dư vào pool spawn.
- **Conveyor remainder tile (runtime)**:
  - Khi người chơi giải đúng phép chia có dư, hệ thống sinh thêm 1 tile số dư vào conveyor.
  - Conveyor dùng danh sách slot riêng `Slot_0..Slot_5`.
  - Tile số dư được clone từ một number tile trên board (không dùng prefab mới) để đồng bộ kích thước/thành phần.
- **Sprite số theo đúng thứ tự Inspector**:
  - Với mảng 19 phần tử: `Element 0..18` map đúng `-9..9`.
  - Đã áp dụng cho cả spawn tile thường và tile số dư.
- **Fix lỗi kích thước về 0 sau click**:
  - Khi chuyển tile vào slot, ưu tiên giữ size theo `RectTransform.rect.size` (fallback `sizeDelta`) để tránh width/height = 0 khi anchor stretch.

### Đang tạm thời / chưa khóa

- **Rule fail-fast theo GDD chưa đồng bộ hoàn toàn**:
  - GDD ghi "nhập sai -> thua ngay", nhưng code hiện vẫn có nhánh "sai thì xóa tile để chơi tiếp" (đang ở trạng thái test).
- **Overflow conveyor chưa chốt thua ngay ở mọi luồng**:
  - Hiện có chặn/sớm return khi đầy; cần khóa luồng xử thua thống nhất theo rule 3.1 + 4.2.
- **Return helper / tương tác conveyor đầy đủ**:
  - Phần sinh tile số dư đã có, nhưng toàn bộ pipeline helper/conveyor theo economy chưa hoàn thiện end-to-end.

---

## 2) Luật gameplay đã chốt (Locked Gameplay Rules)

## 2.1 Tile Types

- Number tiles: từ -9 đến 9.
- Operator tiles: +, -, x, /.
- Mystery tile: giá trị ẩn trên board, chỉ lộ khi người chơi chọn tile đó.

## 2.2 Layering / Unlock Rules

- Board dùng cơ chế chồng lớp kiểu Mahjong (layered overlap).
- Tile bị khóa nếu còn bị che phủ hợp lệ bởi tile khác.
- Khi tile trên bị lấy đi, mọi tile không còn bị che đều chuyển sang trạng thái chọn được.
- Quy tắc mở khóa là mở tất cả tile hợp lệ, không giới hạn chỉ một tile.

## 2.3 Slot Equation Model

Biểu thức chuẩn:

[Slot1] [Slot2] [Slot3] = [Slot4] [Slot5]

- Slot1 và Slot3: number tile.
- Slot2: operator tile (chỉ 1 toán tử cho mỗi phép).
- Slot4, Slot5: biểu diễn kết quả.

## 2.4 Quy tắc ghép kết quả 2 slot (Chỉ chấp nhận dấu âm ở Hàng Chục)

Để đảm bảo tính nhất quán và dễ hiểu, game quy định cách biểu diễn số có 2 chữ số như sau:

### Công thức giải mã

- **Slot 4 (Hàng chục):** Có thể là số âm hoặc dương (-9 đến 9).
- **Slot 5 (Hàng đơn vị):** BẮT BUỘC phải là số không âm (0 đến 9).
- **Kết quả:** `sign(Slot 4) * (abs(Slot 4) * 10 + Slot 5)`.

### Ví dụ

- Slot 4 = 1, Slot 5 = 2 -> **12** (Đúng)
- Slot 4 = -1, Slot 5 = 2 -> **-12** (Đúng)
- Slot 4 = 1, Slot 5 = -2 -> **SAI** (Hàng đơn vị không được âm)
- Slot 4 = -1, Slot 5 = -2 -> **SAI** (Hàng đơn vị không được âm)
- Slot 4 = 0, Slot 5 = 2 -> **2** (Đúng)

Lưu ý:
- Với kết quả 1 chữ số, dùng Slot 4 và để Slot 5 trống.
- Nếu kết quả là số âm 1 chữ số (ví dụ -5), đặt `-5` vào Slot 4.

## 2.5 Division and Remainder

- Chia lấy thương nguyên và dư.
- Nếu a / b có dư, người chơi chỉ cần nhập thương vào slot kết quả.
- Số dư tự động đẩy vào conveyor.
- Ví dụ: -7 / 2 => thương -2, dư -1, tile -1 đưa vào conveyor.

## 2.6 Division by Zero với Mystery Tile

- Tile thường (đã biết giá trị 0) tại vị trí số chia có thể bị chặn từ trước theo validate UI.
- Mystery tile ở vị trí số chia vẫn cho phép chọn.
- Nếu reveal ra 0 khi đang chia, xử thua ngay.

---

## 3) Conveyor

## 3.1 Capacity Rules

- Conveyor hiển thị tối đa 6 tile.
- Không có hidden queue.
- Nếu conveyor đã đủ 6 tile mà vẫn phát sinh tile mới, xử thua ngay.

## 3.2 Ordering Rules

- Người chơi chọn tự do tile trên conveyor (không bắt buộc FIFO khi dùng).
- Quy ước xếp thị giác: tile vào sau xuất hiện từ phía phải.

## 3.3 Return interactions

- Return: đưa các tile đang nằm trên slots về conveyor theo thứ tự.

---

## 4) Win, Lose, Deadlock

## 4.1 Win Conditions

- Campaign / Daily: thắng khi board rỗng và conveyor rỗng.

## 4.2 Lose Conditions

1. Nhập phép tính sai -> thua ngay.
2. Chia cho 0 trong nhánh hợp lệ gameplay -> thua.
3. Conveyor overflow (vượt sức chứa 6 ô) -> thua.
4. Người chơi chủ động Give Up -> thua.

## 4.3 Deadlock Detector (Warning Only)

Phạm vi áp dụng:

- Chỉ Campaign / Level Mode.
- Không áp dụng cho Daily Challenge.
- Chỉ cảnh báo, không tự động xử thua.

Luật nhanh đã chốt:

- Nếu số lượng number tile còn lại > 4 x số operator tile còn lại, hiển thị cảnh báo deadlock tiềm năng.

UX:

- Popup cảnh báo và gợi ý helper (Shuffle, Spawn Number).

---

## 5) Game Modes

## 5.1 Campaign / Level Mode

- Campaign chia 2 giai đoạn generate:
  - Giai đoạn A (Level 1-100): designer tự làm layout riêng từng màn và tự nhập số lượng từng toán tử.
  - Giai đoạn B (Level >= 101): layout được random từ pool hậu 100, toán tử + - * / có số lượng bằng nhau.
- Chỉ có Number tile và Operator tile chiếm spawn slot.
- Gọi:
  - S = tổng số spawn slot của layout (tính từ stacks).
  - O = tổng số operator tile của màn.
  - N = tổng số number tile do thuật toán sinh.
- Ràng buộc cứng:
  - S = O + N.
  - 3 x O <= N <= 4 x O.
- Giai đoạn A:
  - Hệ thống giữ cứng đúng số slot của layout, không để lủng giao diện.
  - O lấy từ operatorProfile do designer nhập.
- Giai đoạn B:
  - O phải chia hết cho 4 để đảm bảo +, -, *, / bằng nhau.
  - Chọn O trước, sau đó mới chọn layout có S nằm trong dải hợp lệ [4 x O, 5 x O].
- Quy tắc chia có dư áp dụng cho toàn bộ Campaign:
  - Nếu phép chia sinh dư r != 0 thì r bắt buộc phải được dùng lại ở các phép sau.
- Bộ phương trình sinh ra phải solvable 100%.
- Mystery tile:
  - mysteryCount được cấu hình theo màn trong campaign_levels.
  - Sau khi sinh xong phép tính và number tile, hệ thống chọn ngẫu nhiên number tile để gắn Mystery theo mysteryCount.

### 5.1.1 Chiến lược sinh số Campaign (đề xuất)

1. Dùng seed deterministic theo playerId + levelIndex + generatorVersion + serverSalt.
2. Xác định O:
  - Level 1-100: lấy từ operatorProfile của level.
  - Level >= 101: chọn O là bội số của 4 theo band độ khó.
3. Xác định layout:
  - Level 1-100: lấy layout cố định do level chỉ định.
  - Level >= 101: random layout từ pool hậu 100 với điều kiện S trong [4 x O, 5 x O].
4. Tính N = S - O và đảm bảo 3 x O <= N <= 4 x O.
5. Sinh ngược (reverse generation): tạo equation graph trước, sau đó rải tile theo thứ tự đảm bảo có đường giải.
6. Ưu tiên sinh phép chia sớm; mọi số dư phải vào danh sách must-use và được tiêu thụ hết.
7. Sau khi graph hợp lệ, gắn Mystery ngẫu nhiên lên number tile theo mysteryCount của level.
8. Nếu vượt số lần generate cho phép, fallback về safe template cùng band độ khó.

## 5.2 Daily Challenge (Weekly Streak System)

- Chuỗi Daily được tính theo tuần (Thứ 2 - Chủ Nhật).
- Tài nguyên: Mỗi ngày hệ thống cung cấp 1 màn chơi mới. Người chơi có thể chơi bù các ngày trước đó trong cùng một tuần.
- **Quy tắc Streak:**
  - Người chơi tích lũy tối đa 7 streak mỗi tuần (tương ứng từ Thứ 2 đến Chủ Nhật).
  - **Điều kiện giữ và mất chuỗi:**
    - Nếu kết thúc tuần (hết ngày Chủ Nhật) mà người chơi không hoàn thành đủ 7 màn (không đạt 7 streak), toàn bộ chuỗi streak tổng sẽ bị reset về 0 (trừ khi có Shield).
    - Streak tổng (Total Streak) là tổng số ngày chơi liên tục qua nhiều tuần.
  - **Tiến hóa Icon Streak:** Khi Total Streak đạt tới các mốc nhất định (ví dụ: 7, 30, 100, 365 ngày), icon streak trong game sẽ thay đổi hình dáng/hiệu ứng để thể hiện đẳng cấp.
- UI calendar hiển thị tiến độ tuần hiện tại (ví dụ: 4/7 ngày đã hoàn thành).
- Độ khó Daily thiết kế đơn giản theo số lượng phép tính.
- Thuật toán tự sinh số dựa trên bộ toán tử của ngày.
- Layout được chọn ngẫu nhiên từ danh sách layout hợp lệ.
- Mỗi người chơi có instance Daily riêng; cho phép resume nếu đang chơi dở.

### Streak Shield
- Mỗi người chơi có 1 shield bảo vệ chuỗi.
- Nếu kết thúc tuần không đủ 7 streak, shield sẽ tự động tiêu thụ để giữ lại Total Streak (nhưng tuần mới vẫn bắt đầu từ 0/7).
- Shield hồi sau đúng 7 ngày tính từ timestamp mất shield.

## 5.3 Endless Mode

- Chưa khóa thiết kế ở phiên bản tài liệu này.
- Có thể bổ sung sau khi Campaign và Daily ổn định telemetry.

---

## 6) Economy & Helpers

## 6.1 Currency

- Gold (G): thưởng qua gameplay, ads, IAP.

## 6.2 Helper Usage Constraint

- Tổng lượt dùng helper mỗi màn: tối đa 3 lần cho toàn bộ nhóm helper.

| Helper  | Chức năng                                       | Gợi ý giá (G) |
| :------ | :------------------------------------------------ | :--------------- |
| Shuffle | Trộn lại toàn bộ tile theo layout hiện tại    | 30               |
| Return  | Thu hồi tile đang đặt trên slot về conveyor | 20               |
| Delete  | Xóa vĩnh viễn 1 tile bất kỳ                  | 50               |
| Spawn # | Sinh 1 number tile do người chơi chọn         | 100              |

## 6.3 Chi tiết triển khai Helper (Implemented Logic)

Hệ thống Helper đã được phát triển hoàn thiện và quản lý tập trung qua `HelperManager.cs` kết hợp với `UIEffectManager` để xử lý Animation.

### 1. Helper_Spawn (Tạo Tile Mới)
- **Nguyên lý:** Mở ra giao diện Popup cho phép người chơi chủ động chọn 1 Tile (Toán tử: +, -, x, / hoặc Số: 1-9).
- **Phạm vi & Cách thức:**
  - Hỗ trợ đổi dấu (Toggle Sign) cho phép đổi từ số dương sang âm và ngược lại trực tiếp trên giao diện.
  - **Rendering logic:** Các Tile mẫu trong popup sử dụng `sortingOrder = 1000`. Khi click chọn, hệ thống tạo bản sao, chuyển `TileType` và hạ `sortingOrder` của bản sao xuống **10** trước khi đưa về Conveyor.
  - Tile sau khi sinh sẽ lập tức bay về ô trống đầu tiên trên Băng chuyền (`Conveyor`).
  - Logic xác định ô trống dựa vào `FormulaManager.Instance.TryGetNextConveyorSlot`.

### 2. Helper_Shuffle (Xáo Trộn Bàn Chơi)
- **Nguyên lý:** Lấy toàn bộ các Tile **hiện đang kích hoạt trên Board** (không tính trên Formula Bar hay Conveyor) và xáo trộn vị trí/giá trị.
- **Phạm vi & Cách thức:**
  - Sử dụng thuật toán **Fisher-Yates** để tráo đổi thuộc tính (`numberValue`, `operatorValue`, `Sprite`) của các Tile.
  - **Animation (Chaos Fly):** Các Tile sẽ nổ tung bay tản ra xung quanh (`MoveTo` ra vị trí ngẫu nhiên) rồi lập tức bay về đúng vị trí Slot ban đầu với thuộc tính mới.
  - **Bảo toàn Mystery:** Trạng thái "Mặt nạ" (`isMystery`) cũng được xáo trộn kèm theo dữ liệu, đảm bảo Tile ẩn không bị lộ giá trị.

### 3. Helper_Return (Rút Lại Nước Đi)
- **Nguyên lý:** Đưa các Tile vừa đẩy lên thanh công thức (`Formula Bar`) quay trở về băng chuyền (`Conveyor`).
- **Phạm vi & Cách thức:**
  - **LIFO (Last In First Out):** Rút các Tile từ phải sang trái (index 4 -> 0) trên Formula Bar.
  - **Sắp xếp:** Đưa về Conveyor nhưng xếp lại đúng thứ tự trái sang phải ban đầu.
  - **Cập nhật mảng Instant:** Dữ liệu mảng `occupiedTiles` và `occupiedConveyorTiles` được hoán đổi lập tức để chặn lỗi logic.
  - **Clipping Fix (Juice):** Hiệu ứng bay bổng (`Y + 50`) thực hiện trong hệ tọa độ của lớp cha cũ (Panel_Equation). Chỉ khi bay xong (onComplete) mới gọi `SetParent` sang Conveyor để tránh Tile bị UI đè lấp.

### 4. Helper_Delete (Phá Hủy Tile)
- **Nguyên lý:** Xóa vĩnh viễn 1 Tile bất kỳ đang ở trạng thái Unlocked (sáng màu) khỏi màn chơi.
- **Phạm vi & Cách thức:**
  - **Hinting:** Khi kích hoạt, hệ thống sẽ tự động làm mờ (`SetDimmed`) các Tile đang bị khóa trên Board (alpha = 0.4) để người chơi dễ nhận biết mục tiêu hợp lệ.
  - **Tương tác:** 
    - Nếu click Tile bị khóa -> Rung lắc báo lỗi (`Shake`).
    - Nếu click Tile hợp lệ -> Xóa.
  - **Xử lý Xóa:**
    - Nếu nằm trên **Board**: Gọi `ResolveOverlapOnAccept()` để lập tức mở khóa cho các Tile bị nó đè bên dưới.
    - Nếu nằm trên **Conveyor**: Xóa mảng và gọi `ShiftConveyorTiles()` dồn hàng sang trái.
    - Nếu nằm trên **Formula Bar**: Dọn trống mảng logic.
  - Tự động tắt chế độ Delete và làm sáng lại bàn chơi sau khi xóa thành công 1 Tile.

---

## 7) Level Matrix Design (Thiết kế ma trận vị trí)

Hệ thống sử dụng mô hình **Layered Grids** để quản lý các lớp Tile.

1.  **Cấu trúc Board Layer:**
    *   Mỗi Layer trong game được đại diện bởi một đối tượng **Board_Grid** riêng biệt.
    *   `Board_Grid` chứa ma trận 11x11 các Slot tọa độ (`Slot_1-1` đến `Slot_11-11`).
    *   Khi bắt đầu màn chơi, hệ thống sẽ sinh ra số lượng `Board_Grid` tương ứng với số lớp trong Layout.
2.  **Quy trình Spawn vào Slot:**
    *   Tile được sinh ra và trở thành **con trực tiếp** của Slot tương ứng tại Layer đó.
    *   Ví dụ: Một Tile ở tọa độ "1-2" tại Layer 0 sẽ nằm trong `Layer_0/Slot_1-2`.
3.  **Giải pháp Cố định Kích thước Tile (Anchor Center System):**
    *   Để Tile không bị thay đổi kích thước bởi các Layout Group hoặc kích thước của Slot:
        *   Khi spawn, Tile sẽ được thiết lập **Anchor về chính giữa (Middle-Center)**: `anchorMin = anchorMax = (0.5, 0.5)`.
        *   Kích thước `sizeDelta` của Tile sẽ được giữ nguyên theo giá trị thiết kế trong Prefab.
        *   Điều này đảm bảo Tile luôn giữ đúng tỷ lệ và kích thước, ngay cả khi Slot cha bị co giãn.

## 7.2 Quy trình Spawn & Di chuyển (Tile Workflow)

### 7.2.1 Quy trình Spawn
1. **Khởi tạo Layer:** Dựa trên dữ liệu Layout, instantiate prefab `Board_Grid` cho mỗi Layer.
2. **Thứ tự:** Spawn từ Layer thấp đến Layer cao.
3. **Phân bổ:** Duyệt danh sách `spawnCoordinates` của từng Layer, tìm Slot tương ứng trong Grid của Layer đó và instantiate Tile làm con của Slot.
4. **Cố định Size:** Áp dụng thiết lập Anchor và Size cho Tile ngay khi spawn.
5. **Khởi tạo Cây Quan Hệ (Overlap Tree):** Sau khi spawn xong toàn bộ board, chạy thuật toán quét mọi cặp Tile. Tile B ở layer cao hơn đè lên Tile A ở layer thấp hơn nếu $|X_a - X_b| \le 1$ và $|Y_a - Y_b| \le 1$.
6. **Thiết lập trạng thái khóa:** Tile bị đè sẽ có `isLocked = true` và màu tối (#676767).

### 7.2.3 Giải pháp Cố định Kích thước Tile (Fixed Size Anchor System)
Để đảm bảo Tile không bị thay đổi kích thước bởi các Layout Group (Conveyor, Equation Slots, Board), hệ thống sử dụng cơ chế **Anchor Snapping**:

1.  **Cấu trúc phân cấp:** 
    *   Các Slot (ví dụ `Slot_1-1`, `ConveyorSlot_0`) chỉ đóng vai trò là **Anchor (Điểm neo)** vị trí.
    *   Các Tile khi được spawn sẽ được đặt vào một Container chung (ví dụ `ActiveTiles_Container`) nằm ngoài sự quản lý của các Layout Group.
2.  **Cơ chế di chuyển:** 
    *   Khi một Tile được gán vào một Slot, nó sẽ di chuyển (Tween/Snap) đến **World Position** của Slot đó.
    *   Tile KHÔNG trở thành con trực tiếp của Slot nếu Slot đó nằm trong một Layout Group đang điều chỉnh kích thước con (như Horizontal/Vertical/Grid Layout Group).
3.  **Quản lý kích thước:** Kích thước của Tile (`RectTransform.sizeDelta`) được xác định một lần duy nhất lúc spawn và được giữ nguyên suốt vòng đời của Tile, bất kể nó đang ở Board hay Conveyor.

### 7.2.2 Quy trình Tile Di chuyển (New)
Quy trình di chuyển Tile giữa Board, Slots và Conveyor tuân thủ các bước:
1. **Xác định đích (Target Identification):** Dựa trên tọa độ đích (Board Matrix) hoặc Index (Equation Slots/Conveyor).
2. **Kiểm tra hợp lệ (Validation):**
   - Di chuyển từ Board: Tile phải ở trạng thái `Unlocked`.
   - Di chuyển đến Board: Tọa độ đích phải nằm trong ma trận 11x11 và tuân thủ quy tắc Layer (thường là trả về lớp trên cùng khả dụng).
3. **Thực thi Animation:** Tile di chuyển mượt mờ từ tọa độ nguồn đến tọa độ đích.
4. **Cập nhật Logic:**
   - Cập nhật tham chiếu Tile tại các đối tượng lưu tọa độ.
   - **Mở khóa dây chuyền (Chain Unlock):** Khi một Tile rời khỏi Board, nó thông báo cho tất cả Tile mà nó đang đè (`coveredTiles`) loại bỏ nó khỏi danh sách `coveringTiles`.
   - Nếu một Tile không còn bị ai đè (`coveringTiles.Count == 0`), nó sẽ tự động chuyển sang trạng thái `Unlocked` (sáng màu và cho phép click).
   - Cập nhật trạng thái `Parent/Children` trong Dependency Graph.

---

## 8) Firebase Data Architecture

Khuyến nghị: dùng Cloud Firestore làm nguồn dữ liệu gameplay chính, Cloud Functions cho job định kỳ và validate server-side.

## 8.1 Logical Tables (Collections)

### A. master_game_configs

- Mục đích: cấu hình toàn cục theo môi trường.
- Khóa chính: configId (default, staging, live).
- Trường chính:
  - conveyorVisibleCap = 6
  - conveyorOverflowLose = true
  - helperUseCapPerLevel = 3
  - dailyResetTimezone = UTC
  - shieldRegenDays = 7

### B. board_layouts (v2)

- Mục đích: lưu cấu trúc lớp theo ma trận 11x11.
- Khóa chính: layoutId
- Trường chính:
  - layoutId
  - layers[]:
    - layerIndex (int)
    - spawnCoordinates[] (string: "x-y")
  - layoutPhase (campaign_pre100, campaign_post100, shared)

### C. campaign_levels

- Mục đích: lưu metadata màn Campaign để máy tự sinh payload.
- Khóa chính: levelId
- Trường chính:
  - chapterId
  - levelIndex
  - generationMode = RULE_V4
  - campaignPhase (pre100, post100)
  - layoutSelectionMode (FIXED_LEVEL_LAYOUT, RANDOM_POST100_POOL)
  - fixedLayoutId (bắt buộc với pre100)
  - operatorProfile (+Count, -Count, xCount, /Count)
  - mysteryCount
  - objective
  - generatorVersion
  - isActive
  - version

### D. daily_challenges

- Mục đích: template challenge theo ngày (không lưu tiến trình cá nhân).
- Khóa chính: dateKey (YYYY-MM-DD, UTC)
- Trường chính:
  - operatorCount
  - operatorPool[]
  - layoutPool[]
  - status (published, draft)


### E. players

- Mục đích: hồ sơ người chơi.
- Khóa chính: playerId
- Trường chính:
  - displayName
  - createdAt
  - gold
  - totalStreak (int): Tổng chuỗi tích lũy qua các tuần
  - streakIconId (string): ID của icon streak hiện tại dựa trên totalStreak
  - shield:
    - hasShield (bool)
    - lastShieldConsumedAt
    - nextShieldRegenAt

### F. player_campaign_progress

- Mục đích: tiến trình Campaign theo người chơi (Lưu trên Server).
- Khóa chính gợi ý: playerId_levelId
- Trường chính:
  - playerId
  - levelId
  - state (not_started, in_progress, cleared, failed)
  - attempts
  - bestCompletionAt
  - helperUses

### G. player_daily_progress

- Mục đích: tiến trình Daily & Streak theo người chơi (Lưu trên Server).
- Khóa chính gợi ý: playerId_currentWeekId (ví dụ: player123_2026_w20)
- Trường chính:
  - playerId
  - weekId (YYYY_wWW)
  - completedDays[] (array: [1, 2, 4]): Danh sách các thứ trong tuần đã hoàn thành (1=Thứ 2, 7=Chủ Nhật)
  - currentStreakCount: 0-7 (Số màn đã xong trong tuần này)
  - dailySnapshots: map các snapshot màn chơi đang dang dở theo từng ngày.
  - lastUpdateAt: timestamp
  - usedShieldThisWeek: boolean

### H. player_inventory

- Mục đích: vật phẩm, booster.
- Khóa chính gợi ý: playerId
- Trường chính:
  - helperTickets
  - cosmetics
  - consumables

### I. economy_transactions

- Mục đích: sổ cái biến động Gold / IAP.
- Khóa chính: txId
- Trường chính:
  - playerId
  - amount
  - currency = GOLD
  - reason (reward, helper_use, iap, ad)
  - balanceAfter
  - createdAt

### J. match_events (optional, analytics)

- Mục đích: event telemetry theo phiên.
- Khóa chính: eventId
- Trường chính:
  - playerId
  - mode
  - levelRef
  - eventType
  - payload
  - ts

## 8.2 Example Documents

### campaign_levels/

```json
{
  "chapterId": "chapter_01",
  "levelIndex": 57,
  "generationMode": "RULE_V4",
  "campaignPhase": "pre100",
  "layoutSelectionMode": "FIXED_LEVEL_LAYOUT",
  "fixedLayoutId": "layout_campaign_057",
  "operatorProfile": {
    "+Count": 3,
    "-Count": 2,
    "xCount": 1,
    "/Count": 1
  },
  "mysteryCount": 4,
  "objective": {
    "type": "clear_board"
  },
  "generatorVersion": 4,
  "isActive": true,
  "version": 6,
  "updatedAt": "2026-04-17T00:00:00Z"
}
```

### board_layouts/

```json
{
  "layoutId": "layout_campaign_057",
  "layoutPhase": "campaign_pre100",
  "stacks": [
    { "cellId": "1-1", "count": 3 },
    { "cellId": "1-2", "count": 1 },
    { "cellId": "2-1", "count": 2 },
    { "cellId": "2-2", "count": 3 }
  ]
}
```

### daily_challenges/

```json
{
  "dateKey": "2026-04-18",
  "operatorCount": 3,
  "operatorPool": ["+", "-", "x", "/"],
  "layoutPool": [
    "layout_campaign_stack_01",
    "layout_campaign_stack_02",
    "layout_campaign_stack_03"
  ],
  "status": "published"
}
```

### player_daily_progress/

```json
{
  "playerId": "player_123",
  "dateKey": "2026-04-18",
  "seed": "2f7d9c0a",
  "selectedLayoutId": "layout_campaign_stack_02",
  "generatedNumbers": [8, 2, -1, 5, 0, 7],
  "generatedOperators": ["+", "-", "/"],
  "snapshot": {
    "board": [{ "cellId": "1-1", "remaining": 2 }],
    "conveyor": [1],
    "moveIndex": 6
  },
"lastCheckpointAt": "2026-04-18T06:14:55Z",
  "state": "đang",
  "usedShieldOnDate": false,
  "streakSnapshot": 14
}
```

## 8.3 Suggested Indexes

- campaign_levels: (chapterId ASC, levelIndex ASC)
- daily_challenges: (dateKey ASC)
- player_campaign_progress: (playerId ASC, state ASC)
- player_daily_progress: (playerId ASC, dateKey DESC)
- economy_transactions: (playerId ASC, createdAt DESC)

## 8.4 Server Functions (Cloud Functions)

1. dailyPublisher:

   - chạy mỗi UTC 00:00
   - publish daily_challenges/{today}
2. shieldRegenerator:

   - kiểm tra players có nextShieldRegenAt <= now
   - set hasShield = true
3. economyGuard:

   - validate giao dịch Gold không âm sai lệch

---

## 9) Technical Architecture (Unity)

## 9.1 Recommended Script Domains

Assets/_Game/

- Scripts/Core/
  - GameManager
  - GameStateMachine
  - RuleEngine
- Scripts/Gameplay/
  - Tile
  - BoardLayerSystem
  - CalculatorEngine
  - ConveyorSystem
  - DeadlockWarningService
- Scripts/Data/
  - LevelDefinition
  - LayoutDefinition
  - FirebaseDTOs
- Scripts/Services/
  - FirebaseService
  - EconomyService
  - CampaignGeneratorService
  - DailyService
  - ShieldService
- Scripts/UI/
  - HUD
  - DailyCalendarView
  - PopupSystem

## 9.2 Event Bus Contracts (gợi ý)

- OnTileSelected
- OnEquationSubmitted
- OnEquationFailed
- OnRemainderGenerated
- OnConveyorChanged
- OnDeadlockWarning
- OnShieldConsumed
- OnShieldRegenerated
- OnLevelCleared

---

## 9.3 Data Security & Persistence Rules

Để đảm bảo tính toàn vẹn dữ liệu và trải nghiệm người dùng mượt mà, hệ thống áp dụng các quy tắc sau:

### 1. Static Content (Levels, Layouts)
- **Định dạng:** JSON.
- **Bảo mật:** Mã hóa **AES-256** trong quá trình Build Pipeline.
- **Triển khai:** File thô dùng trong Editor để dễ debug, file mã hóa được đưa vào `StreamingAssets` hoặc `Addressables` khi đóng gói. Key mã hoá được che giấu (Obfuscated) trong mã nguồn.

### 2. Sensitive User Data (Gold, Streak, Progress)
- **Local Persistence (Offline):**
  - Lưu vào `Application.persistentDataPath`.
  - Sử dụng mã hóa **AES** kết hợp với **Checksum (Hash)** để phát hiện can thiệp file save.
  - Mỗi khi load file, hệ thống tính toán lại Hash và so sánh; nếu sai lệch sẽ xử lý theo quy tắc bảo mật (ví dụ: dùng bản backup hoặc cảnh báo).
- **Cloud Sync (Online):**
  - Đồng bộ qua **Firebase/Unity Cloud Save** khi có kết nối Internet.
  - Sử dụng cơ chế **Dirty Flag**: Chỉ đẩy dữ liệu lên Cloud khi kết thúc màn chơi hoặc khi người chơi quay về Main Menu để tối ưu băng thông.
  - **Conflict Resolution:** Nếu dữ liệu local và cloud khác nhau, ưu tiên bản có `lastUpdateAt` mới nhất hoặc tiến trình xa nhất.

### 3. Session Data (Streak Resume)
- **Mô tả:** Snapshot chi tiết của bàn chơi đang dang dở (vị trí tile, giá trị trên ô công thức...).
- **Lưu trữ:** Chỉ lưu tại **Local** (Snapshot JSON nén) để resume nhanh. Không bắt buộc đẩy lên Cloud trừ khi có nhu cầu chơi xuyên thiết bị (Cross-device resume).

### 4. System Settings (Non-Sensitive)
- **Mô tả:** Volume, Mute, Language, Resolution...
- **Lưu trữ:** Lưu thô (Plain text) qua **PlayerPrefs** hoặc JSON đơn giản để đạt tốc độ truy cập tối đa mà không tốn tài nguyên giải mã.

---

## 10) Scope Notes

- Bản tài liệu này khóa luật cho Campaign và Daily.
- Endless hiện để trạng thái future scope.
- Hệ thống điểm số chi tiết chưa nằm trong phiên bản này.

---

## 11) Source of Truth

Nếu có xung đột giữa tài liệu cũ và tài liệu này, tài liệu này được ưu tiên.

---

## 12) Bảng Dữ Liệu Tóm Tắt + Chú Thích

## 12.1 Danh sách collections

| Collection | Khóa chính | Vai trò | Chú thích |
| :-- | :-- | :-- | :-- |
| master_game_configs | configId | Cấu hình global | Lưu cap conveyor, rule thua, mốc tiến hóa icon streak |
| board_layouts | layoutId | Định nghĩa stack map | Lưu stacks và phase tag để chọn đúng pool layout |
| campaign_levels | levelId | Rule config Campaign | Lưu phase, chọn layout, profile toán tử |
| daily_challenges | dateKey | Template Daily theo ngày | Template gốc để sinh màn chơi mỗi ngày |
| players | playerId | Hồ sơ người chơi | Lưu gold, totalStreak, và icon hiện tại |
| player_campaign_progress | playerId_levelId | Tiến trình Campaign | Lưu trạng thái vượt màn của người chơi |
| player_daily_progress | playerId_weekId | Tiến trình Daily tuần | Theo dõi streak 0-7 trong tuần và snapshot resume |
| player_inventory | playerId | Vật phẩm người chơi | Lưu tickets/cosmetics/consumables |
| economy_transactions | txId | Sổ cái kinh tế | Audit thay đổi gold và lý do giao dịch |
| match_events | eventId | Telemetry (optional) | Lưu sự kiện để phân tích hành vi người chơi |

## 12.2 Chú thích sử dụng nhanh

| Bảng | Nên đọc/ghi khi nào | Lưu ý triển khai |
| :-- | :-- | :-- |
| board_layouts | Lúc tải màn, tạo board ban đầu | Validate tổng số slot = sum(count), không chứa tham số Mystery |
| campaign_levels | Lúc vào level Campaign | Phase pre100 dùng fixedLayout + operatorProfile; phase post100 random layout + equal operator; đọc mysteryCount để gắn Mystery |
| daily_challenges | Lúc tạo challenge ngày | Publish template mỗi ngày theo UTC 00:00 |
| player_campaign_progress | Sau mỗi ván Campaign | Upsert theo playerId_levelId |
| player_daily_progress | Trong lúc chơi Daily và khi thoát | Autosave checkpoint để resume chính xác khi vào lại |
| economy_transactions | Mỗi thay đổi Gold | Ghi theo cơ chế append-only để audit |

## 12.3 Quy tắc đặt tên khóa gợi ý

| Bảng | Mẫu khóa |
| :-- | :-- |
| campaign_levels | campaign_0001, campaign_0002 |
| board_layouts | layout_campaign_stack_01 |
| daily_challenges | 2026-04-17 |
| player_campaign_progress | player123_campaign_0001 |
| player_daily_progress | player123_2026-04-17 |

---

## 14) Bổ sung thiết kế Editor Tool & Layout Strategy (Thảo luận mới)

### 14.1 Chiến thuật Quản lý Lớp (Layer Management Strategy)

Để giải quyết vấn đề hiển thị và logic cho hệ thống chồng lớp (Mahjong-style):

1.  **Hiển thị (Visual): "Local Stack"**
    *   Mỗi tọa độ (Cell) chỉ khởi tạo (Instantiate) tối đa **2 lớp trên cùng**.
    *   Lớp 0 (Top): Trạng thái `Unlocked` (Sáng).
    *   Lớp 1 (Below): Trạng thái `Locked` (Tối).
    *   Khi Lớp 0 bị lấy đi -> Lớp 1 chuyển thành Unlocked -> Tải dữ liệu lớp tiếp theo từ Stack để hiển thị làm Locked mới.
    *   *Mục đích:* Tối ưu hiệu năng và giảm nhiễu thị giác cho người chơi.

2.  **Logic: "Coordinate-Based Dependency"**
    *   Việc Unlock không chỉ dựa trên va chạm vật lý mà dựa trên sự tồn tại của Tile ở Layer cao hơn tại cùng một tọa độ hoặc các tọa độ lân cận (tùy theo quy tắc phủ).
    *   Hỗ trợ thuật toán **Deadlock Detector** bằng cách duyệt đồ thị phụ thuộc.

### 14.2 Công cụ thiết kế màn chơi (Level Editor Tool)

Công cụ này giúp designer tạo ra các tệp JSON layout một cách trực quan thay vì nhập liệu thủ công.

1.  **Giao diện Grid Editor:**
    *   Kích thước lưới cố định: **11x11**.
    *   Cho phép designer chọn Layer để vẽ.
    *   **Layer Tabs:** Chuyển đổi giữa các lớp để chỉ định tọa độ spawn cho lớp đó.
    *   Hiển thị dạng **Heatmap**: Mỗi ô hiện số lớp tổng cộng.

2.  **Thao tác người dùng:**
    *   **Chuột trái:** Đánh dấu/Bỏ đánh dấu tọa độ spawn cho Layer đang chọn.
    *   **Drag (Kéo chuột):** "Vẽ" nhanh một vùng tọa độ.
    *   **Layer Preview (X-Ray):** Nhìn xuyên qua các lớp.

3.  **Tính năng Hệ thống:**
    *   **3D Preview:** Nút bấm để sinh nhanh các khối Cube trong Scene Unity nhằm kiểm tra độ cao thực tế của các stack.
    *   **JSON Export:** Xuất dữ liệu theo đúng Schema `board_layouts` (layers[] {layerIndex, spawnCoordinates[]}).
    *   **Auto-Naming:** Tự động tạo `layoutId` theo định dạng chuẩn.

### 14.3 Quy trình triển khai (Workflow)
1. Designer dùng **Level Editor Tool** để "vẽ" cấu trúc các lớp (Layout) -> Xuất JSON.
2. Hệ thống **Campaign Generator** đọc JSON Layout -> Tính toán số lượng ô trống (S).
3. Dựa trên luật (O+N=S), hệ thống sinh ra bộ số và toán tử (Payload).
4. Thực hiện rải (Shuffle/Distribute) Payload vào các Stack theo thứ tự đảm bảo giải được.


## 13.1 master_game_configs

| Field | Type | Required | Note |
| :-- | :-- | :-- | :-- |
| configId | string | Yes | Document id, ví dụ default, staging, live |
| conveyorVisibleCap | number | Yes | Số ô conveyor hiển thị, hiện tại là 6 |
| conveyorOverflowLose | boolean | Yes | Nếu phát sinh tile mới khi conveyor đã đủ 6 ô thì thua ngay |
| helperUseCapPerLevel | number | Yes | Tổng số lần dùng helper mỗi màn |
| dailyResetTimezone | string | Yes | Múi giờ reset Daily, hiện tại UTC |
| shieldRegenDays | number | Yes | Số ngày hồi shield sau khi mất |

## 13.2 board_layouts

| Field | Type | Required | Note |
| :-- | :-- | :-- | :-- |
| layoutId | string | Yes | Document id của layout |
| layoutPhase | string | Yes | campaign_pre100, campaign_post100, hoặc shared |
| stacks | array<object> | Yes | Danh sách stack tại các cell |
| stacks[].cellId | string | Yes | Tọa độ dạng 1-1 ... 11-11 |
| stacks[].count | number | Yes | Số lớp tile tại cell, >= 1 |

## 13.3 campaign_levels

| Field | Type | Required | Note |
| :-- | :-- | :-- | :-- |
| levelId | string | Yes | Document id màn Campaign |
| chapterId | string | Yes | Nhóm chương của level |
| levelIndex | number | Yes | Thứ tự level trong chapter |
| generationMode | string | Yes | RULE_V4 |
| campaignPhase | string | Yes | pre100 hoặc post100 |
| layoutSelectionMode | string | Yes | FIXED_LEVEL_LAYOUT hoặc RANDOM_POST100_POOL |
| fixedLayoutId | string | No | Bắt buộc với phase pre100 |
| operatorProfile.+Count | number | No | Số operator +, bắt buộc với phase pre100 |
| operatorProfile.-Count | number | No | Số operator -, bắt buộc với phase pre100 |
| operatorProfile.xCount | number | No | Số operator x, bắt buộc với phase pre100 |
| operatorProfile./Count | number | No | Số operator /, bắt buộc với phase pre100 |
| mysteryCount | number | Yes | Số lượng Number tile sẽ được gắn Mystery cho màn |
| objective | object | Yes | Mục tiêu màn, ví dụ clear_board |
| generatorVersion | number | Yes | Version thuật toán sinh Campaign |
| isActive | boolean | Yes | Có mở level cho người chơi hay không |

## 13.4 daily_challenges

| Field | Type | Required | Note |
| :-- | :-- | :-- | :-- |
| dateKey | string | Yes | Document id dạng YYYY-MM-DD theo UTC |
| operatorCount | number | Yes | Số lượng phép tính mục tiêu trong ngày |
| operatorPool | array<string> | Yes | Danh sách toán tử cho phép dùng để sinh số |
| layoutPool | array<string> | Yes | Danh sách layout có thể random cho người chơi |
| status | string | Yes | draft hoặc published |

## 13.5 players

| Field | Type | Required | Note |
| :-- | :-- | :-- | :-- |
| playerId | string | Yes | Document id người chơi |
| displayName | string | Yes | Tên hiển thị |
| createdAt | timestamp | Yes | Thời điểm tạo tài khoản |
| updatedAt | timestamp | Yes | Thời điểm cập nhật hồ sơ gần nhất |
| gold | number | Yes | Số dư vàng hiện tại |
| shield.hasShield | boolean | Yes | Đang còn shield hay không |
| shield.lastShieldConsumedAt | timestamp | No | Thời điểm shield bị dùng gần nhất |
| shield.nextShieldRegenAt | timestamp | No | Mốc giờ shield hồi lại |

## 13.6 player_campaign_progress

| Field | Type | Required | Note |
| :-- | :-- | :-- | :-- |
| playerId | string | Yes | Tham chiếu players |
| levelId | string | Yes | Tham chiếu campaign_levels |
| state | string | Yes | not_started, in_progress, cleared, failed |
| attempts | number | Yes | Số lần chơi level |
| helperUses | number | Yes | Số lần dùng helper ở level |

## 13.7 player_daily_progress

| Field | Type | Required | Note |
| :-- | :-- | :-- | :-- |
| playerId | string | Yes | Tham chiếu players |
| dateKey | string | Yes | Ngày Daily dạng YYYY-MM-DD |
| seed | string | Yes | Seed cá nhân theo playerId + dateKey |
| selectedLayoutId | string | Yes | Layout được random cho người chơi trong ngày |
| generatedNumbers | array<number> | Yes | Dữ liệu số đã sinh cho instance Daily của người chơi |
| generatedOperators | array<string> | Yes | Bộ toán tử đã sinh cho instance Daily của người chơi |
| snapshot | object | Yes | Trạng thái board/conveyor hiện tại để resume |
| lastCheckpointAt | timestamp | Yes | Thời điểm lưu checkpoint gần nhất |
| state | string | Yes | đã, đang, chưa |
| usedShieldOnDate | boolean | Yes | Ngày đó có dùng shield hay không |
| streakSnapshot | number | Yes | Số streak tại thời điểm cập nhật |

## 13.8 player_inventory

| Field | Type | Required | Note |
| :-- | :-- | :-- | :-- |
| playerId | string | Yes | Document id cùng playerId |
| helperTickets | object | Yes | Số lượng vé helper theo loại |
| cosmetics | array<string> | Yes | Danh sách cosmetic đã sở hữu |
| consumables | object | Yes | Vật phẩm tiêu hao và số lượng |

## 13.9 economy_transactions

| Field | Type | Required | Note |
| :-- | :-- | :-- | :-- |
| txId | string | Yes | Document id giao dịch |
| playerId | string | Yes | Người chơi phát sinh giao dịch |
| amount | number | Yes | Giá trị cộng hoặc trừ |
| currency | string | Yes | Hiện tại dùng GOLD |
| reason | string | Yes | reward, helper_use, iap, ad |
| balanceAfter | number | Yes | Số dư sau giao dịch |
| createdAt | timestamp | Yes | Thời điểm tạo giao dịch |

## 13.10 match_events (optional)

| Field | Type | Required | Note |
| :-- | :-- | :-- | :-- |
| eventId | string | Yes | Document id sự kiện |
| playerId | string | Yes | Người chơi phát sinh sự kiện |
| mode | string | Yes | campaign, daily, endless |
| eventType | string | Yes | Loại sự kiện telemetry |
| ts | timestamp | Yes | Thời điểm xảy ra sự kiện |
