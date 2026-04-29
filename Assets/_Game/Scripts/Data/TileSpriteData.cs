using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace NumStrata.Data
{
    [CreateAssetMenu(fileName = "TileSpriteData", menuName = "NumStrata/Data/TileSpriteData")]
    public class TileSpriteData : ScriptableObject
    {
        [Header("Numbers")]
        public Sprite[] numberSprites; // Support 2 modes: [0..9] or [-9..9] (offset +9)
        
        [Header("Operators")]
        public Sprite plusSprite;
        public Sprite minusSprite;
        public Sprite multiplySprite;
        public Sprite divideSprite;

        [Header("Special")]
        public Sprite mysterySprite;

        [ContextMenu("Validate Sprite Data")]
        public void ValidateSpriteData()
        {
            int count = numberSprites == null ? 0 : numberSprites.Length;
            Debug.Log($"[TileSpriteData] numberSprites count={count}. Expected either 10 (0..9) or 19 (-9..9).", this);

            if (plusSprite == null) Debug.LogWarning("[TileSpriteData] Missing plusSprite.", this);
            if (minusSprite == null) Debug.LogWarning("[TileSpriteData] Missing minusSprite.", this);
            if (multiplySprite == null) Debug.LogWarning("[TileSpriteData] Missing multiplySprite.", this);
            if (divideSprite == null) Debug.LogWarning("[TileSpriteData] Missing divideSprite.", this);
            if (mysterySprite == null) Debug.LogWarning("[TileSpriteData] Missing mysterySprite.", this);

            if (numberSprites != null && numberSprites.Any(s => s == null))
            {
                Debug.LogWarning("[TileSpriteData] numberSprites has null entries.", this);
            }
        }

        public Sprite GetNumberSprite(int value)
        {
            if (numberSprites == null || numberSprites.Length == 0)
            {
                return null;
            }

            // Mode A (the order shown in Inspector):
            // Element 0..18 maps strictly to -9..9.
            if (numberSprites.Length == 19)
            {
                int index = value + 9;
                if (index >= 0 && index < 19)
                {
                    return numberSprites[index];
                }

                return null;
            }

            // Mode B: Compact set for 0..9 only
            if (numberSprites.Length == 10)
            {
                if (value >= 0 && value <= 9)
                {
                    return numberSprites[value];
                }

                // No negative sprite data in compact mode.
                return null;
            }

            // Any other array size is treated as invalid mapping to avoid wrong sprite index.
            return null;
        }

        public Sprite GetOperatorSprite(string op)
        {
            switch (op)
            {
                case "+":
                case "＋":
                    return plusSprite;
                case "-":
                case "−":
                case "–":
                    return minusSprite;
                case "x":
                case "*":
                case "×":
                    return multiplySprite;
                case "/":
                case "÷":
                    return divideSprite;
                default: return null;
            }
        }
    }
}
