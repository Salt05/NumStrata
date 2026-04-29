using UnityEngine;

namespace NumStrata.Gameplay
{
    /// <summary>
    /// Đối tượng gắn vào mỗi Slot trong Board_Grid để lưu trữ tọa độ ma trận.
    /// </summary>
    public class BoardCoordinate : MonoBehaviour
    {
        public int Row;
        public int Column;
        
        public string GetCoordinateKey() => $"{Row}-{Column}";

        /// <summary>
        /// Gán tọa độ cho slot
        /// </summary>
        public void SetCoordinate(int r, int c)
        {
            Row = r;
            Column = c;
        }
    }
}
