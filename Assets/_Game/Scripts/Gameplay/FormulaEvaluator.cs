using UnityEngine;
using System;

namespace NumStrata.Gameplay
{
    /// <summary>
    /// Chức năng: Bộ não tính toán của game.
    /// Script này thực hiện giải mã các Tile từ Slot 1 đến Slot 5 theo quy tắc GDD (Sign/Magnitude).
    /// </summary>
    public class FormulaEvaluator : MonoBehaviour
    {
        public static FormulaEvaluator Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// Kiểm tra tính đúng đắn của biểu thức: [Slot1] [Slot2] [Slot3] = [Kết quả từ Slot4 & Slot5]
        /// </summary>
        public bool CheckResult(Tile[] slots)
        {
            if (slots[0] == null || slots[1] == null || slots[2] == null || slots[3] == null)
                return false;

            // 1. Lấy giá trị vế trái
            float val1 = slots[0].numberValue;
            string op = slots[1].operatorValue;
            float val2 = slots[2].numberValue;

            float leftSideResult = 0;

            // 2. Tính toán vế trái
            switch (op)
            {
                case "+": leftSideResult = val1 + val2; break;
                case "-": leftSideResult = val1 - val2; break;
                case "x": case "*": leftSideResult = val1 * val2; break;
                case "/": 
                    if (val2 == 0) return false; // Chia cho 0 sẽ xử lý thua (GDD 2.6)
                    leftSideResult = (float)Math.Floor(val1 / val2); // Lấy phần nguyên theo GDD 2.5
                    break;
                default: return false;
            }

            // 3. Giải mã vế phải (Slot 4 và Slot 5) theo quy tắc Signed Two-Digit Encoding (GDD 2.4)
            int slot4Val = slots[3].numberValue;
            int slot5Val = (slots[4] != null) ? slots[4].numberValue : 0;
            bool hasS5 = (slots[4] != null);

            int rightSideResult = DecodeTwoSlots(slot4Val, slot5Val, hasS5);

            // 4. So sánh
            bool isCorrect = (leftSideResult == rightSideResult);

            // LOG CHI TIẾT ĐỂ DEBUG
            if (rightSideResult == int.MinValue)
            {
                Debug.Log($"[Evaluator] SAI: Slot 5 mang giá trị âm ({slot5Val}). Phép tính tự động thất bại.");
            }
            else
            {
                Debug.Log($"[Evaluator] Kiểm tra: {val1} {op} {val2} = {rightSideResult} (Vế trái tính ra: {leftSideResult}) -> Kết quả: {isCorrect}");
            }

            return isCorrect;
        }

        /// <summary>
        /// Giải mã theo công thức GDD 2.4:
        /// magnitude = abs(Slot4) * 10 + abs(Slot5)
        /// sign = signOf(Slot4) * signOf(Slot5)
        /// </summary>
        private int DecodeTwoSlots(int s4, int s5, bool hasS5)
        {
            // Trường hợp 1: Nếu kết quả chỉ có 1 chữ số (không có Slot 5)
            if (!hasS5)
            {
                return s4;
            }

            // Trường hợp 2: Kết quả 2 chữ số (có Slot 5)
            // LUẬT MỚI: Chỉ chấp nhận dấu âm ở Slot 4 (Hàng chục). Slot 5 (Hàng đơn vị) phải là số dương (0-9).
            // Nếu người chơi nhập Slot 5 âm -> Coi như sai (trả về giá trị không tưởng để fail check)
            if (s5 < 0) return int.MinValue; 

            int magnitude = Math.Abs(s4) * 10 + s5;
            int sign = (s4 < 0) ? -1 : 1;

            return sign * magnitude;
        }
    }
}