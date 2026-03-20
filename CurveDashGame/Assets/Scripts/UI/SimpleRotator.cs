using UnityEngine;

namespace STG.CurveDash
{
    public class SimpleRotator : MonoBehaviour
    {
        [Tooltip("Degrees per second to rotate around the global Y axis.")]
        public float rotationSpeed = 90f;

        private void Update()
        {
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.World);
        }
    }
}
