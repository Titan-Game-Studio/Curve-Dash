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

        [Header("Universal Stat Modifiers")]
        [Tooltip("Data-driven stats granted by this item while equipped. Add any StatType here instead of " +
                 "creating new typed fields — they are routed to the character sheet via AffixStatMapper. " +
                 "Works on every item type (weapon, armor, off-hand, accessory, flask).")]
        public System.Collections.Generic.List<StatModifier> Modifiers = new System.Collections.Generic.List<StatModifier>();

        [Header("Devion Inventory Integration")]
        public DevionGames.InventorySystem.Item DevionAdapter;

        /// <summary>
        /// Every character-sheet stat this item grants, as data. The base returns the universal
        /// <see cref="Modifiers"/> list; each item subclass overrides this to also project its own
        /// typed stat fields (Defense, HealthBonus, …) into the same vocabulary. The equipment adapter
        /// runs this single list through AffixStatMapper — so there are no per-type stat branches anymore.
        /// </summary>
        public virtual System.Collections.Generic.List<StatModifier> GetStatModifiers()
        {
            var list = new System.Collections.Generic.List<StatModifier>();
            if (Modifiers != null) list.AddRange(Modifiers);
            return list;
        }
    }
}
