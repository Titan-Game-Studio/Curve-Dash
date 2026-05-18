using System.Linq;
using DevionGames.InventorySystem;
using DevionGames.UIWidgets;

namespace STG.CurveDash
{
    public class PlayerInventoryScanner : IInventoryScanner
    {
        private ItemContainer _inventoryContainer;
        private readonly AssetManager _assetManager;

        public PlayerInventoryScanner(AssetManager assetManager)
        {
            _assetManager = assetManager;
        }

        private ItemContainer GetInventory()
        {
            if (_inventoryContainer == null)
            {
                _inventoryContainer = WidgetUtility.Find<ItemContainer>("Inventory");
            }
            return _inventoryContainer;
        }

        public OffHandData FindFirstArrow()
        {
            return FindFirst<OffHandData>(data => data.SubType == OffHandType.Arrow);
        }

        public OffHandData FindFirstShield()
        {
            return FindFirst<OffHandData>(data => data.SubType == OffHandType.Shield);
        }

        public OneHandedWeaponData FindFirstOneHandedWeapon()
        {
            return FindFirst<OneHandedWeaponData>(data => true);
        }

        public BowData FindFirstBow()
        {
            return FindFirst<BowData>(data => true);
        }

        private T FindFirst<T>(System.Func<T, bool> predicate) where T : EquippableData
        {
            var inv = GetInventory();
            if (inv == null) return null;

            var items = inv.GetItems<DevionGames.InventorySystem.Item>();
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.Name)) continue;
                var data = _assetManager.GetItem(item.Name);
                if (data is T typedData && predicate(typedData))
                {
                    return typedData;
                }
            }
            return null;
        }
    }
}
