using UnityEngine;

namespace NumStrata.Utils
{
    public static class Mathfs
    {
        /// <summary>
        /// Easing function that starts fast, overshoot, and then settles back.
        /// </summary>
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
