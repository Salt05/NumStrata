using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using NumStrata.Gameplay;

namespace NumStrata.UI
{
    public class LoadingScreenManager : MonoBehaviour
    {
        private static LoadingScreenManager instance;
        public static LoadingScreenManager Instance
        {
            get
            {
                if (instance == null)
                {
                    // 1. Try to load designer-made prefab from Resources
                    GameObject prefab = Resources.Load<GameObject>("LoadingScreenPrefab");
                    if (prefab != null)
                    {
                        GameObject go = Instantiate(prefab);
                        instance = go.GetComponent<LoadingScreenManager>();
                        if (instance != null)
                        {
                            DontDestroyOnLoad(go);
                            return instance;
                        }
                    }

                    // 2. Fallback to empty GameObject
                    GameObject fallbackGo = new GameObject("LoadingScreenManager_Fallback");
                    instance = fallbackGo.AddComponent<LoadingScreenManager>();
                    DontDestroyOnLoad(fallbackGo);
                }
                return instance;
            }
        }

        [Header("Designer UI References")]
        [Tooltip("Drag the CanvasGroup representing the loading screen root overlay here.")]
        public CanvasGroup canvasGroup;
        [Tooltip("Drag the Image representing the progress bar fill here.")]
        public Image progressBarFill;
        [Tooltip("Drag the TextMeshProUGUI for the percentage status here.")]
        public TextMeshProUGUI progressText;
        [Tooltip("Drag the TextMeshProUGUI for gameplay hints here.")]
        public TextMeshProUGUI hintText;
        [Tooltip("Drag the custom tile-based loading animation controller here.")]
        public LoadingAnimationController customLoadingPanel;

        [Header("Debug Settings")]
        public bool showDebugButtons = true;
        private bool isSimulatingLoading = false;

        // Fallbacks for dynamic creation if prefab/references are missing
        private GameObject loadingCanvasInstance;
        private Image fallbackProgressBarFill;
        private TextMeshProUGUI fallbackProgressText;
        private CanvasGroup fallbackCanvasGroup;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // Ensure the designer overlay is hidden on start if attached
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.gameObject.SetActive(false);
            }
            if (customLoadingPanel != null)
            {
                customLoadingPanel.gameObject.SetActive(false);
            }
        }

        public void LoadScene(string sceneName)
        {
            if (customLoadingPanel == null)
            {
                CreateLoadingUI();
            }
            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        private void CreateLoadingUI()
        {
            // A. If using the custom prefab with CanvasGroup reference, activate it
            if (canvasGroup != null)
            {
                canvasGroup.gameObject.SetActive(true);
                canvasGroup.alpha = 0f;
                
                // Show a random gameplay tip if hintText is assigned
                if (hintText != null)
                {
                    hintText.text = GetRandomGameplayHint();
                }
                return;
            }

            // B. Otherwise, fallback to dynamic UI generation
            if (loadingCanvasInstance != null) return;

            loadingCanvasInstance = new GameObject("LoadingCanvas_Fallback");
            DontDestroyOnLoad(loadingCanvasInstance);

            Canvas canvas = loadingCanvasInstance.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            CanvasScaler scaler = loadingCanvasInstance.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            loadingCanvasInstance.AddComponent<GraphicRaycaster>();
            fallbackCanvasGroup = loadingCanvasInstance.AddComponent<CanvasGroup>();
            fallbackCanvasGroup.alpha = 0f;

            // Background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(loadingCanvasInstance.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color32(12, 15, 23, 255);

            // Spinner
            GameObject spinnerObj = new GameObject("Spinner");
            spinnerObj.transform.SetParent(loadingCanvasInstance.transform, false);
            RectTransform spinnerRect = spinnerObj.AddComponent<RectTransform>();
            spinnerRect.sizeDelta = new Vector2(120f, 120f);
            spinnerRect.anchoredPosition = new Vector2(0f, 100f);
            spinnerRect.anchorMin = new Vector2(0.5f, 0.5f);
            spinnerRect.anchorMax = new Vector2(0.5f, 0.5f);
            spinnerRect.pivot = new Vector2(0.5f, 0.5f);
            Image spinnerImg = spinnerObj.AddComponent<Image>();
            spinnerImg.color = new Color32(59, 130, 246, 255);
            spinnerObj.AddComponent<SpinnerRotator>();

            // Text percentage
            GameObject textObj = new GameObject("LoadingText");
            textObj.transform.SetParent(loadingCanvasInstance.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(600f, 50f);
            textRect.anchoredPosition = new Vector2(0f, -50f);
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            fallbackProgressText = textObj.AddComponent<TextMeshProUGUI>();
            fallbackProgressText.text = "ĐANG TẢI... 0%";
            fallbackProgressText.alignment = TextAlignmentOptions.Center;
            fallbackProgressText.fontSize = 28f;
            fallbackProgressText.color = Color.white;
            fallbackProgressText.fontStyle = FontStyles.Bold;

            TMP_FontAsset gameFont = FindGameFont();
            if (gameFont != null) fallbackProgressText.font = gameFont;

            // Hint
            GameObject hintObj = new GameObject("HintText");
            hintObj.transform.SetParent(loadingCanvasInstance.transform, false);
            RectTransform hintRect = hintObj.AddComponent<RectTransform>();
            hintRect.sizeDelta = new Vector2(800f, 60f);
            hintRect.anchoredPosition = new Vector2(0f, -120f);
            hintRect.anchorMin = new Vector2(0.5f, 0.5f);
            hintRect.anchorMax = new Vector2(0.5f, 0.5f);
            hintRect.pivot = new Vector2(0.5f, 0.5f);
            TextMeshProUGUI hintTextComp = hintObj.AddComponent<TextMeshProUGUI>();
            hintTextComp.text = GetRandomGameplayHint();
            hintTextComp.alignment = TextAlignmentOptions.Center;
            hintTextComp.fontSize = 20f;
            hintTextComp.color = new Color32(156, 163, 175, 255);
            if (gameFont != null) hintTextComp.font = gameFont;

            // Bar Container
            GameObject barContainer = new GameObject("ProgressBarContainer");
            barContainer.transform.SetParent(loadingCanvasInstance.transform, false);
            RectTransform barContainerRect = barContainer.AddComponent<RectTransform>();
            barContainerRect.sizeDelta = new Vector2(600f, 20f);
            barContainerRect.anchoredPosition = new Vector2(0f, -250f);
            barContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
            barContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
            barContainerRect.pivot = new Vector2(0.5f, 0.5f);
            Image barContainerImg = barContainer.AddComponent<Image>();
            barContainerImg.color = new Color32(30, 41, 59, 255);

            // Bar Fill
            GameObject barFill = new GameObject("ProgressBarFill");
            barFill.transform.SetParent(barContainer.transform, false);
            RectTransform barFillRect = barFill.AddComponent<RectTransform>();
            barFillRect.anchorMin = new Vector2(0f, 0.5f);
            barFillRect.anchorMax = new Vector2(0f, 0.5f);
            barFillRect.pivot = new Vector2(0f, 0.5f);
            barFillRect.anchoredPosition = Vector2.zero;
            barFillRect.sizeDelta = new Vector2(0f, 20f);
            fallbackProgressBarFill = barFill.AddComponent<Image>();
            fallbackProgressBarFill.color = new Color32(6, 182, 212, 255);
        }

        private string GetRandomGameplayHint()
        {
            string[] hints = new string[]
            {
                "Gợi ý: Dùng dấu ngoặc để ưu tiên các phép tính trước!",
                "Mẹo: Xáo trộn (Shuffle) có thể giải vây cho bạn khi kẹt nước đi.",
                "Gợi ý: Hãy chú ý tới các ô bị đè ở lớp bên dưới.",
                "Gợi ý: Các ô phép chia sẽ lấy phần nguyên nếu chia không hết.",
                "Mẹo: Khiên sẽ bảo vệ bạn khi hết nước đi và tự động trộn board!"
            };
            return hints[Random.Range(0, hints.Length)];
        }

        private TMP_FontAsset FindGameFont()
        {
            if (TileCounter.Instance != null && TileCounter.Instance.txtTileCountTMP != null)
            {
                return TileCounter.Instance.txtTileCountTMP.font;
            }
            return null;
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            if (customLoadingPanel != null)
            {
                customLoadingPanel.gameObject.SetActive(true);
                customLoadingPanel.StartLoop();

                yield return null;

                AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
                op.allowSceneActivation = false;

                while (op.progress < 0.9f)
                {
                    yield return null;
                }

                op.allowSceneActivation = true;
                while (!op.isDone)
                {
                    yield return null;
                }

                customLoadingPanel.SetDoneLoading();

                while (customLoadingPanel.gameObject.activeInHierarchy)
                {
                    yield return null;
                }
            }
            else
            {
                CanvasGroup activeGroup = canvasGroup != null ? canvasGroup : fallbackCanvasGroup;

                // Phase 1: Fade In
                float fadeElapsed = 0f;
                float fadeDuration = 0.2f;
                while (fadeElapsed < fadeDuration)
                {
                    fadeElapsed += Time.unscaledDeltaTime;
                    if (activeGroup != null) activeGroup.alpha = Mathf.Clamp01(fadeElapsed / fadeDuration);
                    yield return null;
                }
                if (activeGroup != null) activeGroup.alpha = 1f;

                yield return new WaitForSecondsRealtime(0.1f);

                // Phase 2: Async Load
                AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
                op.allowSceneActivation = false;

                float progress = 0f;
                while (op.progress < 0.9f)
                {
                    progress = Mathf.MoveTowards(progress, op.progress, Time.unscaledDeltaTime * 0.8f);
                    UpdateProgressBar(progress);
                    yield return null;
                }

                // Fill to 100%
                float finalElapsed = 0f;
                while (finalElapsed < 0.25f)
                {
                    finalElapsed += Time.unscaledDeltaTime;
                    progress = Mathf.MoveTowards(progress, 1f, Time.unscaledDeltaTime * 2f);
                    UpdateProgressBar(progress);
                    yield return null;
                }
                UpdateProgressBar(1f);

                yield return new WaitForSecondsRealtime(0.1f);

                // Activate Scene
                op.allowSceneActivation = true;
                while (!op.isDone)
                {
                    yield return null;
                }

                // Refresh active CanvasGroup reference after loading scene in case references changed
                activeGroup = canvasGroup != null ? canvasGroup : fallbackCanvasGroup;

                // Phase 3: Fade Out
                fadeElapsed = 0f;
                while (fadeElapsed < fadeDuration)
                {
                    fadeElapsed += Time.unscaledDeltaTime;
                    if (activeGroup != null) activeGroup.alpha = Mathf.Clamp01(1f - (fadeElapsed / fadeDuration));
                    yield return null;
                }
                if (activeGroup != null) activeGroup.alpha = 0f;

                // Cleanup
                if (canvasGroup != null)
                {
                    canvasGroup.gameObject.SetActive(false);
                }

                if (loadingCanvasInstance != null)
                {
                    Destroy(loadingCanvasInstance);
                    loadingCanvasInstance = null;
                }
            }
        }

        private void UpdateProgressBar(float value)
        {
            // A. Update Designer UI if available
            if (progressBarFill != null)
            {
                // Supports both fillAmount (Filled Image) or sizeDelta width (Bar)
                if (progressBarFill.type == Image.Type.Filled)
                {
                    progressBarFill.fillAmount = value;
                }
                else
                {
                    RectTransform rect = progressBarFill.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(value * 600f, rect.sizeDelta.y);
                }
            }
            if (progressText != null)
            {
                progressText.text = $"ĐANG TẢI... {Mathf.RoundToInt(value * 100f)}%";
            }

            // B. Update Fallback UI
            if (fallbackProgressBarFill != null)
            {
                RectTransform rect = fallbackProgressBarFill.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(value * 600f, rect.sizeDelta.y);
            }
            if (fallbackProgressText != null)
            {
                fallbackProgressText.text = $"ĐANG TẢI... {Mathf.RoundToInt(value * 100f)}%";
            }
        }

        private void OnGUI()
        {
            if (!showDebugButtons) return;

            // Simple overlay panel to test loading
            GUILayout.BeginArea(new Rect(20, 20, 250, 150));
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>LOADING DEBUG</b>", new GUIStyle { richText = true, normal = new GUIStyleState { textColor = Color.white } });

            if (customLoadingPanel != null)
            {
                if (!isSimulatingLoading)
                {
                    if (GUILayout.Button("Simulate Start Loading", GUILayout.Height(40)))
                    {
                        StartCoroutine(SimulateLoadingRoutine());
                    }
                }
                else
                {
                    if (GUILayout.Button("Simulate Stop Loading", GUILayout.Height(40)))
                    {
                        isSimulatingLoading = false;
                    }
                }
            }
            else
            {
                GUILayout.Label("<color=yellow>Custom Loading Panel not set.\nFallback UI in use.</color>", new GUIStyle { richText = true });
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private IEnumerator SimulateLoadingRoutine()
        {
            isSimulatingLoading = true;
            if (customLoadingPanel != null)
            {
                customLoadingPanel.gameObject.SetActive(true);
                customLoadingPanel.StartLoop();

                while (isSimulatingLoading)
                {
                    yield return null;
                }

                customLoadingPanel.SetDoneLoading();

                while (customLoadingPanel.gameObject.activeInHierarchy)
                {
                    yield return null;
                }
            }
        }
    }
}
