using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using NumStrata.Utils;
using NumStrata.Data;
using NumStrata.UI;

namespace NumStrata.Gameplay
{
    public class HelperManager : MonoBehaviour
    {
        public static HelperManager Instance { get; private set; }

        [Header("UI Structure")]
        public GameObject helperSpawnPopup;
        public Transform[] operatorSlots;
        public Transform[] numberSlots;

        [Header("Buttons")]
        public Canvas btnSpawnGroupCanvas;
        public Button btnSpawnButton;
        public Button btnShuffleGroup;
        public Button btnDeleteGroup;
        public Button btnReturnGroup;
        public Button btnToggleSign;

        [Header("Icons")]
        public Image imgSpawnIcon;
        public Image imgShuffleIcon;
        public Image imgDeleteIcon;
        public Image imgToggleSignIcon;

        [Header("Sprites")]
        public Sprite helperIconCancel;
        public Sprite helperIconNegative;
        public Sprite helperIconPositive;
        
        [Header("Data")]
        public TileSpriteData tileSpriteData;

        // State
        private bool isSpawnActive = false;
        public bool isDeleteActive = false;
        private bool isSignNegative = false;
        private int currentLevelHelperUses = 0;
        public bool hasFreeHelperUsage = false;

        public int GetLevelHelperUses() { return currentLevelHelperUses; }
        public void SetLevelHelperUses(int val) { currentLevelHelperUses = val; }
        public void ResetHelperUses()
        {
            currentLevelHelperUses = 0;
            Debug.Log("[HelperManager] Resetted helper uses to 0.");
        }

        private Sprite defaultSpawnIcon;
        private Sprite defaultShuffleIcon;
        private Sprite defaultDeleteIcon;

        private List<Tile> popupTiles = new List<Tile>();
        private Vector2 lastReferenceSlotSize = Vector2.zero;
        private Vector2 lastScreenSize = Vector2.zero;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Initialize UI state
            if (helperSpawnPopup != null) helperSpawnPopup.SetActive(false);
            }

        private void Start()
        {
            // 1. Lưu trữ icon gốc để dùng khi Reset
            if (imgSpawnIcon != null) defaultSpawnIcon = imgSpawnIcon.sprite;
            if (imgShuffleIcon != null) defaultShuffleIcon = imgShuffleIcon.sprite;
            if (imgDeleteIcon != null) defaultDeleteIcon = imgDeleteIcon.sprite;

            // 2. Chỉ đăng ký listener một lần duy nhất ở đây
            if (btnSpawnButton != null)
            {
                btnSpawnButton.onClick.RemoveAllListeners(); // Xóa sạch để tránh bị gán chồng hoặc gán từ Inspector
                btnSpawnButton.onClick.AddListener(ToggleSpawn);
            }
            if (btnShuffleGroup != null)
            {
                btnShuffleGroup.onClick.RemoveAllListeners();
                btnShuffleGroup.onClick.AddListener(ExecuteShuffle);
            }
            if (btnDeleteGroup != null)
            {
                btnDeleteGroup.onClick.RemoveAllListeners();
                btnDeleteGroup.onClick.AddListener(ToggleDelete);
            }
            if (btnReturnGroup != null)
            {
                btnReturnGroup.onClick.RemoveAllListeners();
                btnReturnGroup.onClick.AddListener(ExecuteReturn);
            }
            if (btnToggleSign != null)
            {
                btnToggleSign.onClick.RemoveAllListeners();
                btnToggleSign.onClick.AddListener(ToggleSign);
            }
            
            // 3. Khởi tạo nội dung Popup
            StartCoroutine(InitializeHelperPopupRoutine());
        }

        private void LateUpdate()
        {
            RectTransform referenceSlot = GetReferenceBoardSlot();
            if (referenceSlot == null)
            {
                return;
            }

            if (Screen.width != (int)lastScreenSize.x || Screen.height != (int)lastScreenSize.y)
            {
                lastScreenSize = new Vector2(Screen.width, Screen.height);
            }

            Vector2 currentSize = referenceSlot.rect.size;
            if (currentSize.x <= 0f || currentSize.y <= 0f)
            {
                return;
            }

            if (!Mathf.Approximately(currentSize.x, lastReferenceSlotSize.x) ||
                !Mathf.Approximately(currentSize.y, lastReferenceSlotSize.y))
            {
                lastReferenceSlotSize = currentSize;
                SyncPopupTileSizes(referenceSlot);
            }
        }

        private IEnumerator InitializeHelperPopupRoutine()
        {
            yield return new WaitForEndOfFrame();

            // 1. Thiết lập cho Operator Slots
            SetupHelperGridSlots(operatorSlots);

            // 2. Thiết lập cho Number Slots
            SetupHelperGridSlots(numberSlots);

            Debug.Log("[HelperManager] Khởi tạo Helper Popup thành công.");
        }

        private void SetupHelperGridSlots(Transform[] slots)
        {
            if (slots == null) return;

            foreach (Transform slot in slots)
            {
                if (slot == null) continue;

                // Xóa Tile cũ nếu có
                foreach (Transform child in slot) Destroy(child.gameObject);

                Tile template = FormulaManager.Instance.GetRandomBoardTileTemplate();
                if (template == null) continue;

                // 2. Instantiate Tile mẫu làm con của Slot với worldPositionStays: false
                GameObject cloneObj = Instantiate(template.gameObject, slot, false);
                cloneObj.name = $"Tile_{slot.name}";
                
                Tile tile = cloneObj.GetComponent<Tile>();
                RectTransform rectT = cloneObj.GetComponent<RectTransform>();

                // 3. Thiết lập thuộc tính chuẩn
                AspectRatioFitter aspectFitter = cloneObj.GetComponent<AspectRatioFitter>();
                if (aspectFitter != null) aspectFitter.enabled = false;

                rectT.anchorMin = new Vector2(0.5f, 0.5f);
                rectT.anchorMax = new Vector2(0.5f, 0.5f);
                rectT.pivot = new Vector2(0.5f, 0.5f);
                rectT.localScale = Vector3.one; // Reset localScale về (1, 1, 1)

                // 4. Gán sizeDelta bằng đúng rect.size của Tile mẫu
                rectT.sizeDelta = template.GetComponent<RectTransform>().rect.size;
                rectT.anchoredPosition = Vector2.zero;

                RectTransform referenceSlot = GetReferenceBoardSlot();
                if (referenceSlot != null)
                {
                    UISizeSync sizeSync = cloneObj.GetComponent<UISizeSync>();
                    if (sizeSync == null) sizeSync = cloneObj.AddComponent<UISizeSync>();
                    sizeSync.target = referenceSlot;
                    sizeSync.syncWidth = true;
                    sizeSync.syncHeight = true;
                    ApplyReferenceSize(rectT, referenceSlot);
                }

                // Logic Helper
                tile.type = TileType.Helper;
                tile.isLocked = false;
                if (tile.backgroundRenderer != null)
                {
                    tile.backgroundRenderer.raycastTarget = true;
                    tile.backgroundRenderer.color = Color.white;
                }

                // 5. Đổi giá trị và Sprite
                ConfigureHelperTile(tile, slot.name);
                tile.type = TileType.Helper; // Đảm bảo giữ type Helper

                // Gán Sorting Order cho Tile lọt vào Popup (Ví dụ: Order = 1000 để nằm lên trên Popup)
                Canvas tileCanvas = cloneObj.GetComponent<Canvas>();
                if (tileCanvas == null) tileCanvas = cloneObj.AddComponent<Canvas>();
                tileCanvas.overrideSorting = true;
                tileCanvas.sortingOrder = 1000;

                GraphicRaycaster tileRaycaster = cloneObj.GetComponent<GraphicRaycaster>();
                if (tileRaycaster == null) cloneObj.AddComponent<GraphicRaycaster>();
                
                popupTiles.Add(tile);
            }
        }

        private void ConfigureHelperTile(Tile tile, string slotName)
        {
            if (tileSpriteData == null) return;

            // Ví dụ: slotName = "Num_5" hoặc "Tile_Helper_+"
            if (slotName.StartsWith("Num_"))
            {
                string valStr = slotName.Replace("Num_", "");
                if (int.TryParse(valStr, out int val))
                {
                    tile.SetupNumber(val, tileSpriteData.GetNumberSprite(val));
                }
            }
            else if (slotName.StartsWith("Tile_Helper_"))
            {
                string op = slotName.Replace("Tile_Helper_", "");
                tile.SetupOperator(op, tileSpriteData.GetOperatorSprite(op));
            }
        }

        private RectTransform GetReferenceBoardSlot()
        {
            if (FormulaManager.Instance == null)
            {
                return null;
            }

            return FormulaManager.Instance.referenceBoardSlot;
        }

        private void SyncPopupTileSizes(RectTransform referenceSlot)
        {
            for (int i = 0; i < popupTiles.Count; i++)
            {
                Tile tile = popupTiles[i];
                if (tile == null) continue;

                RectTransform rectT = tile.GetComponent<RectTransform>();
                if (rectT == null) continue;

                ApplyReferenceSize(rectT, referenceSlot);
            }
        }

        private void ApplyReferenceSize(RectTransform rectT, RectTransform referenceSlot)
        {
            if (rectT == null || referenceSlot == null) return;

            Vector2 size = referenceSlot.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;

            rectT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            rectT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }

        private void ToggleSpawn()
        {
            if (currentLevelHelperUses >= 3 && !isSpawnActive && !hasFreeHelperUsage)
            {
                Debug.LogWarning("[HelperManager] Đã đạt giới hạn sử dụng tối đa 3 Helper trong màn chơi này.");
                return;
            }

            isSpawnActive = !isSpawnActive;
            
            if (isSpawnActive)
            {
                // Tắt các trợ giúp khác nếu đang bật
                if (isDeleteActive) ToggleDelete();

                // Đổi icon sang nút Hủy
                if (imgSpawnIcon != null && helperIconCancel != null) imgSpawnIcon.sprite = helperIconCancel;
                
                // Hiện lớp nền mờ (Shared Dimmer) và Popup
                if (NumStrata.UI.PauseManager.Instance != null) 
                    NumStrata.UI.PauseManager.Instance.ToggleDimmer(true, 0.25f);

                if (btnSpawnGroupCanvas != null) btnSpawnGroupCanvas.overrideSorting = true;
                
                if (helperSpawnPopup != null) 
                {
                    helperSpawnPopup.SetActive(true);
                    if (UIEffectManager.Instance != null)
                        UIEffectManager.Instance.ScaleUp(helperSpawnPopup.transform, 0.25f);
                }
            }
            else
            {
                CloseSpawnPopup();
            }
        }

        private void CloseSpawnPopup()
        {
            isSpawnActive = false;
            // Trả lại icon gốc
            if (imgSpawnIcon != null && defaultSpawnIcon != null) imgSpawnIcon.sprite = defaultSpawnIcon;
            
            if (helperSpawnPopup != null && helperSpawnPopup.activeSelf && UIEffectManager.Instance != null)
            {
                if (NumStrata.UI.PauseManager.Instance != null) 
                    NumStrata.UI.PauseManager.Instance.ToggleDimmer(false, 0.2f);

                UIEffectManager.Instance.ScaleDown(helperSpawnPopup.transform, 0.2f, () => 
                {
                    if (helperSpawnPopup != null) helperSpawnPopup.SetActive(false);
                    if (btnSpawnGroupCanvas != null) btnSpawnGroupCanvas.overrideSorting = false;
                });
            }
            else
            {
                if (helperSpawnPopup != null) helperSpawnPopup.SetActive(false);
                if (btnSpawnGroupCanvas != null) btnSpawnGroupCanvas.overrideSorting = false;
                
                if (NumStrata.UI.PauseManager.Instance != null) 
                    NumStrata.UI.PauseManager.Instance.ToggleDimmer(false, 0f);
            }
        }

        private void ToggleSign()
        {
            isSignNegative = !isSignNegative;
            
            if (imgToggleSignIcon != null)
            {
                imgToggleSignIcon.sprite = isSignNegative ? helperIconNegative : helperIconPositive;
            }

            UpdatePopupTiles();
        }

        private void UpdatePopupTiles()
        {
            if (tileSpriteData == null) return;

            foreach (var tile in popupTiles)
            {
                // Chỉ cập nhật các Tile số
                if (string.IsNullOrEmpty(tile.operatorValue))
                {
                    // Cập nhật giá trị (đổi dấu)
                    tile.numberValue *= -1;
                    
                    // Cập nhật Sprite tương ứng
                    Sprite newSprite = tileSpriteData.GetNumberSprite(tile.numberValue);
                    if (newSprite != null)
                    {
                        tile.backgroundRenderer.sprite = newSprite;
                    }
                }
            }
        }

        public void OnHelperTileClicked(Tile clickedTile)
        {
            if (!isSpawnActive) return;
            isSpawnActive = false;

            if (FormulaManager.Instance == null) return;

            if (FormulaManager.Instance.TryGetNextConveyorSlot(out Transform slot, out int index))
            {
                // 1. Lưu lại Vị trí thế giới (World Position) hiện tại
                Vector3 worldPos = clickedTile.transform.position;

                // Tạo bản sao để di chuyển (giữ popup nguyên vẹn)
                GameObject clonedObj = Instantiate(clickedTile.gameObject);
                Tile spawnedTile = clonedObj.GetComponent<Tile>();
                RectTransform rectT = spawnedTile.GetComponent<RectTransform>();

                // Giảm Sorting Order xuống 10 sau khi click (thay vì 1000 như trong Popup)
                Canvas spawnedCanvas = clonedObj.GetComponent<Canvas>();
                if (spawnedCanvas != null) spawnedCanvas.sortingOrder = 10;

                // Chuyển từ trạng thái Helper sang Tile thật
                if (!string.IsNullOrEmpty(clickedTile.operatorValue))
                {
                    spawnedTile.type = TileType.Operator;
                    spawnedTile.SetupOperator(clickedTile.operatorValue, clickedTile.backgroundRenderer.sprite);
                }
                else
                {
                    spawnedTile.type = TileType.Number;
                    spawnedTile.SetupNumber(clickedTile.numberValue, clickedTile.backgroundRenderer.sprite);
                }

                // 3. Gán cha của Tile sang Slot Conveyor ngay lập tức (worldPositionStays: false)
                rectT.SetParent(slot, false);

                // 4. Reset localScale về (1, 1, 1) ngay lập tức
                rectT.localScale = Vector3.one;

                // 5. Gán lại Vị trí thế giới về vị trí cũ đã lưu
                rectT.position = worldPos;

                // Đăng ký logic vào FormulaManager
                FormulaManager.Instance.RegisterTileToConveyor(spawnedTile, index);
                if (hasFreeHelperUsage)
                {
                    hasFreeHelperUsage = false;
                    Debug.Log("[HelperManager] Free helper usage consumed. Not increasing level uses count.");
                }
                else
                {
                    currentLevelHelperUses++;
                }
                Debug.Log($"[HelperManager] Helper used {currentLevelHelperUses}/3 this level.");

                // Cập nhật số đếm Tile sau khi Helper Spawn thành công
                if (NumStrata.Gameplay.TileCounter.Instance != null) 
                    NumStrata.Gameplay.TileCounter.Instance.UpdateTileCountUI();
                CampaignSaveHooks.EvaluateTemporaryOutcomeAfterBoardChange();

                // 6. Gọi đồng thời
                if (imgSpawnIcon != null && defaultSpawnIcon != null) imgSpawnIcon.sprite = defaultSpawnIcon;
                
                if (NumStrata.UI.PauseManager.Instance != null) 
                    NumStrata.UI.PauseManager.Instance.ToggleDimmer(false, 0.3f);

                if (helperSpawnPopup != null && UIEffectManager.Instance != null)
                {
                    UIEffectManager.Instance.ScaleDown(helperSpawnPopup.transform, 0.3f, () => 
                    {
                        if (helperSpawnPopup != null) helperSpawnPopup.SetActive(false);
                        if (btnSpawnGroupCanvas != null) btnSpawnGroupCanvas.overrideSorting = false;
                    });
                }

                // Move về tâm Slot (0,0)
                if (UIEffectManager.Instance != null)
                {
                    UIEffectManager.Instance.MoveTo(rectT, Vector2.zero, 0.3f);
                }
                else
                {
                    rectT.anchoredPosition = Vector2.zero;
                }
            }
            else
            {
                CloseSpawnPopup();
            }
        }

        private struct TileData
        {
            public TileType type;
            public int numberValue;
            public string operatorValue;
            public Sprite sprite;
            public bool isMystery;
        }

        public void ExecuteShuffle()
        {
            ExecuteShuffle(false);
        }

        public void ExecuteShuffle(bool isFree)
        {
            if (!isFree)
            {
                if (currentLevelHelperUses >= 3 && !hasFreeHelperUsage)
                {
                    Debug.LogWarning("[HelperManager] Đã đạt giới hạn sử dụng tối đa 3 Helper trong màn chơi này.");
                    return;
                }
            }

            LevelLoader levelLoader = LevelLoader.Instance;
            if (levelLoader == null) return;

            List<Tile> activeTiles = levelLoader.GetActiveBoardTiles();
            if (activeTiles == null || activeTiles.Count <= 1) return;

            // Bước 1: Thu thập dữ liệu
            List<TileData> tileDataList = new List<TileData>();
            foreach (Tile tile in activeTiles)
            {
                tileDataList.Add(new TileData
                {
                    type = tile.type,
                    numberValue = tile.numberValue,
                    operatorValue = tile.operatorValue,
                    sprite = tile.backgroundRenderer != null ? tile.backgroundRenderer.sprite : null,
                    isMystery = tile.isMystery
                });
            }

            // Bước 2: Xáo trộn (Fisher-Yates)
            for (int i = 0; i < tileDataList.Count; i++)
            {
                int randomIndex = UnityEngine.Random.Range(i, tileDataList.Count);
                TileData temp = tileDataList[i];
                tileDataList[i] = tileDataList[randomIndex];
                tileDataList[randomIndex] = temp;
            }

            // Bước 3 & 4: Hiệu ứng Chaos Fly và Tráo đổi
            for (int i = 0; i < activeTiles.Count; i++)
            {
                Tile tile = activeTiles[i];
                TileData data = tileDataList[i];
                RectTransform rect = tile.GetComponent<RectTransform>();

                // Tính toán vị trí ngẫu nhiên
                Vector2 randomPos = UnityEngine.Random.insideUnitCircle * 300f;

                if (UIEffectManager.Instance != null)
                {
                    // Bay ra
                    UIEffectManager.Instance.MoveTo(rect, randomPos, 0.2f, () =>
                    {
                        // Gán lại dữ liệu sau khi bay ra
                        if (data.type == TileType.Operator)
                        {
                            tile.SetupOperator(data.operatorValue, data.sprite);
                        }
                        else
                        {
                            tile.SetupNumber(data.numberValue, data.sprite);
                        }
                        tile.SetMysteryMask(data.isMystery);

                        // Bay về tâm của Slot cũ
                        UIEffectManager.Instance.MoveTo(rect, Vector2.zero, 0.3f, () =>
                        {
                            // Cập nhật độ sáng tối theo đúng logic đè lớp
                            tile.SetVisualState(tile.coveringTiles.Count == 0);
                            
                            // Hiệu ứng Scale nhẹ (1.2 -> 1.0) để tạo cảm giác "vừa hạ cánh"
                            UIEffectManager.Instance.ScaleUp(tile.transform, 0.2f, 1.2f);
                        });
                    });
                }
            }

            if (!isFree)
            {
                if (hasFreeHelperUsage)
                {
                    hasFreeHelperUsage = false;
                    Debug.Log("[HelperManager] Free Shuffle usage consumed. Not increasing level uses count.");
                }
                else
                {
                    currentLevelHelperUses++;
                }
                Debug.Log($"[HelperManager] Helper used {currentLevelHelperUses}/3 this level.");

                if (NumStrata.Gameplay.TileCounter.Instance != null)
                    NumStrata.Gameplay.TileCounter.Instance.UpdateTileCountUI();
                CampaignSaveHooks.EvaluateTemporaryOutcomeAfterBoardChange();
            }
        }

        private void ToggleDelete()
        {
            if (!isDeleteActive)
            {
                if (currentLevelHelperUses >= 3 && !hasFreeHelperUsage)
                {
                    Debug.LogWarning("[HelperManager] Đã đạt giới hạn sử dụng tối đa 3 Helper trong màn chơi này.");
                    return;
                }
            }

            isDeleteActive = !isDeleteActive;
            if (isDeleteActive)
            {
                if (isSpawnActive) ToggleSpawn();

                if (imgDeleteIcon != null && helperIconCancel != null) imgDeleteIcon.sprite = helperIconCancel;
            }
            else
            {
                if (imgDeleteIcon != null && defaultDeleteIcon != null) imgDeleteIcon.sprite = defaultDeleteIcon;
            }

            // Bật/tắt khả năng click cho các Tile trên Formula Bar
            if (FormulaManager.Instance != null)
            {
                foreach (Tile tile in FormulaManager.Instance.occupiedTiles)
                {
                    if (tile != null && tile.backgroundRenderer != null)
                    {
                        tile.backgroundRenderer.raycastTarget = isDeleteActive;
                    }
                }
            }
        }

        public void ExecuteTileDeletion(Tile tile)
        {
            if (tile == null) return;

            // Charge helper use
            if (hasFreeHelperUsage)
            {
                hasFreeHelperUsage = false;
                Debug.Log("[HelperManager] Free Delete usage consumed. Not increasing level uses count.");
            }
            else
            {
                currentLevelHelperUses++;
            }
            Debug.Log($"[HelperManager] Helper used {currentLevelHelperUses}/3 this level.");

            bool isRemoved = false;

            if (FormulaManager.Instance != null)
            {
                // Kiểm tra trên Conveyor
                for (int i = 0; i < FormulaManager.Instance.occupiedConveyorTiles.Length; i++)
                {
                    if (FormulaManager.Instance.occupiedConveyorTiles[i] == tile)
                    {
                        FormulaManager.Instance.occupiedConveyorTiles[i] = null;
                        FormulaManager.Instance.ShiftConveyorTiles();
                        isRemoved = true;
                        break;
                    }
                }

                // Kiểm tra trên Formula Bar
                if (!isRemoved)
                {
                    for (int i = 0; i < FormulaManager.Instance.occupiedTiles.Length; i++)
                    {
                        if (FormulaManager.Instance.occupiedTiles[i] == tile)
                        {
                            FormulaManager.Instance.occupiedTiles[i] = null;
                            isRemoved = true;
                            break;
                        }
                    }
                }
            }

            if (!isRemoved)
            {
                // Tile nằm trên Board
                tile.ResolveOverlapOnAccept();
                LevelLoader loader = LevelLoader.Instance;
                if (loader != null && loader.allSpawnedTiles.Contains(tile))
                {
                    loader.allSpawnedTiles.Remove(tile);
                }
            }

            if (tile.backgroundRenderer != null)
            {
                tile.backgroundRenderer.raycastTarget = false;
            }

            if (UIEffectManager.Instance != null)
            {
                UIEffectManager.Instance.ScaleDown(tile.transform, 0.2f, () =>
                {
                    if (tile != null) 
                    {
                        tile.gameObject.SetActive(false);
                        Destroy(tile.gameObject);
                    }
                    if (NumStrata.Gameplay.TileCounter.Instance != null) NumStrata.Gameplay.TileCounter.Instance.UpdateTileCountUI();
                    CampaignSaveHooks.EvaluateTemporaryOutcomeAfterBoardChange();
                });
            }
            else
            {
                if (tile != null) tile.gameObject.SetActive(false);
                Destroy(tile.gameObject);
                if (NumStrata.Gameplay.TileCounter.Instance != null) NumStrata.Gameplay.TileCounter.Instance.UpdateTileCountUI();
                CampaignSaveHooks.EvaluateTemporaryOutcomeAfterBoardChange();
            }

            // Tắt chế độ xóa sau khi xóa thành công 1 tile
            ToggleDelete();
        }

        private void ExecuteReturn()
        {
            if (currentLevelHelperUses >= 3 && !hasFreeHelperUsage)
            {
                Debug.LogWarning("[HelperManager] Đã đạt giới hạn sử dụng tối đa 3 Helper trong màn chơi này.");
                return;
            }

            if (FormulaManager.Instance == null) return;

            // Bước 1: Kiểm tra dung lượng Conveyor
            List<int> availableConveyorIndices = new List<int>();
            for (int i = 0; i < FormulaManager.Instance.occupiedConveyorTiles.Length; i++)
            {
                if (FormulaManager.Instance.occupiedConveyorTiles[i] == null)
                {
                    availableConveyorIndices.Add(i);
                }
            }

            if (availableConveyorIndices.Count == 0)
            {
                Debug.Log("[HelperManager] Conveyor is full, cannot return tiles.");
                return;
            }

            // Bước 2: Xác định Tile mục tiêu
            List<int> occupiedFormulaIndices = FormulaManager.Instance.GetOccupiedFormulaIndicesRightToLeft();
            if (occupiedFormulaIndices.Count == 0)
            {
                Debug.Log("[HelperManager] Formula bar is empty, nothing to return.");
                return;
            }

            int returnCount = Mathf.Min(availableConveyorIndices.Count, occupiedFormulaIndices.Count);

            // Lấy ra đúng số lượng Tile sẽ rút về (ưu tiên từ phải sang trái)
            List<int> targetFormulaIndices = occupiedFormulaIndices.GetRange(0, returnCount);
            
            // Đảo ngược danh sách vừa lấy để trở lại thứ tự từ trái sang phải.
            // Nhờ đó, khi xếp lên Conveyor, Tile nằm bên trái công thức sẽ vào ô bên trái của Conveyor.
            targetFormulaIndices.Reverse();

            // Bước 3: Thực hiện di chuyển
            for (int i = 0; i < returnCount; i++)
            {
                int fIdx = targetFormulaIndices[i];
                int cIdx = availableConveyorIndices[i];

                Tile tile = FormulaManager.Instance.occupiedTiles[fIdx];
                if (tile == null) continue;

                // Cập nhật logic ngay lập tức
                FormulaManager.Instance.occupiedConveyorTiles[cIdx] = tile;
                FormulaManager.Instance.occupiedTiles[fIdx] = null;

                // Hiệu ứng "Hút ngược" (Juice)
                RectTransform rectT = tile.GetComponent<RectTransform>();
                Transform targetSlot = FormulaManager.Instance.conveyorSlots[cIdx];

                if (rectT != null && targetSlot != null)
                {
                    if (tile.backgroundRenderer != null)
                    {
                        tile.backgroundRenderer.raycastTarget = false; // Tạm tắt click khi đang bay
                    }

                    // Tính toán tọa độ đích tương đối với lớp cha CŨ (Formula Slot)
                    Vector3 startWorldPos = rectT.position;
                    rectT.position = targetSlot.position;
                    Vector2 targetAnchoredPos = rectT.anchoredPosition;
                    rectT.position = startWorldPos; // Trả về vị trí ban đầu

                    if (UIEffectManager.Instance != null)
                    {
                        Vector2 currentPos = rectT.anchoredPosition;
                        Vector2 upPos = currentPos + new Vector2(0, 50f);

                        // Bay lên 1 chút (offset y + 50)
                        UIEffectManager.Instance.MoveTo(rectT, upPos, 0.1f, () =>
                        {
                            // Sau đó bay về đích (VẪN ĐANG Ở LỚP CHA CŨ ĐỂ KHÔNG BỊ CHE)
                            UIEffectManager.Instance.MoveTo(rectT, targetAnchoredPos, 0.25f, () =>
                            {
                                // HẠ CÁNH XONG MỚI ĐỔI LỚP CHA
                                rectT.SetParent(targetSlot, true); 
                                rectT.localScale = Vector3.one;
                                rectT.anchoredPosition = Vector2.zero; // Đảm bảo nằm ngay tâm

                                if (tile.backgroundRenderer != null)
                                {
                                    tile.backgroundRenderer.raycastTarget = true;
                                }
                            });
                        });
                    }
                    else
                    {
                        rectT.SetParent(targetSlot, false);
                        rectT.localScale = Vector3.one;
                        rectT.anchoredPosition = Vector2.zero;
                        if (tile.backgroundRenderer != null)
                        {
                            tile.backgroundRenderer.raycastTarget = true;
                        }
                    }
                }
            }

            // Charge helper use
            if (hasFreeHelperUsage)
            {
                hasFreeHelperUsage = false;
                Debug.Log("[HelperManager] Free Return usage consumed. Not increasing level uses count.");
            }
            else
            {
                currentLevelHelperUses++;
            }
            Debug.Log($"[HelperManager] Helper used {currentLevelHelperUses}/3 this level.");

            if (NumStrata.Gameplay.TileCounter.Instance != null)
                NumStrata.Gameplay.TileCounter.Instance.UpdateTileCountUI();
            CampaignSaveHooks.EvaluateTemporaryOutcomeAfterBoardChange();
        }
    }
}
