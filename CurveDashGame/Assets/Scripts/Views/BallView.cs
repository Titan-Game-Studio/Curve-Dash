using System;
using System.Collections.Generic;
using System.Threading;
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
        
        private int _skinIndex = -1;
        private int _vfxIndex = -1;
        private int _characterIndex = -1;

        private CancellationTokenSource _skinCts;
        private CancellationTokenSource _vfxCts;
        private CancellationTokenSource _characterCts;

        private readonly Dictionary<int, GameObject> _cachedSkins = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _cachedVfx = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _cachedCharacters = new Dictionary<int, GameObject>();

        private bool _isInvincible;
        private float _blinkTimer;
        private bool _isWhite;
        private Material _whiteMaterial;
        private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

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
            UpdateSkin(0);
            UpdateVFX(0);
            UpdateCharacter(0);
        }

        private void OnDestroy()
        {
            _skinCts?.Cancel(); 
            _skinCts?.Dispose();
            _vfxCts?.Cancel(); 
            _vfxCts?.Dispose();
            _characterCts?.Cancel(); 
            _characterCts?.Dispose();
        }

        public void UpdateSkin(int skinIndex)
        {
            if (skinIndex == _skinIndex) return;
            _skinIndex = skinIndex;
            LoadOrEnableAsset(_skinIndex, _skinContainerTransform, _cachedSkins, _assetManager.LoadBallSkinAsync, ref _skinCts, OnVisualLoaded);
        }

        public void UpdateVFX(int vfxIndex)
        {
            if (vfxIndex == _vfxIndex) return;
            _vfxIndex = vfxIndex;
            LoadOrEnableAsset(_vfxIndex, _vfxContainerTransform, _cachedVfx, _assetManager.LoadVFXAsync, ref _vfxCts, null);
        }

        public void UpdateCharacter(int characterIndex)
        {
            if (characterIndex == _characterIndex) return;
            _characterIndex = characterIndex;
            LoadOrEnableAsset(_characterIndex, _characterContainerTransform, _cachedCharacters, _assetManager.LoadCharacterAsync, ref _characterCts, OnVisualLoaded);
        }

        private void LoadOrEnableAsset(
            int index, 
            Transform container, 
            Dictionary<int, GameObject> cache, 
            Action<int, Action<GameObject>> loadFunc,
            ref CancellationTokenSource cts,
            Action onLoadedCallback)
        {
            foreach (var kvp in cache)
            {
                if (kvp.Value != null) kvp.Value.SetActive(false);
            }

            if (cache.TryGetValue(index, out var existingObj) && existingObj != null)
            {
                existingObj.SetActive(true);
                onLoadedCallback?.Invoke();
                return;
            }

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            loadFunc(index, prefab => 
            {
                if (this == null || token.IsCancellationRequested) return;
                
                var obj = Instantiate(prefab, container);
                cache[index] = obj;
                onLoadedCallback?.Invoke();
            });
        }

        private void OnVisualLoaded()
        {
            if (_isInvincible)
            {
                RestoreOriginalMaterials();
                _isInvincible = false;
                SetInvincible(true);
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
                
                var renderers = new List<Renderer>();
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