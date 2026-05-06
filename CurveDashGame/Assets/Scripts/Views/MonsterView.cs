using UnityEngine;
using Zenject;
using System.Collections.Generic;

namespace STG.CurveDash.Views
{
    public class MonsterView : MonoBehaviour
    {
        [SerializeField] private Transform _modelContainer;
        private RangeCircleVisualizer _rangeVisualizer;
        private GameObject _currentModel;

        private float _damageFlashTimer;
        private float _blinkTimer;
        private bool _isWhite;
        private Material _whiteMaterial;
        private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

        private void Update()
        {
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
                rangeObj.transform.localPosition = new Vector3(0, 1.2f, 0); // slightly above ground
                _rangeVisualizer = rangeObj.AddComponent<RangeCircleVisualizer>();
                _rangeVisualizer.SetColor(Color.red);
            }
            if (data != null)
            {
                _rangeVisualizer.SetRadius(data.AttackRange);
            }
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


