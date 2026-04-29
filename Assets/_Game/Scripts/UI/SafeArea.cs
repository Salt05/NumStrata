using UnityEngine;

namespace NumStrata.UI
{
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea = new Rect(0, 0, 0, 0);
        private Vector2 lastScreenSize = new Vector2(0, 0);
        private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();

            if (rectTransform == null)
            {
                Debug.LogError("SafeArea: No RectTransform found on " + gameObject.name);
                enabled = false;
                return;
            }

            Refresh();
        }

        void Update()
        {
            Refresh();
        }

        void Refresh()
        {
            Rect safeArea = Screen.safeArea;

            if (safeArea != lastSafeArea || 
                Screen.width != lastScreenSize.x || 
                Screen.height != lastScreenSize.y || 
                Screen.orientation != lastOrientation)
            {
                lastScreenSize.x = Screen.width;
                lastScreenSize.y = Screen.height;
                lastOrientation = Screen.orientation;
                ApplySafeArea(safeArea);
            }
        }

        void ApplySafeArea(Rect r)
        {
            lastSafeArea = r;

            // Check for valid screen dimensions
            if (Screen.width <= 0 || Screen.height <= 0) return;

            // Convert safe area rectangle from pixels to normalized anchor coordinates
            Vector2 anchorMin = r.position;
            Vector2 anchorMax = r.position + r.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            // Limit values to [0, 1] range to avoid issues with some devices
            anchorMin.x = Mathf.Clamp01(anchorMin.x);
            anchorMin.y = Mathf.Clamp01(anchorMin.y);
            anchorMax.x = Mathf.Clamp01(anchorMax.x);
            anchorMax.y = Mathf.Clamp01(anchorMax.y);

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;

            // Reset offsets to 0 to match anchors exactly
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}