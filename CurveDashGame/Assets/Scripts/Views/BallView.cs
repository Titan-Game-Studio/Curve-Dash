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

        private void Start()
        {
            Init();
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
    }

    public class BallViewFactory : PlaceholderFactory<BallView>
    {
    }
}