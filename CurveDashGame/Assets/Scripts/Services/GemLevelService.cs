using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    /// <summary>
    /// Tracks XP and level (1–20) for each socketed active gem.
    /// XP required per level = level × 100 (e.g. lv1→2 = 100, lv19→20 = 1900).
    /// Damage multiplier = 1 + (level − 1) × 0.05  (5 % per level; 1.95× at lv20).
    /// </summary>
    public class GemLevelService
    {
        public const int MaxLevel = 20;
        private const long XPPerLevelFactor = 100L;  // XPRequired(lv) = lv * factor
        private const long XPPerKill = 10L;

        private readonly DataManager _dataManager;

        public GemLevelService(DataManager dataManager)
        {
            _dataManager = dataManager;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public GemProgressEntry GetEntry(AbilityData ability)
        {
            if (ability == null) return new GemProgressEntry { AbilityKey = "", Level = 1 };
            var list = EnsureList();
            string key = ability.name;
            var entry = list.Find(e => e.AbilityKey == key);
            if (entry == null)
            {
                entry = new GemProgressEntry { AbilityKey = key, Level = 1, CurrentXP = 0 };
                list.Add(entry);
            }
            return entry;
        }

        public int GetLevel(AbilityData ability) => GetEntry(ability).Level;

        /// <summary>+5 % weapon-damage multiplier per level above 1.</summary>
        public float GetDamageMultiplier(AbilityData ability)
            => MultiplierForLevel(GetLevel(ability));

        public static float MultiplierForLevel(int level)
            => 1f + (level - 1) * 0.05f;

        public static long XPRequired(int level) => level * XPPerLevelFactor;

        /// <summary>
        /// Gives XP equal to <see cref="XPPerKill"/> to every active (non-aura) PoEAbility in
        /// <paramref name="abilities"/>. Returns names of gems that leveled up this call.
        /// </summary>
        public List<string> AddKillXP(IEnumerable<AbilityData> abilities)
        {
            var leveledUp = new List<string>();
            bool anyChange = false;

            foreach (var ab in abilities)
            {
                if (!(ab is PoEAbility poeAb) || poeAb.SkillType == PoEAbilityType.Aura) continue;
                var entry = GetEntry(ab);
                if (entry.Level >= MaxLevel) continue;

                entry.CurrentXP += XPPerKill;
                anyChange = true;

                while (entry.Level < MaxLevel && entry.CurrentXP >= XPRequired(entry.Level))
                {
                    entry.CurrentXP -= XPRequired(entry.Level);
                    entry.Level++;
                    leveledUp.Add($"{ab.AbilityName} (lv {entry.Level})");
                }
            }

            // Persist only when something actually changed (avoid PlayerPrefs write every frame)
            if (anyChange) _dataManager?.SaveData();

            return leveledUp;
        }

        // ── Private ───────────────────────────────────────────────────────────

        private List<GemProgressEntry> EnsureList()
        {
            if (_dataManager?.UserData == null) return new List<GemProgressEntry>();
            if (_dataManager.UserData.GemProgress == null)
                _dataManager.UserData.GemProgress = new List<GemProgressEntry>();
            return _dataManager.UserData.GemProgress;
        }
    }
}
