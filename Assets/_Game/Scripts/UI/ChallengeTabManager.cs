using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using NumStrata.Data;

namespace NumStrata.UI
{
    /// <summary>
    /// Quản lý giao diện Tab Challenge theo GDD và yêu cầu của người dùng.
    /// </summary>
    public class ChallengeTabManager : MonoBehaviour
    {
        [Header("Streak Section")]
        [SerializeField] private TMP_Text streakValueText;
        [SerializeField] private Image streakIconImage;

        [Header("Streak Icon Config")]
        [Tooltip("Thứ tự icon tương ứng: 1, 7, 14, 30, 60, 120, 240, 480 ngày")]
        [SerializeField] private List<Sprite> streakIcons;

        [Header("Week Progress Section")]
        [Tooltip("Danh sách 7 Image đại diện cho Mon đến Sun theo thứ tự.")]
        [SerializeField] private List<Image> weekDayTabs; 
        [SerializeField] private Color completedColor = Color.white;
        [SerializeField] private Color upcomingColor = new Color(1f, 1f, 1f, 0.4f);

        [Header("Reward Section")]
        [SerializeField] private TMP_Text rewardProgressText;
        [SerializeField] private Slider rewardSlider;

        [Header("Buttons")]
        [SerializeField] private Button playButton;

        [Header("Config")]
        [SerializeField] private string gameplaySceneName = "MainGame";
        [SerializeField] private string tempLevelId = "campaign_0001";

        private void Start()
        {
            if (playButton != null)
            {
                playButton.onClick.AddListener(OnPlayButtonClicked);
            }
            
            RefreshUI();
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

        /// <summary>
        /// Cập nhật toàn bộ giao diện dựa trên PlayerData hiện tại.
        /// </summary>
        public void RefreshUI()
        {
            if (LocalDataManager.Instance == null || LocalDataManager.Instance.CurrentPlayer == null)
            {
                Debug.LogWarning("[ChallengeTabManager] LocalDataManager or CurrentPlayer is null.");
                return;
            }

            PlayerData player = LocalDataManager.Instance.CurrentPlayer;

            // 1. Streak Value (VD: 123 Days)
            if (streakValueText != null)
            {
                streakValueText.text = $"{player.totalStreak} Days";
            }

            // 1.1 Streak Icon
            UpdateStreakIcon(player.totalStreak);

            // 2. Week Tabs (Mon-Sun)
            UpdateWeekTabs(player.daily);

            // 3. Reward Section
            UpdateRewardSection(player.daily);
        }

        private void UpdateWeekTabs(DailySaveData dailyData)
        {
            if (weekDayTabs == null || weekDayTabs.Count == 0) return;

            // Giả định weekDayTabs[0] là Thứ 2, weekDayTabs[6] là Chủ Nhật.
            // completedDays chứa các giá trị 1-7 (1=T2, 7=CN).
            
            for (int i = 0; i < weekDayTabs.Count; i++)
            {
                int dayValue = i + 1; // 1 to 7
                bool isCompleted = dailyData.completedDays.Contains(dayValue);
                
                if (weekDayTabs[i] != null)
                {
                    weekDayTabs[i].color = isCompleted ? completedColor : upcomingColor;
                }
            }
        }

        private void UpdateRewardSection(DailySaveData dailyData)
        {
            int completedCount = dailyData.currentStreakCount;
            
            // Đảm bảo logic đồng bộ: completedCount khớp với số lượng trong danh sách
            if (dailyData.completedDays != null)
            {
                completedCount = dailyData.completedDays.Count;
            }

            if (rewardProgressText != null)
            {
                rewardProgressText.text = $"{completedCount}/7 received the reward";
            }

            if (rewardSlider != null)
            {
                rewardSlider.maxValue = 7;
                rewardSlider.value = completedCount;
            }
        }

        private void UpdateStreakIcon(int totalStreak)
        {
            if (streakIconImage == null || streakIcons == null || streakIcons.Count == 0) return;

            // Thresholds: 1, 7, 14, 30, 60, 120, 240, 480
            int[] thresholds = { 1, 7, 14, 30, 60, 120, 240, 480 };
            int iconIndex = 0;

            for (int i = 0; i < thresholds.Length; i++)
            {
                if (totalStreak >= thresholds[i])
                {
                    iconIndex = i;
                }
                else
                {
                    break;
                }
            }

            // Đảm bảo không vượt quá số lượng sprite thực tế trong list
            if (iconIndex < streakIcons.Count)
            {
                streakIconImage.sprite = streakIcons[iconIndex];
            }
        }

        private void OnPlayButtonClicked()
        {
            Debug.Log($"[ChallengeTabManager] Play button clicked. Setting level to {tempLevelId} and loading {gameplaySceneName}");
            
            // Đánh dấu là đang chơi chế độ Challenge để khi thắng hệ thống biết tăng streak
            // (Tạm thời dùng PlayerPrefs để lưu trạng thái này)
            PlayerPrefs.SetInt("IsChallengeMode", 1);
            
            // Lưu Level ID để Gameplay scene load đúng file JSON
            PlayerPrefs.SetString(CampaignSession.PendingLevelIdKey, tempLevelId);
            PlayerPrefs.Save();

            LoadingScreenManager.Instance.LoadScene(gameplaySceneName);
        }
    }
}
