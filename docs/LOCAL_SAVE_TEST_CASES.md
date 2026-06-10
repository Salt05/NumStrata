# Test cases — Local Save System (chi tiết từng bước)

**Môi trường:** Unity Editor, Play Mode  
**Thư mục save (Windows):**

```
%USERPROFILE%\AppData\LocalLow\DefaultCompany\NumStrata\
```

Trong đó có: `PlayerData.json`, `PlayerData.bak`, `SessionResume.json` (khi đang chơi dở).

**Mẹo:** Mở folder nhanh — chạy game, xem Console log `[LocalDataManager] Player data saved: ...` và copy đường dẫn.

---

## Chuẩn bị chung

1. Mở project NumStrata trong Unity.
2. Scene build: **MainMenu** → **Gameplay** (đã có trong Build Settings).
3. Trước mỗi nhóm test cần “sạch”: **tắt Play**, xóa toàn bộ file trong folder save ở trên (hoặc đổi tên folder backup).
4. Đảm bảo scene **MainMenu** có object gắn **LocalDataManager** (hoặc để Home tự tạo khi Play).

---

## Phase 1 — Models & helpers

### TC 1.1 — `IndexToLevelId`

**Mục tiêu:** Số màn 3 → chuỗi `campaign_0003`.

**Các bước:**

1. Mở bất kỳ script Editor tạm hoặc thêm 1 dòng debug trong `LocalDataManager.Awake` (tùy chọn):
   ```csharp
   Debug.Log(CampaignSession.IndexToLevelId(3));
   ```
2. Enter Play Mode (scene có `LocalDataManager`).
3. Mở **Window → General → Console**.

**Pass khi:** Console in đúng `campaign_0003` (không có `.json`).

**Fail khi:** Sai format, index 0, hoặc null.

---

### TC 1.2 — `LevelIdToIndex`

**Mục tiêu:** Parse ngược từ tên file.

**Các bước:**

1. Log hoặc evaluate:
   ```csharp
   Debug.Log(CampaignSession.LevelIdToIndex("campaign_0012"));
   ```
2. Play Mode → xem Console.

**Pass khi:** In ra `12`.

**Fail khi:** `0` hoặc sai số với chuỗi hợp lệ.

---

### TC 1.3 — Normalize campaign pointer

**Mục tiêu:** Khi `currentLevelId` lệch index, load sẽ sửa lại.

**Các bước:**

1. Chạy game 1 lần để có `PlayerData.json`.
2. **Tắt Play.**
3. Mở `PlayerData.json` bằng Notepad.
4. Sửa trong `"campaign"`:
   - `"currentLevelIndex": 2`
   - `"currentLevelId": ""` (hoặc `"campaign_0099"`)
5. Lưu file.
6. Play MainMenu lại (LocalDataManager load lại).

**Pass khi:** Sau load, mở lại JSON thấy `currentLevelId` = `"campaign_0002"` (khớp index 2).

**Fail khi:** Id vẫn rỗng/sai và gameplay load nhầm màn.

---

### TC 1.4 — Compile

**Mục tiêu:** Không còn reference type cũ.

**Các bước:**

1. Quay lại Unity, đợi compile xong (góc dưới không quay vòng).
2. Mở **Window → General → Console**, lọc **Error**.
3. (Tuỳ chọn) **File → Build Settings → Build** hoặc chỉ cần 0 error trong Editor.

**Pass khi:** Không error kiểu `SessionResume không tồn tại`, `LevelProgress`, `UpdateLevelState`.

---

## Phase 2 — LocalDataManager

### TC 2.1 — Tân thủ (profile mới)

**Mục tiêu:** Lần đầu vào game tạo save mặc định.

**Các bước:**

1. Tắt Play.
2. Xóa hết file trong folder `...\NumStrata\`.
3. Play scene **MainMenu** (hoặc scene có `LocalDataManager` + Home).
4. Đợi 1–2 giây (Awake + LoadData).
5. Mở folder save — phải xuất hiện `PlayerData.json`.
6. Mở file JSON, tìm block `"campaign"`.

**Pass khi:**

- `"currentLevelIndex": 1`
- `"currentLevelId": "campaign_0001"`
- `"hasActiveRun": false`
- Có `"playerId"` (chuỗi UUID dài)

---

### TC 2.2 — `saveVersion` và timestamp

**Các bước:**

1. Dùng `PlayerData.json` từ TC 2.1 (hoặc sau bất kỳ lần chơi nào).
2. Mở file, tìm dòng đầu các field envelope.

**Pass khi:**

- `"saveVersion": 1`
- `"lastModifiedAt"` là số > 0 (unix giây, ví dụ `1730000000`)

---

### TC 2.3 — `AddGold` không ghi disk ngay

**Mục tiêu:** Chỉ dirty RAM, không flush giữa chừng.

**Các bước:**

1. Play MainMenu, ghi nhận `gold` hiện tại trong JSON (ví dụ `0`).
2. **Không tắt Play.** Trong khi đang Play:
   - Cách A: Tạm thêm nút debug gọi `LocalDataManager.Instance.AddGold(10);`
   - Cách B: Dùng **Runtime Debug** / Inspector custom button nếu có.
3. Ngay sau khi gọi `AddGold`, **không** bấm Pause, **không** Alt+Tab.
4. Mở `PlayerData.json` từ Explorer (Windows vẫn đọc được khi game đang chạy — có thể cần refresh F5).

**Pass khi:** `gold` trong file **vẫn là giá trị cũ** (ví dụ vẫn 0).

**Lưu ý:** Nếu UI Home đã refresh gold từ RAM, UI có thể hiện 10 nhưng file vẫn 0 — đó là đúng.

---

### TC 2.4 — Pause flush

**Mục tiêu:** Milestone pause ghi disk.

**Các bước:**

1. Tiếp TC 2.3 (đã `AddGold(10)`, file chưa đổi).
2. **Alt+Tab** ra ngoài Unity (hoặc bấm Pause trên Game view) để `OnApplicationPause(true)` chạy.
   - Hoặc: Stop Play — `OnApplicationQuit` cũng flush.
3. Mở lại `PlayerData.json`.

**Pass khi:** `"gold": 10` (hoặc = giá trị cũ + 10).

Console có thể có: `[LocalDataManager] Player data saved: ...`

---

### TC 2.5 — Backup `.bak`

**Các bước:**

1. Đảm bảo đã flush ít nhất 1 lần (có `PlayerData.json`).
2. Gây flush lần 2: vào Gameplay (BeginCampaignRun) hoặc pause lại.
3. Kiểm tra folder save.

**Pass khi:** Có file `PlayerData.bak` cạnh `PlayerData.json`.

---

### TC 2.6 — `BeginCampaignRun`

**Các bước:**

1. Từ MainMenu, bấm **Play** vào scene Gameplay.
2. Đợi level load xong (Console: `[LevelLoader] Level load completed.`).
3. **Stop Play** (hoặc Pause app).
4. Mở `PlayerData.json` → `"campaign"`.

**Pass khi:**

- `"hasActiveRun": true`
- `"currentLevelId"` vẫn là màn đang chơi (vd. `campaign_0001` nếu mới tân thủ)

---

### TC 2.7 — `CompleteCampaignLevel`

**Cách A — Chơi thật (khuyến nghị):**

1. Tân thủ hoặc reset save, Play → Gameplay màn 1.
2. Giải hết tile trên board (đến khi counter = 0 / board trống).
3. Console: `[LocalDataManager] Level cleared. Now at campaign_0002.`
4. Mở JSON.

**Pass khi:**

- `currentLevelIndex`: **2**
- `currentLevelId`: **campaign_0002**
- `hasActiveRun`: **false**
- Không còn `SessionResume.json` (hoặc đã bị xóa)

**Cách B — Debug (nhanh):**

1. Play Gameplay, trong Inspector chọn object có `LocalDataManager`.
2. Gọi public method qua script debug tạm: `CompleteCampaignLevel()` — *chỉ nếu bạn thêm ContextMenu / button test.*

---

### TC 2.8 — `FailCampaignLevel` (API)

**Các bước:**

1. Play Gameplay để có `SessionResume.json` (chơi vài giây).
2. Ghi `currentLevelIndex` trong JSON (vd. 1).
3. Gọi debug `LocalDataManager.Instance.FailCampaignLevel();` (script tạm hoặc ContextMenu bạn thêm).
4. Kiểm tra folder + JSON.

**Pass khi:**

- Index **không đổi**
- `hasActiveRun`: false
- `SessionResume.json` **biến mất**

---

### TC 2.9 — Resume corrupt

**Các bước:**

1. Có sẵn `PlayerData.json` hợp lệ + `SessionResume.json` (đang chơi dở).
2. Ghi `gold` và `currentLevelIndex` để so sánh sau.
3. **Tắt Play.** Mở `SessionResume.json`, xóa hết nội dung, gõ `{broken` , lưu.
4. Play lại (load game).

**Pass khi:**

- `SessionResume.json` bị xóa hoặc không còn dùng được
- `PlayerData.json`: gold và index **giống trước khi test**

---

### TC 2.10 — Settings trong PlayerPrefs

**Các bước:**

1. Play MainMenu.
2. Trên Home, xem format ngày (tiếng Việt nếu `Language` = vi).
3. **Stop Play.** Mở `PlayerData.json`, tìm chữ `Language` / `SoundVolume`.

**Pass khi:** **Không** có field settings trong PlayerData (chỉ economy + campaign).

4. (Tuỳ chọn) Dùng Registry/PlayerPrefs viewer hoặc script:
   ```csharp
   PlayerPrefs.SetString("Language", "en"); PlayerPrefs.Save();
   ```
5. Play lại → ngày hiển thị kiểu tiếng Anh (MONDAY, JANUARY ...).

---

## Phase 3 — Home UI

### TC 3.1 — Nhãn Level = index

**Các bước:**

1. Save tân thủ (TC 2.1).
2. Play **MainMenu**, mở tab Home.
3. Nhìn ô **Level** trên Progress Card (số lớn giữa card).

**Pass khi:** Hiển thị **1**.

---

### TC 3.2 — Sau clear màn 1

**Các bước:**

1. Hoàn thành TC 2.7 (đã clear màn 1, JSON index = 2).
2. Load scene **MainMenu** (Pause → Home hoặc Play từ đầu scene menu).
3. Xem lại số Level trên Home.

**Pass khi:** Hiển thị **2**.

---

### TC 3.3 — Nút Play

**Các bước:**

1. Ở Home với index = 2 (sau clear màn 1).
2. Mở Console, clear log.
3. Bấm **Play**.
4. Đọc log ngay sau click.

**Pass khi:**

- Log: `Starting campaign level 'campaign_0002'`
- Scene chuyển sang Gameplay

5. (Tuỳ chọn) Sau khi level load, kiểm tra PlayerPrefs key `PendingLevelId` đã bị xóa bởi LevelLoader — không còn giá trị cũ sau load.

---

### TC 3.4 — Gold / Streak đọc từ file

**Các bước:**

1. **Stop Play.**
2. Mở `PlayerData.json`, đổi `"gold": 9999`, `"totalStreak": 42`, lưu.
3. Play MainMenu → Home.
4. Nhìn header: coin và streak.

**Pass khi:** UI hiện **9999** và **42** (format có dấu phẩy nghìn tùy locale).

---

## Phase 4 — Gameplay hooks

### TC 4.1 — Load đúng file campaign

**Các bước:**

1. Tân thủ: Play Home → Gameplay.
2. Console khi load:

**Pass khi:**

- Không warning `Không tìm thấy JSON cho 'campaign_0001'`
- Level spawn đúng layout file `Assets/_Game/Resources/Campaign/campaign_0001.json`

3. Sau clear màn 1, Play lại → log / nội dung màn phải tương ứng `campaign_0002.json`.

---

### TC 4.2 — Save SessionResume theo mốc (Home/close)

**Các bước:**

1. Play Gameplay, chơi 10–15 giây.
2. Trong lúc đang chơi, theo dõi `SessionResume.json` (nếu có từ lần trước thì xóa trước khi test).
3. Không bấm Home, không pause app.

**Pass khi:**

- **Không** tạo/cập nhật `SessionResume.json` chỉ vì thao tác gameplay
- Khi bấm Pause → Home, file `SessionResume.json` được ghi/cập nhật
- Khi Alt+Tab (pause app) hoặc stop app đúng vòng đời, file `SessionResume.json` được ghi/cập nhật

**Cách đo chính xác hơn:** Mở `SessionResume.json`, ghi `"savedAt"`, chơi thêm nhưng không pause/home thì `savedAt` không đổi.

---

### TC 4.3 — Thắng tạm thời (remaining tile = 0)

**Các bước:**

1. Tân thủ, vào Gameplay màn 1.
2. Chơi đến khi **hết tile** trên board (UI đếm tile = 0).
3. Đợi animation xóa tile xong (~0.5s).
4. Đọc Console.

**Pass khi:**

- `[LocalDataManager] Level cleared. Now at campaign_0002.`
- `SessionResume.json` không còn
- `PlayerData.json`: index 2

---

### TC 4.4 — Về Home khi đang chơi dở

**Các bước:**

1. Tân thủ, Play → Gameplay, chơi vài nước (có resume).
2. Ghi index trong JSON = 1.
3. Mở Pause → bấm **Home**.
4. Mở JSON + folder save.

**Pass khi:**

- `currentLevelIndex` vẫn **1**
- `SessionResume.json` có dữ liệu mới (`savedAt` cập nhật)
- Khi Play lại vẫn vào cùng màn hiện tại

---

### TC 4.5 — Retry sau abandon

**Các bước:**

1. Tiếp TC 4.4 (chưa clear màn).
2. Ở MainMenu Home, bấm **Play** lại.
3. Quan sát level + Console.

**Pass khi:**

- Vẫn load **campaign_0001** (cùng index)
- Board **spawn mới** từ JSON level (không khôi phục tile cũ từ resume — đúng scope hiện tại)

---

### TC 4.6 — Không replay màn đã clear

**Các bước:**

1. Hoàn thành clear màn 1 (index = 2).
2. Về MainMenu, bấm Play.

**Pass khi:**

- Log: `campaign_0002`
- **Không** còn load `campaign_0001` làm màn chính

---

### TC 4.7 — Thua tạm thời (remaining tile < 4 và helper left = 0)

**Các bước:**

1. Vào Gameplay.
2. Dùng helper đến hết lượt (3 lần) để `helperUsesLeft = 0`.
3. Chơi đến khi tile còn lại trên board còn 1–3 tile.
4. Đợi animation kết thúc và đọc Console.

**Pass khi:**

- Log warning: `LOSE (temporary rule): remainingTiles=... helperUsesLeft=0`
- `FailCampaignLevel()` được ghi nhận (index không tăng)
- Về Home rồi Play lại vẫn cùng level hiện tại

---

## Phase 5 — QA tổng hợp

### TC 5.1 — Offline

**Các bước:**

1. Tắt Wi‑Fi / Ethernet trên PC (hoặc Airplane mode trên thiết bị build).
2. Play full loop: MainMenu → Gameplay → chơi → Pause → Home.
3. Mở JSON khi đã flush.

**Pass khi:** Không crash vì mạng; save local vẫn ghi.

---

### TC 5.2 — Kill app giữa ván

**Các bước:**

1. Play Gameplay, chơi 30s, **Alt+Tab** (flush pause) hoặc để autosave chạy.
2. **Force stop** Play trong Unity (hoặc kill app trên device).
3. Mở lại game / Play lại.

**Pass khi:**

- `PlayerData.json` còn, `hasActiveRun` có thể true/false tùy đã pause chưa
- `SessionResume.json` có thể còn (chưa restore board — chỉ kiểm tra **tồn tại file**)

---

### TC 5.3 — Kill app sau clear

**Các bước:**

1. Clear màn 1, thấy log level cleared.
2. Stop Play ngay hoặc về MainMenu rồi Stop.
3. Play lại MainMenu.

**Pass khi:** Home vẫn Level **2**; JSON index = 2.

---

## Chưa test pass (ngoài scope)

| Hạng mục | Lý do |
| -------- | ----- |
| Restore board từ resume | Chưa implement |
| Cloud sync | Chưa implement |
| Fail từ luồng thua GDD | `FailCampaignLevel` chỉ test gọi tay |

---

## Bảng tra nhanh Pass / Fail

| Triệu chứng | Có thể do |
| ----------- | --------- |
| Home Level luôn 1 | Chưa flush / chưa clear / JSON không load |
| Play load sai màn | `PendingLevelId` hoặc Resources thiếu file |
| Gold UI đổi nhưng file không | **Đúng** nếu chưa milestone — TC 2.3 |
| Clear xong vẫn màn 1 | `IsBoardCleared` chưa chạy / chưa hết tile |
| Resume ghi liên tục | `resumeSaveIntervalSeconds` quá thấp trên LevelLoader |
