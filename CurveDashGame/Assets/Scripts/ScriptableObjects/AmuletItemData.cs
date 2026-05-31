using UnityEngine;

namespace STG.CurveDash
{
    // No 3D model needed — equipped in the Amulet slot for stat bonuses only.
    [CreateAssetMenu(fileName = "New Amulet", menuName = "Curve-Dash/Items/Amulet Data")]
    public class AmuletItemData : ItemData
    {
        [Header("Stats")]
        public int HealthBonus;
        public int ManaBonus;
        public float CritChanceBonus;
        public float AllResistances;

        public AmuletItemData()
        {
            Type = ItemType.Accessory;
        }
    }
}
