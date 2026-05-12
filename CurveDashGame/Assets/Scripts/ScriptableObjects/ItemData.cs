using UnityEngine;

namespace STG.CurveDash
{
    public enum ItemRarity
    {
        Normal,
        Magic,
        Rare,
        Unique
    }

    public enum ItemType
    {
        Weapon,
        Armor,
        Accessory,
        Flask
    }

    public abstract class ItemData : ScriptableObject
    {
        public string ItemName;
        public Sprite Icon;
        public ItemRarity Rarity;
        public ItemType Type;
        public UnityEngine.AddressableAssets.AssetReferenceGameObject Prefab;

        
        [TextArea]
        public string Description;

        [Header("Devion Inventory Integration")]
        public DevionGames.InventorySystem.Item DevionAdapter;
    }
}
