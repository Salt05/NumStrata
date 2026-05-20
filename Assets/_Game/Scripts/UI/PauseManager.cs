using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

namespace NumStrata.UI
{
    public class PauseManager : MonoBehaviour
    {
        public static PauseManager Instance { get; private set; }

        [Header("UI References")]
        public GameObject bgDimmer;
        public GameObject popupPause;
        public Canvas btnSpawnGroupCanvas;

        [Header("Dimmer Settings")]
        public float maxBlurSize = 3f;
        public float maxAlpha = 0.5f;
        public float transitionDuration = 0.25f;

        [Header("Scene Management")]
        public string homeSceneName = "MainMenu";

        private Coroutine dimmerCoroutine;
        private Material runtimeDimmerMaterial;
        private Image bgImageCache;

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

            // Ensure initial state & cache UI components
            if (bgDimmer != null) 
            {
                bgDimmer.SetActive(false);
                bgImageCache = bgDimmer.GetComponent<Image>();
                
                // Tự động lấy Material từ Image và tạo bản sao (Instance)
                // Điều này ngăn chặn việc code làm thay đổi file Asset Material gốc trong Project
                if (bgImageCache != null && bgImageCache.material != null)
                {
                    bgImageCache.material = new Material(bgImageCache.material);
                    runtimeDimmerMaterial = bgImageCache.material;
                }
            }

            if (popupPause != null) popupPause.SetActive(false);
        }

        public void ToggleDimmer(bool active, float duration)
        {
            if (bgDimmer == null) return;

            if (dimmerCoroutine != null)
            {
                StopCoroutine(dimmerCoroutine);
            }

            if (active)
            {
                bgDimmer.SetActive(true);
                dimmerCoroutine = StartCoroutine(DimmerRoutine(0f, maxBlurSize, 0f, maxAlpha, duration, true));
            }
            else
            {
                dimmerCoroutine = StartCoroutine(DimmerRoutine(maxBlurSize, 0f, maxAlpha, 0f, duration, false));
            }
        }

        private IEnumerator DimmerRoutine(float startBlur, float endBlur, float startAlpha, float endAlpha, float duration, bool keepActive)
        {
            float elapsed = 0f;
            
            if (runtimeDimmerMaterial != null) startBlur = runtimeDimmerMaterial.GetFloat("_BlurSize");
            if (bgImageCache != null) startAlpha = bgImageCache.color.a;

            if (duration <= 0f)
            {
                if (runtimeDimmerMaterial != null) runtimeDimmerMaterial.SetFloat("_BlurSize", endBlur);
                if (bgImageCache != null)
                {
                    Color c = bgImageCache.color;
                    c.a = endAlpha;
                    bgImageCache.color = c;
                }
                if (!keepActive) bgDimmer.SetActive(false);
                yield break;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                
                if (runtimeDimmerMaterial != null)
                {
                    runtimeDimmerMaterial.SetFloat("_BlurSize", Mathf.Lerp(startBlur, endBlur, t));
                }
                
                if (bgImageCache != null)
                {
                    Color c = bgImageCache.color;
                    c.a = Mathf.Lerp(startAlpha, endAlpha, t);
                    bgImageCache.color = c;
                }
                
                yield return null;
            }

            if (runtimeDimmerMaterial != null) runtimeDimmerMaterial.SetFloat("_BlurSize", endBlur);
            if (bgImageCache != null)
            {
                Color c = bgImageCache.color;
                c.a = endAlpha;
                bgImageCache.color = c;
            }

            if (!keepActive)
            {
                bgDimmer.SetActive(false);
            }
        }

        public void OpenPauseMenu()
        {
            ToggleDimmer(true, transitionDuration);
            if (popupPause != null) popupPause.SetActive(true);

            Time.timeScale = 0f;
            Debug.Log("[PauseManager] Game Paused.");
        }

        public void ResumeGame()
        {
            ToggleDimmer(false, transitionDuration);
            if (popupPause != null) popupPause.SetActive(false);

            Time.timeScale = 1f;
            Debug.Log("[PauseManager] Game Resumed.");
        }

        public void RestartLevel()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            Debug.Log("[PauseManager] Restarting level...");
        }

        public void GoToHome()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(homeSceneName);
            Debug.Log($"[PauseManager] Loading home scene: {homeSceneName}");
        }

        public void ToggleMusic()
        {
            Debug.Log("[PauseManager] Music toggle clicked.");
        }

        public void ToggleSound()
        {
            Debug.Log("[PauseManager] Sound toggle clicked.");
        }
    }
}
