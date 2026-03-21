using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    [RequireComponent(typeof(Rigidbody))]
    public class BlockPartView : MonoBehaviour
    {
        [Inject] private AssetManager _assetManager;
        [Inject] private DataManager _dataManager;
        [Inject] private ShopService _shopService;
        
        private GameObject _currentSkin;
        private Color _currentColor = Color.white;
        private int _skinIndex = -1;

        private void OnEnable()
        {
            if (_shopService != null) _shopService.OnBlockPartSkinEquipped += UpdateSkin;
        }

        private void OnDisable()
        {
            if (_shopService != null) _shopService.OnBlockPartSkinEquipped -= UpdateSkin;
        }

        private void Start()
        {
            UpdateSkin(_dataManager.UserData.CurrentBlockPartSkin);
        }

        public void UpdateSkin(int skinIndex)
        {
            if (_skinIndex == skinIndex && _currentSkin != null) return;
            _skinIndex = skinIndex;

            if (_currentSkin != null) Destroy(_currentSkin);

            if (_assetManager.BlockPartSkinCount > 0)
            {
                _assetManager.LoadBlockPartSkinAsync(skinIndex, prefab => {
                    if (this == null) return;
                    if (_currentSkin != null) Destroy(_currentSkin);
                    _currentSkin = Instantiate(prefab, transform);
                    ApplyColor(_currentColor);
                });
            }
            else
            {
                ApplyColor(_currentColor);
            }
        }

        public void SetColor(Color color)
        {
            _currentColor = color;
            ApplyColor(color);
        }

        private void ApplyColor(Color color)
        {
            if (_currentSkin != null)
            {
                var renderers = _currentSkin.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers) {
                    if (r.material.HasProperty("_Color")) 
                        r.material.color = color;
                }
            }
            else 
            {
                var r = GetComponent<Renderer>();
                if (r != null && r.material.HasProperty("_Color")) 
                    r.material.color = color;
            }
        }
    }
    
    public class BlockPartViewFactory : PlaceholderFactory<BlockPartView>
    {
    }
}