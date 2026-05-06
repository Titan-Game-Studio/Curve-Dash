using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace STG.CurveDash
{
    [System.Serializable]
    public class WeaponInstance
    {
        public WeaponData BaseData;
        public ItemRarity Rarity;
        public List<StatModifier> Affixes = new List<StatModifier>();
        
        public float FinalMinDamage { get; private set; }
        public float FinalMaxDamage { get; private set; }
        public float FinalAttackSpeed { get; private set; }

        public WeaponInstance(WeaponData baseData, ItemRarity rarity = ItemRarity.Normal)
        {
            BaseData = baseData;
            Rarity = rarity;
            CalculateFinalStats();
        }

        public bool CanAddAffix(AffixType type)
        {
            int max = 0;
            switch (Rarity)
            {
                case ItemRarity.Magic: max = 1; break;
                case ItemRarity.Rare: max = 3; break;
                case ItemRarity.Unique: max = 10; break; // Unique có thể có rất nhiều dòng cố định
            }
            
            int currentCount = Affixes.Count(x => x.AffixType == type);
            return currentCount < max;
        }

        public void AddAffix(StatModifier modifier)
        {
            if (CanAddAffix(modifier.AffixType))
            {
                Affixes.Add(modifier);
                CalculateFinalStats();
            }
            else
            {
                Debug.LogWarning($"[WeaponInstance] Cannot add more {modifier.AffixType} to a {Rarity} item!");
            }
        }

        public void CalculateFinalStats()
        {
            if (BaseData == null) return;

            float flatAddedDamage = 0;
            float increasedDamagePercent = 0;
            float increasedSpeedPercent = 0;

            foreach (var affix in Affixes)
            {
                switch (affix.Type)
                {
                    case StatType.AddedPhysicalDamage: flatAddedDamage += affix.Value; break;
                    case StatType.IncreasedPhysicalDamage: increasedDamagePercent += affix.Value; break;
                    case StatType.IncreasedAttackSpeed: increasedSpeedPercent += affix.Value; break;
                }
            }

            FinalMinDamage = (BaseData.BaseMinDamage + flatAddedDamage) * (1 + increasedDamagePercent / 100f);
            FinalMaxDamage = (BaseData.BaseMaxDamage + flatAddedDamage) * (1 + increasedDamagePercent / 100f);
            FinalAttackSpeed = BaseData.BaseAttackSpeed * (1 + increasedSpeedPercent / 100f);
        }

        public string GetDisplayName()
        {
            if (BaseData == null) return "Unknown Item";
            if (Rarity == ItemRarity.Normal) return BaseData.ItemName;

            string prefix = Affixes.FirstOrDefault(x => x.AffixType == AffixType.Prefix)?.AffixName ?? "";
            string suffix = Affixes.FirstOrDefault(x => x.AffixType == AffixType.Suffix)?.AffixName ?? "";
            
            string fullName = BaseData.ItemName;
            if (!string.IsNullOrEmpty(prefix)) fullName = $"{prefix} {fullName}";
            if (!string.IsNullOrEmpty(suffix)) fullName = $"{fullName} of {suffix}";

            return fullName;
        }

        public float GetRandomDamage()
        {
            return Random.Range(FinalMinDamage, FinalMaxDamage);
        }
    }
}
