using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace STG.CurveDash.UI
{
    public enum ShopTab
    {
        Character,
        Skin,
        Trail,
        Tile
    }

    public class ShopUI : MonoBehaviour
    {
        [Inject] private ShopService _shopService;
        [Inject] private DataManager _dataManager;
        [Inject] private AssetManager _assetManager;
        [Inject] private TGS.Ads.IAdService _adService;

        [Header("Tabs")]
        public Button CharacterTabButton;
        public Button SkinTabButton;
        public Button TrailTabButton;
        public Button TileTabButton;

        [Header("Grid")]
        public Transform ContentContainer;
        public ShopItemElement ItemPrefab;

        [Header("Info & Controls")]
        public Text TotalCoinsText;
        public Button CloseButton;
        public Button WatchAdButton;

        private ShopTab _currentTab = ShopTab.Skin;
        private List<ShopItemElement> _spawnedItems = new List<ShopItemElement>();

        // We will default cost to 100 for now, or you can expand this logic
        private const int DefaultCost = 100;

        private void Start()
        {
            if (CharacterTabButton) CharacterTabButton.onClick.AddListener(() => SwitchTab(ShopTab.Character));
            if (SkinTabButton) SkinTabButton.onClick.AddListener(() => SwitchTab(ShopTab.Skin));
            if (TrailTabButton) TrailTabButton.onClick.AddListener(() => SwitchTab(ShopTab.Trail));
            if (TileTabButton) TileTabButton.onClick.AddListener(() => SwitchTab(ShopTab.Tile));
            if (CloseButton) CloseButton.onClick.AddListener(CloseShop);
            if (WatchAdButton) WatchAdButton.onClick.AddListener(OnWatchAdClicked);

            SwitchTab(ShopTab.Skin); // Default tab
        }

        public void CloseShop()
        {
            gameObject.SetActive(false);
        }

        private void OnWatchAdClicked()
        {
            if (WatchAdButton) WatchAdButton.interactable = false;
            
            _adService.ShowRewarded(success =>
            {
                if (WatchAdButton) WatchAdButton.interactable = true;
                if (success)
                {
                    _dataManager.AddCoin(100);
                    RefreshUI();
                }
            });
        }

        private void OnEnable()
        {
            if (_dataManager != null) 
                RefreshUI();
        }

        public void SwitchTab(ShopTab tab)
        {
            _currentTab = tab;
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (TotalCoinsText != null) 
                TotalCoinsText.text = $"Coins: {_dataManager.UserData.TotalCoins}";

            // Clear old items
            foreach (var item in _spawnedItems)
            {
                Destroy(item.gameObject);
            }
            _spawnedItems.Clear();

            int itemCount = GetItemCountForCurrentTab();
            
            for (int i = 0; i < itemCount; i++)
            {
                var element = Instantiate(ItemPrefab, ContentContainer);
                element.gameObject.SetActive(true);
                
                bool isUnlocked = IsItemUnlocked(i);
                bool isEquipped = IsItemEquipped(i);
                
                var config = GetConfigForCurrentTab(i);
                string itemName = config != null ? config.Name : $"{_currentTab} {i + 1}";
                int itemPrice = config != null ? config.Price : DefaultCost;
                
                string statusOrPrice = isEquipped ? "Equipped" : (isUnlocked ? "Select" : $"{itemPrice} Coins");

                int index = i;
                element.Setup(index, itemName, statusOrPrice, isEquipped, OnItemClicked);
                
                LoadIconForCurrentTab(index, element);
                
                _spawnedItems.Add(element);
            }
        }

        private void LoadIconForCurrentTab(int index, ShopItemElement element)
        {
            System.Action<Sprite> onLoaded = sprite => {
                if (element != null) element.SetIcon(sprite);
            };

            switch (_currentTab)
            {
                case ShopTab.Character: _assetManager.LoadCharacterIconAsync(index, onLoaded); break;
                case ShopTab.Skin: _assetManager.LoadBallSkinIconAsync(index, onLoaded); break;
                case ShopTab.Trail: _assetManager.LoadVFXIconAsync(index, onLoaded); break;
                case ShopTab.Tile: _assetManager.LoadBlockPartSkinIconAsync(index, onLoaded); break;
            }
        }

        private void OnItemClicked(int index)
        {
            bool isUnlocked = IsItemUnlocked(index);
            
            if (!isUnlocked)
            {
                // Try buy
                var config = GetConfigForCurrentTab(index);
                int itemPrice = config != null ? config.Price : DefaultCost;
                
                bool success = TryBuyItem(index, itemPrice);
                if (success)
                {
                    EquipItem(index);
                }
                else
                {
                    Debug.Log("Not enough coins!");
                }
            }
            else
            {
                // Equip
                EquipItem(index);
            }
            
            RefreshUI(); // Update texts/buttons
        }

        #region Wrapper Methods

        private int GetItemCountForCurrentTab()
        {
            return _currentTab switch
            {
                ShopTab.Character => _assetManager.CharacterCount,
                ShopTab.Skin => _assetManager.BallSkinCount,
                ShopTab.Trail => _assetManager.VFXCount,
                ShopTab.Tile => _assetManager.BlockPartSkinCount,
                _ => 0
            };
        }

        private ShopItemConfig GetConfigForCurrentTab(int index)
        {
            return _currentTab switch
            {
                ShopTab.Character => _assetManager.GetCharacterConfig(index),
                ShopTab.Skin => _assetManager.GetBallSkinConfig(index),
                ShopTab.Trail => _assetManager.GetVFXConfig(index),
                ShopTab.Tile => _assetManager.GetBlockPartSkinConfig(index),
                _ => null
            };
        }

        private bool IsItemUnlocked(int index)
        {
            return _currentTab switch
            {
                ShopTab.Character => _shopService.IsCharacterUnlocked(index),
                ShopTab.Skin => _shopService.IsBallSkinUnlocked(index),
                ShopTab.Trail => _shopService.IsVFXUnlocked(index),
                ShopTab.Tile => _shopService.IsBlockPartSkinUnlocked(index),
                _ => false
            };
        }

        private bool IsItemEquipped(int index)
        {
            return _currentTab switch
            {
                ShopTab.Character => _dataManager.UserData.CurrentCharacter == index,
                ShopTab.Skin => _dataManager.UserData.CurrentBallSkin == index,
                ShopTab.Trail => _dataManager.UserData.CurrentVFX == index,
                ShopTab.Tile => _dataManager.UserData.CurrentBlockPartSkin == index,
                _ => false
            };
        }

        private bool TryBuyItem(int index, int cost)
        {
            return _currentTab switch
            {
                ShopTab.Character => _shopService.TryUnlockCharacter(index, cost),
                ShopTab.Skin => _shopService.TryUnlockBallSkin(index, cost),
                ShopTab.Trail => _shopService.TryUnlockVFX(index, cost),
                ShopTab.Tile => _shopService.TryUnlockBlockPartSkin(index, cost),
                _ => false
            };
        }

        private void EquipItem(int index)
        {
            switch (_currentTab)
            {
                case ShopTab.Character: _shopService.EquipCharacter(index); break;
                case ShopTab.Skin: _shopService.EquipBallSkin(index); break;
                case ShopTab.Trail: _shopService.EquipVFX(index); break;
                case ShopTab.Tile: _shopService.EquipBlockPartSkin(index); break;
            }
        }

        #endregion
    }
}
