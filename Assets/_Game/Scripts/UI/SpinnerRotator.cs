using UnityEngine;

namespace NumStrata.UI
{
    public class SpinnerRotator : MonoBehaviour
    {
        [Tooltip("Degrees to rotate per second. Negative values rotate clockwise.")]
        public float rotationSpeed = -180f;

        private void Update()
        {
            transform.Rotate(0f, 0f, rotationSpeed * Time.unscaledDeltaTime);
        }
    }
}
