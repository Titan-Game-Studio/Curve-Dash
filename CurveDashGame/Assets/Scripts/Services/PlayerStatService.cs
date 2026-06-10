using System;
using System.Linq;
using System.Collections.Generic;
using Leopotam.EcsLite;
using UnityEngine;

namespace STG.CurveDash
{
    public class PlayerStatService
    {
        private readonly EcsWorld world;
        private readonly DataManager dataManager;

        public const int ScoreForCrystal = 1;
        public const int Damege = 1;
        public const int MaxHeart = 5;
        public const int ScoreForStep = 1;
        // --- Leveling / EXP ---
        // EXP is earned ONLY by killing monsters (granted by ObstacleSystem on enemy death). The per-run
        // Score (HighScore) is separate and still accrues from steps/coins/kills via AddScore.
        public const int BaseExpToLevel = 300;        // EXP to go from level 1 → 2.
        public const int LevelsToDoubleExp = 10;      // EXP requirement DOUBLES every this many levels.

        // Each character level auto-grants this many points to Strength, Dexterity AND Intelligence.
        // Applied as a Devion stat MODIFIER (not BaseValue) so it never wipes manually-allocated points.
        public const float AttributeGrowthPerLevel = 3f;

        /// <summary>
        /// EXP required to advance FROM <paramref name="level"/> to the next. Smooth geometric curve that
        /// rises a little each level (×2^(1/LevelsToDoubleExp) ≈ +7%/level) and doubles exactly every
        /// <see cref="LevelsToDoubleExp"/> levels:  req = Base × 2^((level-1)/LevelsToDoubleExp).
        /// </summary>
        public static int ExpToNextLevel(int level)
        {
            if (level < 1) level = 1;
            double req = BaseExpToLevel * System.Math.Pow(2.0, (level - 1) / (double)LevelsToDoubleExp);
            return Mathf.Max(1, (int)System.Math.Round(req));
        }

        // --- PoE-style Energy Shield recharge ---
        // ES starts recharging after this many seconds without taking any damage, then refills at
        // ShieldRechargeFractionPerSec of maximum ES per second. Any damage resets the delay.
        public const float ShieldRechargeDelay = 2f;
        public const float ShieldRechargeFractionPerSec = 1f / 3f; // ~33% of max ES / sec → full in ~3s
        private float _timeSinceLastDamage = 0f;

        // Exp into the CURRENT level, clamped to [0, ExpToNextLevel(level)] so the "x / threshold" bar
        // never shows a negative or overflow. Exp is persistent and decoupled from the per-run Score.
        private static int ExpIntoCurrentLevel(in PlayerStatComponent c) =>
            Mathf.Clamp(c.Exp, 0, ExpToNextLevel(c.Level));
        private bool hasPushedInitialStats = false;

        // Source token tagging the per-level attribute growth modifier so we can replace it cleanly
        // (RemoveModifiersFromSource) without touching gear modifiers or the manually-allocated BaseValue.
        private readonly object _attributeGrowthSource = new object();

        private readonly EcsPool<PlayerStatComponent> playerStatPool;
        private readonly EcsPool<PlayerLevelUpComponent> playerLevelUpPool;
        private readonly EcsFilter playerStatFilter;

        public PlayerStatService(EcsWorld world, DataManager dataManager)
        {
            this.world = world;
            this.dataManager = dataManager;

            playerStatPool = world.GetPool<PlayerStatComponent>();
            playerLevelUpPool = world.GetPool<PlayerLevelUpComponent>();
            playerStatFilter = world.Filter<PlayerStatComponent>().End();
        }

        public ref PlayerStatComponent GetPlayerStat()
        {
            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var playerStatComponent = ref playerStatPool.Get(playerStat);
            SyncWithDevionGames(ref playerStatComponent, false);
            return ref playerStatComponent;
        }

        // Per-run score only (drives HighScore). EXP/leveling is intentionally NOT granted here anymore —
        // experience comes solely from killing monsters via AddExp, so steps and coins no longer level you.
        public void AddScore(int score)
        {
            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var playerStatComponent = ref playerStatPool.Get(playerStat);
            playerStatComponent.Score += score;
        }

        /// <summary>
        /// Grants experience and handles leveling. This is the ONLY source of EXP — called from the
        /// enemy-death path so killing monsters is the sole way to level. Handles multi-level-ups,
        /// per-level rewards (life/mana + Free Points) and persistence.
        /// </summary>
        public void AddExp(int amount)
        {
            if (amount <= 0) return;

            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var playerStatComponent = ref playerStatPool.Get(playerStat);

            playerStatComponent.Exp += amount;

            bool leveled = false;
            int guard = 0; // safety net against a pathological curve causing an endless loop
            while (playerStatComponent.Exp >= ExpToNextLevel(playerStatComponent.Level) && guard++ < 1000)
            {
                playerStatComponent.Exp -= ExpToNextLevel(playerStatComponent.Level);
                playerStatComponent.Level++;
                leveled = true;
                AddLife(20f);
                AddMana(10f);
                // 1 Free Point per level, +1 bonus on every 10th level.
                GrantFreePoints(1 + (playerStatComponent.Level % 10 == 0 ? 1 : 0));
                // Mirror the authoritative level onto the Devion "Level" stat so the character sheet
                // shows the real level (its built-in Exp→Level system is not used here).
                PushLevelToDevion(playerStatComponent.Level);
            }

            // Refresh the per-level attribute growth modifier to match the new level.
            if (leveled) ApplyAttributeLevelGrowth(playerStatComponent.Level);

            if (leveled && !playerLevelUpPool.Has(playerStat))
                playerLevelUpPool.Add(playerStat);

            // Persist level/XP so progression carries across runs and deaths. Only write on a ding to
            // avoid hammering PlayerPrefs; GameEnd saves any partial XP at run end.
            if (dataManager?.UserData != null)
            {
                dataManager.UserData.CharacterLevel = playerStatComponent.Level;
                dataManager.UserData.CharacterExp = playerStatComponent.Exp;
                if (leveled) dataManager.SaveData();
            }

            // Push AFTER any level-up so the bar shows progress into the NEW level (resets on ding).
            if (cachedExpStat != null) cachedExpStat.BaseValue = ExpIntoCurrentLevel(playerStatComponent);
        }

        private void PushLevelToDevion(int level)
        {
            try
            {
                if (cachedLevelStat == null)
                {
                    var handler = DevionGames.StatSystem.StatsManager.GetStatsHandler("Player Stats");
                    cachedLevelStat = handler?.GetStat("Level");
                }
                if (cachedLevelStat != null) cachedLevelStat.BaseValue = level;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[PlayerStatService] PushLevelToDevion failed: {ex.Message}");
            }
        }

        // Re-applies the per-level attribute growth as a Flat modifier on Strength/Dexterity/Intelligence.
        // Deterministic from Level (= (Level-1) × AttributeGrowthPerLevel), so it is correct after a restart
        // (recomputed from the persisted level) and never double-counts. Manually-allocated points live in
        // BaseValue and are untouched; gear bonuses are separate modifiers from PlayerView.
        private void ApplyAttributeLevelGrowth(int level)
        {
            try
            {
                var handler = DevionGames.StatSystem.StatsManager.GetStatsHandler("Player Stats");
                if (handler == null) return;

                float growth = Mathf.Max(0, level - 1) * AttributeGrowthPerLevel;
                ApplyGrowthModifier(handler.GetStat("Strength"), growth);
                ApplyGrowthModifier(handler.GetStat("Dexterity"), growth);
                ApplyGrowthModifier(handler.GetStat("Intelligence"), growth);
                handler.onUpdate?.Invoke();
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[PlayerStatService] ApplyAttributeLevelGrowth failed: {ex.Message}");
            }
        }

        private void ApplyGrowthModifier(DevionGames.StatSystem.Stat stat, float growth)
        {
            if (stat == null) return;
            stat.RemoveModifiersFromSource(_attributeGrowthSource);
            if (growth > 0f)
                stat.AddModifier(new DevionGames.StatSystem.StatModifier(
                    growth, DevionGames.StatSystem.StatModType.Flat, _attributeGrowthSource));
        }

        private void GrantFreePoints(int amount)
        {
            try
            {
                if (amount <= 0) return;
                if (cachedFreePointsStat == null)
                {
                    var handler = DevionGames.StatSystem.StatsManager.GetStatsHandler("Player Stats");
                    cachedFreePointsStat = handler?.GetStat("Free Points");
                }
                cachedFreePointsStat?.Add(amount);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[PlayerStatService] GrantFreePoints failed: {ex.Message}");
            }
        }

        public void AddGold(int gold)
        {
            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var playerStatComponent = ref playerStatPool.Get(playerStat);
            playerStatComponent.Gold += gold;
            AddScore(10);
        }

        public void AddLife(float amount)
        {
            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var playerStatComponent = ref playerStatPool.Get(playerStat);
            SyncWithDevionGames(ref playerStatComponent, false);
            playerStatComponent.CurrentLife = Mathf.Min(playerStatComponent.MaxLife, playerStatComponent.CurrentLife + amount);
            SyncWithDevionGames(ref playerStatComponent, true);
        }

        public void AddMana(float amount)
        {
            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var playerStatComponent = ref playerStatPool.Get(playerStat);
            SyncWithDevionGames(ref playerStatComponent, false);
            playerStatComponent.CurrentMana = Mathf.Min(playerStatComponent.MaxMana, playerStatComponent.CurrentMana + amount);
            SyncWithDevionGames(ref playerStatComponent, true);
        }

        // Returns true and deducts mana if enough is available; returns false otherwise.
        public bool TrySpendMana(float amount)
        {
            if (amount <= 0f) return true;
            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var comp = ref playerStatPool.Get(playerStat);
            SyncWithDevionGames(ref comp, false);
            if (comp.CurrentMana < amount) return false;
            comp.CurrentMana -= amount;
            SyncWithDevionGames(ref comp, true);
            return true;
        }

        public void AddEnergyShield(float amount)
        {
            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var playerStatComponent = ref playerStatPool.Get(playerStat);
            SyncWithDevionGames(ref playerStatComponent, false);
            playerStatComponent.CurrentEnergyShield = Mathf.Min(playerStatComponent.MaxEnergyShield, playerStatComponent.CurrentEnergyShield + amount);
            SyncWithDevionGames(ref playerStatComponent, true);
        }

        // PoE-style Energy Shield recharge. Call once per frame. After ShieldRechargeDelay seconds
        // without taking damage, ES refills at ShieldRechargeFractionPerSec of maximum per second.
        public void TickShieldRecharge(float deltaTime)
        {
            if (playerStatFilter.GetEntitiesCount() == 0) return;

            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var comp = ref playerStatPool.Get(playerStat);

            _timeSinceLastDamage += deltaTime;

            // Nothing to recharge (no shield stat) or already full → just keep counting the delay.
            if (comp.MaxEnergyShield <= 0f) return;

            SyncWithDevionGames(ref comp, false);
            if (comp.CurrentEnergyShield >= comp.MaxEnergyShield) return;

            // Still inside the post-damage delay window → no recharge yet.
            if (_timeSinceLastDamage < ShieldRechargeDelay) return;

            float recharge = comp.MaxEnergyShield * ShieldRechargeFractionPerSec * deltaTime;
            comp.CurrentEnergyShield = Mathf.Min(comp.MaxEnergyShield, comp.CurrentEnergyShield + recharge);
            SyncWithDevionGames(ref comp, true);
        }

        // bypassShield: chaos-style damage ignores Energy Shield and hits Life directly (PoE rule).
        public bool TakeDamage(float damage, Action onDeath = null, bool bypassShield = false)
        {
            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var playerStatComponent = ref playerStatPool.Get(playerStat);

            if (playerStatComponent.InvincibleTimer > 0f)
            {
                // Ignore damage during invincibility frame
                return false;
            }

            // Sync latest values from Devion Games first (this also pulls the live Armour total — gear +
            // affixes + any active flask Armor modifier — into the component).
            SyncWithDevionGames(ref playerStatComponent, false);

            float oldLife = playerStatComponent.CurrentLife;
            float oldShield = playerStatComponent.CurrentEnergyShield;

            // PoE-style Armour mitigation: a hit's physical damage is reduced by armour/(armour+10·hit),
            // capped at 90%. This is the ONLY place Armour matters, so equipped armour AND active flasks
            // (e.g. a Granite Flask's +Armor) now genuinely lessen incoming hits. Chaos (bypassShield)
            // ignores armour, matching PoE.
            if (!bypassShield && playerStatComponent.Armour > 0f && damage > 0f)
            {
                float reduction = playerStatComponent.Armour / (playerStatComponent.Armour + 10f * damage);
                reduction = Mathf.Clamp(reduction, 0f, 0.9f);
                damage *= 1f - reduction;
            }

            // PoE-style mitigation: Energy Shield absorbs incoming damage before Life. Whatever the
            // shield can't soak overflows to Life. Chaos damage (bypassShield) skips the shield entirely.
            float remaining = damage;
            if (!bypassShield && playerStatComponent.CurrentEnergyShield > 0f)
            {
                float absorbed = Mathf.Min(playerStatComponent.CurrentEnergyShield, remaining);
                playerStatComponent.CurrentEnergyShield -= absorbed;
                remaining -= absorbed;
            }

            UnityEngine.Debug.Log($"<color=orange>[PlayerStatService] TakeDamage CALLED: damage={damage}, shield before={oldShield}, currentLife before={oldLife}, lifeDamage={remaining}</color>");

            playerStatComponent.CurrentLife -= remaining;
            playerStatComponent.InvincibleTimer = 1.5f; // 1.5 seconds of invincibility iframe
            _timeSinceLastDamage = 0f;                  // restart the Energy Shield recharge delay

            if (playerStatComponent.CurrentLife <= 0)
            {
                playerStatComponent.CurrentLife = 0;
                UnityEngine.Debug.Log("<color=red>[PlayerStatService] PLAYER DIED! Invoking death callback...</color>");
                onDeath?.Invoke();
            }

            UnityEngine.Debug.Log($"<color=orange>[PlayerStatService] TakeDamage AFTER damage: shield after={playerStatComponent.CurrentEnergyShield}, currentLife after={playerStatComponent.CurrentLife}</color>");

            // Sync updated values back to Devion Games so the UI and database are perfectly updated
            SyncWithDevionGames(ref playerStatComponent, true);
            return true;
        }

        public void GameStart(int level)
        {
            var playerStat = world.NewEntity();
            hasPushedInitialStats = false;

            ref var playerStatComponent = ref playerStatPool.Add(playerStat);
            // Persistent progression: resume the saved level/XP (carries across runs & deaths).
            int savedLevel = dataManager?.UserData?.CharacterLevel ?? 1;
            int savedExp = dataManager?.UserData?.CharacterExp ?? 0;
            playerStatComponent.Level = Mathf.Max(level, savedLevel);
            playerStatComponent.Exp = Mathf.Clamp(savedExp, 0, ExpToNextLevel(playerStatComponent.Level));
            playerStatComponent.Score = 0;  // per-run score always starts fresh
            playerStatComponent.Gold = 0;

            // Read starting stats from CurveDash_Character_Stats asset; fall back to constants only if DB unavailable
            float baseLife = 500f, baseMana = 50f, baseShield = 0f;
            var db = DevionGames.StatSystem.StatsManager.Database;
            if (db != null)
            {
                foreach (var stat in db.items)
                {
                    if (stat == null) continue;
                    switch (stat.Name)
                    {
                        case "Heart":  baseLife   = stat.BaseValue; break;
                        case "Mana":   baseMana   = stat.BaseValue; break;
                        case "Shield": baseShield = stat.BaseValue; break;
                    }
                }
                UnityEngine.Debug.Log($"<color=cyan>[PlayerStatService] GameStart: read from asset Heart={baseLife}, Mana={baseMana}, Shield={baseShield}</color>");
            }

            playerStatComponent.MaxLife = baseLife;
            playerStatComponent.CurrentLife = baseLife;
            playerStatComponent.MaxMana = baseMana;
            playerStatComponent.CurrentMana = baseMana;
            playerStatComponent.MaxEnergyShield = baseShield;
            playerStatComponent.CurrentEnergyShield = baseShield;
            playerStatComponent.InvincibleTimer = 0f;
            RestoreResult(ref playerStatComponent);

            UnityEngine.Debug.Log($"<color=cyan>[PlayerStatService] GameStart: initialized starting life={playerStatComponent.CurrentLife}</color>");

            // Sync to Devion on game start (this will be ignored because player object hasn't spawned yet)
            SyncWithDevionGames(ref playerStatComponent, true);
        }

        public void GameEnd()
        {
            // new record
            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var playerStatComponent = ref playerStatPool.Get(playerStat);
            if (playerStatComponent.Score > playerStatComponent.HighScore)
                playerStatComponent.HighScore = playerStatComponent.Score;

            UnityEngine.Debug.Log($"<color=yellow>[PlayerStatService] GameEnd: Score={playerStatComponent.Score}, HighScore={playerStatComponent.HighScore}, Level={playerStatComponent.Level}, Exp={playerStatComponent.Exp}</color>");
            StoreResult(playerStatComponent);

            // Persist the run's progression (including partial XP) so nothing is lost on death/quit.
            if (dataManager?.UserData != null)
            {
                dataManager.UserData.CharacterLevel = playerStatComponent.Level;
                dataManager.UserData.CharacterExp = playerStatComponent.Exp;
                dataManager.SaveData();
            }
        }

        private DevionGames.StatSystem.StatsHandler cachedHandler;
        // Attributes (have CurrentValue)
        private DevionGames.StatSystem.Attribute cachedHeartStat;
        private DevionGames.StatSystem.Attribute cachedManaStat;
        private DevionGames.StatSystem.Attribute cachedShieldStat;
        // Plain stats (value only)
        private DevionGames.StatSystem.Stat cachedExpStat;
        private DevionGames.StatSystem.Stat cachedFreePointsStat;
        private DevionGames.StatSystem.Stat cachedLevelStat;
        private DevionGames.StatSystem.Stat cachedStrStat;
        private DevionGames.StatSystem.Stat cachedDexStat;
        private DevionGames.StatSystem.Stat cachedIntStat;
        private DevionGames.StatSystem.Stat cachedArmourStat;
        private DevionGames.StatSystem.Stat cachedEvasionStat;
        private DevionGames.StatSystem.Stat cachedAccuracyStat;
        private DevionGames.StatSystem.Stat cachedFireResStat;
        private DevionGames.StatSystem.Stat cachedColdResStat;
        private DevionGames.StatSystem.Stat cachedLightResStat;
        private DevionGames.StatSystem.Stat cachedChaosResStat;
        private DevionGames.StatSystem.Stat cachedCritChanceStat;
        private DevionGames.StatSystem.Stat cachedCritMultStat;
        private DevionGames.StatSystem.Stat cachedLifeRegenStat;
        private DevionGames.StatSystem.Stat cachedMoveSpeedStat;
        private DevionGames.StatSystem.Stat cachedBlockStat;

        private void ClearStatCache()
        {
            cachedHandler = null;
            cachedHeartStat = null; cachedManaStat = null; cachedShieldStat = null;
            cachedExpStat = null; cachedFreePointsStat = null; cachedLevelStat = null;
            cachedStrStat = null; cachedDexStat = null; cachedIntStat = null;
            cachedArmourStat = null; cachedEvasionStat = null; cachedAccuracyStat = null;
            cachedFireResStat = null; cachedColdResStat = null; cachedLightResStat = null;
            cachedChaosResStat = null; cachedCritChanceStat = null; cachedCritMultStat = null;
            cachedLifeRegenStat = null; cachedMoveSpeedStat = null; cachedBlockStat = null;
        }

        private void SyncWithDevionGames(ref PlayerStatComponent comp, bool pushToDevion = false)
        {
            try
            {
                var handler = DevionGames.StatSystem.StatsManager.GetStatsHandler("Player Stats");
                if (handler == null) { ClearStatCache(); return; }

                if (cachedHandler != handler)
                {
                    cachedHandler = handler;
                    cachedHeartStat      = handler.GetStat("Heart")          as DevionGames.StatSystem.Attribute;
                    cachedManaStat       = handler.GetStat("Mana")           as DevionGames.StatSystem.Attribute;
                    cachedShieldStat     = handler.GetStat("Shield")         as DevionGames.StatSystem.Attribute;
                    cachedExpStat        = handler.GetStat("Exp");
                    cachedFreePointsStat = handler.GetStat("Free Points");
                    cachedStrStat        = handler.GetStat("Strength");
                    cachedDexStat      = handler.GetStat("Dexterity");
                    cachedIntStat      = handler.GetStat("Intelligence");
                    cachedArmourStat   = handler.GetStat("Armor");
                    cachedEvasionStat  = handler.GetStat("Evasion Rating");
                    cachedAccuracyStat = handler.GetStat("Accuracy Rating");
                    cachedFireResStat  = handler.GetStat("Fire Resistance");
                    cachedColdResStat  = handler.GetStat("Cold Resistance");
                    cachedLightResStat = handler.GetStat("Lightning Resistance");
                    cachedChaosResStat = handler.GetStat("Chaos Resistance");
                    cachedCritChanceStat = handler.GetStat("Critical Strike");
                    cachedCritMultStat   = handler.GetStat("Critical Multiplier");
                    cachedLifeRegenStat  = handler.GetStat("Life Regeneration");
                    cachedMoveSpeedStat  = handler.GetStat("Movement Speed");
                    cachedBlockStat      = handler.GetStat("Block Chance");
                    UnityEngine.Debug.Log($"<color=green>[PlayerStatService] StatsHandler cached. Heart={cachedHeartStat != null}, Mana={cachedManaStat != null}, Shield={cachedShieldStat != null}, FireRes={cachedFireResStat != null}</color>");
                }

                // First sync after player spawns: read configured values from the asset as source of truth.
                if (!hasPushedInitialStats)
                {
                    UnityEngine.Debug.Log("<color=green>[PlayerStatService] FIRST TIME Sync: Reading from CurveDash_Character_Stats...</color>");

                    // Push the restored Level to Devion FIRST: Heart/Mana max values are formula-driven
                    // (Heart = base + Level*10 + Str/2), so the level must be live before we read them.
                    // Otherwise max is computed at level 1, CurrentValue is pinned to that low max, and a
                    // later pull raises MaxLife while CurrentLife stays low — the HP bar opens non-full on
                    // restart/revive. Reading after the push guarantees CurrentLife == MaxLife (full HP).
                    PushLevelToDevion(comp.Level);

                    // Apply per-level attribute growth now that the player has spawned and the level is live.
                    // (Recomputed from the restored level, so it's correct across runs/deaths.)
                    ApplyAttributeLevelGrowth(comp.Level);

                    if (cachedHeartStat != null)
                    {
                        float max = cachedHeartStat.Value > 0f ? cachedHeartStat.Value : cachedHeartStat.BaseValue;
                        comp.MaxLife = max; comp.CurrentLife = max;
                        cachedHeartStat.CurrentValue = max;
                        UnityEngine.Debug.Log($"[PlayerStatService] Heart Value={cachedHeartStat.Value} Base={cachedHeartStat.BaseValue} → MaxLife={max}");
                    }
                    else
                    {
                        string all = string.Join(", ", handler.m_Stats.Select(x => x != null ? $"'{x.Name}'" : "null"));
                        UnityEngine.Debug.LogWarning($"[PlayerStatService] Heart not found! Stats: {all}");
                    }

                    if (cachedManaStat != null)
                    {
                        float max = cachedManaStat.Value > 0f ? cachedManaStat.Value : cachedManaStat.BaseValue;
                        comp.MaxMana = max; comp.CurrentMana = max;
                        cachedManaStat.CurrentValue = max;
                    }

                    if (cachedShieldStat != null)
                    {
                        float max = cachedShieldStat.Value > 0f ? cachedShieldStat.Value : cachedShieldStat.BaseValue;
                        comp.MaxEnergyShield = max; comp.CurrentEnergyShield = max;
                        cachedShieldStat.CurrentValue = max;
                    }

                    if (cachedExpStat != null) cachedExpStat.BaseValue = ExpIntoCurrentLevel(comp);
                    PullPlainStats(ref comp);
                    hasPushedInitialStats = true;
                    handler.onUpdate?.Invoke();
                }
                else if (pushToDevion)
                {
                    if (cachedHeartStat  != null) cachedHeartStat.CurrentValue  = comp.CurrentLife;
                    if (cachedManaStat   != null) cachedManaStat.CurrentValue   = comp.CurrentMana;
                    if (cachedShieldStat != null) cachedShieldStat.CurrentValue = comp.CurrentEnergyShield;
                    handler.onUpdate?.Invoke();
                }
                else
                {
                    if (cachedHeartStat  != null) { comp.MaxLife           = cachedHeartStat.Value;  comp.CurrentLife           = cachedHeartStat.CurrentValue; }
                    if (cachedManaStat   != null) { comp.MaxMana            = cachedManaStat.Value;   comp.CurrentMana           = cachedManaStat.CurrentValue; }
                    if (cachedShieldStat != null) { comp.MaxEnergyShield   = cachedShieldStat.Value; comp.CurrentEnergyShield   = cachedShieldStat.CurrentValue; }
                    PullPlainStats(ref comp);
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[PlayerStatService] Failed to sync with Devion Games Stat System: {ex.Message}");
            }
        }

        private void PullPlainStats(ref PlayerStatComponent comp)
        {
            if (cachedStrStat      != null) comp.Strength           = cachedStrStat.Value;
            if (cachedDexStat      != null) comp.Dexterity          = cachedDexStat.Value;
            if (cachedIntStat      != null) comp.Intelligence       = cachedIntStat.Value;
            if (cachedArmourStat   != null) comp.Armour             = cachedArmourStat.Value;
            if (cachedEvasionStat  != null) comp.EvasionRating      = cachedEvasionStat.Value;
            if (cachedAccuracyStat != null) comp.AccuracyRating     = cachedAccuracyStat.Value;
            if (cachedFireResStat  != null) comp.FireResistance      = cachedFireResStat.Value;
            if (cachedColdResStat  != null) comp.ColdResistance      = cachedColdResStat.Value;
            if (cachedLightResStat != null) comp.LightningResistance = cachedLightResStat.Value;
            if (cachedChaosResStat != null) comp.ChaosResistance     = cachedChaosResStat.Value;
            if (cachedCritChanceStat != null) comp.CritChance        = cachedCritChanceStat.Value;
            if (cachedCritMultStat   != null) comp.CritMultiplier    = cachedCritMultStat.Value;
            if (cachedLifeRegenStat  != null) comp.LifeRegen         = cachedLifeRegenStat.Value;
            if (cachedMoveSpeedStat  != null) comp.MovementSpeed     = cachedMoveSpeedStat.Value;
            if (cachedBlockStat      != null) comp.BlockChance       = cachedBlockStat.Value;
        }

        public void Clear()
        {
            var playerStat = playerStatFilter.GetRawEntities()[0];
            world.DelEntity(playerStat);
            ClearStatCache();
        }

        private void StoreResult(in PlayerStatComponent playerStatComponent)
        {
            PlayerPrefs.SetInt("AmazingTrack_HighScore", playerStatComponent.HighScore);
        }

        private void RestoreResult(ref PlayerStatComponent playerStatComponent)
        {
            if (PlayerPrefs.HasKey("AmazingTrack_HighScore"))
                playerStatComponent.HighScore = PlayerPrefs.GetInt("AmazingTrack_HighScore");
        }
    }
}