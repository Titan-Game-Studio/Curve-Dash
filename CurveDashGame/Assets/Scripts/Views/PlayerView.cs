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
        [Inject] private GameSettings _gameSettings;
        
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
        
        private float _damageFlashTimer;


        private Animator _characterAnimator;
        private Animator _mountAnimator;
        private bool _isRunning;
        private RuntimeAnimatorController _originalCharacterController;
        
        private EquippableData _leftHandItem;   // Tay Trái: GreatSword, Hammer, Bow, Shield
        private EquippableData _rightHandItem;  // Tay Phải: Sword, Arrow
        private GameObject _rightWeaponObj;
        private GameObject _leftWeaponObj;
        private ModularCharacterView _modularView;
        private RangeCircleVisualizer _rangeVisualizer;

        public EquippableData LeftHandItem => _leftHandItem;
        public EquippableData RightHandItem => _rightHandItem;
        public WeaponInstance CurrentWeaponInstance { get; set; }

        /// <summary>
        /// Retrieves all abilities socketed inside equipped armor pieces (Helmet, Chest, Gloves, Boots)
        /// using the DataManager's UserData and AssetManager's catalog.
        /// </summary>
        public List<AbilityData> GetEquippedArmorAbilities()
        {
            var list = new List<AbilityData>();
            if (_dataManager == null || _assetManager == null || _dataManager.UserData == null || _dataManager.UserData.EquippedItems == null) 
                return list;

            var slotsToCheck = new List<EquipmentSlot> { EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Hands, EquipmentSlot.Feet };
            foreach (var slot in slotsToCheck)
            {
                if (_dataManager.UserData.EquippedItems.TryGetValue(slot, out string itemId))
                {
                    var item = _assetManager.GetItem(itemId);
                    if (item is ArmorItemData armor && armor.Abilities != null)
                    {
                        foreach (var ab in armor.Abilities)
                        {
                            if (ab != null && !list.Contains(ab))
                            {
                                list.Add(ab);
                            }
                        }
                    }
                }
            }
            return list;
        }

        public float MovementSpeed { get; set; } = 5f;
        public float AttackSpeed { get; set; } = 1f;

        private void Start()
        {
            Init();
            
            // Create simulator/game-view range visualizer for player
            var rangeObj = new GameObject("PlayerRangeVisualizer");
            rangeObj.transform.SetParent(transform, false);
            rangeObj.transform.localPosition = new Vector3(0, 0.05f, 0); // slightly above ground
            _rangeVisualizer = rangeObj.AddComponent<RangeCircleVisualizer>();
            _rangeVisualizer.SetColor(Color.cyan);
            UpdateRangeVisualizer();
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
            // Logic bất tử cũ (nếu muốn mờ đi thay vì chớp trắng)
            else if (_isInvincible)
            {
                // Có thể thêm hiệu ứng mờ alpha ở đây nếu muốn
            }

            // Sync animator speeds based on active states
            UpdateAnimatorSpeeds();

            // Toggle range debug visualization on simulator (runs in built games too)
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current[UnityEngine.InputSystem.Key.F3].wasPressedThisFrame)
            {
                RangeCircleVisualizer.IsDebugEnabled = !RangeCircleVisualizer.IsDebugEnabled;
                Debug.Log($"[Debug] Range circle debug visualization toggled: {RangeCircleVisualizer.IsDebugEnabled}");
            }
        }



        private void Init()
        {
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

            if (_modularView != null)
            {
                _modularView.OnAttackHitEvent -= OnAnimationHitTriggered;
            }
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

        #region SMART EQUIPMENT SYSTEM (Rule Based)

        public void Equip(EquippableData item)
        {
            if (item == null) return;

            // QUY TẮC 1: VŨ KHÍ 2 TAY (GreatSword, Hammer)
            if (item is TwoHandedWeaponData twoHanded)
            {
                _leftHandItem = twoHanded;
                _rightHandItem = null; // Khóa/Xóa tay phải
            }
            // QUY TẮC 2: CUNG (Great Bow)
            else if (item is BowData bow)
            {
                _leftHandItem = bow;
                _rightHandItem = bow.DefaultArrow; // Tự động đeo tên
            }
            // QUY TẮC 3: KIẾM 1 TAY (Sword)
            else if (item is OneHandedWeaponData sword)
            {
                // Nếu tay phải đang cầm Cung hoặc 2 tay, thì xóa đi để cầm kiếm
                if (_leftHandItem is TwoHandedWeaponData || _leftHandItem is BowData)
                {
                    _leftHandItem = null;
                }

                // Nếu tay phải chưa có kiếm, hoặc đang cầm thứ khác (như Arrow lẻ)
                if (!(_rightHandItem is OneHandedWeaponData))
                {
                    _rightHandItem = sword;
                }
                else
                {
                    // Nếu tay phải đã có kiếm, thì lắp vào tay trái (Song kiếm)
                    _leftHandItem = sword;
                }
            }
            // QUY TẮC 4: ĐỒ PHỤ (Shield, Arrow)
            else if (item is OffHandData offHand)
            {
                if (offHand.SubType == OffHandType.Shield)
                {
                    // Khiên luôn vào tay trái
                    _leftHandItem = offHand;
                    // Nếu đang cầm vũ khí 2 tay thì phải bỏ đi
                    if (_leftHandItem is TwoHandedWeaponData || _leftHandItem is BowData) _rightHandItem = null;
                }
                else if (offHand.SubType == OffHandType.Arrow)
                {
                    // Tên luôn vào tay phải
                    _rightHandItem = offHand;
                }
            }

            RefreshWeaponVisuals();
            RefreshAnimator();
            UpdateRangeVisualizer();
        }

        private void UpdateRangeVisualizer()
        {
            if (_rangeVisualizer == null) return;
            float range = 2f; // Default
            if (_rightHandItem is WeaponData weapon) range = weapon.BaseAttackRange;
            else if (_leftHandItem is WeaponData weapon2) range = weapon2.BaseAttackRange;
            _rangeVisualizer.SetRadius(range);
        }


        private void RefreshWeaponVisuals()
        {
            if (_rightWeaponObj != null) Destroy(_rightWeaponObj);
            if (_leftWeaponObj != null) Destroy(_leftWeaponObj);

            var rSlot = _rightHandSlot != null ? _rightHandSlot : transform;
            var lSlot = _leftHandSlot != null ? _leftHandSlot : transform;

            // Instantiate tay trái (User's priority: GreatSword, Hammer, Bow, Shield)
            if (_leftHandItem != null && _leftHandItem.VisualModel != null)
            {
                _leftWeaponObj = Instantiate(_leftHandItem.VisualModel, lSlot);

                _leftWeaponObj.transform.localPosition = _leftHandItem.PositionOffset;
                _leftWeaponObj.transform.localRotation = Quaternion.Euler(_leftHandItem.RotationOffset);
                _leftWeaponObj.transform.localScale = Vector3.one;
            }

            // Instantiate tay phải (One-Handed Sword, Arrows)
            if (_rightHandItem != null && _rightHandItem.VisualModel != null)
            {
                _rightWeaponObj = Instantiate(_rightHandItem.VisualModel, rSlot);

                _rightWeaponObj.transform.localPosition = _rightHandItem.PositionOffset;
                _rightWeaponObj.transform.localRotation = Quaternion.Euler(_rightHandItem.RotationOffset);
                _rightWeaponObj.transform.localScale = Vector3.one;
            }
        }

        private void RefreshAnimator()
        {
            if (_characterAnimator == null) return;

            RuntimeAnimatorController targetController = _originalCharacterController;

            // Logic chọn Hoạt ảnh dựa trên Rule
            
            // 1. ƯU TIÊN VŨ KHÍ 2 TAY HOẶC CUNG (Luôn ở tay trái theo Rule)
            if (_leftHandItem is TwoHandedWeaponData || _leftHandItem is BowData)
            {
                if (_leftHandItem.MainAnimator != null) targetController = _leftHandItem.MainAnimator;
            }
            // 2. ƯU TIÊN KIẾM 1 TAY (Vũ khí chính ở tay phải)
            else if (_rightHandItem is OneHandedWeaponData sword)
            {
                if (_leftHandItem == null)
                {
                    targetController = sword.MainAnimator;
                }
                else if (_leftHandItem is OneHandedWeaponData)
                {
                    // Song kiếm
                    if (sword.DualWieldController != null) targetController = sword.DualWieldController;
                }
                else if (_leftHandItem is OffHandData offHand && offHand.SubType == OffHandType.Shield)
                {
                    // Kiếm + Khiên
                    if (sword.SwordShieldController != null) targetController = sword.SwordShieldController;
                }
                else
                {
                    // Trường hợp khác (ví dụ cầm kiếm + item lạ)
                    targetController = sword.MainAnimator;
                }
            }
            // 3. TRƯỜNG HỢP CHỈ CẦM KHIÊN
            else if (_leftHandItem is OffHandData oh && oh.SubType == OffHandType.Shield)
            {
                if (oh.MainAnimator != null) targetController = oh.MainAnimator;
            }


            if (_characterAnimator.runtimeAnimatorController != targetController)
            {
                _characterAnimator.runtimeAnimatorController = targetController;
                
                // Sau khi đổi Controller, gán lại các biến
                _characterAnimator.SetBool("IsRunning", _isRunning);

                // Ép nhảy vào State phù hợp nếu nó tồn tại trong Controller mới
                string stateName = _isRunning ? "Run" : "Idle";
                if (_characterAnimator.HasState(0, Animator.StringToHash(stateName)))
                {
                    _characterAnimator.Play(stateName, 0, 0f);
                }
                
                // Cập nhật ngay lập tức
                _characterAnimator.Update(0f);
            }



        }

        #endregion

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
            if (_modularView != null)
            {
                _modularView.OnAttackHitEvent -= OnAnimationHitTriggered;
            }

            if (_cachedCharacters.TryGetValue(_characterId, out var characterObj) && characterObj != null)
            {
                _modularView = characterObj.GetComponent<ModularCharacterView>();
                if (_modularView != null)
                {
                    _modularView.OnAttackHitEvent += OnAnimationHitTriggered;
                }
                // Tìm Animator trên chính object nhân vật trước, tránh tìm nhầm vào vũ khí con
                _characterAnimator = characterObj.GetComponent<Animator>();
                if (_characterAnimator == null) 
                    _characterAnimator = characterObj.GetComponentInChildren<Animator>();

                
                if (_originalCharacterController == null && _characterAnimator != null)
                {
                    _originalCharacterController = _characterAnimator.runtimeAnimatorController;
                }

                if (_modularView != null)
                {
                    _rightHandSlot = _modularView.RightHandSlot;
                    _leftHandSlot = _modularView.LeftHandSlot;
                }
                
                RefreshWeaponVisuals();
                RefreshAnimator();
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
            if (_skinContainerTransform != null) renderers.AddRange(_skinContainerTransform.GetComponentsInChildren<Renderer>());
            if (_characterContainerTransform != null) renderers.AddRange(_characterContainerTransform.GetComponentsInChildren<Renderer>());

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

        public void SetInvincible(bool isInvincible)
        {
            _isInvincible = isInvincible;
            if (!isInvincible && _damageFlashTimer <= 0)
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

        private Action _onAttackImpactCallback;
        private Coroutine _attackDelayCoroutine;

        public void TriggerAttack(Action onImpact, float attackSpeed, bool useAnimationEvent, float attackHitDelay, string animationTrigger = "Attack")
        {
            if (_attackDelayCoroutine != null)
            {
                StopCoroutine(_attackDelayCoroutine);
                _attackDelayCoroutine = null;
            }

            _onAttackImpactCallback = onImpact;

            // Trigger the attack animation
            PlayAttack(animationTrigger);

            if (useAnimationEvent)
            {
                // Start a safety timeout (e.g. 1.5s / attackSpeed) to guarantee the attack triggers even if the animation event fails
                float safetyTimeout = (attackHitDelay > 0 ? attackHitDelay * 2.0f : 1.0f) / attackSpeed;
                _attackDelayCoroutine = StartCoroutine(CoSafetyTimeout(safetyTimeout));
            }
            else
            {
                // Start standard delay timer
                float delay = attackHitDelay / attackSpeed;
                _attackDelayCoroutine = StartCoroutine(CoAttackDelay(delay));
            }
        }

        private void TriggerImpact()
        {
            if (_attackDelayCoroutine != null)
            {
                StopCoroutine(_attackDelayCoroutine);
                _attackDelayCoroutine = null;
            }

            if (_onAttackImpactCallback != null)
            {
                var callback = _onAttackImpactCallback;
                _onAttackImpactCallback = null; // Clear to prevent double triggering
                callback.Invoke();
            }
        }

        private void OnAnimationHitTriggered()
        {
            TriggerImpact();
        }

        private System.Collections.IEnumerator CoAttackDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            TriggerImpact();
        }

        private System.Collections.IEnumerator CoSafetyTimeout(float timeout)
        {
            yield return new WaitForSeconds(timeout);
            TriggerImpact();
        }

        public void PlayAttack(string triggerName = "Attack")
        {
            if (_characterAnimator == null) return;

            for (int i = 0; i < _characterAnimator.layerCount; i++)
            {
                var stateInfo = _characterAnimator.GetCurrentAnimatorStateInfo(i);
                if (stateInfo.IsTag("Attack")) return;
            }

            _characterAnimator.SetTrigger(triggerName);
        }

        private void UpdateAnimatorSpeeds()
        {
            if (_characterAnimator == null) return;

            bool isAttacking = false;
            for (int i = 0; i < _characterAnimator.layerCount; i++)
            {
                var stateInfo = _characterAnimator.GetCurrentAnimatorStateInfo(i);
                if (stateInfo.IsTag("Attack"))
                {
                    isAttacking = true;
                    break;
                }
            }

            if (isAttacking)
            {
                // Attack animation speed scales with the attack speed
                _characterAnimator.speed = AttackSpeed;
                
                // Mount doesn't attack, run it at normal speed (or idle)
                if (_mountAnimator != null) _mountAnimator.speed = 1f;
            }
            else if (_isRunning)
            {
                // Run animation speed scales with the movement speed
                // Normalize by baseline movement speed (e.g. BallInitialSpeed)
                float baseSpeed = _gameSettings != null ? _gameSettings.BallInitialSpeed : 5f;
                if (baseSpeed <= 0f) baseSpeed = 5f;
                
                float runSpeedFactor = MovementSpeed / baseSpeed;
                _characterAnimator.speed = runSpeedFactor;
                
                if (_mountAnimator != null) _mountAnimator.speed = runSpeedFactor;
            }
            else
            {
                // Idle or other states
                _characterAnimator.speed = 1f;
                if (_mountAnimator != null) _mountAnimator.speed = 1f;
            }
        }

        #region DEBUG GIZMOS

        private void OnDrawGizmos()
        {
            float range = 2f; // Default
            if (_rightHandItem is WeaponData weapon) range = weapon.BaseAttackRange;
            else if (_leftHandItem is WeaponData weapon2) range = weapon2.BaseAttackRange;


            // 1. Vẽ tầm đánh
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, range);

            // 2. Tìm và vẽ quái vật gần đó (Tối ưu: Chỉ tìm trong bán kính rộng hơn tầm đánh chút)
            Gizmos.color = Color.yellow;
            var colliders = Physics.OverlapSphere(transform.position, range * 2f);
            int count = 0;
            foreach (var col in colliders)
            {
                if (count > 5) break; // Giới hạn số lượng line để không bị loạn
                if (col.CompareTag("Enemy") || col.gameObject.name.Contains("Monster"))
                {
                    Gizmos.DrawLine(transform.position, col.transform.position);
                    count++;
                }
            }

        }

        #endregion
    }

    public class PlayerViewFactory : PlaceholderFactory<PlayerView> { }
}

