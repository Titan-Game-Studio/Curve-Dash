using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace STG.CurveDash
{
    public class AddressablesController
    {
        // Track loaded assets
        private readonly Dictionary<AssetReference, AsyncOperationHandle> assetHandles = new();

        // Track instantiated GameObjects
        private readonly Dictionary<GameObject, AsyncOperationHandle<GameObject>> instanceHandles = new();

        public void LoadAssetAsync<T>(AssetReference reference, Action<T> onLoaded)
        {
            if (assetHandles.TryGetValue(reference, out var existingHandle))
            {
                // ReSharper disable once MergeIntoPattern
                if (existingHandle.IsDone && existingHandle.Result is T result)
                {
                    onLoaded?.Invoke(result);
                    return;
                }
            }

            var handle = reference.LoadAssetAsync<T>();
            assetHandles[reference] = handle;

            handle.Completed += op =>
            {
                // ReSharper disable once MergeIntoPattern
                // ReSharper disable once ConvertTypeCheckPatternToNullCheck
                if (op.Status == AsyncOperationStatus.Succeeded && op.Result is T asset)
                {
                    onLoaded?.Invoke(asset);
                }
                else
                {
                    Debug.LogError($"[AddressablesController] Failed to load asset {reference.RuntimeKey}");
                }
            };
        }

        public void InstantiateAsync(AssetReference reference,
            Transform parent = null,
            Action<GameObject> onInstantiated = null)
        {
            var handle = reference.InstantiateAsync(parent);
            handle.Completed += op =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    var instance = op.Result;
                    instanceHandles[instance] = op;
                    onInstantiated?.Invoke(instance);
                }
                else
                {
                    Debug.LogError($"[AddressablesController] Failed to instantiate {reference.RuntimeKey}");
                }
            };
        }

        // Release a loaded asset
        public void UnloadAsset(AssetReference reference)
        {
            if (assetHandles.TryGetValue(reference, out var handle))
            {
                Addressables.Release(handle);
                assetHandles.Remove(reference);
            }
        }

        // Release a specific instance
        public void ReleaseInstance(GameObject instance)
        {
            if (instance != null && instanceHandles.TryGetValue(instance, out var handle))
            {
                Addressables.ReleaseInstance(handle);
                instanceHandles.Remove(instance);
            }
            else
            {
                Debug.LogWarning("[AddressablesController] Attempted to release untracked instance");
            }
        }

        // Release everything safely
        public void ReleaseAll()
        {
            foreach (var handle in assetHandles.Values)
                Addressables.Release(handle);

            foreach (var handle in instanceHandles.Values)
                Addressables.ReleaseInstance(handle);

            assetHandles.Clear();
            instanceHandles.Clear();
        }
    }
}