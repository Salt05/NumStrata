using UnityEngine;
using UnityEngine.UI;

namespace NumStrata.UI
{
    /// <summary>
    /// Tự động điều chỉnh Spacing cho Board_Grid dựa trên kích thước Board (RectTransform).
    /// Đảm bảo các RowGroup luôn overlap đúng tỉ lệ khi màn hình thay đổi.
    /// </summary>
    [RequireComponent(typeof(VerticalLayoutGroup))]
    [ExecuteAlways]
    public class ResponsiveBoardSpacing : MonoBehaviour
    {
        [Header("Ratios")]
        [Tooltip("Tỉ lệ spacing so với chiều rộng của Board. Mặc định -0.1064 (tương đương tileSize * -0.56)")]
        [SerializeField] private float spacingRatio = -0.1064f;

        private VerticalLayoutGroup layoutGroup;
        private RectTransform rectTransform;

        private void Awake()
        {
            layoutGroup = GetComponent<VerticalLayoutGroup>();
            rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            UpdateLayout();
        }

        private void Start()
        {
            UpdateLayout();
        }

        // Tự động gọi khi RectTransform thay đổi kích thước (do Canvas Scaler hoặc Resize window)
        private void OnRectTransformDimensionsChange()
        {
            UpdateLayout();
        }

        public void UpdateLayout()
        {
            if (layoutGroup == null || rectTransform == null) return;

            float width = rectTransform.rect.width;
            if (width <= 0) return;

            // Tính toán và áp dụng spacing
            float newSpacing = width * spacingRatio;
            if (!Mathf.Approximately(layoutGroup.spacing, newSpacing))
            {
                layoutGroup.spacing = newSpacing;
            }

            // Đảm bảo các thiết lập layout luôn đúng để tự động căn chỉnh kích thước con
            if (!layoutGroup.childControlHeight || !layoutGroup.childForceExpandHeight)
            {
                layoutGroup.childControlHeight = true;
                layoutGroup.childForceExpandHeight = true;
                layoutGroup.childControlWidth = true;
                layoutGroup.childForceExpandWidth = true;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UpdateLayout();
        }
#endif
    }
}
