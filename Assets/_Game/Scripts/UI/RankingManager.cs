using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Firebase.Firestore;
using Firebase.Extensions;
using NumStrata.Data;
using System.Linq;

namespace NumStrata.UI
{
    public class RankingManager : MonoBehaviour
    {
        public enum RankingMode { Campaign, Streak }

        [Header("UI References")]
        [SerializeField] private Transform contentContainer;
        [SerializeField] private RankingItemUI itemPrefab;
        [SerializeField] private RankingItemUI personalItemUI;
        
        [Header("Switch Settings")]
        [SerializeField] private Button switchButton;
        [SerializeField] private Image switchImage;
        [SerializeField] private Sprite campaignSwitchSprite;
        [SerializeField] private Sprite streakSwitchSprite;

        private RankingMode currentMode = RankingMode.Campaign;
        
        // Caching
        private List<DocumentSnapshot> cachedCampaignRanking = null;
        private List<DocumentSnapshot> cachedStreakRanking = null;
        private bool isFetching = false;

        private void Start()
        {
            if (switchButton != null)
            {
                switchButton.onClick.AddListener(ToggleRankingMode);
            }
            else if (switchImage != null)
            {
                // Tự động thêm Component Button vào Image để có thể click được
                Button tempButton = switchImage.gameObject.GetComponent<Button>();
                if (tempButton == null)
                {
                    tempButton = switchImage.gameObject.AddComponent<Button>();
                }
                tempButton.onClick.AddListener(ToggleRankingMode);
            }

            // Tự động load dữ liệu ngay khi mở game (theo yêu cầu test)
            if (CloudSyncManager.Instance != null)
            {
                if (CloudSyncManager.Instance.IsConnected)
                {
                    LoadRanking(currentMode);
                }
                
                // Lắng nghe sự kiện kết nối thành công để tự động lấy BXH
                CloudSyncManager.Instance.OnConnectionStatusChanged += HandleConnectionStatusChanged;
            }
        }

        private void HandleConnectionStatusChanged(bool isConnected)
        {
            if (isConnected)
            {
                // Khi Firebase vừa kết nối xong, tự động tải Ranking
                LoadRanking(currentMode);
            }
        }

        private void OnEnable()
        {
            // Tự động load lại ranking mỗi khi bật UI Ranking này lên (để lấy điểm mới nhất)
            if (CloudSyncManager.Instance != null && CloudSyncManager.Instance.IsConnected)
            {
                // Force fetch new data when enabled
                cachedCampaignRanking = null;
                cachedStreakRanking = null;
                LoadRanking(currentMode);
            }
        }

        private void OnDestroy()
        {
            if (switchButton != null)
            {
                switchButton.onClick.RemoveListener(ToggleRankingMode);
            }

            if (CloudSyncManager.Instance != null)
            {
                CloudSyncManager.Instance.OnConnectionStatusChanged -= HandleConnectionStatusChanged;
            }
        }

        public void ToggleRankingMode()
        {
            if (isFetching) return;

            currentMode = currentMode == RankingMode.Campaign ? RankingMode.Streak : RankingMode.Campaign;
            
            Debug.Log($"[RankingManager] Đã chuyển sang tab: {currentMode}");

            if (switchImage != null)
            {
                if (currentMode == RankingMode.Campaign && campaignSwitchSprite != null)
                    switchImage.sprite = campaignSwitchSprite;
                else if (currentMode == RankingMode.Streak && streakSwitchSprite != null)
                    switchImage.sprite = streakSwitchSprite;
            }

            LoadRanking(currentMode);
        }

        private void LoadRanking(RankingMode mode)
        {
            if (mode == RankingMode.Campaign && cachedCampaignRanking != null)
            {
                DisplayRanking(cachedCampaignRanking, mode);
                UpdatePersonalRanking(cachedCampaignRanking, mode);
            }
            else if (mode == RankingMode.Streak && cachedStreakRanking != null)
            {
                DisplayRanking(cachedStreakRanking, mode);
                UpdatePersonalRanking(cachedStreakRanking, mode);
            }
            else
            {
                FetchRankingFromFirebase(mode);
            }
        }

        private void FetchRankingFromFirebase(RankingMode mode)
        {
            if (CloudSyncManager.Instance == null || CloudSyncManager.Instance.Db == null)
            {
                Debug.LogWarning("[RankingManager] Firebase/Firestore is not ready yet.");
                return;
            }

            isFetching = true;
            string orderByField = mode == RankingMode.Campaign ? "currentLevelIndex" : "totalStreak";

            Debug.Log($"[RankingManager] Fetching Top 50 {mode}...");

            Query query = CloudSyncManager.Instance.Db.Collection("users")
                .OrderByDescending(orderByField)
                .Limit(50);

            query.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                isFetching = false;
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError($"[RankingManager] Failed to fetch ranking: {task.Exception}");
                    return;
                }

                List<DocumentSnapshot> results = task.Result.Documents.ToList();
                
                if (mode == RankingMode.Campaign)
                    cachedCampaignRanking = results;
                else
                    cachedStreakRanking = results;

                DisplayRanking(results, mode);
                UpdatePersonalRanking(results, mode);
            });
        }

        private void DisplayRanking(List<DocumentSnapshot> documents, RankingMode mode)
        {
            if (contentContainer == null || itemPrefab == null) return;

            foreach (Transform child in contentContainer)
            {
                Destroy(child.gameObject);
            }

            int rank = 1;
            foreach (DocumentSnapshot doc in documents)
            {
                string name = doc.GetValue<string>("displayName");
                if (string.IsNullOrEmpty(name)) name = "Unknown Player";
                
                // Bỏ qua các người chơi chưa liên kết Google (tên mặc định Player_...)
                if (name.StartsWith("Player_"))
                    continue;

                RankingItemUI newItem = Instantiate(itemPrefab, contentContainer);
                newItem.gameObject.SetActive(true); // Quan trọng: Đảm bảo Object được bật để Coroutine có thể chạy
                
                string avatar = doc.GetValue<string>("avatarUrl");
                long score = 0;
                
                if (mode == RankingMode.Campaign)
                {
                    if (doc.TryGetValue("currentLevelIndex", out long levelScore)) score = levelScore;
                }
                else
                {
                    if (doc.TryGetValue("totalStreak", out long streakScore)) score = streakScore;
                }

                newItem.Setup(rank, name, score, avatar);
                rank++;
            }
        }

        private void UpdatePersonalRanking(List<DocumentSnapshot> documents, RankingMode mode)
        {
            if (personalItemUI == null || LocalDataManager.Instance == null || LocalDataManager.Instance.CurrentPlayer == null) 
                return;

            PlayerData player = LocalDataManager.Instance.CurrentPlayer;
            string myId = player.playerId;

            int myRank = -1;
            for (int i = 0; i < documents.Count; i++)
            {
                if (documents[i].Id == myId)
                {
                    myRank = i + 1;
                    break;
                }
            }

            string myName = player.displayName;
            if (string.IsNullOrEmpty(myName)) myName = "You";
            string myAvatar = player.avatarUrl;
            long myScore = mode == RankingMode.Campaign ? player.campaign.currentLevelIndex : player.totalStreak;

            if (myRank != -1)
            {
                personalItemUI.Setup(myRank, myName, myScore, myAvatar);
            }
            else
            {
                personalItemUI.Setup(100, myName, myScore, myAvatar); // 100 nghĩa là > 50, script RankingItemUI sẽ hiện "50+"
            }
        }
    }
}
