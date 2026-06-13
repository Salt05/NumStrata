using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using NumStrata.Data;
using NumStrata.Gameplay;
using NumStrata.Utils;

namespace NumStrata.UI
{
    public class GameplayUIManager : MonoBehaviour
    {
        public static GameplayUIManager Instance { get; private set; }

        [Header("Win Popup Panel References")]
        [Tooltip("Drag the Win Popup GameObject from the scene Canvas hierarchy here.")]
        public GameObject winPopupPanel;
        public TextMeshProUGUI winDescText;
        public TextMeshProUGUI winRewardText;
        public Button winNextButton;
        public Button winHomeButton;

        [Header("Lose Popup Panel References")]
        [Tooltip("Drag the Lose Popup GameObject from the scene Canvas hierarchy here.")]
        public GameObject losePopupPanel;
        public TextMeshProUGUI loseDescText;
        public Button loseRetryButton;
        public Button loseAdButton;
        public Button loseHomeButton;

        [Header("Loading Board Panel References")]
        [Tooltip("Drag the Loading Board Wheel panel GameObject here.")]
        public GameObject loadingBoardWheelPanel;

        // Fallbacks for dynamic creation if inspector fields are not set
        private GameObject fallbackWinPopup;
        private GameObject fallbackLosePopup;
        private GameObject fallbackLoadingWheel;

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

            // Deactivate panels at start
            if (winPopupPanel != null) winPopupPanel.SetActive(false);
            if (losePopupPanel != null) losePopupPanel.SetActive(false);
            if (loadingBoardWheelPanel != null) loadingBoardWheelPanel.SetActive(false);
        }

        public void ShowWinScreen()
        {
            Time.timeScale = 0f;

            if (PauseManager.Instance != null)
            {
                PauseManager.Instance.ToggleDimmer(true, 0.25f);
            }

            // Award 50 gold on level complete
            if (LocalDataManager.Instance != null)
            {
                LocalDataManager.Instance.AddGold(50);
                LocalDataManager.Instance.FlushPlayerDataIfDirty();
            }

            bool nextLevelExists = false;
            if (LocalDataManager.Instance != null)
            {
                string nextLevelId = LocalDataManager.Instance.GetCurrentLevelId();
                bool isChallenge = PlayerPrefs.GetInt("IsChallengeMode", 0) == 1;
                string resourcePath = isChallenge ? "Streak/" + nextLevelId : "Campaign/" + nextLevelId;
                TextAsset nextLevelAsset = Resources.Load<TextAsset>(resourcePath);
                nextLevelExists = (nextLevelAsset != null);
            }

            // A. If the user has assigned a custom Win Panel, use it!
            if (winPopupPanel != null)
            {
                winPopupPanel.SetActive(true);

                if (winRewardText != null) winRewardText.text = "+50 VÀNG";

                if (!nextLevelExists && PlayerPrefs.GetInt("IsChallengeMode", 0) == 0)
                {
                    // Fallback to "Coming Soon" if no more levels (C7)
                    if (winDescText != null) winDescText.text = "Chúc mừng bạn đã hoàn thành tất cả cấp độ hiện tại!\nCác thử thách mới sẽ sớm ra mắt.";
                    if (winNextButton != null) winNextButton.gameObject.SetActive(false);
                }
                else
                {
                    if (winDescText != null) winDescText.text = "Bạn đã hoàn thành cấp độ này thành công!";
                    if (winNextButton != null) winNextButton.gameObject.SetActive(true);
                }

                // Bind Button Click Events
                if (winNextButton != null)
                {
                    winNextButton.onClick.RemoveAllListeners();
                    winNextButton.onClick.AddListener(OnNextLevelClicked);
                }
                if (winHomeButton != null)
                {
                    winHomeButton.onClick.RemoveAllListeners();
                    winHomeButton.onClick.AddListener(OnHomeClicked);
                }

                StartCoroutine(AnimatePopupOpen(winPopupPanel.transform));
            }
            else
            {
                // B. Fallback to basic dynamic Win Screen if references are missing
                Debug.LogWarning("[GameplayUIManager] winPopupPanel reference not set. Falling back to dynamic Win Screen.");
                ShowFallbackWinScreen(nextLevelExists);
            }
        }

        public void ShowLoseScreen()
        {
            Time.timeScale = 0f;

            if (PauseManager.Instance != null)
            {
                PauseManager.Instance.ToggleDimmer(true, 0.25f);
            }

            // A. If the user has assigned a custom Lose Panel, use it!
            if (losePopupPanel != null)
            {
                losePopupPanel.SetActive(true);

                if (loseDescText != null) loseDescText.text = "Không còn nước đi hợp lệ và đã dùng hết lượt trợ giúp.";

                if (loseAdButton != null)
                {
                    loseAdButton.gameObject.SetActive(true);
                    loseAdButton.onClick.RemoveAllListeners();
                    loseAdButton.onClick.AddListener(OnAdContinueClicked);
                }

                if (loseRetryButton != null)
                {
                    loseRetryButton.onClick.RemoveAllListeners();
                    loseRetryButton.onClick.AddListener(OnRetryClicked);
                }
                if (loseHomeButton != null)
                {
                    loseHomeButton.onClick.RemoveAllListeners();
                    loseHomeButton.onClick.AddListener(OnHomeClicked);
                }

                StartCoroutine(AnimatePopupOpen(losePopupPanel.transform));
            }
            else
            {
                // B. Fallback to basic dynamic Lose Screen if references are missing
                Debug.LogWarning("[GameplayUIManager] losePopupPanel reference not set. Falling back to dynamic Lose Screen.");
                ShowFallbackLoseScreen();
            }
        }

        public void ShowLoadingWheel(bool show)
        {
            // A. If the user has assigned a custom Loading Board Wheel, use it!
            if (loadingBoardWheelPanel != null)
            {
                loadingBoardWheelPanel.SetActive(show);
            }
            else
            {
                // B. Fallback to dynamic Loading Wheel
                if (show)
                {
                    if (fallbackLoadingWheel != null) return;

                    Transform canvasParent = GetCanvasParent();
                    if (canvasParent == null) return;

                    fallbackLoadingWheel = new GameObject("LoadingWheel_Fallback");
                    fallbackLoadingWheel.transform.SetParent(canvasParent, false);

                    RectTransform rect = fallbackLoadingWheel.AddComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.sizeDelta = Vector2.zero;

                    Image img = fallbackLoadingWheel.AddComponent<Image>();
                    img.color = new Color(0, 0, 0, 0.4f);

                    GameObject spinner = new GameObject("Spinner");
                    spinner.transform.SetParent(fallbackLoadingWheel.transform, false);

                    RectTransform spinnerRect = spinner.AddComponent<RectTransform>();
                    spinnerRect.sizeDelta = new Vector2(100f, 100f);
                    spinnerRect.anchoredPosition = Vector2.zero;

                    Image spinnerImg = spinner.AddComponent<Image>();
                    if (LevelLoader.Instance != null && LevelLoader.Instance.tileSpriteData != null && LevelLoader.Instance.tileSpriteData.mysterySprite != null)
                    {
                        spinnerImg.sprite = LevelLoader.Instance.tileSpriteData.mysterySprite;
                    }
                    spinnerImg.color = new Color32(59, 130, 246, 255);

                    spinner.AddComponent<SpinnerRotator>();

                    GameObject txtObj = new GameObject("LoadingText");
                    txtObj.transform.SetParent(fallbackLoadingWheel.transform, false);
                    RectTransform txtRect = txtObj.AddComponent<RectTransform>();
                    txtRect.anchoredPosition = new Vector2(0f, -80f);
                    txtRect.sizeDelta = new Vector2(400f, 50f);

                    TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
                    txt.text = "ĐANG TẠO BÀN CHƠI...";
                    txt.alignment = TextAlignmentOptions.Center;
                    txt.fontSize = 24f;
                    txt.color = Color.white;
                    txt.fontStyle = FontStyles.Bold;
                    TMP_FontAsset font = GetGameFont();
                    if (font != null) txt.font = font;
                }
                else
                {
                    if (fallbackLoadingWheel != null)
                    {
                        Destroy(fallbackLoadingWheel);
                        fallbackLoadingWheel = null;
                    }
                }
            }
        }

        private void OnNextLevelClicked()
        {
            Time.timeScale = 1f;
            if (winPopupPanel != null) winPopupPanel.SetActive(false);
            if (fallbackWinPopup != null) Destroy(fallbackWinPopup);

            bool isChallenge = PlayerPrefs.GetInt("IsChallengeMode", 0) == 1;
            if (isChallenge)
            {
                PlayerPrefs.SetString("TargetTabName", "Challenge");
                PlayerPrefs.Save();
                SceneManager.LoadScene("MainMenu");
            }
            else
            {
                string nextLevelId = LocalDataManager.Instance.GetCurrentLevelId();
                string resourcePath = "Campaign/" + nextLevelId;
                TextAsset nextLevelAsset = Resources.Load<TextAsset>(resourcePath);

                if (nextLevelAsset != null)
                {
                    PlayerPrefs.SetInt("IsChallengeMode", 0);
                    PlayerPrefs.SetString(CampaignSession.PendingLevelIdKey, nextLevelId);
                    PlayerPrefs.Save();

                    if (LoadingScreenManager.Instance != null)
                    {
                        LoadingScreenManager.Instance.LoadScene("Gameplay");
                    }
                    else
                    {
                        SceneManager.LoadScene("Gameplay");
                    }
                }
                else
                {
                    PlayerPrefs.SetString("TargetTabName", "Home");
                    PlayerPrefs.Save();
                    SceneManager.LoadScene("MainMenu");
                }
            }
        }

        private void OnRetryClicked()
        {
            Time.timeScale = 1f;
            if (losePopupPanel != null) losePopupPanel.SetActive(false);
            if (fallbackLosePopup != null) Destroy(fallbackLosePopup);

            if (PauseManager.Instance != null)
            {
                PauseManager.Instance.RestartLevel();
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }

        private void OnAdContinueClicked()
        {
            if (HelperManager.Instance == null) return;

            // 1. Reset helper uses to 0
            HelperManager.Instance.ResetHelperUses();

            // 2. Grant 1 free helper usage of any type
            HelperManager.Instance.hasFreeHelperUsage = true;

            // 3. Unlock gameplay interactions
            LevelLoader.IsLevelActive = true;

            // 4. Close the Lose popup UI
            if (losePopupPanel != null) losePopupPanel.SetActive(false);
            if (fallbackLosePopup != null) Destroy(fallbackLosePopup);

            if (PauseManager.Instance != null)
            {
                PauseManager.Instance.ToggleDimmer(false, 0.25f);
            }

            Time.timeScale = 1f;

            // 5. Automatically Shuffle the board immediately (free shuffle)
            HelperManager.Instance.ExecuteShuffle(true);

            Debug.Log("[GameplayUIManager] Ad watched! Helper uses reset, free usage granted, board shuffled, and gameplay resumed.");
        }

        private void OnHomeClicked()
        {
            Time.timeScale = 1f;
            if (winPopupPanel != null) winPopupPanel.SetActive(false);
            if (losePopupPanel != null) losePopupPanel.SetActive(false);
            if (fallbackWinPopup != null) Destroy(fallbackWinPopup);
            if (fallbackLosePopup != null) Destroy(fallbackLosePopup);

            if (PauseManager.Instance != null)
            {
                PauseManager.Instance.GoToHome();
            }
            else
            {
                SceneManager.LoadScene("MainMenu");
            }
        }

        private void ShowFallbackWinScreen(bool nextLevelExists)
        {
            if (fallbackWinPopup != null) return;

            Transform canvasParent = GetCanvasParent();
            if (canvasParent == null) return;

            fallbackWinPopup = new GameObject("Popup_Win_Fallback");
            fallbackWinPopup.transform.SetParent(canvasParent, false);

            RectTransform rect = fallbackWinPopup.AddComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(580f, 620f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.zero;

            Image imgBg = fallbackWinPopup.AddComponent<Image>();
            if (PauseManager.Instance != null && PauseManager.Instance.popupPause != null)
            {
                Image refBg = PauseManager.Instance.popupPause.GetComponent<Image>();
                if (refBg != null)
                {
                    imgBg.sprite = refBg.sprite;
                    imgBg.type = Image.Type.Sliced;
                }
            }
            imgBg.color = new Color32(20, 24, 33, 245);

            fallbackWinPopup.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.5f);

            TMP_FontAsset mainFont = GetGameFont();

            // No Title Text generated per user request (designed directly in bg)

            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(fallbackWinPopup.transform, false);
            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchoredPosition = new Vector2(0f, 70f);
            descRect.sizeDelta = new Vector2(500f, 80f);
            TextMeshProUGUI descTxt = descObj.AddComponent<TextMeshProUGUI>();
            descTxt.text = "Bạn đã hoàn thành cấp độ này thành công!";
            descTxt.alignment = TextAlignmentOptions.Center;
            descTxt.fontSize = 24f;
            descTxt.color = Color.white;
            if (mainFont != null) descTxt.font = mainFont;

            GameObject rewardObj = new GameObject("Reward");
            rewardObj.transform.SetParent(fallbackWinPopup.transform, false);
            RectTransform rewardRect = rewardObj.AddComponent<RectTransform>();
            rewardRect.anchoredPosition = new Vector2(0f, -10f);
            rewardRect.sizeDelta = new Vector2(500f, 50f);
            TextMeshProUGUI rewardTxt = rewardObj.AddComponent<TextMeshProUGUI>();
            rewardTxt.text = "+50 VÀNG";
            rewardTxt.alignment = TextAlignmentOptions.Center;
            rewardTxt.fontSize = 30f;
            rewardTxt.fontStyle = FontStyles.Bold;
            rewardTxt.color = new Color32(252, 211, 77, 255);
            if (mainFont != null) rewardTxt.font = mainFont;

            if (!nextLevelExists && PlayerPrefs.GetInt("IsChallengeMode", 0) == 0)
            {
                descTxt.text = "Chúc mừng bạn đã hoàn thành tất cả cấp độ hiện tại!\nCác thử thách mới sẽ sớm ra mắt.";
                rewardRect.gameObject.SetActive(false);
                CreateUIButton(fallbackWinPopup.transform, "Btn_Home", "TRANG CHỦ", new Vector2(0f, -150f), new Vector2(320f, 70f), new Color32(75, 85, 99, 255), OnHomeClicked);
            }
            else
            {
                CreateUIButton(fallbackWinPopup.transform, "Btn_Next", "TIẾP TỤC", new Vector2(0f, -120f), new Vector2(320f, 70f), new Color32(16, 185, 129, 255), OnNextLevelClicked);
                CreateUIButton(fallbackWinPopup.transform, "Btn_Home", "TRANG CHỦ", new Vector2(0f, -210f), new Vector2(320f, 70f), new Color32(75, 85, 99, 255), OnHomeClicked);
            }

            StartCoroutine(AnimatePopupOpen(fallbackWinPopup.transform));
        }

        private void ShowFallbackLoseScreen()
        {
            if (fallbackLosePopup != null) return;

            Transform canvasParent = GetCanvasParent();
            if (canvasParent == null) return;

            fallbackLosePopup = new GameObject("Popup_Lose_Fallback");
            fallbackLosePopup.transform.SetParent(canvasParent, false);

            RectTransform rect = fallbackLosePopup.AddComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(580f, 650f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.zero;

            Image imgBg = fallbackLosePopup.AddComponent<Image>();
            if (PauseManager.Instance != null && PauseManager.Instance.popupPause != null)
            {
                Image refBg = PauseManager.Instance.popupPause.GetComponent<Image>();
                if (refBg != null)
                {
                    imgBg.sprite = refBg.sprite;
                    imgBg.type = Image.Type.Sliced;
                }
            }
            imgBg.color = new Color32(28, 20, 20, 245);

            fallbackLosePopup.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.5f);

            TMP_FontAsset mainFont = GetGameFont();

            // No Title Text generated per user request (designed directly in bg)

            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(fallbackLosePopup.transform, false);
            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchoredPosition = new Vector2(0f, 60f);
            descRect.sizeDelta = new Vector2(500f, 100f);
            TextMeshProUGUI descTxt = descObj.AddComponent<TextMeshProUGUI>();
            descTxt.text = "Không còn nước đi hợp lệ và đã dùng hết lượt trợ giúp.";
            descTxt.alignment = TextAlignmentOptions.Center;
            descTxt.fontSize = 24f;
            descTxt.color = Color.white;
            if (mainFont != null) descTxt.font = mainFont;

            CreateUIButton(fallbackLosePopup.transform, "Btn_Ad", "XEM QUẢNG CÁO", new Vector2(0f, -100f), new Vector2(320f, 65f), new Color32(245, 158, 11, 255), OnAdContinueClicked);
            CreateUIButton(fallbackLosePopup.transform, "Btn_Retry", "CHƠI LẠI", new Vector2(0f, -180f), new Vector2(320f, 65f), new Color32(59, 130, 246, 255), OnRetryClicked);
            CreateUIButton(fallbackLosePopup.transform, "Btn_Home", "TRANG CHỦ", new Vector2(0f, -260f), new Vector2(320f, 65f), new Color32(75, 85, 99, 255), OnHomeClicked);

            StartCoroutine(AnimatePopupOpen(fallbackLosePopup.transform));
        }

        private Transform GetCanvasParent()
        {
            GameObject safeArea = GameObject.Find("UI_SafeArea_Container");
            if (safeArea != null) return safeArea.transform;

            GameObject canvas = GameObject.Find("Canvas");
            if (canvas != null) return canvas.transform;

            return null;
        }

        private TMP_FontAsset GetGameFont()
        {
            if (TileCounter.Instance != null && TileCounter.Instance.txtTileCountTMP != null)
            {
                return TileCounter.Instance.txtTileCountTMP.font;
            }
            return null;
        }

        private Button CreateUIButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bgColor, UnityEngine.Events.UnityAction onClickAction)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image img = btnObj.AddComponent<Image>();
            if (PauseManager.Instance != null && PauseManager.Instance.popupPause != null)
            {
                Button refBtn = PauseManager.Instance.popupPause.GetComponentInChildren<Button>();
                if (refBtn != null && refBtn.GetComponent<Image>() != null)
                {
                    img.sprite = refBtn.GetComponent<Image>().sprite;
                    img.type = Image.Type.Sliced;
                }
            }
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(onClickAction);

            GameObject txtObj = new GameObject("Label");
            txtObj.transform.SetParent(btnObj.transform, false);

            RectTransform txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchoredPosition = Vector2.zero;
            txtRect.sizeDelta = size;
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;

            TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = 24f;
            txt.color = Color.white;
            txt.fontStyle = FontStyles.Bold;

            TMP_FontAsset mainFont = GetGameFont();
            if (mainFont != null) txt.font = mainFont;

            return btn;
        }

        private IEnumerator AnimatePopupOpen(Transform popup)
        {
            float elapsed = 0f;
            float duration = 0.25f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scale = Mathfs.EaseOutBack(t);
                popup.localScale = new Vector3(scale, scale, 1f);

                yield return null;
            }

            popup.localScale = Vector3.one;
        }
    }
}
