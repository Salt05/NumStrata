using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

namespace NumStrata.UI
{
    public class RankingItemUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI scoreText; // Không bắt buộc, nhưng tốt nếu có
        [SerializeField] private RawImage avatarImage;

        [Header("Item Configuration")]
        [SerializeField] private bool isPersonalItem = false; // Đánh dấu true cho MyRank ở dưới cùng

        [Header("Background Styling")]
        [SerializeField] private Image backgroundImage; // Image nền của Row
        [SerializeField] private Sprite top1Bg;
        [SerializeField] private Sprite top2Bg;
        [SerializeField] private Sprite top3Bg;
        [SerializeField] private Sprite defaultBg; // Sprite cho top 4 trở đi

        private string pendingAvatarUrl;

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(pendingAvatarUrl))
            {
                StartCoroutine(LoadAvatarCoroutine(pendingAvatarUrl));
                pendingAvatarUrl = null;
            }
        }

        public void Setup(int rank, string playerName, long score, string avatarUrl)
        {
            // Thiết lập tên và điểm
            if (nameText != null) nameText.text = playerName;
            if (scoreText != null) scoreText.text = score.ToString();

            // Xử lý Rank Text
            if (rankText != null)
            {
                // Nếu là Top 1,2,3 và KHÔNG phải MyRank -> Ẩn text đi (vì BG đã có số)
                if (rank <= 3 && !isPersonalItem)
                {
                    rankText.text = ""; 
                }
                else
                {
                    // Nếu > 50 thì hiển thị ngoài Top 50
                    if (rank > 50) 
                        rankText.text = "50+";
                    else 
                        rankText.text = rank.ToString();
                }
            }

            // Xử lý đổi Background
            if (backgroundImage != null && !isPersonalItem)
            {
                if (rank == 1 && top1Bg != null)
                {
                    backgroundImage.sprite = top1Bg;
                }
                else if (rank == 2 && top2Bg != null)
                {
                    backgroundImage.sprite = top2Bg;
                }
                else if (rank == 3 && top3Bg != null)
                {
                    backgroundImage.sprite = top3Bg;
                }
                else if (defaultBg != null)
                {
                    backgroundImage.sprite = defaultBg;
                }
            }

            // Tải Avatar
            if (avatarImage != null)
            {
                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    if (gameObject.activeInHierarchy)
                    {
                        StartCoroutine(LoadAvatarCoroutine(avatarUrl));
                    }
                    else
                    {
                        // Lưu lại URL, khi nào UI này được bật (OnEnable) thì sẽ tự động tải
                        pendingAvatarUrl = avatarUrl;
                    }
                }
                else
                {
                    avatarImage.texture = null; 
                }
            }
        }

        private IEnumerator LoadAvatarCoroutine(string url)
        {
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(request);
                    if (avatarImage != null)
                    {
                        avatarImage.texture = texture;
                    }
                }
                else
                {
                    Debug.LogWarning($"[RankingItemUI] Failed to load avatar: {url} - {request.error}");
                }
            }
        }
    }
}
