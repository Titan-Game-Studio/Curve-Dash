using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

namespace STG.CurveDash
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerView : MonoBehaviour
    {
        [Inject] private AssetManager _assetManager;
        [Inject] private DataManager _dataManager;
        [Inject] private ShopService _shopService;
        
        [SerializeField] private Transform _skinContainerTransform;
        [SerializeField] private Transform _auraContainerTransform;
        [SerializeField] private Transform _characterContainerTransform;
        
        private Transform _rightHandSlot;
        private Transform _leftHandSlot;
        
        private int _skinIndex = -1;
        private int _auraIndex = -1;
        private string _characterId = "";

        private CancellationTokenSource _skinCts;
        private CancellationTokenSource _auraCts;
        private CancellationTokenSource _characterCts;

        private readonly Dictionary<int, GameObject> _cachedSkins = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _cachedAuras = new Dictionary<int, GameObject>();
        private readonly Dictionary<string, GameObject> _cachedCharacters = new Dictionary<string, GameObject>();

        private bool _isInvincible;
        private float _blinkTimer;
        private bool _isWhite;
        private Material _whiteMaterial;
        private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

        private Animator _characterAnimator;
        private Animator _mountAnimator;
        private bool _isRunning;
        private RuntimeAnimatorController _originalCharacterController;
        private WeaponData _currentWeaponData;
        private ModularCharacterView _modularView;

        private void Start()
        {
            Init();
        }

        private void OnEnable()
        {
            if (_shopService != null)
            {
                _shopService.OnMountSkinEquipped += UpdateSkin;
                _shopService.OnVFXEquipped += UpdateAura;
                _shopService.OnCharacterEquipped += UpdateCharacter;
            }
        }

        private void OnDisable()
        {
            if (_shopService != null)
            {
                _shopService.OnMountSkinEquipped -= UpdateSkin;
                _shopService.OnVFXEquipped -= UpdateAura;
                _shopService.OnCharacterEquipped -= UpdateCharacter;
            }
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
            // Load character bằng ID thay vì Index
            UpdateCharacter(_dataManager.UserData.CurrentCharacterId);
        }

        private void OnDestroy()
        {
            _skinCts?.Cancel(); 
            _skinCts?.Dispose();
            _auraCts?.Cancel(); 
            _auraCts?.Dispose();
            _characterCts?.Cancel(); 
            _characterCts?.Dispose();
        }

        public void UpdateSkin(int index)
        {
            if (_skinIndex == index) return;
            _skinIndex = index;
            
            LoadOrEnableAsset(index, _skinContainerTransform, _cachedSkins, _assetManager.LoadMountSkin, ref _skinCts, OnVisualLoaded);
        }

        public void UpdateAura(int index)
        {
            if (_auraIndex == index) return;
            _auraIndex = index;
            
            LoadOrEnableAsset(index, _auraContainerTransform, _cachedAuras, _assetManager.LoadVFX, ref _auraCts, OnVisualLoaded);
        }

        public void UpdateCharacter(string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || _characterId == characterId) return;
            _characterId = characterId;

            // Ẩn tất cả nhân vật cũ
            foreach (var kvp in _cachedCharacters)
            {
                if (kvp.Value != null) kvp.Value.SetActive(false);
            }

            if (_cachedCharacters.TryGetValue(characterId, out var existingObj) && existingObj != null)
            {
                existingObj.SetActive(true);
                OnVisualLoaded();
                return;
            }

            _characterCts?.Cancel();
            _characterCts?.Dispose();
            _characterCts = new CancellationTokenSource();
            var token = _characterCts.Token;

            _assetManager.LoadCharacter(characterId, prefab => 
            {
                if (this == null || token.IsCancellationRequested) return;
                
                var obj = Instantiate(prefab, _characterContainerTransform);
                _cachedCharacters[characterId] = obj;
                OnVisualLoaded();
            });
        }

        public void EquipWeapon(WeaponData data)
        {
            _currentWeaponData = data;
            
            if (_rightWeaponObj != null) Destroy(_rightWeaponObj);
            if (_leftWeaponObj != null) Destroy(_leftWeaponObj);

            if (data != null)
            {
                // Swap animator controller based on weapon
                if (_characterAnimator != null && data.AnimatorController != null)
                {
                    _characterAnimator.runtimeAnimatorController = data.AnimatorController;
                    _characterAnimator.SetBool("IsRunning", _isRunning);
                }
                else if (_characterAnimator != null)
                {
                    _characterAnimator.runtimeAnimatorController = _originalCharacterController;
                    _characterAnimator.SetBool("IsRunning", _isRunning);
                }

                var rContainer = _rightHandSlot != null ? _rightHandSlot : transform;
                var lContainer = _leftHandSlot != null ? _leftHandSlot : transform;

                // Trang bị tay phải nếu có
                if (data.RightHandModel != null)
                {
                    _rightWeaponObj = Instantiate(data.RightHandModel, rContainer);
                    _rightWeaponObj.transform.localPosition = data.RightHandPositionOffset;
                    _rightWeaponObj.transform.localRotation = Quaternion.Euler(data.RightHandRotationOffset);
                    _rightWeaponObj.transform.localScale = Vector3.one;
                }
                
                // Trang bị tay trái nếu có 
                if (data.LeftHandModel != null)
                {
                    _leftWeaponObj = Instantiate(data.LeftHandModel, lContainer);
                    _leftWeaponObj.transform.localPosition = data.LeftHandPositionOffset;
                    _leftWeaponObj.transform.localRotation = Quaternion.Euler(data.LeftHandRotationOffset);
                    _leftWeaponObj.transform.localScale = Vector3.one;
                }
            }
            else
            {
                if (_characterAnimator != null)
                {
                    _characterAnimator.runtimeAnimatorController = _originalCharacterController;
                    _characterAnimator.SetBool("IsRunning", _isRunning);
                }
            }
        }

        private GameObject _rightWeaponObj;
        private GameObject _leftWeaponObj;

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
            if (_cachedCharacters.TryGetValue(_characterId, out var characterObj) && characterObj != null)
            {
                _modularView = characterObj.GetComponent<ModularCharacterView>();
                _characterAnimator = characterObj.GetComponentInChildren<Animator>();
                
                // Lưu lại Controller mặc định nếu chưa có
                if (_originalCharacterController == null && _characterAnimator != null)
                {
                    _originalCharacterController = _characterAnimator.runtimeAnimatorController;
                }

                if (_modularView != null)
                {
                    _rightHandSlot = _modularView.RightHandSlot;
                    _leftHandSlot = _modularView.LeftHandSlot;
                }
                
                if (_currentWeaponData != null)
                {
                    EquipWeapon(_currentWeaponData);
                }
            }

            if (_cachedSkins.TryGetValue(_skinIndex, out var mountObj) && mountObj != null)
            {
                _mountAnimator = mountObj.GetComponentInChildren<Animator>();
            }
            else
            {
                _mountAnimator = null;
            }

            SetRunning(_isRunning);

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
                if (_skinContainerTransform != null) renderers.AddRange(_skinContainerTransform.GetComponentsInChildren<Renderer>());
                if (_characterContainerTransform != null) renderers.AddRange(_characterContainerTransform.GetComponentsInChildren<Renderer>());

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

        public void SetRunning(bool isRunning)
        {
            _isRunning = isRunning;
            if (_characterAnimator != null) _characterAnimator.SetBool("IsRunning", isRunning);
            if (_mountAnimator != null) _mountAnimator.SetBool("IsRunning", isRunning);
        }

        public void PlayAttack()
        {
            if (_characterAnimator == null) return;

            // Kiểm tra Tag "Attack" trên tất cả các Layer để tránh spam
            for (int i = 0; i < _characterAnimator.layerCount; i++)
            {
                var stateInfo = _characterAnimator.GetCurrentAnimatorStateInfo(i);
                if (stateInfo.IsTag("Attack")) return;
            }

            _characterAnimator.SetTrigger("Attack");
        }
    }

    public class PlayerViewFactory : PlaceholderFactory<PlayerView> { }
}



