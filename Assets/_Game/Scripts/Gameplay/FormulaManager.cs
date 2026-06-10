using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using NumStrata.Utils;
using NumStrata.UI;

namespace NumStrata.Gameplay
{
    /// <summary>
    /// Chức năng: Quản lý thanh công thức của game.
    /// Script này lưu trữ các slot trống ở thanh công thức và xử lý logic nhận Tile khi người chơi click.
    /// Nó áp dụng các thay đổi về UI (tắt AspectRatio, đổi Anchor, reset tọa độ) để Tile nằm gọn trong Slot.
    /// </summary>
    public class FormulaManager : MonoBehaviour
    {
        public static FormulaManager Instance { get; private set; }

        [Header("Responsive Settings")]
        [Tooltip("A slot from the board used as a size reference for tiles in formula slots.")]
        public RectTransform referenceBoardSlot;

        [Header("Formula Slots")]
        [Tooltip("The 5 slots available in the formula bar.")]
        public Transform[] formulaSlots = new Transform[5];

        [Header("Conveyor Slots")]
        [Tooltip("Conveyor slots ordered from Slot_0 to Slot_5.")]
        public Transform[] conveyorSlots = new Transform[6];
        [Tooltip("Sprite data used to render spawned remainder tiles on conveyor.")]
        public NumStrata.Data.TileSpriteData tileSpriteData;

        // Mảng lưu trữ trạng thái các ô công thức (ô nào chứa tile nào)
        public Tile[] occupiedTiles = new Tile[5];
        public Tile[] occupiedConveyorTiles = new Tile[6];

        // Hệ thống hàng đợi để thực hiện hoạt ảnh Visual sau khi đã phê duyệt Logic
        private struct PlacementOrder { public Tile tile; public int slotIndex; }
        private Queue<PlacementOrder> visualQueue = new Queue<PlacementOrder>();
        private bool isProcessingQueue = false;
        private Vector2 lastReferenceSlotSize = Vector2.zero;
        private Vector2 lastScreenSize = Vector2.zero;

        /// <summary>
        /// Khởi tạo Singleton để các script khác (như Tile.cs) có thể dễ dàng gọi đến FormulaManager.
        /// </summary>
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                AutoBindConveyorSlotsIfNeeded();
                AutoBindReferenceSlotIfNeeded();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void LateUpdate()
        {
            if (Screen.width != (int)lastScreenSize.x || Screen.height != (int)lastScreenSize.y)
            {
                lastScreenSize = new Vector2(Screen.width, Screen.height);
                AutoBindReferenceSlotIfNeeded();
            }

            if (referenceBoardSlot == null)
            {
                return;
            }

            Vector2 currentSize = referenceBoardSlot.rect.size;
            if (currentSize.x <= 0f || currentSize.y <= 0f)
            {
                return;
            }

            if (!Mathf.Approximately(currentSize.x, lastReferenceSlotSize.x) ||
                !Mathf.Approximately(currentSize.y, lastReferenceSlotSize.y))
            {
                lastReferenceSlotSize = currentSize;
                SyncOccupiedTileSizes();
            }
        }

        private void AutoBindReferenceSlotIfNeeded()
        {
            if (referenceBoardSlot != null) return;
            
            // Tìm một slot bất kỳ trên board để làm mẫu
            GameObject slotObj = GameObject.Find("Slot_1-1");
            if (slotObj == null) slotObj = GameObject.FindWithTag("BoardSlot");
            if (slotObj == null)
            {
                BoardCoordinate coord = Object.FindFirstObjectByType<BoardCoordinate>();
                if (coord != null) slotObj = coord.gameObject;
            }

            if (slotObj != null)
            {
                referenceBoardSlot = slotObj.GetComponent<RectTransform>();
            }
        }

        /// <summary>
        /// Tín hiệu "Xin phép" từ Tile. Đầu não sẽ kiểm tra xem có chỗ trống không.
        /// </summary>
public bool RequestTilePlacement(Tile tile)
        {
            int targetSlotIndex = -1;

            if (tile.type == TileType.Operator)
            {
                if (occupiedTiles[1] == null) targetSlotIndex = 1;
            }
            else
            {
                int[] numberSlotIndices = { 0, 2, 3, 4 };
                foreach (int i in numberSlotIndices)
                {
                    if (occupiedTiles[i] == null)
                    {
                        targetSlotIndex = i;
                        break;
                    }
                }
            }

            if (targetSlotIndex == -1)
            {
                Debug.Log($"[FormulaBrain] Từ chối {tile.name}: Hết chỗ!");
                return false;
            }

            // PHÊ DUYỆT:
            occupiedTiles[targetSlotIndex] = tile; // Giữ chỗ logic
            tile.ResolveOverlapOnAccept();        // Cho phép mở khóa Board

            // Đưa vào hàng đợi hoạt ảnh
            visualQueue.Enqueue(new PlacementOrder { tile = tile, slotIndex = targetSlotIndex });
            if (!isProcessingQueue)
            {
                StartCoroutine(ProcessVisualQueue());
            }

            return true;
        }

        private System.Collections.IEnumerator ProcessVisualQueue()
        {
            isProcessingQueue = true;
            while (visualQueue.Count > 0)
            {
                PlacementOrder order = visualQueue.Dequeue();
                if (order.tile != null)
                {
                    // Giải phóng Conveyor nếu cần
                    bool wasOnConveyor = false;
                    for (int i = 0; i < occupiedConveyorTiles.Length; i++)
                    {
                        if (occupiedConveyorTiles[i] == order.tile)
                        {
                            occupiedConveyorTiles[i] = null;
                            wasOnConveyor = true;
                            break;
                        }
                    }

                    if (wasOnConveyor)
                    {
                        ShiftConveyorTiles();
                    }

                    SnapTileToSlotWithAnimation(order.tile, formulaSlots[order.slotIndex]);
                    CheckFormulaCompletion();
                }
                yield return null;
            }
            isProcessingQueue = false;
        }

        /// <summary>
        /// Dồn các tile trên Conveyor về bên trái để lấp đầy các khoảng trống.
        /// </summary>
        public void ShiftConveyorTiles()
        {
            int insertPos = 0;
            for (int i = 0; i < occupiedConveyorTiles.Length; i++)
            {
                if (occupiedConveyorTiles[i] != null)
                {
                    if (i != insertPos)
                    {
                        // 1. Dịch chuyển logic
                        Tile tile = occupiedConveyorTiles[i];
                        occupiedConveyorTiles[insertPos] = tile;
                        occupiedConveyorTiles[i] = null;
                        
                        // 2. Cập nhật hình ảnh
                        RectTransform rectT = tile.GetComponent<RectTransform>();
                        Transform targetSlot = conveyorSlots[insertPos];
                        
                        if (rectT != null && targetSlot != null)
                        {
                            Vector2 renderedSize = rectT.rect.size;
                            Vector2 sizeBefore = (renderedSize.x > 0f && renderedSize.y > 0f) ? renderedSize : rectT.sizeDelta;
                            Vector3 scaleBefore = rectT.localScale;
                            
                            tile.transform.SetParent(targetSlot, true);
                            
                            rectT.anchorMin = new Vector2(0.5f, 0.5f);
                            rectT.anchorMax = new Vector2(0.5f, 0.5f);
                            rectT.pivot = new Vector2(0.5f, 0.5f);
                            rectT.sizeDelta = sizeBefore;
                            rectT.localScale = scaleBefore;
                            rectT.SetAsLastSibling();
                            
                            if (UIEffectManager.Instance != null)
                            {
                                UIEffectManager.Instance.MoveTo(rectT, Vector2.zero, 0.25f);
                            }
                            else
                            {
                                rectT.anchoredPosition = Vector2.zero;
                            }
                        }
                    }
                    insertPos++;
                }
            }
        }

        /// <summary>
        /// Lấy danh sách các index đang có Tile trên thanh công thức, từ phải sang trái.
        /// </summary>
        public List<int> GetOccupiedFormulaIndicesRightToLeft()
        {
            List<int> indices = new List<int>();
            for (int i = occupiedTiles.Length - 1; i >= 0; i--)
            {
                if (occupiedTiles[i] != null)
                {
                    indices.Add(i);
                }
            }
            return indices;
        }

        /// <summary>
        /// Kiểm tra xem người chơi đã nhập đủ các ô cần thiết để thực hiện phép tính chưa.
        /// Logic mới: Tự động nhận biết số chữ số của kết quả để kiểm tra ngay lập tức.
        /// </summary>
        private void CheckFormulaCompletion()
        {
            // Các ô bắt buộc cơ bản: Slot 1, Slot 2, Slot 3
            if (occupiedTiles[0] != null && occupiedTiles[1] != null && occupiedTiles[2] != null)
            {
                // Tính toán kết quả vế trái trước để biết cần bao nhiêu Slot ở vế phải
                float leftSide = CalculateLeftSide();

                // Trường hợp 1: Kết quả là số có 1 chữ số (từ -9 đến 9)
                if (leftSide > -10 && leftSide < 10)
                {
                    // Chỉ cần người chơi nhập xong Slot 4 (index 3) là kiểm tra ngay
                    if (occupiedTiles[3] != null)
                    {
                        EvaluateCurrentFormula();
                    }
                }
                // Trường hợp 2: Kết quả là số có 2 chữ số (vd: 12, -15...)
                else
                {
                    // Bắt buộc phải nhập đủ cả Slot 4 và Slot 5 (index 3 và 4) mới kiểm tra
                    if (occupiedTiles[3] != null && occupiedTiles[4] != null)
                    {
                        EvaluateCurrentFormula();
                    }
                }
            }
        }

        /// <summary>
        /// Hàm phụ tính nhanh vế trái để hỗ trợ việc kiểm tra tự động.
        /// </summary>
        private float CalculateLeftSide()
        {
            float v1 = occupiedTiles[0].numberValue;
            string op = occupiedTiles[1].operatorValue;
            float v2 = occupiedTiles[2].numberValue;

            switch (op)
            {
                case "+": return v1 + v2;
                case "-": return v1 - v2;
                case "x": case "*": return v1 * v2;
                case "/": return (v2 != 0) ? (float)System.Math.Floor(v1 / v2) : 0;
                default: return 0;
            }
        }

        /// <summary>
        /// Gửi dữ liệu sang bộ não tính toán và xử lý kết quả Thắng/Thua tạm thời.
        /// </summary>
        private void EvaluateCurrentFormula()
        {
            if (FormulaEvaluator.Instance != null)
            {
                bool isCorrect = FormulaEvaluator.Instance.CheckResult(occupiedTiles);
                
                if (isCorrect)
                {
                    TrySpawnRemainderTileToConveyor();
                    Debug.Log("<color=green>[Formula] Phép tính CHÍNH XÁC!</color>");
                    
                    // Logic CLEAR ngay lập tức, Visual DELAYED
                    ClearFormula(true);
                }
                else
                {
                    if (occupiedTiles[4] != null || (occupiedTiles[0] != null && occupiedTiles[1] != null && occupiedTiles[2] != null && occupiedTiles[3] != null))
                    {
                         Debug.LogWarning("<color=red>[Formula] Phép tính SAI! Xóa để chơi tiếp.</color>");
                         ClearFormula(false);
                    }
                }
            }
        }

        /// <summary>
        /// Xóa các tile trong thanh công thức.
        /// Giải pháp: Xóa logic lập tức, chờ visual bay xong rồi thu nhỏ.
        /// </summary>
        public void ClearFormula(bool destroy)
        {
            Tile[] tilesToClear = (Tile[])occupiedTiles.Clone();
            for (int i = 0; i < occupiedTiles.Length; i++) occupiedTiles[i] = null;

            StartCoroutine(VisualClearSequence(tilesToClear, 0.3f));
        }

        private System.Collections.IEnumerator VisualClearSequence(Tile[] tiles, float delay)
        {
            yield return new WaitForSeconds(delay);
            foreach (var tile in tiles)
            {
                if (tile != null)
                {
                    if (UIEffectManager.Instance != null)
                        StartCoroutine(ImprovedShrinkRoutine(tile.transform));
                    else
                        Destroy(tile.gameObject);
                }
            }
        }

        private System.Collections.IEnumerator ImprovedShrinkRoutine(Transform target)
        {
            float elapsed = 0;
            float duration = 0.3f;
            Vector3 startScale = target.localScale;
            
            // Phase 1: Nhấn nhẹ xuống (Pop up một chút trước khi biến mất)
            float popDuration = 0.1f;
            while (elapsed < popDuration)
            {
                if (target == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / popDuration;
                // Scale to 1.2 then back
                target.localScale = startScale * (1f + Mathf.Sin(t * Mathf.PI) * 0.15f);
                yield return null;
            }

            // Phase 2: Thu nhỏ biến mất nhanh
            elapsed = 0;
            Vector3 currentScale = target.localScale;
            while (elapsed < duration)
            {
                if (target == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Ease In Back (Hơi lùi lại rồi biến mất nhanh)
                float easeIn = t * t * t; // Simple cubic ease in
                
                target.localScale = Vector3.Lerp(currentScale, Vector3.zero, easeIn);
                yield return null;
            }
            if (target != null) 
            {
                target.gameObject.SetActive(false);
                Destroy(target.gameObject);
                if (TileCounter.Instance != null) TileCounter.Instance.UpdateTileCountUI();
                StartCoroutine(DelayedCampaignSaveCheck());
            }
        }

        private System.Collections.IEnumerator DelayedCampaignSaveCheck()
        {
            yield return new WaitForSeconds(0.35f);
            CampaignSaveHooks.EvaluateTemporaryOutcomeAfterBoardChange();
        }

        /// <summary>
        /// Hàm thực thi quy trình 4 bước khi chuyển Tile từ Board vào Slot công thức.
        /// </summary>
        private void AssignTileToSlot(Tile tile, Transform slotTransform, int slotIndex)
        {
            // Đánh dấu ô này đã bị chiếm bởi Tile hiện tại
            occupiedTiles[slotIndex] = tile;
            SnapTileToSlotWithAnimation(tile, slotTransform);
        }

        private void SnapTileToSlotWithAnimation(Tile tile, Transform slotTransform)
        {
            RectTransform rectT = tile.GetComponent<RectTransform>();
            if (rectT == null) return;

            // Đảm bảo Tile luôn bằng kích thước Board Slot mẫu
            if (referenceBoardSlot != null)
            {
                UISizeSync sizeSync = tile.GetComponent<UISizeSync>();
                if (sizeSync == null) sizeSync = tile.gameObject.AddComponent<UISizeSync>();
                sizeSync.target = referenceBoardSlot;
                sizeSync.syncWidth = true;
                sizeSync.syncHeight = true;
            }

            Vector3 scaleBefore = rectT.localScale;
            AspectRatioFitter aspectFitter = tile.GetComponent<AspectRatioFitter>();
            if (aspectFitter != null) aspectFitter.enabled = false;

            // Đổi cha về Slot
            tile.transform.SetParent(slotTransform, true); 

            rectT.anchorMin = new Vector2(0.5f, 0.5f);
            rectT.anchorMax = new Vector2(0.5f, 0.5f);
            rectT.pivot = new Vector2(0.5f, 0.5f);
            rectT.localScale = scaleBefore;
            rectT.SetAsLastSibling();

            if (UIEffectManager.Instance != null)
            {
                UIEffectManager.Instance.MoveTo(rectT, Vector2.zero, 0.25f);
            }
            else
            {
                rectT.anchoredPosition = Vector2.zero;
            }

            ApplyReferenceSize(tile);
        }

        private void TrySpawnRemainderTileToConveyor()
        {
            if (occupiedTiles[0] == null || occupiedTiles[1] == null || occupiedTiles[2] == null)
            {
                return;
            }

            string op = occupiedTiles[1].operatorValue;
            if (op != "/")
            {
                return;
            }

            int a = occupiedTiles[0].numberValue;
            int b = occupiedTiles[2].numberValue;
            if (b == 0)
            {
                return;
            }

            int remainder = Mathf.Abs(a % b);
            if (remainder == 0)
            {
                return;
            }

            if (!TryGetNextConveyorSlot(out Transform targetSlot, out int slotIndex))
            {
                Debug.LogWarning("[FormulaManager] Conveyor đã đầy, không thể spawn thêm tile số dư.");
                return;
            }

            Tile randomBoardTile = GetRandomBoardTileTemplate();
            if (randomBoardTile == null)
            {
                Debug.LogWarning("[FormulaManager] Không tìm thấy tile board để clone kích thước cho tile số dư.");
                return;
            }

            GameObject spawnedObject = Instantiate(randomBoardTile.gameObject);
            Tile spawnedTile = spawnedObject.GetComponent<Tile>();
            if (spawnedTile == null)
            {
                Destroy(spawnedObject);
                Debug.LogError("[FormulaManager] Tile clone cho conveyor bị thiếu component Tile.");
                return;
            }

            SnapTileToSlotKeepingSize(spawnedTile, targetSlot, randomBoardTile);
            spawnedTile.coveredTiles.Clear();
            spawnedTile.coveringTiles.Clear();
            spawnedTile.layerId = 0;
            spawnedTile.gridX = 0;
            spawnedTile.gridY = 0;
            spawnedTile.SetVisualState(true);

            if (spawnedTile.backgroundRenderer != null)
            {
                spawnedTile.backgroundRenderer.raycastTarget = true;
            }

            Sprite remainderSprite = GetNumberSpriteByConfiguredOrder(remainder);
            if (remainderSprite == null && randomBoardTile.backgroundRenderer != null)
            {
                remainderSprite = randomBoardTile.backgroundRenderer.sprite;
            }

            spawnedTile.SetupRemainder(remainder, remainderSprite);
            spawnedTile.gameObject.name = $"Tile_Remainder_{remainder}_Slot_{slotIndex}";
            occupiedConveyorTiles[slotIndex] = spawnedTile;

            // Hiệu ứng Pop-in cho Tile mới trên conveyor
            if (UIEffectManager.Instance != null)
            {
                UIEffectManager.Instance.ScaleUp(spawnedTile.transform, 0.3f);
            }
        }

        public bool TryGetNextConveyorSlot(out Transform slot, out int index)
        {
            slot = null;
            index = -1;

            for (int i = 0; i < conveyorSlots.Length; i++)
            {
                if (conveyorSlots[i] == null)
                {
                    continue;
                }

                if (occupiedConveyorTiles[i] == null)
                {
                    slot = conveyorSlots[i];
                    index = i;
                    return true;
                }
            }

            return false;
        }

        public void RegisterTileToConveyor(Tile tile, int index)
        {
            if (index >= 0 && index < occupiedConveyorTiles.Length)
            {
                occupiedConveyorTiles[index] = tile;
            }
        }

        public Tile GetRandomBoardTileTemplate()
        {
            Tile[] allTiles = FindObjectsOfType<Tile>(true);
            List<Tile> candidates = new List<Tile>();

            foreach (Tile tile in allTiles)
            {
                if (tile == null || tile.gameObject == null)
                {
                    continue;
                }

                if (System.Array.IndexOf(occupiedTiles, tile) >= 0 || System.Array.IndexOf(occupiedConveyorTiles, tile) >= 0)
                {
                    continue;
                }

                if (!tile.isAssigned)
                {
                    continue;
                }

                if (tile.type != TileType.Number)
                {
                    continue;
                }

                if (IsTileInsideFormulaOrConveyor(tile))
                {
                    continue;
                }

                candidates.Add(tile);
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            int randomIndex = Random.Range(0, candidates.Count);
            return candidates[randomIndex];
        }

        private bool IsTileInsideFormulaOrConveyor(Tile tile)
        {
            if (tile == null) return false;

            Transform tileTransform = tile.transform;
            for (int i = 0; i < formulaSlots.Length; i++)
            {
                if (formulaSlots[i] != null && tileTransform.IsChildOf(formulaSlots[i]))
                {
                    return true;
                }
            }

            for (int i = 0; i < conveyorSlots.Length; i++)
            {
                if (conveyorSlots[i] != null && tileTransform.IsChildOf(conveyorSlots[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private void SnapTileToSlotKeepingSize(Tile tile, Transform slotTransform)
        {
            SnapTileToSlotKeepingSize(tile, slotTransform, null);
        }

        private void SnapTileToSlotKeepingSize(Tile tile, Transform slotTransform, Tile sourceTile)
        {
            if (tile == null || slotTransform == null) return;

            RectTransform rectT = tile.GetComponent<RectTransform>();
            if (rectT == null) return;

            // Đảm bảo Tile luôn bằng kích thước Board Slot mẫu
            if (referenceBoardSlot != null)
            {
                UISizeSync sizeSync = tile.GetComponent<UISizeSync>();
                if (sizeSync == null) sizeSync = tile.gameObject.AddComponent<UISizeSync>();
                sizeSync.target = referenceBoardSlot;
                sizeSync.syncWidth = true;
                sizeSync.syncHeight = true;
            }

            AspectRatioFitter aspectFitter = tile.GetComponent<AspectRatioFitter>();
            if (aspectFitter != null) aspectFitter.enabled = false;

            tile.transform.SetParent(slotTransform, false);

            rectT.anchorMin = new Vector2(0.5f, 0.5f);
            rectT.anchorMax = new Vector2(0.5f, 0.5f);
            rectT.pivot = new Vector2(0.5f, 0.5f);
            rectT.localScale = Vector3.one;
            rectT.anchoredPosition = Vector2.zero;

            ApplyReferenceSize(tile);
        }

        private void SyncOccupiedTileSizes()
        {
            for (int i = 0; i < occupiedTiles.Length; i++)
            {
                if (occupiedTiles[i] != null)
                {
                    ApplyReferenceSize(occupiedTiles[i]);
                }
            }

            for (int i = 0; i < occupiedConveyorTiles.Length; i++)
            {
                if (occupiedConveyorTiles[i] != null)
                {
                    ApplyReferenceSize(occupiedConveyorTiles[i]);
                }
            }
        }

        private void ApplyReferenceSize(Tile tile)
        {
            if (tile == null || referenceBoardSlot == null) return;

            RectTransform rectT = tile.GetComponent<RectTransform>();
            if (rectT == null) return;

            Vector2 size = referenceBoardSlot.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;

            rectT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            rectT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }

        private void AutoBindConveyorSlotsIfNeeded()
        {
            bool hasAny = false;
            for (int i = 0; i < conveyorSlots.Length; i++)
            {
                if (conveyorSlots[i] != null)
                {
                    hasAny = true;
                    break;
                }
            }

            if (hasAny)
            {
                return;
            }

            Transform conveyorContent = null;
            GameObject conveyorObject = GameObject.Find("Conveyor_Content");
            if (conveyorObject != null)
            {
                conveyorContent = conveyorObject.transform;
            }

            for (int i = 0; i < conveyorSlots.Length; i++)
            {
                Transform slot = null;
                if (conveyorContent != null)
                {
                    slot = FindChildRecursive(conveyorContent, $"Slot_{i}");
                }
                if (slot == null)
                {
                    GameObject fallback = GameObject.Find($"Slot_{i}");
                    if (fallback != null) slot = fallback.transform;
                }
                if (slot != null)
                {
                    conveyorSlots[i] = slot;
                }
            }
        }

        private Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null) return null;
            if (root.name == targetName) return root;
            foreach (Transform child in root)
            {
                Transform found = FindChildRecursive(child, targetName);
                if (found != null) return found;
            }
            return null;
        }

        private Sprite GetNumberSpriteByConfiguredOrder(int value)
        {
            NumStrata.Data.TileSpriteData source = tileSpriteData;
            if (source == null)
            {
                LevelLoader loader = FindObjectOfType<LevelLoader>(true);
                if (loader != null)
                {
                    source = loader.tileSpriteData;
                }
            }

            if (source == null)
            {
                return null;
            }

            return source.GetNumberSprite(value);
        }
    }
}