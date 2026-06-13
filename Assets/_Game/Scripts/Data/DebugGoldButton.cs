using NumStrata.Data;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

namespace NumStrata.Data
{
    public class DebugGoldButton : MonoBehaviour
{
    [Header("Profile UI Elements")]
    [SerializeField] public GameObject profileContainer;
    [SerializeField] public TextMeshProUGUI displayNameText;
    [SerializeField] public Image avatarImage;

    private bool showLoginPopup = false;
    private string inputName = "guest";
    private string inputAvatarUrl = "data:image/png;base64,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";

    private void Start()
    {
        // Ẩn profileContainer lúc đầu, hoặc hiện nếu đã có sẵn dữ liệu avatar
        if (profileContainer != null)
        {
            PlayerData player = LocalDataManager.Instance?.CurrentPlayer;
            if (player != null && !string.IsNullOrEmpty(player.avatarUrl))
            {
                profileContainer.SetActive(true);
                UpdateProfileUI(player.displayName, player.avatarUrl);
            }
            else
            {
                profileContainer.SetActive(false);
            }
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        int width = 221;
        int height = 40;
        int x = 10;
        int y = 10;
        int gap = 10;

        // Add Gold
        if (GUI.Button(new Rect(x, y, width, height), "Add Gold +10"))
        {
            LocalDataManager.Instance.AddGold(10);
            Debug.Log("[DEBUG] AddGold(10)");
        }

        y += height + gap;

        // Complete Campaign Level
        if (GUI.Button(new Rect(x, y, width, height), "Complete Campaign Level"))
        {
            LocalDataManager.Instance.FailCampaignLevel();
            Debug.Log("[DEBUG] FailCampaignLevel()");
        }

        y += height + gap;

        // Google Sign-In Show Pop-up Button
        if (GUI.Button(new Rect(x, y, width, height), "Google Login & Show Profile"))
        {
            showLoginPopup = true;
            Debug.Log("[DEBUG] Opened Google Login Simulation Pop-up.");
        }

        y += height + gap;

        // Google Sign-In Real SDK Button
        if (GUI.Button(new Rect(x, y, width, height), "Google Sign-In (REAL)"))
        {
            Debug.Log("[DEBUG] Google Sign-In (REAL) clicked.");
            TriggerGoogleLoginReal();
        }

        // Vẽ Pop-up Đăng nhập Google giả lập trong Editor
        if (showLoginPopup)
        {
            // Tăng kích thước Pop-up lên 600x350 và căn giữa màn hình
            Rect popupRect = new Rect(Screen.width / 2 - 300, Screen.height / 2 - 175, 1600, 950);
            GUI.Box(popupRect, "Google Sign-In Simulator");

            int startX = (int)popupRect.x + 30;
            int startY = (int)popupRect.y + 50;
            int fieldWidth = 540;

            // Thiết lập font size lớn hơn cho tiêu đề/label
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 14;
            labelStyle.fontStyle = FontStyle.Bold;

            GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField);
            textFieldStyle.fontSize = 13;

            // Nhãn và trường nhập Tên
            GUI.Label(new Rect(startX, startY, fieldWidth, 25), "Google Display Name (Tên hiển thị):", labelStyle);
            inputName = GUI.TextField(new Rect(startX, startY + 30, fieldWidth, 35), inputName, textFieldStyle);

            // Nhãn và trường nhập Avatar URL
            GUI.Label(new Rect(startX, startY + 85, fieldWidth, 25), "Google Avatar Image URL (Link ảnh đại diện):", labelStyle);
            inputAvatarUrl = GUI.TextField(new Rect(startX, startY + 115, fieldWidth, 35), inputAvatarUrl, textFieldStyle);

            // Nút bấm đăng nhập
            if (GUI.Button(new Rect(startX, startY + 180, 250, 45), "Sign In (Đăng nhập)"))
            {
                showLoginPopup = false;
                TriggerGoogleLogin(inputName, inputAvatarUrl);
            }

            // Nút bấm hủy
            if (GUI.Button(new Rect(startX + 290, startY + 180, 250, 45), "Cancel (Hủy)"))
            {
                showLoginPopup = false;
            }
        }
    }
#endif

    private void TriggerGoogleLogin(string name, string avatarUrl)
    {
        if (CloudSyncManager.Instance == null)
        {
            Debug.LogError("[DebugGoldButton] CloudSyncManager Instance is null.");
            return;
        }

        CloudSyncManager.Instance.MockGoogleSignIn(name, avatarUrl, (success, message) =>
        {
            if (success)
            {
                Debug.Log($"[DebugGoldButton] Mock Google Sign-In success! User: {name}");
                if (profileContainer != null)
                {
                    profileContainer.SetActive(true);
                }
                UpdateProfileUI(name, avatarUrl);
            }
            else
            {
                Debug.LogError($"[DebugGoldButton] Mock Google Sign-In failed: {message}");
            }
        });
    }

    private void TriggerGoogleLoginReal()
    {
        if (CloudSyncManager.Instance == null)
        {
            Debug.LogError("[DebugGoldButton] CloudSyncManager Instance is null.");
            return;
        }

        CloudSyncManager.Instance.SignInWithGoogleReal((success, message) =>
        {
            if (success)
            {
                PlayerData player = LocalDataManager.Instance.CurrentPlayer;
                string displayName = player?.displayName ?? "Google User";
                string avatarUrl = player?.avatarUrl ?? "";

                Debug.Log($"[DebugGoldButton] Google Sign-In (REAL) success! Name: {displayName}");
                if (profileContainer != null)
                {
                    profileContainer.SetActive(true);
                }
                UpdateProfileUI(displayName, avatarUrl);
            }
            else
            {
                Debug.LogError($"[DebugGoldButton] Google Sign-In (REAL) failed: {message}");
            }
        });
    }

    private void UpdateProfileUI(string displayName, string avatarUrl)
    {
        if (displayNameText != null)
        {
            displayNameText.text = displayName;
        }

        if (avatarImage != null && !string.IsNullOrEmpty(avatarUrl))
        {
            StartCoroutine(DownloadAvatarCoroutine(avatarUrl));
        }
    }

    private IEnumerator DownloadAvatarCoroutine(string url)
    {
        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            // Thêm User-Agent giả lập Trình duyệt để Google không chặn request tải ảnh (Lỗi 400 Bad Request)
            request.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
                if (texture != null && avatarImage != null)
                {
                    avatarImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    Debug.Log("[DebugGoldButton] Avatar downloaded and displayed successfully.");
                }
            }
            else
            {
                Debug.LogError($"[DebugGoldButton] Failed to download avatar from {url}: {request.error}");
            }
        }
    }
}
}