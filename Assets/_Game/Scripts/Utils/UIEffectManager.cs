using UnityEngine;
using System.Collections;
using System;

namespace NumStrata.Utils
{
    /// <summary>
    /// Chức năng: Quản lý tập trung các hiệu ứng chuyển động, phóng to thu nhỏ cho UI.
    /// Giúp code ở các script logic sạch hơn, chỉ cần gọi hàm và truyền tham số.
    /// </summary>
    public class UIEffectManager : MonoBehaviour
    {
        public static UIEffectManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Di chuyển mượt mà một RectTransform đến vị trí anchoredPosition đích.
        /// </summary>
        public void MoveTo(RectTransform rect, Vector2 targetAnchoredPos, float duration, Action onComplete = null)
        {
            if (rect == null) return;
            StartCoroutine(MoveRoutine(rect, targetAnchoredPos, duration, onComplete));
        }

        private IEnumerator MoveRoutine(RectTransform rect, Vector2 endPos, float duration, Action onComplete)
        {
            Vector2 startPos = rect.anchoredPosition;
            float elapsed = 0;
            while (elapsed < duration)
            {
                if (rect == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Công thức EaseOut Quat (nhanh lúc đầu, chậm dần về cuối)
                t = 1 - Mathf.Pow(1 - t, 2); 
                
                rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                yield return null;
            }
            if (rect != null) rect.anchoredPosition = endPos;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Hiệu ứng phóng to (Scale Up) từ 0 đến size mặc định.
        /// </summary>
        public void ScaleUp(Transform target, float duration, float startScale = 0f, Action onComplete = null)
        {
            if (target == null) return;
            StartCoroutine(ScaleRoutine(target, Vector3.one, startScale, duration, onComplete));
        }

        /// <summary>
        /// Hiệu ứng thu nhỏ (Scale Down) về 0.
        /// </summary>
        public void ScaleDown(Transform target, float duration, Action onComplete = null)
        {
            if (target == null) return;
            StartCoroutine(ScaleRoutine(target, Vector3.zero, target.localScale.x, duration, onComplete));
        }

        private IEnumerator ScaleRoutine(Transform target, Vector3 endScale, float startS, float duration, Action onComplete)
        {
            target.localScale = Vector3.one * startS;
            float elapsed = 0;
            while (elapsed < duration)
            {
                if (target == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Sử dụng Lerp đơn giản hoặc Ease cho Scale
                target.localScale = Vector3.Lerp(Vector3.one * startS, endScale, t);
                yield return null;
            }
            if (target != null) target.localScale = endScale;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Hiệu ứng rung (Shake) khi bấm sai hoặc bị khóa.
/// </summary>
        public void Shake(RectTransform rect, float duration, float strength)
        {
            if (rect == null) return;
            StartCoroutine(ShakeRoutine(rect, duration, strength));
        }

        private IEnumerator ShakeRoutine(RectTransform rect, float duration, float strength)
        {
            Vector2 originalPos = rect.anchoredPosition;
            float elapsed = 0;
            while (elapsed < duration)
            {
                if (rect == null) yield break;
                elapsed += Time.deltaTime;
                float randomOffset = UnityEngine.Random.Range(-strength, strength);
                rect.anchoredPosition = originalPos + new Vector2(randomOffset, 0);
                yield return null;
            }
            if (rect != null) rect.anchoredPosition = originalPos;
        }
    }
}
