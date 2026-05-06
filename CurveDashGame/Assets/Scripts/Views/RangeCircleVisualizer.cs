using UnityEngine;

namespace STG.CurveDash
{
    [RequireComponent(typeof(LineRenderer))]
    public class RangeCircleVisualizer : MonoBehaviour
    {
        public static bool IsDebugEnabled = true; // Enabled by default so user can see it on simulator

        [SerializeField] private float _radius = 2f;
        [SerializeField] private int _segments = 36;
        [SerializeField] private Color _color = Color.green;

        private LineRenderer _lineRenderer;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.positionCount = _segments + 1;
            _lineRenderer.useWorldSpace = false;
            _lineRenderer.startWidth = 0.04f;
            _lineRenderer.endWidth = 0.04f;
            _lineRenderer.loop = true;

            // Simple unlit material for line renderer
            Shader defaultLineShader = Shader.Find("Sprites/Default");
            if (defaultLineShader != null)
            {
                _lineRenderer.material = new Material(defaultLineShader);
            }
            
            _lineRenderer.startColor = _color;
            _lineRenderer.endColor = _color;

            CreateCircle();
        }

        private void Update()
        {
            _lineRenderer.enabled = IsDebugEnabled;
            if (IsDebugEnabled)
            {
                // Align flat with the ground plane (X/Z plane)
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        public void SetRadius(float radius)
        {
            _radius = radius;
            CreateCircle();
        }

        public void SetColor(Color color)
        {
            _color = color;
            if (_lineRenderer != null)
            {
                _lineRenderer.startColor = _color;
                _lineRenderer.endColor = _color;
            }
        }

        private void CreateCircle()
        {
            if (_lineRenderer == null) return;

            float deltaTheta = (2f * Mathf.PI) / _segments;
            float theta = 0f;

            for (int i = 0; i < _segments + 1; i++)
            {
                float x = _radius * Mathf.Cos(theta);
                float y = _radius * Mathf.Sin(theta);

                // Since rotated 90 deg around X, local coords will map perfectly flat parallel to the ground
                _lineRenderer.SetPosition(i, new Vector3(x, y, 0f));
                theta += deltaTheta;
            }
        }
    }
}
