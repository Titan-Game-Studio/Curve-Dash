using UnityEngine;

namespace STG.CurveDash
{
    public enum EquipmentSlot
    {
        Head,
        Body,
        Hands,
        Feet,
        MainHand,
        OffHand,
        Amulet,
        Ring1,
        Ring2,
        Belt
    }

    [CreateAssetMenu(fileName = "New Armor", menuName = "Curve-Dash/Items/Armor Data")]
    public class ArmorItemData : ItemData
    {
        public EquipmentSlot Slot;
        public int ModularPartIndex; // Index for the modular mesh part
        public string MeshPartName; // Exact child object name in GanzSe modular character
        
        [Header("Stats")]
        public int Defense;
        public int HealthBonus;

        [Header("Sockets")]
        [Tooltip("Active abilities or support gems socketed in this armor piece.")]
        public System.Collections.Generic.List<AbilityData> Abilities = new System.Collections.Generic.List<AbilityData>();

        public int MaxSockets
        {
            get
            {
                switch (Slot)
                {
                    case EquipmentSlot.Body: return 6;
                    case EquipmentSlot.Head: return 4;
                    case EquipmentSlot.Hands: return 4;
                    case EquipmentSlot.Feet: return 4;
                    default: return 0;
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            int max = MaxSockets;
            if (Abilities != null && Abilities.Count > max)
            {
                Abilities.RemoveRange(max, Abilities.Count - max);
            }
        }
#endif
        
        public ArmorItemData()
        {
            Type = ItemType.Armor;
        }

        public override System.Collections.Generic.List<StatModifier> GetStatModifiers()
        {
            var list = base.GetStatModifiers();
            if (Defense != 0)     list.Add(new StatModifier(StatType.AddedArmour, Defense));
            if (HealthBonus != 0) list.Add(new StatModifier(StatType.AddedLife, HealthBonus));
            return list;
        }

        public override System.Collections.Generic.List<ItemTag> GetAffixTags()
            => new System.Collections.Generic.List<ItemTag> { ItemTag.Armour };
    }
}

