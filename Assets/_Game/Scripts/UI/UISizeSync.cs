using UnityEngine;

namespace NumStrata.UI
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class UISizeSync : MonoBehaviour
    {
        public RectTransform target;
        public bool syncWidth = true;
        public float widthMultiplier = 1.0f;
        public float widthOffset = 0f;
        
        public bool syncHeight = false;
        public float heightMultiplier = 1.0f;
        public float heightOffset = 0f;

        private RectTransform myRect;

        void OnEnable()
        {
            myRect = GetComponent<RectTransform>();
        }

        void LateUpdate()
        {
            if (target == null || myRect == null) return;

            if (syncWidth)
            {
                float targetWidth = (target.rect.width * widthMultiplier) + widthOffset;
                if (!Mathf.Approximately(myRect.rect.width, targetWidth))
                {
                    myRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
                }
            }
            
            if (syncHeight)
            {
                float targetHeight = (target.rect.height * heightMultiplier) + heightOffset;
                if (!Mathf.Approximately(myRect.rect.height, targetHeight))
                {
                    myRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
                }
            }
        }
    }
}