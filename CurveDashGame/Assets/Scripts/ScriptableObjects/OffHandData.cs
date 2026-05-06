using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    public enum OffHandType
    {
        Shield,
        Arrow
    }

    [CreateAssetMenu(fileName = "NewOffHandItem", menuName = "Curve Dash/Weapons/Off-Hand Item (Shield-Arrow)")]
    public class OffHandData : EquippableData
    {
        public OffHandType SubType;
        
        [Header("Stats")]
        public float BlockChance = 20f;
        public float BonusDamage = 0f;

        [Header("Sockets (PoE Style)")]
        public List<AbilityData> Abilities = new List<AbilityData>();
        public int MaxSockets => 3;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Abilities != null && Abilities.Count > MaxSockets)
            {
                Debug.LogWarning($"[OffHandData] {name} only supports {MaxSockets} sockets.");
                Abilities.RemoveRange(MaxSockets, Abilities.Count - MaxSockets);
            }
        }
#endif
    }
}
