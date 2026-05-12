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
        public List<AbilityData> DynamicAbilities = new List<AbilityData>();

        public List<AbilityData> GetAbilities()
        {
            var list = new List<AbilityData>();
            if (BaseData != null && BaseData.Abilities != null)
            {
                list.AddRange(BaseData.Abilities);
            }
            list.AddRange(DynamicAbilities);
            return list;
        }
        
        public float FinalMinDamage { get; private set; }
        public float FinalMaxDamage { get; private set; }
        public float FinalAttackSpeed { get; private set; }

        public WeaponInstance(WeaponData baseData, ItemRarity rarity = ItemRarity.Normal, bool autoRoll = true)
        {
            BaseData = baseData;
            Rarity = rarity;
            if (autoRoll)
            {
                RollRandomAffixes();
            }
            CalculateFinalStats();
        }

        private void RollRandomAffixes()
        {
            if (Rarity == ItemRarity.Normal) return;

            var templates = new List<AffixData>();
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AffixData");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var affix = UnityEditor.AssetDatabase.LoadAssetAtPath<AffixData>(path);
                if (affix != null) templates.Add(affix);
            }
#endif
            if (templates.Count == 0)
            {
                var loaded = Resources.LoadAll<AffixData>("");
                templates.AddRange(loaded);
            }

            if (templates.Count == 0) return;

            int numPrefixes = 0;
            int numSuffixes = 0;

            if (Rarity == ItemRarity.Magic)
            {
                numPrefixes = Random.Range(0, 2);
                numSuffixes = Random.Range(0, 2);
                if (numPrefixes == 0 && numSuffixes == 0)
                {
                    if (Random.value > 0.5f) numPrefixes = 1;
                    else numSuffixes = 1;
                }
            }
            else if (Rarity == ItemRarity.Rare)
            {
                numPrefixes = Random.Range(1, 4);
                numSuffixes = Random.Range(1, 4);
            }
            else if (Rarity == ItemRarity.Unique)
            {
                numPrefixes = Random.Range(0, 2);
                numSuffixes = Random.Range(0, 2);
            }

            var prefixes = templates.FindAll(t => t.TypeOfAffix == AffixType.Prefix);
            var suffixes = templates.FindAll(t => t.TypeOfAffix == AffixType.Suffix);

            int pCount = 0;
            int iter = 0;
            while (pCount < numPrefixes && prefixes.Count > 0 && iter < 50)
            {
                iter++;
                var p = prefixes[Random.Range(0, prefixes.Count)];
                if (p != null && !Affixes.Exists(a => a.AffixName == p.AffixName))
                {
                    Affixes.Add(p.Roll());
                    pCount++;
                }
            }

            int sCount = 0;
            iter = 0;
            while (sCount < numSuffixes && suffixes.Count > 0 && iter < 50)
            {
                iter++;
                var s = suffixes[Random.Range(0, suffixes.Count)];
                if (s != null && !Affixes.Exists(a => a.AffixName == s.AffixName))
                {
                    Affixes.Add(s.Roll());
                    sCount++;
                }
            }
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
            
            float baseSpeed = BaseData.BaseAttackSpeed > 0.05f ? BaseData.BaseAttackSpeed : 1.0f;
            FinalAttackSpeed = baseSpeed * (1 + increasedSpeedPercent / 100f);
            if (FinalAttackSpeed < 0.1f) FinalAttackSpeed = 0.1f;
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
