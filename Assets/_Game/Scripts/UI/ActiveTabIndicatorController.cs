using System.Collections;
using UnityEngine;

namespace NumStrata.UI
{
    public class ActiveTabIndicatorController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform indicator;
        [SerializeField] private RectTransform indicatorRoot;
        [SerializeField] private RectTransform[] tabs;

        [Header("Animation")]
        [SerializeField] private float moveDuration = 0.2f;
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool matchTabWidth = true;

        private Coroutine moveRoutine;

        public void SelectTab(int index)
        {
            if (indicator == null || tabs == null || tabs.Length == 0)
            {
                return;
            }

            if (index < 0 || index >= tabs.Length || tabs[index] == null)
            {
                return;
            }

            if (indicatorRoot == null)
            {
                indicatorRoot = indicator.parent as RectTransform;
            }

            if (indicatorRoot == null)
            {
                return;
            }

            if (indicator.parent != indicatorRoot)
            {
                indicator.SetParent(indicatorRoot, worldPositionStays: true);
            }

            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
            }

            moveRoutine = StartCoroutine(AnimateToTab(tabs[index]));
        }

        private IEnumerator AnimateToTab(RectTransform target)
        {
            Vector2 startPos = indicator.anchoredPosition;
            float startWidth = indicator.rect.width;

            Vector2 targetPos = GetTargetAnchoredPosition(target);
            float targetWidth = matchTabWidth ? target.rect.width : startWidth;

            if (moveDuration <= 0f)
            {
                indicator.anchoredPosition = targetPos;
                if (matchTabWidth)
                {
                    indicator.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
                }
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < moveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / moveDuration);
                float eased = ease != null ? ease.Evaluate(t) : t;

                indicator.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, eased);

                if (matchTabWidth)
                {
                    float width = Mathf.LerpUnclamped(startWidth, targetWidth, eased);
                    indicator.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                }

                yield return null;
            }

            indicator.anchoredPosition = targetPos;
            if (matchTabWidth)
            {
                indicator.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
            }
        }

        private Vector2 GetTargetAnchoredPosition(RectTransform target)
        {
            Vector3 worldCenter = target.TransformPoint(target.rect.center);
            Vector3 local = indicatorRoot.InverseTransformPoint(worldCenter);
            return new Vector2(local.x, indicator.anchoredPosition.y);
        }
    }
}
