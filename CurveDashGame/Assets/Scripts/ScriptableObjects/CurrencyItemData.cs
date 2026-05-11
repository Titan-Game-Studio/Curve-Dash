using UnityEngine;

namespace STG.CurveDash
{
    public enum CurrencyType
    {
        ScrollOfWisdom,
        OrbOfTransmutation,
        OrbOfAlteration,
        ChaosOrb,
        ExaltedOrb,
        BlacksmithWhetstone,
        ArmourerScrap
    }

    [CreateAssetMenu(fileName = "New Currency Item", menuName = "Curve-Dash/Items/Currency")]
    public class CurrencyItemData : ItemData
    {
        [Header("Currency Properties")]
        public CurrencyType CurrencyType;
        
        private void Reset()
        {
            Type = ItemType.Accessory;
        }
    }
}
