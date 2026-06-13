using UnityEngine;
using System.Collections.Generic;

namespace NumStrata.Gameplay
{
    public class TileCounter : MonoBehaviour
    {
        public static TileCounter Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        [Header("UI References")]
        public TMPro.TextMeshProUGUI txtTileCountTMP; // Dùng cho TextMeshPro

        private void Start()
        {
            // Cập nhật số lượng ngay lần đầu tiên
            UpdateTileCountUI();
        }

        /// <summary>
        /// Duyệt toàn bộ Scene để đếm số lượng Tile còn lại (Bỏ qua các Tile được tạo bởi Helper)
        /// </summary>
        /// <returns>Số lượng Tile có thể chơi được</returns>
        public int CountRemainingPlayableTiles()
        {
            int count = 0;
            foreach (Tile tile in Tile.AllExistingTiles)
            {
                // Chỉ đếm những Tile đang active và có type khác Helper
                if (tile != null && tile.gameObject.activeInHierarchy && tile.type != TileType.Helper)
                {
                    count++;
                }
            }
            
            return count;
        }

        /// <summary>
        /// Gắn số đếm được lên đối tượng Text UI
        /// </summary>
        public void UpdateTileCountUI()
        {
            int remaining = CountRemainingPlayableTiles();
            
            if (txtTileCountTMP != null) txtTileCountTMP.text = remaining.ToString();
        }

        /// <summary>
        /// Kiểm tra điều kiện thắng (Đã dọn sạch Tile chưa)
        /// </summary>
        public bool IsBoardCleared()
        {
            return CountRemainingPlayableTiles() == 0;
        }
    }
}
