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

    [CreateAssetMenu(fileName = "New Armor", menuName = "Curve Dash/Items/Armor Data")]
    public class ArmorItemData : ItemData
    {
        public EquipmentSlot Slot;
        public int ModularPartIndex; // Index for the modular mesh part
        
        [Header("Stats")]
        public int Defense;
        public int HealthBonus;
        
        public ArmorItemData()
        {
            Type = ItemType.Armor;
        }
    }
}
