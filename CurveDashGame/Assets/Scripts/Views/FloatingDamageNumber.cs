using UnityEngine;

namespace STG.CurveDash.Views
{
    // Lightweight world-space floating damage number. Spawns at a position, drifts upward while
    // fading out, billboards toward the camera, then self-destroys. Used for both enemy and player hits.
    public class FloatingDamageNumber : MonoBehaviour
    {
        private const float Lifetime = 0.8f;

        private TextMesh _textMesh;
        private float _age;
        private Vector3 _velocity;
        private Color _baseColor;
        private Camera _camera;

        private static Font _font;

        public static void Spawn(Vector3 worldPos, float amount, Color color, bool crit = false)
        {
            var go = new GameObject("DamageNumber");
            go.transform.position = worldPos;
            var fdn = go.AddComponent<FloatingDamageNumber>();
            fdn.Init(Mathf.RoundToInt(amount).ToString(), color, crit);
        }

        private void Init(string text, Color color, bool crit)
        {
            if (_font == null)
            {
                // Unity 6 ships the legacy dynamic font under this name (Arial.ttf was removed).
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            _textMesh = gameObject.AddComponent<TextMesh>();
            _textMesh.text = crit ? text + "!" : text;
            _textMesh.font = _font;
            _textMesh.fontSize = 64;
            _textMesh.characterSize = crit ? 0.14f : 0.10f;
            _textMesh.fontStyle = crit ? FontStyle.Bold : FontStyle.Normal;
            _textMesh.anchor = TextAnchor.MiddleCenter;
            _textMesh.alignment = TextAlignment.Center;
            _textMesh.color = color; // per-vertex color; the GUI/Text shader fades with alpha
            _baseColor = color;

            // TextMesh renders via the font's material; make sure it draws on top of other transparents.
            var mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 5000;

            float xDrift = Random.Range(-0.6f, 0.6f);
            _velocity = new Vector3(xDrift, 2.5f, 0f);
            _camera = Camera.main;
        }

        private void Update()
        {
            _age += Time.deltaTime;

            transform.position += _velocity * Time.deltaTime;
            _velocity.y -= 4f * Time.deltaTime; // ease the upward motion to a stop

            if (_camera == null) _camera = Camera.main;
            if (_camera != null) transform.rotation = _camera.transform.rotation;

            // Fade out over the second half of the lifetime.
            if (_textMesh != null)
            {
                float t = _age / Lifetime;
                Color c = _baseColor;
                c.a = Mathf.Clamp01(1f - Mathf.Max(0f, (t - 0.4f) / 0.6f));
                _textMesh.color = c;
            }

            if (_age >= Lifetime) Destroy(gameObject);
        }
    }
}
