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
        private readonly AssetCatalog _catalog;

        private readonly Dictionary<AssetReferenceGameObject, GameObject> _assetCache = new();

        private readonly Dictionary<AssetReferenceGameObject, List<Action<GameObject>>> _pendingLoadCallbacks = new();
        
        private AsyncOperationHandle _initHandle;

        public AssetManager(AssetCatalog catalog, AddressablesController controller)
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

        public void LoadBallSkinAsync(int index, Action<GameObject> onLoaded)
        {
            LoadAsync(_catalog.BallSkins, index, onLoaded);
        }

        public void LoadVFXAsync(int index, Action<GameObject> onLoaded)
        {
            LoadAsync(_catalog.VFXs, index, onLoaded);
        }

        public void LoadCharacterAsync(int index, Action<GameObject> onLoaded)
        {
            LoadAsync(_catalog.Characters, index, onLoaded);
        }

        public void LoadObstacleAsync(int index, Action<GameObject> onLoaded)
        {
            LoadAsync(_catalog.Obstacles, index, onLoaded);
        }

        public void LoadCloudAsync(int index, Action<GameObject> onLoaded)
        {
            LoadAsync(_catalog.Clouds, index, onLoaded);
        }
        
        public int ObstacleCount => _catalog.Obstacles?.Count ?? 0;
        public int CloudCount => _catalog.Clouds?.Count ?? 0;
        
        public void LoadAudioAsync(AudioKey key, Action<AudioClip> onLoaded)
        {
            var mapping = _catalog.AudioAssets.Find(m => m.Key == key);
    
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

        public void InstantiateVFX(int index, Transform parent = null, Action<GameObject> onSpawned = null)
        {
            if (!IsValid(_catalog.VFXs, index)) return;

            var reference = _catalog.VFXs[index];
            _controller.InstantiateAsync(reference, parent, onSpawned);
        }

        #endregion

        private void LoadAsync(List<AssetReferenceGameObject> list, int index, Action<GameObject> onLoaded)
        {
            if (!IsValid(list, index)) return;

            var reference = list[index];

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

        private bool IsValid(List<AssetReferenceGameObject> list, int index)
        {
            if (index < 0 || index >= list.Count)
            {
                Debug.LogError($"[AssetManager] Invalid index {index}");
                return false;
            }

            return true;
        }

        // Release a loaded asset
        public void UnloadAsset(List<AssetReferenceGameObject> list, int index)
        {
            if (!IsValid(list, index)) return;

            var reference = list[index];

            _assetCache.Remove(reference);
            _pendingLoadCallbacks.Remove(reference);

            _controller.UnloadAsset(reference);
        }

        public void ReleaseAll()
        {
            _assetCache.Clear();
            _pendingLoadCallbacks.Clear();

            _controller.ReleaseAll();
        }
    }
}