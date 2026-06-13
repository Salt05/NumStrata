using UnityEngine;
using System.Collections;
using System.Collections.Generic;
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
                elapsed += Time.unscaledDeltaTime;
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
        /// Phóng to hoặc thu nhỏ đến một giá trị Scale cụ thể.
        /// </summary>
        public void ScaleTo(Transform target, Vector3 targetScale, float duration, Action onComplete = null)
        {
            if (target == null) return;
            float currentS = target.localScale.x;
            StartCoroutine(ScaleRoutine(target, targetScale, currentS, duration, onComplete));
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
            Vector3 startScale = Vector3.one * startS;
            float elapsed = 0;
            while (elapsed < duration)
            {
                if (target == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                
                // Công thức EaseOut Sine cho mượt
                t = Mathf.Sin(t * Mathf.PI * 0.5f);
                
                target.localScale = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }
            if (target != null) target.localScale = endScale;
            onComplete?.Invoke();
        }

        // --- Hệ thống Hover & Float ---
        private System.Collections.Generic.Dictionary<Transform, Coroutine> activeFloats = new System.Collections.Generic.Dictionary<Transform, Coroutine>();
        private System.Collections.Generic.Dictionary<Transform, Vector2> originalPositions = new System.Collections.Generic.Dictionary<Transform, Vector2>();

        /// <summary>
        /// Bắt đầu hiệu ứng lơ lửng cho một Transform.
        /// </summary>
        public void StartFloating(RectTransform rect, float amplitude = 10f, float speed = 5f)
        {
            if (rect == null) return;
            
            // Chỉ lưu vị trí gốc một lần duy nhất khi bắt đầu bay
            if (!originalPositions.ContainsKey(rect))
            {
                originalPositions[rect] = rect.anchoredPosition;
            }

            // Dừng coroutine cũ nếu đang chạy (nhưng KHÔNG reset để tránh giật hình)
            if (activeFloats.TryGetValue(rect, out Coroutine routine))
            {
                if (routine != null) StopCoroutine(routine);
                activeFloats.Remove(rect);
            }

            activeFloats[rect] = StartCoroutine(FloatRoutine(rect, amplitude, speed));
        }

        /// <summary>
        /// Dừng hiệu ứng lơ lửng và đưa về vị trí ban đầu.
        /// </summary>
        public void StopFloating(RectTransform rect)
        {
            if (rect == null) return;

            // 1. Dừng ngay lập tức Coroutine đang chạy
            if (activeFloats.TryGetValue(rect, out Coroutine routine))
            {
                if (routine != null) StopCoroutine(routine);
                activeFloats.Remove(rect);
            }

            // 2. Reset cứng theo yêu cầu: Top, Bottom, Rotation Z = 0
            // Thực hiện ngoài block if originalPositions để đảm bảo luôn được thực thi
            rect.localEulerAngles = new Vector3(rect.localEulerAngles.x, rect.localEulerAngles.y, 0f); // Rotation Z -> 0
            rect.offsetMax = Vector2.zero; // Top -> 0, Right -> 0
            rect.offsetMin = Vector2.zero; // Bottom -> 0, Left -> 0

            // 3. Trả về anchoredPosition gốc nếu có lưu trữ
            if (originalPositions.TryGetValue(rect, out Vector2 basePos))
            {
                rect.anchoredPosition = basePos;
                originalPositions.Remove(rect);
            }
        }

        private IEnumerator FloatRoutine(RectTransform rect, float amplitude, float speed)
        {
            Vector2 basePos = rect.anchoredPosition;
            Quaternion baseRot = rect.localRotation;
            float elapsed = 0;
            while (true)
            {
                if (rect == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                
                // Hiệu ứng lơ lửng (Y) - Giảm biên độ xuống một nửa (amplitude mặc định 10f -> 5f)
                float yOffset = Mathf.Sin(elapsed * speed) * (amplitude * 0.5f);
                rect.anchoredPosition = basePos + new Vector2(0, yOffset);
                
                // Hiệu ứng xoay nhẹ (Z) - Giảm cường độ từ 5 độ xuống 2 độ cho nhẹ nhàng
                float zRot = Mathf.Sin(elapsed * speed * 0.8f) * 2f; 
                rect.localRotation = baseRot * Quaternion.Euler(0, 0, zRot);
                
                yield return null;
            }
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
            // Tần số rung (số càng lớn lắc càng nhanh, khoảng 30-40 là vừa cho hiệu ứng chậm mượt)
            float speed = 40f;

            while (elapsed < duration)
            {
                if (rect == null) yield break;
                elapsed += Time.unscaledDeltaTime;

                // Sử dụng hàm Sin để tạo chuyển động qua lại mượt mà
                // (elapsed / duration) dùng để giảm dần biên độ về 0 khi sắp hết thời gian
                float damping = 1.0f - (elapsed / duration);
                float offset = Mathf.Sin(elapsed * speed) * strength * damping;

                rect.anchoredPosition = originalPos + new Vector2(offset, 0);
                yield return null;
            }
            if (rect != null) rect.anchoredPosition = originalPos;
        }
    }
}
