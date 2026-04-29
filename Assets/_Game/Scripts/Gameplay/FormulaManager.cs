using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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

        [Header("Formula Slots")]
        [Tooltip("The 5 slots available in the formula bar.")]
        public Transform[] formulaSlots = new Transform[5];

        [Header("Conveyor Slots")]
        [Tooltip("Conveyor slots ordered from Slot_0 to Slot_5.")]
        public Transform[] conveyorSlots = new Transform[6];
        [Tooltip("Sprite data used to render spawned remainder tiles on conveyor.")]
        public NumStrata.Data.TileSpriteData tileSpriteData;

        // Mảng lưu trữ trạng thái các ô công thức (ô nào chứa tile nào)
        private Tile[] occupiedTiles = new Tile[5];
        private Tile[] occupiedConveyorTiles = new Tile[6];

        /// <summary>
        /// Khởi tạo Singleton để các script khác (như Tile.cs) có thể dễ dàng gọi đến FormulaManager.
        /// </summary>
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                AutoBindConveyorSlotsIfNeeded();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Hàm này thử thêm Tile bị click vào vị trí phù hợp trong 5 ô công thức.
        /// Slot 2 (index 1) dành cho Toán tử.
        /// Slot 1, 3, 4, 5 (index 0, 2, 3, 4) dành cho Con số.
        /// </summary>
        public bool TryAddTile(Tile tile)
        {
            if (tile.type == TileType.Operator)
            {
                // Nếu là toán tử, chỉ được vào Slot 2 (index 1)
                if (occupiedTiles[1] == null)
                {
                    AssignTileToSlot(tile, formulaSlots[1], 1);
                    CheckFormulaCompletion(); // Kiểm tra xem đã đủ để tính chưa
                    return true;
                }
            }
            else
            {
                // Nếu là số, tìm ô trống trong các slot 1, 3, 4, 5
                int[] numberSlotIndices = { 0, 2, 3, 4 };
                foreach (int i in numberSlotIndices)
                {
                    if (occupiedTiles[i] == null)
                    {
                        AssignTileToSlot(tile, formulaSlots[i], i);
                        CheckFormulaCompletion(); // Kiểm tra xem đã đủ để tính chưa
                        return true;
                    }
                }
            }

            Debug.LogWarning("[FormulaManager] Không có ô phù hợp cho loại Tile này!");
            return false;
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
            // Gọi sang FormulaEvaluator (sẽ tạo ở bước sau)
            if (FormulaEvaluator.Instance != null)
            {
                bool isCorrect = FormulaEvaluator.Instance.CheckResult(occupiedTiles);
                
                if (isCorrect)
                {
                    TrySpawnRemainderTileToConveyor();
                    Debug.Log("<color=green>[Formula] Phép tính CHÍNH XÁC!</color>");
                    ClearFormula(true);
                }
                else
                {
                    // Nếu sai, theo yêu cầu: báo log, xóa tile tạm thời và cho chơi tiếp
                    // Lưu ý: Nếu đầy cả 5 ô mà vẫn sai thì mới thực hiện xóa để tránh người chơi đang nhập dở Slot 5
                    if (occupiedTiles[4] != null || (occupiedTiles[0] != null && occupiedTiles[1] != null && occupiedTiles[2] != null && occupiedTiles[3] != null))
                    {
                         // Tạm thời chỉ xóa khi người chơi nhập sai để họ chơi tiếp (theo yêu cầu)
                         // Thực tế sau này sẽ xử thua theo GDD
                         Debug.LogWarning("<color=red>[Formula] Phép tính SAI! Tạm thời xóa tile để chơi tiếp.</color>");
                         ClearFormula(false);
                    }
                }
            }
        }

        /// <summary>
        /// Xóa các tile trong thanh công thức.
        /// </summary>
        /// <param name="destroy">Nếu true sẽ xóa vĩnh viễn (khi thắng), false có thể trả về (tùy logic sau này)</param>
        public void ClearFormula(bool destroy)
        {
            for (int i = 0; i < occupiedTiles.Length; i++)
            {
                if (occupiedTiles[i] != null)
                {
                    if (destroy)
                    {
                        Destroy(occupiedTiles[i].gameObject);
                    }
                    else
                    {
                        // Reset lại tile để có thể click lại (vì khi click thành công ta đã tắt raycast)
                        Image img = occupiedTiles[i].backgroundRenderer;
                        if (img != null) img.raycastTarget = true;
                        
                        Destroy(occupiedTiles[i].gameObject); // Tạm thời vẫn xóa theo yêu cầu của bạn
                    }
                    occupiedTiles[i] = null;
                }
            }
        }

        /// <summary>
        /// Hàm thực thi quy trình 4 bước khi chuyển Tile từ Board vào Slot công thức.
        /// </summary>
        private void AssignTileToSlot(Tile tile, Transform slotTransform, int slotIndex)
        {
            // Đánh dấu ô này đã bị chiếm bởi Tile hiện tại
            occupiedTiles[slotIndex] = tile;
            SnapTileToSlotKeepingSize(tile, slotTransform);
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
        }

        private bool TryGetNextConveyorSlot(out Transform slot, out int index)
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

        private Tile GetRandomBoardTileTemplate()
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
            if (tile == null || slotTransform == null)
            {
                return;
            }

            RectTransform rectT = tile.GetComponent<RectTransform>();
            Vector2 sizeBefore = Vector2.zero;
            Vector3 scaleBefore = Vector3.one;
            RectTransform sourceRect = sourceTile != null ? sourceTile.GetComponent<RectTransform>() : null;

            if (rectT != null)
            {
                if (sourceRect != null)
                {
                    // Dùng trực tiếp size của tile mẫu trên board để tránh bị slot/layout ép về size mặc định.
                    Vector2 sourceRenderedSize = sourceRect.rect.size;
                    sizeBefore = (sourceRenderedSize.x > 0f && sourceRenderedSize.y > 0f) ? sourceRenderedSize : sourceRect.sizeDelta;
                    scaleBefore = sourceRect.localScale;
                }
                else
                {
                    // Với tile click từ board, ưu tiên lấy kích thước đang render thực tế.
                    // sizeDelta có thể = 0 nếu anchor đang ở chế độ stretch.
                    Vector2 renderedSize = rectT.rect.size;
                    sizeBefore = (renderedSize.x > 0f && renderedSize.y > 0f) ? renderedSize : rectT.sizeDelta;
                    scaleBefore = rectT.localScale;
                }
            }

            AspectRatioFitter aspectFitter = tile.GetComponent<AspectRatioFitter>();
            if (aspectFitter != null)
            {
                aspectFitter.enabled = false;
            }

            tile.transform.SetParent(slotTransform, false);

            if (rectT != null)
            {
                rectT.anchorMin = new Vector2(0.5f, 0.5f);
                rectT.anchorMax = new Vector2(0.5f, 0.5f);
                rectT.pivot = new Vector2(0.5f, 0.5f);
                rectT.sizeDelta = sizeBefore;
                rectT.localScale = scaleBefore;
                rectT.anchoredPosition = Vector2.zero;
            }
            else
            {
                tile.transform.localPosition = Vector3.zero;
            }
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