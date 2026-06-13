using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace NumStrata.UI
{
    /// <summary>
    /// Quản lý tập trung nhiều Switch UI trong cùng một component.
    /// Cho phép gán nhiều object và xử lý animation/log/event tại một nơi duy nhất.
    /// </summary>
    public class UISwitchManager : MonoBehaviour
    {
        public enum SwitchType
        {
            Sound,
            Music,
            Vibration,
            DarkMode,
            NativeRefreshRate,
            AccountConnect,
            RemoveAds,
            Generic
        }

        [System.Serializable]
        public class SwitchItem
        {
            public string label; // Tên gợi nhớ trong Inspector
            public SwitchType type;
            public GameObject interactionObject; // Object nhận click (phải có Raycast Target)
            public Animator animator;
            public bool isActive = true;
            
            [Space]
            public UnityEvent<bool> onValueChanged;

            [HideInInspector] public float lastClickTime = -1f;
        }

        [Header("Danh sách các Switch")]
        public List<SwitchItem> switchList = new List<SwitchItem>();

        [Header("Cấu hình chung")]
        public string animatorParameter = "IsActive";
        public float clickCooldown = 0.5f;

        private int _parameterHash;

        private void Awake()
        {
            // Trim() để tránh lỗi khoảng trắng thừa trong Inspector
            _parameterHash = Animator.StringToHash(animatorParameter.Trim());

            foreach (var item in switchList)
            {
                if (item.animator != null)
                {
                    item.animator.keepAnimatorStateOnDisable = true;
                }
            }
        }

        private void Start()
        {
            foreach (var item in switchList)
            {
                if (item.type == SwitchType.AccountConnect)
                {
                    if (NumStrata.Data.CloudSyncManager.Instance != null)
                    {
                        item.isActive = NumStrata.Data.CloudSyncManager.Instance.IsGoogleConnected();
                    }
                    else
                    {
                        item.isActive = false;
                    }
                }
                else
                {
                    // Tải trạng thái đã lưu từ PlayerPrefs
                    string saveKey = "Setting_" + item.type.ToString();
                    if (PlayerPrefs.HasKey(saveKey))
                    {
                        item.isActive = PlayerPrefs.GetInt(saveKey) == 1;
                    }
                }

                if (item.interactionObject != null)
                {
                    // Tự động thêm một "Proxy" để bắt sự kiện click cho từng object trong danh sách
                    var proxy = item.interactionObject.GetComponent<UISwitchClickProxy>();
                    if (proxy == null) proxy = item.interactionObject.AddComponent<UISwitchClickProxy>();
                    
                    SwitchItem currentItem = item;
                    proxy.Initialize(
                        () => OnSwitchClicked(currentItem),
                        () => {
                            if (currentItem.type == SwitchType.AccountConnect && NumStrata.Data.CloudSyncManager.Instance != null)
                            {
                                currentItem.isActive = NumStrata.Data.CloudSyncManager.Instance.IsGoogleConnected();
                            }
                            UpdateVisuals(currentItem, true); // Tự động sync khi object được Enable (mở tab Setting)
                        }
                    );
                }
            }

            // Đồng bộ lần đầu (nếu tab đang mở sẵn)
            StartCoroutine(InitialSyncCoroutine());
        }

        private System.Collections.IEnumerator InitialSyncCoroutine()
        {
            yield return null; 

            foreach (var item in switchList)
            {
                if (item.type == SwitchType.AccountConnect && NumStrata.Data.CloudSyncManager.Instance != null)
                {
                    item.isActive = NumStrata.Data.CloudSyncManager.Instance.IsGoogleConnected();
                }
                UpdateVisuals(item, true);
            }
        }

        // Xóa LateUpdate vì nó có thể gây tranh chấp với Animation Clips
        // private void LateUpdate() { ... }

        private void OnSwitchClicked(SwitchItem item)
        {
            if (Time.time - item.lastClickTime < clickCooldown) return;

            item.lastClickTime = Time.time;
            item.isActive = !item.isActive;

            // Lưu trạng thái mới vào PlayerPrefs
            string saveKey = "Setting_" + item.type.ToString();
            PlayerPrefs.SetInt(saveKey, item.isActive ? 1 : 0);
            PlayerPrefs.Save();

            // Cập nhật giao diện - Chạy animation bình thường (Smooth)
            UpdateVisuals(item, false);

            // Ghi log chung
            Debug.Log($"<color=#00FF00>[SwitchManager]</color> <b>{item.type} ({item.label})</b> saved & changed to: {(item.isActive ? "ON" : "OFF")}");

            // Kích hoạt event riêng của từng switch
            item.onValueChanged?.Invoke(item.isActive);
        }

        private void UpdateVisuals(SwitchItem item, bool isInstant)
        {
            if (item.animator == null) return;

            if (item.animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning($"[UISwitchManager] Animator trên {item.label} chưa có Controller!");
                return;
            }

            // Luôn set parameter kể cả khi inactive, để khi nó active nó có giá trị đúng
            item.animator.SetBool(_parameterHash, item.isActive);

            // Chỉ thực hiện Play() nếu object đang active
            if (isInstant && item.animator.gameObject.activeInHierarchy)
            {
                // Ép Animator tính toán ngay
                item.animator.Update(0f); 

                string stateName = item.isActive ? "SwitchWait" : "IsOff";
                string fullPath = "Switch." + stateName;
                
                // Nhảy trực tiếp tới State để tránh chạy Entry transition sai
                if (item.animator.HasState(0, Animator.StringToHash(fullPath)))
                {
                    item.animator.Play(fullPath, 0, 1f);
                }
                else
                {
                    item.animator.Play(stateName, 0, 1f);
                }
                
                // Đảm bảo các thay đổi của Play() cũng được thực thi ngay
                item.animator.Update(0f);
            }
        }

        /// <summary>
        /// API để các script khác có thể tìm và đổi trạng thái switch từ bên ngoài
        /// </summary>
        public void SetSwitchState(SwitchType type, bool state, bool triggerEvent = true)
        {
            var item = switchList.Find(x => x.type == type);
            if (item != null)
            {
                item.isActive = state;
                UpdateVisuals(item, false);
                if (triggerEvent) item.onValueChanged?.Invoke(state);
            }
        }

        #region Các hàm nhận Logic (Dùng để gán vào onValueChanged)

        public void ToggleSounds(bool isActive)
        {
            Debug.Log($"<color=#FF5722>[Settings Logic]</color> Sounds is now: {(isActive ? "ENABLED" : "DISABLED")}");
            // Sau này gọi SoundManager.Instance.SetMute(!isActive);
        }

        public void ToggleDarkMode(bool isActive)
        {
            Debug.Log($"<color=#3F51B5>[Settings Logic]</color> Dark Mode is now: {(isActive ? "ON" : "OFF")}");
            // Sau làm gọi ThemeManager.Instance.SetTheme(isActive);
        }

        public void ToggleVibrations(bool isActive)
        {
            Debug.Log($"<color=#4CAF50>[Settings Logic]</color> Vibrations is now: {(isActive ? "ENABLED" : "DISABLED")}");
            // Sau này gọi HapticManager.Instance.Enabled = isActive;
        }

        public void ToggleNativeRefreshRate(bool isActive)
        {
            Debug.Log($"<color=#FFEB3B>[Settings Logic]</color> Native Refresh Rate is now: {(isActive ? "HIGH (Native)" : "LOW (60 FPS)")}");
            // Sau này gọi Application.targetFrameRate = isActive ? (int)Screen.currentResolution.refreshRateRatio.value : 60;
        }

        public void ToggleAccountConnect(bool isActive)
        {
            Debug.Log($"<color=#9C27B0>[Settings Logic]</color> Account Connection is now: {(isActive ? "CONNECTED" : "DISCONNECTED")}");
            
            if (isActive)
            {
                if (NumStrata.Data.CloudSyncManager.Instance != null)
                {
                    NumStrata.Data.CloudSyncManager.Instance.SignInWithGoogleReal((success, message) =>
                    {
                        if (success)
                        {
                            if (NumStrata.Data.LocalDataManager.Instance != null && NumStrata.Data.LocalDataManager.Instance.CurrentPlayer != null)
                            {
                                var player = NumStrata.Data.LocalDataManager.Instance.CurrentPlayer;
                                Debug.Log($"<color=#9C27B0>[Settings Logic]</color> Google Sign-In Success! Name: {player.displayName}, Avatar: {player.avatarUrl}");
                            }
                        }
                        else
                        {
                            Debug.LogError($"<color=#FF0000>[Settings Logic]</color> Google Sign-In Failed: {message}");
                            SetSwitchState(SwitchType.AccountConnect, false, false);
                            PlayerPrefs.SetInt("Setting_AccountConnect", 0);
                            PlayerPrefs.Save();
                        }
                    });
                }
                else
                {
                    Debug.LogError("[Settings Logic] CloudSyncManager.Instance is null!");
                    SetSwitchState(SwitchType.AccountConnect, false, false);
                    PlayerPrefs.SetInt("Setting_AccountConnect", 0);
                    PlayerPrefs.Save();
                }
            }
            else
            {
                if (NumStrata.Data.CloudSyncManager.Instance != null)
                {
                    NumStrata.Data.CloudSyncManager.Instance.SignOut();
                }
            }
        }

        public void ToggleRemoveAds(bool isActive)
        {
            Debug.Log($"<color=#F44336>[Settings Logic]</color> Remove Ads state is now: {(isActive ? "ACTIVE (No Ads)" : "INACTIVE (Show Ads)")}");
            // Sau này gọi IAPManager.Instance.PurchaseRemoveAds(); hoặc kiểm tra quyền sở hữu
        }

        #endregion
    }

    /// <summary>
    /// Class phụ trợ để chuyển hướng sự kiện click từ Object con về Manager
    /// </summary>
    public class UISwitchClickProxy : MonoBehaviour, IPointerClickHandler
    {
        private System.Action _onClickAction;
        private System.Action _onEnableAction;

        public void Initialize(System.Action onClick, System.Action onEnable)
        {
            _onClickAction = onClick;
            _onEnableAction = onEnable;
        }

        private void OnEnable()
        {
            // Khi Object được bật lên (ví dụ khi mở Tab Setting), tự động yêu cầu Manager đồng bộ Visual
            _onEnableAction?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onClickAction?.Invoke();
        }
    }
}
