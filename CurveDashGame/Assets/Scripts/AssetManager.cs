using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace STG.CurveDash
{
    public class AssetManager
    {
        private readonly AddressablesController _controller;
        private readonly GameAssetCatalog _catalog;

        private readonly Dictionary<AssetReferenceGameObject, GameObject> _assetCache = new();

        private readonly Dictionary<AssetReferenceGameObject, List<Action<GameObject>>> _pendingLoadCallbacks = new();
        
        private readonly Dictionary<object, object> _spriteCache = new();
        private readonly Dictionary<object, List<Action<Sprite>>> _pendingSpriteLoadCallbacks = new();
        
        private AsyncOperationHandle _initHandle;

        public AssetManager(GameAssetCatalog catalog, AddressablesController controller)
        {
            _catalog = catalog;
            _controller = controller;
        }

        public void InitializeAddressables()
        {
            _initHandle = Addressables.InitializeAsync();
        }

        public bool IsReady()
        {
            return _initHandle.IsValid() && _initHandle.IsDone;
        }

        #region LOAD_ASSET_ASYNC

        public void LoadMountSkinAsync(int index, Action<GameObject> onLoaded)
        {
            if (index >= 0 && index < MountSkinCount) LoadGameObjectRefAsync(_catalog.MountSkins.Items[index].Prefab, onLoaded);
        }

        public void LoadBlockPartSkinAsync(int index, Action<GameObject> onLoaded)
        {
            if (index >= 0 && index < BlockPartSkinCount) LoadGameObjectRefAsync(_catalog.BlockPartSkins.Items[index].Prefab, onLoaded);
        }

        public void LoadAuraAsync(int index, Action<GameObject> onLoaded)
        {
            if (index >= 0 && index < AuraCount) LoadGameObjectRefAsync(_catalog.Auras.Items[index].Prefab, onLoaded);
        }

        public void LoadCharacterAsync(int index, Action<GameObject> onLoaded)
        {
            if (index >= 0 && index < CharacterCount) LoadGameObjectRefAsync(_catalog.Characters.Items[index].Prefab, onLoaded);
        }

        public void LoadObstacleAsync(int index, Action<GameObject> onLoaded)
        {
            LoadGameObjectAsync(_catalog.Obstacles.References, index, onLoaded);
        }

        public void LoadCloudAsync(int index, Action<GameObject> onLoaded)
        {
            LoadGameObjectAsync(_catalog.Clouds.References, index, onLoaded);
        }
        
        public void LoadMountSkinIconAsync(int index, Action<Sprite> onLoaded) => LoadSpriteRefAsync((index >= 0 && index < MountSkinCount) ? _catalog.MountSkins.Items[index].Icon : null, onLoaded);
        public void LoadBlockPartSkinIconAsync(int index, Action<Sprite> onLoaded) => LoadSpriteRefAsync((index >= 0 && index < BlockPartSkinCount) ? _catalog.BlockPartSkins.Items[index].Icon : null, onLoaded);
        public void LoadAuraIconAsync(int index, Action<Sprite> onLoaded) => LoadSpriteRefAsync((index >= 0 && index < AuraCount) ? _catalog.Auras.Items[index].Icon : null, onLoaded);
        public void LoadCharacterIconAsync(int index, Action<Sprite> onLoaded) => LoadSpriteRefAsync((index >= 0 && index < CharacterCount) ? _catalog.Characters.Items[index].Icon : null, onLoaded);
        
        public ShopItemConfig GetMountSkinConfig(int index) => (index >= 0 && index < MountSkinCount) ? _catalog.MountSkins.Items[index] : null;
        public ShopItemConfig GetBlockPartSkinConfig(int index) => (index >= 0 && index < BlockPartSkinCount) ? _catalog.BlockPartSkins.Items[index] : null;
        public ShopItemConfig GetAuraConfig(int index) => (index >= 0 && index < AuraCount) ? _catalog.Auras.Items[index] : null;
        public ShopItemConfig GetCharacterConfig(int index) => (index >= 0 && index < CharacterCount) ? _catalog.Characters.Items[index] : null;
        
        public int MountSkinCount => _catalog.MountSkins?.Items?.Count ?? 0;
        public int AuraCount => _catalog.Auras?.Items?.Count ?? 0;
        public int CharacterCount => _catalog.Characters?.Items?.Count ?? 0;
        public int ObstacleCount => _catalog.Obstacles?.References?.Count ?? 0;
        public int CloudCount => _catalog.Clouds?.References?.Count ?? 0;
        public int BlockPartSkinCount => _catalog.BlockPartSkins?.Items?.Count ?? 0;
        
        public void LoadAudioAsync(AudioKey key, Action<AudioClip> onLoaded)
        {
            var mapping = _catalog.AudioCatalog.AudioAssets.Find(m => m.Key == key);
    
            if (mapping.Reference == null || !mapping.Reference.RuntimeKeyIsValid())
            {
                Debug.LogError($"[AssetManager] AudioKey {key} not found or invalid in Catalog");
                return;
            }

            _controller.LoadAssetAsync<AudioClip>(mapping.Reference, clip =>
            {
                onLoaded?.Invoke(clip);
            });
        }

        #endregion

        #region INSTANTIATE_ASYNC

        public void InstantiateAura(int index, Transform parent = null, Action<GameObject> onSpawned = null)
        {
            if (index < 0 || index >= AuraCount) return;

            var reference = _catalog.Auras.Items[index].Prefab;
            _controller.InstantiateAsync(reference, parent, onSpawned);
        }

        public void InstantiateMountSkin(int index, Transform parent = null, Action<GameObject> onSpawned = null)
        {
            if (index < 0 || index >= MountSkinCount) return;

            var reference = _catalog.MountSkins.Items[index].Prefab;
            _controller.InstantiateAsync(reference, parent, onSpawned);
        }

        #endregion

        private void LoadGameObjectAsync(List<AssetReferenceGameObject> list, int index, Action<GameObject> onLoaded)
        {
            if (list == null || index < 0 || index >= list.Count) return;
            LoadGameObjectRefAsync(list[index], onLoaded);
        }

        private void LoadGameObjectRefAsync(AssetReferenceGameObject reference, Action<GameObject> onLoaded)
        {
            if (reference == null || !reference.RuntimeKeyIsValid()) return;

            if (_assetCache.TryGetValue(reference, out var cached) && cached != null)
            {
                onLoaded?.Invoke(cached);
                return;
            }

            if (_pendingLoadCallbacks.TryGetValue(reference, out var pending))
            {
                if (onLoaded != null) pending.Add(onLoaded);
                return;
            }

            _pendingLoadCallbacks[reference] = new List<Action<GameObject>>();
            if (onLoaded != null) _pendingLoadCallbacks[reference].Add(onLoaded);

            _controller.LoadAssetAsync<GameObject>(reference, asset =>
            {
                _assetCache[reference] = asset;

                if (_pendingLoadCallbacks.TryGetValue(reference, out var callbacks))
                {
                    foreach (var cb in callbacks)
                        cb?.Invoke(asset);
                    _pendingLoadCallbacks.Remove(reference);
                }
            });
        }
        
        private void LoadSpriteRefAsync(AssetReferenceT<Sprite> reference, Action<Sprite> onLoaded)
        {
            if (reference == null || !reference.RuntimeKeyIsValid()) return;

            if (_spriteCache.TryGetValue(reference, out var cached) && cached != null)
            {
                onLoaded?.Invoke((Sprite)cached);
                return;
            }

            if (_pendingSpriteLoadCallbacks.TryGetValue(reference, out var pending))
            {
                if (onLoaded != null) pending.Add(onLoaded);
                return;
            }

            _pendingSpriteLoadCallbacks[reference] = new List<Action<Sprite>>();
            if (onLoaded != null) _pendingSpriteLoadCallbacks[reference].Add(onLoaded);

            _controller.LoadAssetAsync<Sprite>(reference, asset =>
            {
                _spriteCache[reference] = asset;

                if (_pendingSpriteLoadCallbacks.TryGetValue(reference, out var callbacks))
                {
                    foreach (var cb in callbacks)
                        cb?.Invoke(asset);
                    _pendingSpriteLoadCallbacks.Remove(reference);
                }
            });
        }

        public void UnloadAsset(AssetReferenceGameObject reference)
        {
            if (reference == null) return;

            _assetCache.Remove(reference);
            _pendingLoadCallbacks.Remove(reference);

            _controller.UnloadAsset(reference);
        }

        public void ReleaseAll()
        {
            _assetCache.Clear();
            _pendingLoadCallbacks.Clear();
            _spriteCache.Clear();
            _pendingSpriteLoadCallbacks.Clear();

            _controller.ReleaseAll();
        }
    }
}

