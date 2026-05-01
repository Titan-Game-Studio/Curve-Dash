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
        private int _characterIndex = -1;

        private CancellationTokenSource _skinCts;
        private CancellationTokenSource _auraCts;
        private CancellationTokenSource _characterCts;

        private readonly Dictionary<int, GameObject> _cachedSkins = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _cachedAuras = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _cachedCharacters = new Dictionary<int, GameObject>();

        private bool _isInvincible;
        private float _blinkTimer;
        private bool _isWhite;
        private Material _whiteMaterial;
        private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

        private Animator _characterAnimator;
        private Animator _mountAnimator;
        private bool _isRunning;

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
            // Người chơi sẽ bắt đầu "trắng tay", chỉ có Character. 
            // Thú cưỡi và Aura phải nhặt trên đường.
            UpdateCharacter(_dataManager.UserData.CurrentCharacter);
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

        public void UpdateSkin(int skinIndex)
        {
            if (skinIndex == _skinIndex) return;
            _skinIndex = skinIndex;
            LoadOrEnableAsset(_skinIndex, _skinContainerTransform, _cachedSkins, _assetManager.LoadMountSkinAsync, ref _skinCts, OnVisualLoaded);
        }

        public void UpdateAura(int auraIndex)
        {
            if (auraIndex == _auraIndex) return;
            _auraIndex = auraIndex;
            LoadOrEnableAsset(_auraIndex, _auraContainerTransform, _cachedAuras, _assetManager.LoadAuraAsync, ref _auraCts, null);
        }

        public void UpdateCharacter(int characterIndex)
        {
            if (characterIndex == _characterIndex) return;
            _characterIndex = characterIndex;
            LoadOrEnableAsset(_characterIndex, _characterContainerTransform, _cachedCharacters, _assetManager.LoadCharacterAsync, ref _characterCts, OnVisualLoaded);
        }

        private GameObject _rightWeaponObj;
        private GameObject _leftWeaponObj;
        
        private WeaponData _currentWeaponData;

        public void EquipWeapon(WeaponData data)
        {
            _currentWeaponData = data;
            
            if (_rightWeaponObj != null) Destroy(_rightWeaponObj);
            if (_leftWeaponObj != null) Destroy(_leftWeaponObj);

            if (data != null)
            {
                var rContainer = _rightHandSlot != null ? _rightHandSlot : transform;
                var lContainer = _leftHandSlot != null ? _leftHandSlot : transform;

                // Trang bị tay phải nếu có
                if (data.RightHandModel != null)
                {
                    _rightWeaponObj = Instantiate(data.RightHandModel, rContainer);
                    _rightWeaponObj.transform.localPosition = Vector3.zero;
                    _rightWeaponObj.transform.localRotation = Quaternion.identity;
                }
                
                // Trang bị tay trái nếu có (Hỗ trợ 2 tay cho bất kỳ vũ khí nào miễn là có gán LeftHandModel)
                if (data.LeftHandModel != null)
                {
                    _leftWeaponObj = Instantiate(data.LeftHandModel, lContainer);
                    _leftWeaponObj.transform.localPosition = Vector3.zero;
                    _leftWeaponObj.transform.localRotation = Quaternion.identity;
                }
            }
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
            // Extract Bones and Animator from the currently active character
            if (_cachedCharacters.TryGetValue(_characterIndex, out var characterObj) && characterObj != null)
            {
                var bones = characterObj.GetComponent<CharacterBones>();
                if (bones != null)
                {
                    _rightHandSlot = bones.RightHandSlot;
                    _leftHandSlot = bones.LeftHandSlot;
                }
                
                _characterAnimator = characterObj.GetComponentInChildren<Animator>();
                
                // Re-equip the weapon on the new character's hands
                if (_currentWeaponData != null)
                {
                    EquipWeapon(_currentWeaponData);
                }
            }

            // Extract Animator from Mount
            if (_cachedSkins.TryGetValue(_skinIndex, out var mountObj) && mountObj != null)
            {
                _mountAnimator = mountObj.GetComponentInChildren<Animator>();
            }
            else
            {
                _mountAnimator = null;
            }

            // Sync animation state
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

        public void SetRunning(bool isRunning)
        {
            _isRunning = isRunning;
            
            if (_mountAnimator != null)
            {
                _mountAnimator.SetBool("IsRunning", isRunning);
                if (_characterAnimator != null)
                {
                    _characterAnimator.SetBool("IsRunning", false); // Tạm thời ngồi yên trên thú
                }
            }
            else
            {
                if (_characterAnimator != null)
                {
                    _characterAnimator.SetBool("IsRunning", isRunning);
                }
            }
        }

        public void PlayAttack()
        {
            if (_characterAnimator != null)
            {
                _characterAnimator.SetTrigger("Attack");
            }
        }
    }

    public class PlayerViewFactory : PlaceholderFactory<PlayerView>
    {
    }
}

