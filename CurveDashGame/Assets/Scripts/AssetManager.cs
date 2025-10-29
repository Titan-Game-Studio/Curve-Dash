using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace STG.CurveDash
{
    public class AssetManager
    {
        private readonly AddressablesController _controller;
        private readonly AssetCatalog _catalog;

        public AssetManager(AssetCatalog catalog, AddressablesController controller)
        {
            _catalog = catalog;
            _controller = controller;
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
            _controller.LoadAssetAsync<GameObject>(reference, onLoaded);
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

        public void ReleaseAll()
        {
            _controller.ReleaseAll();
        }
    }
}