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

        // --- Extended damage profile (all StatTypes now feed combat) ---
        public float AddedFireDamage { get; private set; }   // flat fire added to the hit
        public float AddedColdDamage { get; private set; }   // flat cold added to the hit
        public float BonusCritChance { get; private set; }   // % added on top of the character's crit chance
        public float LifeStealPercent { get; private set; }  // % of damage dealt returned as life
        public float KnockbackForce { get; private set; }    // shove strength applied to the enemy on hit

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

            // Weighted, tag- and item-level-gated generation. Existing affix assets (no tiers / no tags)
            // behave like before: unrestricted, single open tier at weight 1000.
            var itemTags  = BaseData != null ? BaseData.GetAffixTags() : null;
            int itemLevel = BaseData != null ? BaseData.ItemLevel : 1;

            var rolled = AffixRoller.RollAffixes(templates, itemTags, itemLevel, numPrefixes, numSuffixes);
            foreach (var mod in rolled)
            {
                if (!Affixes.Exists(a => a.AffixName == mod.AffixName))
                    Affixes.Add(mod);
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
            float fire = 0, cold = 0, critChance = 0, lifeSteal = 0, knockback = 0;

            foreach (var affix in Affixes)
            {
                switch (affix.Type)
                {
                    case StatType.AddedPhysicalDamage:     flatAddedDamage       += affix.Value; break;
                    case StatType.IncreasedPhysicalDamage: increasedDamagePercent += affix.Value; break;
                    case StatType.IncreasedAttackSpeed:    increasedSpeedPercent  += affix.Value; break;
                    case StatType.AddedFireDamage:         fire                   += affix.Value; break;
                    case StatType.AddedColdDamage:         cold                   += affix.Value; break;
                    case StatType.IncreasedCriticalChance: critChance             += affix.Value; break;
                    case StatType.LifeStealPercentage:     lifeSteal              += affix.Value; break;
                    case StatType.KnockbackForce:          knockback              += affix.Value; break;
                }
            }

            FinalMinDamage = (BaseData.BaseMinDamage + flatAddedDamage) * (1 + increasedDamagePercent / 100f);
            FinalMaxDamage = (BaseData.BaseMaxDamage + flatAddedDamage) * (1 + increasedDamagePercent / 100f);

            float baseSpeed = BaseData.BaseAttackSpeed > 0.05f ? BaseData.BaseAttackSpeed : 1.0f;
            FinalAttackSpeed = baseSpeed * (1 + increasedSpeedPercent / 100f);
            if (FinalAttackSpeed < 0.1f) FinalAttackSpeed = 0.1f;

            AddedFireDamage  = fire;
            AddedColdDamage  = cold;
            BonusCritChance  = critChance;
            LifeStealPercent = lifeSteal;
            KnockbackForce   = knockback;
        }

        /// <summary>Flat elemental damage (fire + cold) rolled on this weapon, added to every hit.</summary>
        public float GetElementalDamage() => AddedFireDamage + AddedColdDamage;

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
