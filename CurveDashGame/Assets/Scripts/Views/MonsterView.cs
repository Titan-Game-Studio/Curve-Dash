using UnityEngine;
using Zenject;
using System.Collections.Generic;

namespace STG.CurveDash.Views
{
    public class MonsterView : MonoBehaviour
    {
        [SerializeField] private Transform _modelContainer;
        [Header("Health Bar")]
        [SerializeField] private float _healthBarHeight = 2.2f; // local Y offset above the monster
        [SerializeField] private float _healthBarWidth = 1.0f;
        [SerializeField] private float _healthBarThickness = 0.14f;
        private RangeCircleVisualizer _rangeVisualizer;
        private GameObject _currentModel;

        private float _damageFlashTimer;
        private float _blinkTimer;
        private bool _isWhite;
        private Material _whiteMaterial;
        private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

        // World-space billboard health bar (built once, lazily)
        private Transform _healthBarRoot;
        private Transform _healthBarFillPivot;
        private Material _healthBarFillMaterial;
        private Camera _camera;

        private void Update()
        {
            // Billboard the health bar toward the camera so it is always readable.
            if (_healthBarRoot != null)
            {
                if (_camera == null) _camera = Camera.main;
                if (_camera != null)
                    _healthBarRoot.rotation = _camera.transform.rotation;
            }

            if (_damageFlashTimer > 0)
            {
                _damageFlashTimer -= Time.deltaTime;
                _blinkTimer += Time.deltaTime;
                if (_blinkTimer > 0.05f)
                {
                    _blinkTimer = 0f;
                    _isWhite = !_isWhite;
                    ToggleWhiteMaterials(_isWhite);
                }

                if (_damageFlashTimer <= 0)
                {
                    RestoreOriginalMaterials();
                    _isWhite = false;
                }
            }
        }

        public void FlashWhite(float duration)
        {
            if (_damageFlashTimer > 0) return; // Đang chớp rồi thì thôi
            
            _damageFlashTimer = duration;
            _blinkTimer = 0;
            _isWhite = true;
            
            PrepareOriginalMaterials();
            ToggleWhiteMaterials(true);
        }

        private void PrepareOriginalMaterials()
        {
            _originalMaterials.Clear();
            var renderers = new List<Renderer>();
            if (_modelContainer != null) renderers.AddRange(_modelContainer.GetComponentsInChildren<Renderer>());

            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) continue;
                _originalMaterials[r] = r.sharedMaterials;
            }

            if (_whiteMaterial == null)
            {
                _whiteMaterial = new Material(Shader.Find("Unlit/Color"));
                _whiteMaterial.color = Color.white;
            }
        }

        private void ToggleWhiteMaterials(bool useWhite)
        {
            foreach (var kvp in _originalMaterials)
            {
                if (kvp.Key == null) continue;
                if (useWhite)
                {
                    var mats = new Material[kvp.Value.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = _whiteMaterial;
                    kvp.Key.sharedMaterials = mats;
                }
                else kvp.Key.sharedMaterials = kvp.Value;
            }
        }

        private void RestoreOriginalMaterials()
        {
            foreach (var kvp in _originalMaterials)
            {
                if (kvp.Key != null) kvp.Key.sharedMaterials = kvp.Value;
            }
            _originalMaterials.Clear();
        }

        public void Setup(MonsterData data)
        {
            // Xóa model cũ nếu có
            if (_currentModel != null)
            {
                Destroy(_currentModel);
            }

            if (data != null && data.Prefab != null)
            {
                _currentModel = Instantiate(data.Prefab, _modelContainer);
                _currentModel.transform.localPosition = Vector3.zero;
                _currentModel.transform.localRotation = Quaternion.identity;
            }

            // Create simulator/game-view range visualizer for monster
            if (_rangeVisualizer == null)
            {
                var rangeObj = new GameObject("MonsterRangeVisualizer");
                rangeObj.transform.SetParent(transform, false);
                rangeObj.transform.localPosition = new Vector3(0, 0.55f, 0); // slightly above ground
                _rangeVisualizer = rangeObj.AddComponent<RangeCircleVisualizer>();
                _rangeVisualizer.SetColor(Color.red);
            }
            if (data != null)
            {
                _rangeVisualizer.SetRadius(data.AttackRange);
            }

            // Build (lazily) and reset the health bar to full whenever this view is set up for a monster.
            EnsureHealthBar();
            SetHealth(1f);
        }

        // Lazily constructs a simple two-quad world-space health bar: a dark background and a
        // colored fill anchored to the left edge so it shrinks toward the right as HP drops.
        private void EnsureHealthBar()
        {
            if (_healthBarRoot != null) return;

            var rootObj = new GameObject("HealthBar");
            _healthBarRoot = rootObj.transform;
            _healthBarRoot.SetParent(transform, false);
            _healthBarRoot.localPosition = new Vector3(0f, _healthBarHeight, 0f);

            var barShader = Shader.Find("Unlit/Color");

            // Background (dark) quad, centered.
            var bg = BuildQuad("BG", _healthBarRoot, new Color(0.05f, 0.05f, 0.05f, 1f), barShader);
            bg.transform.localScale = new Vector3(_healthBarWidth, _healthBarThickness, 1f);
            bg.transform.localPosition = Vector3.zero;

            // Left-anchored pivot: positioned at the bar's left edge, scaled on X to size the fill.
            var pivotObj = new GameObject("FillPivot");
            _healthBarFillPivot = pivotObj.transform;
            _healthBarFillPivot.SetParent(_healthBarRoot, false);
            _healthBarFillPivot.localPosition = new Vector3(-_healthBarWidth * 0.5f, 0f, -0.001f); // slightly in front of bg
            _healthBarFillPivot.localScale = new Vector3(_healthBarWidth, 1f, 1f);

            // Fill quad: unit-width with its left edge sitting on the pivot, so pivot.localScale.x = HP fraction.
            var fill = BuildQuad("Fill", _healthBarFillPivot, Color.green, barShader);
            _healthBarFillMaterial = fill.GetComponent<Renderer>().material;
            fill.transform.localScale = new Vector3(1f, _healthBarThickness, 1f);
            fill.transform.localPosition = new Vector3(0.5f, 0f, 0f);
        }

        private static GameObject BuildQuad(string name, Transform parent, Color color, Shader shader)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col); // health bar must not interfere with physics/raycasts
            quad.transform.SetParent(parent, false);

            var mat = new Material(shader) { color = color };
            quad.GetComponent<Renderer>().material = mat;
            return quad;
        }

        // fraction in [0,1]; updates the fill width and color (green → yellow → red).
        public void SetHealth(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            EnsureHealthBar();

            var pivotScale = _healthBarFillPivot.localScale;
            pivotScale.x = _healthBarWidth * fraction;
            _healthBarFillPivot.localScale = pivotScale;

            if (_healthBarFillMaterial != null)
            {
                Color c = fraction > 0.5f
                    ? Color.Lerp(Color.yellow, Color.green, (fraction - 0.5f) * 2f)
                    : Color.Lerp(Color.red, Color.yellow, fraction * 2f);
                _healthBarFillMaterial.color = c;
            }

            // Hide the bar entirely at full health for a cleaner look; show once damaged.
            if (_healthBarRoot != null)
                _healthBarRoot.gameObject.SetActive(fraction < 0.999f);
        }

        // Convenience overload driven from the combat system using raw HP values.
        public void SetHealth(float current, float max)
        {
            SetHealth(max > 0f ? current / max : 0f);
        }


        public void OnDespawned()
        {
            _damageFlashTimer = 0f;
            RestoreOriginalMaterials();
            _isWhite = false;

            if (_currentModel != null)
            {
                Destroy(_currentModel);
                _currentModel = null;
            }
        }
    }

    public class MonsterViewPool : MonoMemoryPool<MonsterView>
    {
        protected override void OnDespawned(MonsterView item)
        {
            item.OnDespawned();
            base.OnDespawned(item);
        }
    }

}


