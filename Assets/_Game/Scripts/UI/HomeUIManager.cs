using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using NumStrata.Data;

namespace NumStrata.UI
{
    public class HomeUIManager : MonoBehaviour
    {
        [Header("Header (shared)")]
        [SerializeField] private TextMeshProUGUI dateText;
        [SerializeField] private TextMeshProUGUI coinValueText;
        [SerializeField] private TextMeshProUGUI streakValueText;

        [Header("Home panel")]
        [SerializeField] private TextMeshProUGUI levelValueText;
        [SerializeField] private Button playButton;

        [Header("Campaign launch")]
        [SerializeField] private string gameplaySceneName = "Gameplay";

        private void Awake()
        {
            EnsureLocalDataManager();

            if (playButton != null)
            {
                playButton.onClick.AddListener(OnPlayClicked);
            }
        }

        private void OnDestroy()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayClicked);
            }
        }

        private void OnEnable()
        {
            RefreshUI();
            if (LocalDataManager.Instance != null)
            {
                LocalDataManager.Instance.OnSyncCompleted += RefreshUI;
            }
        }

        private void OnDisable()
        {
            if (LocalDataManager.Instance != null)
            {
                LocalDataManager.Instance.OnSyncCompleted -= RefreshUI;
            }
        }

        public void RefreshUI()
        {
            RefreshDate();
            RefreshPlayerStats();
            RefreshLevelDisplay();
        }

        private void RefreshDate()
        {
            if (dateText == null) return;

            bool useVietnamese = LocalDataManager.Instance != null
                && LocalDataManager.Instance.Language.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

            CultureInfo culture = useVietnamese
                ? new CultureInfo("vi-VN")
                : CultureInfo.InvariantCulture;

            string format = useVietnamese ? "dddd, d MMMM yyyy" : "dddd, MMMM d, yyyy";
            dateText.text = DateTime.Now.ToString(format, culture).ToUpper(culture);
        }

        private void RefreshPlayerStats()
        {
            PlayerData player = LocalDataManager.Instance?.CurrentPlayer;
            int gold = player?.gold ?? 0;
            int streak = player?.totalStreak ?? 0;

            if (coinValueText != null)
            {
                coinValueText.text = gold.ToString("N0", CultureInfo.InvariantCulture);
            }

            if (streakValueText != null)
            {
                streakValueText.text = streak.ToString("N0", CultureInfo.InvariantCulture);
            }
        }

        private void RefreshLevelDisplay()
        {
            if (levelValueText == null) return;

            int level = LocalDataManager.Instance != null
                ? LocalDataManager.Instance.GetCurrentLevelIndex()
                : 1;
            levelValueText.text = level.ToString();
        }

        private void OnPlayClicked()
        {
            if (LocalDataManager.Instance == null) return;

            string levelId = LocalDataManager.Instance.GetCurrentLevelId();
            
            // CHỐT: Đảm bảo IsChallengeMode = 0 khi vào từ Campaign
            PlayerPrefs.SetInt("IsChallengeMode", 0);
            PlayerPrefs.SetString(CampaignSession.PendingLevelIdKey, levelId);
            PlayerPrefs.Save();

            Debug.Log($"[HomeUIManager] Starting campaign level '{levelId}', loading scene '{gameplaySceneName}'.");
            SceneManager.LoadScene(gameplaySceneName);
        }

        private static void EnsureLocalDataManager()
        {
            if (LocalDataManager.Instance != null) return;

            var go = new GameObject("LocalDataManager");
            go.AddComponent<LocalDataManager>();
            Debug.Log("[HomeUIManager] Created LocalDataManager (was missing in scene).");
        }
    }
}
