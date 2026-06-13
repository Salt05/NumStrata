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
        
        // Caching (Lưu cache ngắn hạn)
        private List<Dictionary<string, object>> cachedCampaignRanking = null;
        private List<Dictionary<string, object>> cachedStreakRanking = null;
        private float lastFetchTime = -1f;
        private const float CACHE_COOLDOWN = 600f; // 10 phút (600 giây)
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
            // Tự động load lại ranking mỗi khi bật UI Ranking này lên (kết hợp cache ngắn hạn)
            if (CloudSyncManager.Instance != null && CloudSyncManager.Instance.IsConnected)
            {
                if (Time.time - lastFetchTime > CACHE_COOLDOWN || lastFetchTime < 0)
                {
                    // Đã qua thời gian cooldown, xóa cache để bắt tải mới
                    cachedCampaignRanking = null;
                    cachedStreakRanking = null;
                }
                
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

            Debug.Log($"[RankingManager] Fetching System Rankings...");

            DocumentReference docRef = CloudSyncManager.Instance.Db.Collection("system").Document("rankings");

            docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                isFetching = false;
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError($"[RankingManager] Failed to fetch ranking: {task.Exception}");
                    return;
                }

                DocumentSnapshot snapshot = task.Result;
                if (!snapshot.Exists)
                {
                    Debug.LogWarning("[RankingManager] System rankings document does not exist yet.");
                    return;
                }

                // Lấy mảng dữ liệu tương ứng với mode
                string fieldName = mode == RankingMode.Campaign ? "campaign" : "streak";
                
                if (snapshot.TryGetValue(fieldName, out List<object> rankingList))
                {
                    List<Dictionary<string, object>> parsedList = new List<Dictionary<string, object>>();
                    foreach (object item in rankingList)
                    {
                        if (item is Dictionary<string, object> dict)
                        {
                            parsedList.Add(dict);
                        }
                    }

                    if (mode == RankingMode.Campaign)
                        cachedCampaignRanking = parsedList;
                    else
                        cachedStreakRanking = parsedList;

                    lastFetchTime = Time.time; // Cập nhật thời gian fetch cuối cùng

                    DisplayRanking(parsedList, mode);
                    UpdatePersonalRanking(parsedList, mode);
                }
            });
        }

        private void DisplayRanking(List<Dictionary<string, object>> documents, RankingMode mode)
        {
            if (contentContainer == null || itemPrefab == null) return;

            foreach (Transform child in contentContainer)
            {
                Destroy(child.gameObject);
            }

            int rank = 1;
            foreach (var doc in documents)
            {
                string name = doc.ContainsKey("displayName") ? doc["displayName"].ToString() : "Unknown Player";
                string avatar = doc.ContainsKey("avatarUrl") ? doc["avatarUrl"].ToString() : "";
                long score = doc.ContainsKey("score") ? System.Convert.ToInt64(doc["score"]) : 0;

                RankingItemUI newItem = Instantiate(itemPrefab, contentContainer);
                newItem.gameObject.SetActive(true); // Quan trọng: Đảm bảo Object được bật để Coroutine có thể chạy
                
                newItem.Setup(rank, name, score, avatar);
                rank++;
            }
        }

        private void UpdatePersonalRanking(List<Dictionary<string, object>> documents, RankingMode mode)
        {
            if (personalItemUI == null || LocalDataManager.Instance == null || LocalDataManager.Instance.CurrentPlayer == null) 
                return;

            PlayerData player = LocalDataManager.Instance.CurrentPlayer;
            string myId = player.playerId;

            int myRank = -1;
            for (int i = 0; i < documents.Count; i++)
            {
                if (documents[i].ContainsKey("id") && documents[i]["id"].ToString() == myId)
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
