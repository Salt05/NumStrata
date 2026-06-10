using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using NumStrata.Utils;

namespace NumStrata.Gameplay
{
    /// <summary>
    /// Thuộc tính quy định loại của Tile (Số, Phép toán, hoặc Ẩn Số)
    /// </summary>
    public enum TileType { Number, Operator, Mystery, Remainder, Helper }

    /// <summary>
    /// Chức năng: Đại diện cho một viên Tile duy nhất trên Board game.
    /// Script này chịu trách nhiệm: Lưu trữ giá trị (số hoặc phép toán), thay đổi hình ảnh hiển thị dựa trên trạng thái (khóa/mở khóa), 
    /// và đặc biệt là xử lý tương tác khi người chơi Click chuột/chạm tay vào Tile.
    /// </summary>
    public class Tile : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Settings")]
        public TileType type;
        public int numberValue;
        public string operatorValue;
        public bool isLocked;
        public bool isMystery;

        [Header("Position")]
        public int layerId;
        public int gridX;
        public int gridY;

        [Header("Overlap Tracking")]
        public List<Tile> coveringTiles = new List<Tile>(); // Tiles that overlap this tile (layer above)
        public List<Tile> coveredTiles = new List<Tile>();  // Tiles this tile overlaps (layer below)

        [Header("References")]
        public Image backgroundRenderer; // Dùng để hiển thị hình nền và nội dung của Tile
        public GameObject mysteryOverlay; // Lớp phủ dấu "?" 
        public bool isAssigned; // đã gán giá trị/phiên bản chưa

        private RectTransform rectTransform;
        private int originalSortingOrder;
        private Canvas tileCanvas;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            tileCanvas = GetComponent<Canvas>();
        }

        /// <summary>
        /// Khởi tạo Tile mang giá trị là Con Số. Gán hình ảnh tương ứng.
        /// </summary>
        public void SetupNumber(int value, Sprite numberSprite)
        {
            type = TileType.Number;
            numberValue = value;
            operatorValue = string.Empty;
            isAssigned = true;

            if (backgroundRenderer == null)
            {
                Debug.LogError($"[Tile] Thiếu backgroundRenderer ở {name}", this);
                return;
            }

            if (numberSprite == null)
            {
                Debug.LogError($"[Tile] Không tìm thấy hình ảnh con số cho giá trị={value} ở {name}", this);
            }

            backgroundRenderer.sprite = numberSprite; // Cập nhật hình ảnh số

            SetMysteryMask(false);
        }

        /// <summary>
        /// Khởi tạo Tile dạng Số Dư (spawn ở conveyor khi phép chia có dư).
        /// Về bản chất là một tile số, nhưng có type riêng để dễ theo dõi/debug.
        /// </summary>
        public void SetupRemainder(int value, Sprite numberSprite)
        {
            type = TileType.Remainder;
            numberValue = value;
            operatorValue = string.Empty;
            isAssigned = true;

            if (backgroundRenderer == null)
            {
                Debug.LogError($"[Tile] Thiếu backgroundRenderer ở {name}", this);
                return;
            }

            if (numberSprite == null)
            {
                Debug.LogError($"[Tile] Không tìm thấy hình ảnh con số dư cho giá trị={value} ở {name}", this);
            }

            backgroundRenderer.sprite = numberSprite;
            backgroundRenderer.color = Color.white;

            SetMysteryMask(false);
        }

        /// <summary>
        /// Khởi tạo Tile mang giá trị là Phép Toán (+, -, x, /). Gán hình ảnh tương ứng.
        /// </summary>
        public void SetupOperator(string op, Sprite operatorSprite)
        {
            type = TileType.Operator;
            operatorValue = op;
            numberValue = 0;
            isAssigned = true;

            if (backgroundRenderer == null)
            {
                Debug.LogError($"[Tile] Thiếu backgroundRenderer ở {name}", this);
                return;
            }

            if (operatorSprite == null)
            {
                Debug.LogError($"[Tile] Không tìm thấy hình ảnh cho phép toán='{op}' ở {name}", this);
            }

            backgroundRenderer.sprite = operatorSprite; // Cập nhật hình phép toán

            SetMysteryMask(false);
        }

        public void SetMysteryMask(bool mask)
        {
            isMystery = mask;
            if (mysteryOverlay != null)
            {
                mysteryOverlay.SetActive(mask);
                if (mask)
                {
                    UnityEngine.UI.Image overlayImage = mysteryOverlay.GetComponent<UnityEngine.UI.Image>();
                    if (overlayImage != null)
                    {
                        // Gán sprite mystery từ TileSpriteData (fix lỗi overlay trắng xóa khi thiếu sprite)
                        if (LevelLoader.Instance != null && LevelLoader.Instance.tileSpriteData != null)
                        {
                            Sprite mysterySprite = LevelLoader.Instance.tileSpriteData.mysterySprite;
                            if (mysterySprite != null)
                            {
                                overlayImage.sprite = mysterySprite;
                            }
                        }
                        overlayImage.color = isLocked ? new Color32(103, 103, 103, 255) : Color.white;
                    }
                }
            }
        }

        /// <summary>
        /// Cập nhật hiển thị của Tile dựa trên việc nó có bị khóa (bị Tile khác đề lên) hay không.
        /// </summary>
        public void SetVisualState(bool unlocked)
        {
            isLocked = !unlocked;
            Color targetColor = unlocked ? Color.white : new Color32(103, 103, 103, 255);

            if (backgroundRenderer != null)
            {
                backgroundRenderer.color = targetColor;
            }

            if (mysteryOverlay != null)
            {
                UnityEngine.UI.Image overlayImage = mysteryOverlay.GetComponent<UnityEngine.UI.Image>();
                if (overlayImage != null)
                {
                    overlayImage.color = targetColor;
                }
            }
        }

        public void SetDimmed(bool isDimmed)
        {
            if (backgroundRenderer != null)
            {
                Color c = backgroundRenderer.color;
                c.a = isDimmed ? 0.4f : 1f;
                backgroundRenderer.color = c;
            }
            if (mysteryOverlay != null)
            {
                UnityEngine.UI.Image overlayImage = mysteryOverlay.GetComponent<UnityEngine.UI.Image>();
                if (overlayImage != null)
                {
                    Color c = overlayImage.color;
                    c.a = isDimmed ? 0.4f : 1f;
                    overlayImage.color = c;
                }
            }
        }

        public void AddCoveringTile(Tile tileAbove)
        {
            if (!coveringTiles.Contains(tileAbove)) coveringTiles.Add(tileAbove);
            SetVisualState(coveringTiles.Count == 0);
        }

        public void RemoveCoveringTile(Tile tileAbove)
        {
            if (coveringTiles.Contains(tileAbove)) coveringTiles.Remove(tileAbove);
            SetVisualState(coveringTiles.Count == 0);
        }

        public void AddCoveredTile(Tile tileBelow)
        {
            if (!coveredTiles.Contains(tileBelow)) coveredTiles.Add(tileBelow);
        }

        /// <summary>
        /// Bắt sự kiện khi người chơi Click chuột hoặc chạm tay vào Tile trên màn hình.
        /// Được hỗ trợ bởi Interface IPointerClickHandler của hệ thống UI Unity.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (type == TileType.Helper)
            {
                if (HelperManager.Instance != null)
                {
                    HelperManager.Instance.OnHelperTileClicked(this);
                }
                return;
            }

            if (HelperManager.Instance != null && HelperManager.Instance.isDeleteActive)
            {
                if (!isLocked)
                {
                    HelperManager.Instance.ExecuteTileDeletion(this);
                }
                return;
            }

            // Nếu Tile đang bị đè lên (khóa), rung lắc mạnh để thông báo cho người chơi
            if (isLocked)
            {
                if (UIEffectManager.Instance != null && rectTransform != null)
                {
                    UIEffectManager.Instance.Shake(rectTransform, 0.3f, 15f);
                }
                Debug.Log($"[Tile] {gameObject.name} đang bị khóa và không thể click.");
                return;
            }

            // Lột mặt nạ Mystery
            if (isMystery)
            {
                SetMysteryMask(false);
            }

            // Gửi tín hiệu lên đầu não (FormulaManager)
            if (FormulaManager.Instance != null)
            {

                // Nếu đầu não phê duyệt (cho phép placement)
                bool approved = FormulaManager.Instance.RequestTilePlacement(this);
                
                if (approved)
                {
                    // Chỉ khi được phê duyệt mới tắt tương tác click
                    if (backgroundRenderer != null)
                    {
                        backgroundRenderer.raycastTarget = false;
                    }
                }
            }
            else
            {
                Debug.LogWarning("[Tile] FormulaManager.Instance không có dữ liệu. Bạn đã đưa script FormulaManager vào scene chưa?");
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Chỉ chạy hiệu ứng khi giữ Tile và không bị khóa
            if (isLocked) return;

            if (tileCanvas != null)
            {
                originalSortingOrder = tileCanvas.sortingOrder;
                tileCanvas.sortingOrder = 900;
            }

            if (UIEffectManager.Instance != null && backgroundRenderer != null)
            {
                RectTransform bgRect = backgroundRenderer.rectTransform;
                // Tăng scale lên 1.1 cho nhẹ nhàng
                UIEffectManager.Instance.ScaleTo(bgRect, Vector3.one * 1.1f, 0.15f);
                // Bắt đầu hiệu ứng lơ lửng + xoay (giảm amplitude mặc định)
                UIEffectManager.Instance.StartFloating(bgRect, 5f, 5f);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Trả lại sorting order ban đầu
            if (tileCanvas != null)
            {
                tileCanvas.sortingOrder = originalSortingOrder;
            }

            // Trả lại trạng thái mặc định ngay khi thả tay
            if (UIEffectManager.Instance != null && backgroundRenderer != null)
            {
                RectTransform bgRect = backgroundRenderer.rectTransform;
                // Dừng hiệu ứng lơ lửng và reset vị trí/xoay
                UIEffectManager.Instance.StopFloating(bgRect);
                // Thu nhỏ về 1.0 mượt mà
                UIEffectManager.Instance.ScaleTo(bgRect, Vector3.one, 0.15f);
            }
        }

        /// <summary>
        /// Giải phóng các Tile bị Tile này đè lên. 
        /// Chỉ được gọi từ FormulaManager khi Tile chính thức được chấp nhận vào Slot.
        /// Sorting Order không cần điều chỉnh vì đã được tính cố định theo công thức (Row * RowWeight) + (Layer * LayerWeight).
        /// </summary>
        public void ResolveOverlapOnAccept()
        {
            foreach (var tileBelow in coveredTiles)
            {
                if (tileBelow != null)
                {
                    // Remove tile này khỏi danh sách che phủ của tile bên dưới (để mở khóa)
                    tileBelow.RemoveCoveringTile(this);
                }
            }
            coveredTiles.Clear();
        }
    }
}

