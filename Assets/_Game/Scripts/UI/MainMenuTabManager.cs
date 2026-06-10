using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

/// <summary>
/// Quản lý việc chuyển đổi giữa các Tab (Home, Challenge, Settings) trong Main Menu.
/// </summary>
public class MainMenuTabManager : MonoBehaviour
{
    [System.Serializable]
    public class TabItem
    {
        public string tabName;
        public GameObject tabContent;    // Nội dung bên trong BODY (Home, Challenge, Setting...)
        public GameObject tabButtonObj;  // Nút bấm ở NAVIGATION (HomeTab, ChallengeTab...)
        public bool isSettingTab = false; // Đánh dấu nếu đây là Tab Setting để ẩn Header
    }

    [Header("Cấu hình Tabs")]
    public List<TabItem> tabs = new List<TabItem>();
    public int defaultTabIndex = 0;

    [Header("Tham chiếu chung")]
    public GameObject headerObject; // HEADER container cần ẩn/hiện

    private void Awake()
    {
        // Singleton pattern cho TabManager để các Scene khác có thể gọi
        Instance = this;
    }

    public static MainMenuTabManager Instance { get; private set; }

    private void Start()
    {
        // Khởi tạo các Proxy click cho các nút bấm (vì nút là hình ảnh bình thường)
        foreach (var tab in tabs)
        {
            if (tab.tabButtonObj != null)
            {
                var proxy = tab.tabButtonObj.GetComponent<TabClickProxy>();
                if (proxy == null) proxy = tab.tabButtonObj.AddComponent<TabClickProxy>();
                
                TabItem currentTab = tab; // Tránh closure issue
                proxy.Initialize(() => SwitchTab(currentTab));
            }
        }

        // Ưu tiên mở Tab theo Tên được yêu cầu từ scene trước
        string targetTabName = PlayerPrefs.GetString("TargetTabName", "");
        if (!string.IsNullOrEmpty(targetTabName))
        {
            SwitchTabByName(targetTabName);
            PlayerPrefs.DeleteKey("TargetTabName");
            PlayerPrefs.Save();
        }
        else if (tabs.Count > defaultTabIndex)
        {
            SwitchTab(tabs[defaultTabIndex]);
        }
    }

    public void SwitchTabByName(string name)
    {
        var target = tabs.Find(t => t.tabName.Equals(name, System.StringComparison.OrdinalIgnoreCase));
        if (target != null) SwitchTab(target);
    }

    public void SwitchTab(TabItem targetTab)
    {
        if (targetTab == null) return;

        // 1. Bật/Tắt nội dung các Tab
        foreach (var tab in tabs)
        {
            if (tab.tabContent != null)
                tab.tabContent.SetActive(tab == targetTab);
            
            // Ở đây bạn có thể thêm logic đổi màu/sprite cho nút bấm để người dùng biết tab nào đang chọn
        }

        // 2. Xử lý HEADER theo yêu cầu
        if (headerObject != null)
        {
            // Nếu là Tab Setting thì ẩn Header (false), ngược lại thì hiện (true)
            headerObject.SetActive(!targetTab.isSettingTab);
        }

        Debug.Log($"<color=cyan>[TabManager]</color> Switched to: {targetTab.tabName}. Header Active: {!targetTab.isSettingTab}");
    }
}

/// <summary>
/// Class phụ trợ để nhận sự kiện click cho Image
/// </summary>
public class TabClickProxy : MonoBehaviour, IPointerClickHandler
{
    private System.Action _onClickAction;

    public void Initialize(System.Action onClick)
    {
        _onClickAction = onClick;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _onClickAction?.Invoke();
    }
}
