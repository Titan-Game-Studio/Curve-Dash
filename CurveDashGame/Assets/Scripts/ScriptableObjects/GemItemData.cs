using UnityEngine;

namespace STG.CurveDash
{
    public enum GemType
    {
        Skill,
        Support
    }

    [CreateAssetMenu(fileName = "New Gem Item", menuName = "Curve-Dash/Items/Gem")]
    public class GemItemData : ItemData
    {
        [Header("Gem Properties")]
        public GemType GemType;
        public AbilityData EmbeddedAbility;
        public Color SocketColor = Color.green; // Green, Red, Blue matching PoE sockets
        
        private void Reset()
        {
            Type = ItemType.Accessory;
        }
    }
}

