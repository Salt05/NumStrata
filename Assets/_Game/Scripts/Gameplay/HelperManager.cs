using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using NumStrata.Utils;
using NumStrata.Data;

namespace NumStrata.Gameplay
{
    public class HelperManager : MonoBehaviour
    {
        public static HelperManager Instance { get; private set; }

        [Header("UI Structure")]
        public GameObject helperDimmer;
        public GameObject helperSpawnPopup;

        [Header("Buttons")]
        public Button btnSpawnGroup;
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
        private bool isShuffleActive = false;
        private bool isDeleteActive = false;
        private bool isSignNegative = false;

        private Sprite defaultSpawnIcon;
        private Sprite defaultShuffleIcon;
        private Sprite defaultDeleteIcon;

        private List<Tile> popupTiles = new List<Tile>();

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

            // Save default icons
            if (imgSpawnIcon != null) defaultSpawnIcon = imgSpawnIcon.sprite;
            if (imgShuffleIcon != null) defaultShuffleIcon = imgShuffleIcon.sprite;
            if (imgDeleteIcon != null) defaultDeleteIcon = imgDeleteIcon.sprite;

            // Add listeners
            if (btnSpawnGroup != null) btnSpawnGroup.onClick.AddListener(ToggleSpawn);
            if (btnShuffleGroup != null) btnShuffleGroup.onClick.AddListener(ToggleShuffle);
            if (btnDeleteGroup != null) btnDeleteGroup.onClick.AddListener(ToggleDelete);
            if (btnReturnGroup != null) btnReturnGroup.onClick.AddListener(ExecuteReturn);
            if (btnToggleSign != null) btnToggleSign.onClick.AddListener(ToggleSign);

            // Initialize UI
            if (helperDimmer != null) helperDimmer.SetActive(false);
            if (helperSpawnPopup != null) helperSpawnPopup.SetActive(false);
        }

        private void Start()
        {
            if (helperSpawnPopup != null)
            {
                popupTiles.AddRange(helperSpawnPopup.GetComponentsInChildren<Tile>(true));
                foreach (var tile in popupTiles)
                {
                    tile.type = TileType.Helper;
                }
            }
        }

        private void ToggleSpawn()
        {
            isSpawnActive = !isSpawnActive;
            
            if (isSpawnActive)
            {
                // Reset states of other helpers
                if (isShuffleActive) ToggleShuffle();
                if (isDeleteActive) ToggleDelete();

                if (imgSpawnIcon != null && helperIconCancel != null) imgSpawnIcon.sprite = helperIconCancel;
                if (helperDimmer != null) helperDimmer.SetActive(true);
                if (helperSpawnPopup != null) 
                {
                    helperSpawnPopup.SetActive(true);
                    if (UIEffectManager.Instance != null)
                    {
                        UIEffectManager.Instance.ScaleUp(helperSpawnPopup.transform, 0.25f);
                    }
                    else
                    {
                        helperSpawnPopup.transform.localScale = Vector3.one;
                    }
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
            if (imgSpawnIcon != null && defaultSpawnIcon != null) imgSpawnIcon.sprite = defaultSpawnIcon;
            
            if (helperSpawnPopup != null && UIEffectManager.Instance != null)
            {
                UIEffectManager.Instance.ScaleDown(helperSpawnPopup.transform, 0.2f, () => 
                {
                    if (helperSpawnPopup != null) helperSpawnPopup.SetActive(false);
                    if (helperDimmer != null) helperDimmer.SetActive(false);
                });
            }
            else
            {
                if (helperSpawnPopup != null) helperSpawnPopup.SetActive(false);
                if (helperDimmer != null) helperDimmer.SetActive(false);
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
                // Only update if it's not an operator
                if (string.IsNullOrEmpty(tile.operatorValue))
                {
                    int absValue = Mathf.Abs(tile.numberValue);
                    int newValue = isSignNegative ? -absValue : absValue;
                    tile.numberValue = newValue;
                    
                    Sprite newSprite = tileSpriteData.GetNumberSprite(newValue);
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

            if (FormulaManager.Instance == null) return;

            if (!FormulaManager.Instance.TryGetNextConveyorSlot(out Transform slot, out int index))
            {
                Debug.LogWarning("[HelperManager] Conveyor đầy, không thể spawn!");
                return;
            }

            // Clone tile
            GameObject clonedObj = Instantiate(clickedTile.gameObject, clickedTile.transform.position, clickedTile.transform.rotation, helperSpawnPopup.transform.parent);
            Tile spawnedTile = clonedObj.GetComponent<Tile>();
            
            // Set type back to normal
            if (!string.IsNullOrEmpty(clickedTile.operatorValue))
            {
                spawnedTile.type = TileType.Operator;
                spawnedTile.operatorValue = clickedTile.operatorValue;
            }
            else
            {
                spawnedTile.type = TileType.Number;
                spawnedTile.numberValue = clickedTile.numberValue;
            }

            spawnedTile.isLocked = false;
if (spawnedTile.backgroundRenderer != null)
            {
                spawnedTile.backgroundRenderer.raycastTarget = true;
                spawnedTile.backgroundRenderer.color = Color.white;
            }

            // Register to conveyor
            FormulaManager.Instance.RegisterTileToConveyor(spawnedTile, index);

            // Move clone to conveyor
            spawnedTile.transform.SetParent(slot, true);
            RectTransform rectT = spawnedTile.GetComponent<RectTransform>();
            if (rectT != null)
            {
                rectT.anchorMin = new Vector2(0.5f, 0.5f);
                rectT.anchorMax = new Vector2(0.5f, 0.5f);
                rectT.pivot = new Vector2(0.5f, 0.5f);
                rectT.SetAsLastSibling();

                if (UIEffectManager.Instance != null)
                {
                    UIEffectManager.Instance.MoveTo(rectT, Vector2.zero, 0.3f);
                }
                else
                {
                    rectT.anchoredPosition = Vector2.zero;
                }
            }

            // Close popup
            CloseSpawnPopup();
        }

        private void ToggleShuffle()
        {
            isShuffleActive = !isShuffleActive;
            if (isShuffleActive)
            {
                if (isSpawnActive) ToggleSpawn();
                if (isDeleteActive) ToggleDelete();

                if (imgShuffleIcon != null && helperIconCancel != null) imgShuffleIcon.sprite = helperIconCancel;
            }
            else
            {
                if (imgShuffleIcon != null && defaultShuffleIcon != null) imgShuffleIcon.sprite = defaultShuffleIcon;
            }
        }

        private void ToggleDelete()
        {
            isDeleteActive = !isDeleteActive;
            if (isDeleteActive)
            {
                if (isSpawnActive) ToggleSpawn();
                if (isShuffleActive) ToggleShuffle();

                if (imgDeleteIcon != null && helperIconCancel != null) imgDeleteIcon.sprite = helperIconCancel;
            }
            else
            {
                if (imgDeleteIcon != null && defaultDeleteIcon != null) imgDeleteIcon.sprite = defaultDeleteIcon;
            }
        }

        private void ExecuteReturn()
        {
            // Execute return immediately
            Debug.Log("[HelperManager] Return executed");
        }
    }
}
