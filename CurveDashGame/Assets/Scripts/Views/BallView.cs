using System;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

namespace STG.CurveDash
{
    [RequireComponent(typeof(Rigidbody))]
    public class BallView : MonoBehaviour
    {
        [Inject] private AssetManager _assetManager;
        [SerializeField] private Transform _skinContainerTransform;
        [SerializeField] private Transform _vfxContainerTransform;
        [SerializeField] private Transform _characterContainerTransform;
        
        private int _skinIndex = 0;
        private int _vfxIndex = 0;
        private int _characterIndex = 0;

        private bool _isInvincible;
        private float _blinkTimer;
        private bool _isWhite;
        private Material _whiteMaterial;
        private System.Collections.Generic.Dictionary<Renderer, Material[]> _originalMaterials = new System.Collections.Generic.Dictionary<Renderer, Material[]>();

        private void Start()
        {
            Init();
        }

        private void Update()
        {
            if (_isInvincible)
            {
                _blinkTimer += Time.deltaTime;
                if (_blinkTimer > 0.025f)
                {
                    _blinkTimer = 0f;
                    _isWhite = !_isWhite;
                    ToggleWhiteMaterials(_isWhite);
                }
            }
        }

        private void Init()
        {
            ResetAll();
            _assetManager.LoadBallSkinAsync(_skinIndex, prefab => { Instantiate(prefab, _skinContainerTransform); });
            _assetManager.LoadVFXAsync(_vfxIndex,vfx => { Instantiate(vfx,  _vfxContainerTransform); });
            _assetManager.LoadCharacterAsync(_characterIndex,character => { Instantiate(character, _characterContainerTransform); });
        }

        private void ResetAll()
        {
            DestroyAllChildren(_skinContainerTransform);
            DestroyAllChildren(_vfxContainerTransform);
            DestroyAllChildren(_characterContainerTransform);
        }

        private void UpdateSkin(int skinIndex = 0)
        {
            if (skinIndex == _skinIndex)
                return;
            _skinIndex = skinIndex;
            DestroyAllChildren(_skinContainerTransform);
            _assetManager.LoadBallSkinAsync(_skinIndex, prefab => { Instantiate(prefab, _skinContainerTransform); });
        }

        private void DestroyAllChildren(Transform inTransform)
        {
            foreach (Transform child in inTransform)
            {
                if (child !=  null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        public void SetInvincible(bool isInvincible)
        {
            if (_isInvincible == isInvincible) return;
            
            _isInvincible = isInvincible;
            
            if (isInvincible)
            {
                if (_whiteMaterial == null)
                {
                    _whiteMaterial = new Material(Shader.Find("Unlit/Color"));
                    _whiteMaterial.color = Color.white;
                }
                
                _originalMaterials.Clear();
                
                var renderers = new System.Collections.Generic.List<Renderer>();
                if (_skinContainerTransform != null)
                    renderers.AddRange(_skinContainerTransform.GetComponentsInChildren<Renderer>());
                if (_characterContainerTransform != null)
                    renderers.AddRange(_characterContainerTransform.GetComponentsInChildren<Renderer>());

                foreach (var r in renderers)
                {
                    if (r is ParticleSystemRenderer) continue;
                    _originalMaterials[r] = r.sharedMaterials;
                }
                
                _blinkTimer = 0;
                _isWhite = true;
                ToggleWhiteMaterials(true);
            }
            else
            {
                RestoreOriginalMaterials();
            }
        }

        private void ToggleWhiteMaterials(bool useWhite)
        {
            foreach (var kvp in _originalMaterials)
            {
                if (kvp.Key == null) continue;
                
                if (useWhite)
                {
                    var whiteMats = new Material[kvp.Value.Length];
                    for (int i = 0; i < whiteMats.Length; i++) whiteMats[i] = _whiteMaterial;
                    kvp.Key.sharedMaterials = whiteMats;
                }
                else
                {
                    kvp.Key.sharedMaterials = kvp.Value;
                }
            }
        }

        private void RestoreOriginalMaterials()
        {
            foreach (var kvp in _originalMaterials)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.sharedMaterials = kvp.Value;
                }
            }
            _originalMaterials.Clear();
        }
    }

    public class BallViewFactory : PlaceholderFactory<BallView>
    {
    }
}