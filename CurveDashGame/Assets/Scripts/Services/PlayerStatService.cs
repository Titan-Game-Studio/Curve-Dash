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

        public const int ScoreForCrystal = 1;
        public const int Damege = 1;
        public const int MaxHeart = 5;
        public const int ScoreForStep = 1;
        private const int ScoreForNextLevel = 300;
        private bool hasPushedInitialStats = false;

        private readonly EcsPool<PlayerStatComponent> playerStatPool;
        private readonly EcsPool<PlayerLevelUpComponent> playerLevelUpPool;
        private readonly EcsFilter playerStatFilter;

        public PlayerStatService(EcsWorld world)
        {
            this.world = world;

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

        public void AddScore(int score)
        {
            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var playerStatComponent = ref playerStatPool.Get(playerStat);
            playerStatComponent.Score += score;
            if (playerStatComponent.Score > playerStatComponent.Level * ScoreForNextLevel)
            {
                playerStatComponent.Level++;
                playerLevelUpPool.Add(playerStat);
                AddLife(20f);
                AddMana(10f);
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

        public bool TakeDamage(float damage, Action onDeath = null)
        {
            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var playerStatComponent = ref playerStatPool.Get(playerStat);
            
            if (playerStatComponent.InvincibleTimer > 0f)
            {
                // Ignore damage during invincibility frame
                return false;
            }

            // Sync latest values from Devion Games first
            SyncWithDevionGames(ref playerStatComponent, false);

            float oldLife = playerStatComponent.CurrentLife;
            UnityEngine.Debug.Log($"<color=orange>[PlayerStatService] TakeDamage CALLED: damage={damage}, currentLife before={oldLife}</color>");

            playerStatComponent.CurrentLife -= damage;
            playerStatComponent.InvincibleTimer = 1.5f; // 1.5 seconds of invincibility iframe

            if (playerStatComponent.CurrentLife <= 0)
            {
                playerStatComponent.CurrentLife = 0;
                UnityEngine.Debug.Log("<color=red>[PlayerStatService] PLAYER DIED! Invoking death callback...</color>");
                onDeath?.Invoke();
            }

            UnityEngine.Debug.Log($"<color=orange>[PlayerStatService] TakeDamage AFTER damage: currentLife after={playerStatComponent.CurrentLife}</color>");

            // Sync updated values back to Devion Games so the UI and database are perfectly updated
            SyncWithDevionGames(ref playerStatComponent, true);
            return true;
        }

        public void GameStart(int level)
        {
            var playerStat = world.NewEntity();
            hasPushedInitialStats = false;

            ref var playerStatComponent = ref playerStatPool.Add(playerStat);
            playerStatComponent.Level = level;
            playerStatComponent.Score = 0;
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

            UnityEngine.Debug.Log($"<color=yellow>[PlayerStatService] GameEnd: Score={playerStatComponent.Score}, HighScore={playerStatComponent.HighScore}</color>");
            StoreResult(playerStatComponent);
        }

        private DevionGames.StatSystem.StatsHandler cachedHandler;
        // Attributes (have CurrentValue)
        private DevionGames.StatSystem.Attribute cachedHeartStat;
        private DevionGames.StatSystem.Attribute cachedManaStat;
        private DevionGames.StatSystem.Attribute cachedShieldStat;
        // Plain stats (value only)
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
                    cachedHeartStat    = handler.GetStat("Heart")                as DevionGames.StatSystem.Attribute;
                    cachedManaStat     = handler.GetStat("Mana")                 as DevionGames.StatSystem.Attribute;
                    cachedShieldStat   = handler.GetStat("Shield")               as DevionGames.StatSystem.Attribute;
                    cachedStrStat      = handler.GetStat("Strength");
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