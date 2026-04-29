using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace NumStrata.Gameplay
{
    /// <summary>
    /// Thuộc tính quy định loại của Tile (Số, Phép toán, hoặc Ẩn Số)
    /// </summary>
    public enum TileType { Number, Operator, Mystery, Remainder }

    /// <summary>
    /// Chức năng: Đại diện cho một viên Tile duy nhất trên Board game.
    /// Script này chịu trách nhiệm: Lưu trữ giá trị (số hoặc phép toán), thay đổi hình ảnh hiển thị dựa trên trạng thái (khóa/mở khóa), 
    /// và đặc biệt là xử lý tương tác khi người chơi Click chuột/chạm tay vào Tile.
    /// </summary>
    public class Tile : MonoBehaviour, IPointerClickHandler
    {
        [Header("Settings")]
        public TileType type;
        public int numberValue;
        public string operatorValue;
        public bool isLocked;

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

        /// <summary>
        /// Khởi tạo Tile mang giá trị là Con Số. Gán hình ảnh tương ứng.
        /// </summary>
        public void SetupNumber(int value, Sprite numberSprite)
        {
            type = TileType.Number;
            numberValue = value;
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

            if (mysteryOverlay != null)
            {
                mysteryOverlay.SetActive(false); // Tắt lớp ẩn số
            }
        }

        /// <summary>
        /// Khởi tạo Tile dạng Số Dư (spawn ở conveyor khi phép chia có dư).
        /// Về bản chất là một tile số, nhưng có type riêng để dễ theo dõi/debug.
        /// </summary>
        public void SetupRemainder(int value, Sprite numberSprite)
        {
            type = TileType.Remainder;
            numberValue = value;
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

            if (mysteryOverlay != null)
            {
                mysteryOverlay.SetActive(false);
            }
        }

        /// <summary>
        /// Khởi tạo Tile mang giá trị là Phép Toán (+, -, x, /). Gán hình ảnh tương ứng.
        /// </summary>
        public void SetupOperator(string op, Sprite operatorSprite)
        {
            type = TileType.Operator;
            operatorValue = op;
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

            if (mysteryOverlay != null)
            {
                mysteryOverlay.SetActive(false); // Tắt lớp ẩn số
            }
        }

        /// <summary>
        /// Khởi tạo Tile dạng Ẩn Số (Mystery - người chơi không biết bên dưới là gì).
        /// </summary>
        public void SetupMystery(Sprite mysterySprite)
        {
            type = TileType.Mystery;
            isAssigned = true;

            if (backgroundRenderer == null)
            {
                Debug.LogError($"[Tile] Thiếu backgroundRenderer ở {name}", this);
                return;
            }

            if (mysterySprite == null)
            {
                Debug.LogError($"[Tile] Không tìm thấy hình ảnh dấu hỏi ở {name}", this);
            }

            backgroundRenderer.sprite = mysterySprite;

            if (mysteryOverlay != null)
            {
                mysteryOverlay.SetActive(true); // Bật lớp ẩn số (Dấu chấm hỏi)
            }
        }

        /// <summary>
        /// Cập nhật hiển thị của Tile dựa trên việc nó có bị khóa (bị Tile khác đề lên) hay không.
        /// </summary>
        public void SetVisualState(bool unlocked)
        {
            isLocked = !unlocked;
            if (backgroundRenderer != null)
            {
                // Màu #676767 tương đương (103/255, 103/255, 103/255)
                backgroundRenderer.color = unlocked ? Color.white : new Color32(103, 103, 103, 255);
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
            // Nếu Tile đang bị đè lên (khóa), bỏ qua lượt click này
            if (isLocked)
            {
                Debug.Log($"[Tile] {gameObject.name} đang bị khóa và không thể click.");
                return;
            }

            // Gọi hệ thống FormulaManager để thử đẩy Tile này vào slot trên thanh công thức
            if (FormulaManager.Instance != null)
            {
                bool success = FormulaManager.Instance.TryAddTile(this);
                if (success)
                {
                    // Nếu đẩy thành công, tắt tính năng nhận click của Tile này
                    if (backgroundRenderer != null)
                    {
                        backgroundRenderer.raycastTarget = false;
                    }

                    // Mở khóa cho các tile bị nó đè lên
                    foreach (var tileBelow in coveredTiles)
                    {
                        if (tileBelow != null)
                        {
                            tileBelow.RemoveCoveringTile(this);
                        }
                    }
                    coveredTiles.Clear();
                }
            }
            else
            {
                Debug.LogWarning("[Tile] FormulaManager.Instance không có dữ liệu. Bạn đã đưa script FormulaManager vào scene chưa?");
            }
        }
    }
}
