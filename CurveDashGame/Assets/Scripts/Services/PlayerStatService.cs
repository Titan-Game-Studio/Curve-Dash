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
            playerStatComponent.MaxLife = 500f;
            playerStatComponent.CurrentLife = 500f;
            playerStatComponent.MaxMana = 50f;
            playerStatComponent.CurrentMana = 50f;
            playerStatComponent.MaxEnergyShield = 0f; // Starting shield is 0 so health drops immediately on hit
            playerStatComponent.CurrentEnergyShield = 0f;
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
        private DevionGames.StatSystem.Attribute cachedHeartStat;
        private DevionGames.StatSystem.Attribute cachedManaStat;
        private DevionGames.StatSystem.Attribute cachedShieldStat;

        private void SyncWithDevionGames(ref PlayerStatComponent comp, bool pushToDevion = false)
        {
            try
            {
                var handler = DevionGames.StatSystem.StatsManager.GetStatsHandler("Player Stats");
                if (handler == null)
                {
                    cachedHandler = null;
                    cachedHeartStat = null;
                    cachedManaStat = null;
                    cachedShieldStat = null;
                    return;
                }

                if (cachedHandler != handler)
                {
                    cachedHandler = handler;
                    cachedHeartStat = handler.GetStat("Heart") as DevionGames.StatSystem.Attribute;
                    cachedManaStat = handler.GetStat("Mana") as DevionGames.StatSystem.Attribute;
                    cachedShieldStat = handler.GetStat("Shield") as DevionGames.StatSystem.Attribute;
                    
                    UnityEngine.Debug.Log($"<color=green>[PlayerStatService] Caching new StatsHandler. Heart null? {cachedHeartStat == null}, Mana null? {cachedManaStat == null}, Shield null? {cachedShieldStat == null}</color>");
                }

                // The first time the player spawns and the stats handler is detected, 
                // we MUST initialize the BaseValues of the stats to our starting stats (100 Health / 30 Shield).
                if (!hasPushedInitialStats)
                {
                    UnityEngine.Debug.Log("<color=green>[PlayerStatService] FIRST TIME Sync: Initializing Devion Games BaseValues...</color>");
                    
                    if (cachedHeartStat != null)
                    {
                        cachedHeartStat.BaseValue = comp.MaxLife;
                        cachedHeartStat.CurrentValue = comp.CurrentLife;
                        UnityEngine.Debug.Log($"[PlayerStatService] Heart set to Base={cachedHeartStat.BaseValue}, Current={cachedHeartStat.CurrentValue}");
                    }
                    else
                    {
                        string allStatNames = string.Join(", ", handler.m_Stats.Select(x => x != null ? $"'{x.Name}'" : "null"));
                        UnityEngine.Debug.LogWarning($"[PlayerStatService] Heart stat NOT found in 'Player Stats' handler! Existing stats are: {allStatNames}");
                    }
                    
                    if (cachedManaStat != null)
                    {
                        cachedManaStat.BaseValue = comp.MaxMana;
                        cachedManaStat.CurrentValue = comp.CurrentMana;
                    }

                    if (cachedShieldStat != null)
                    {
                        cachedShieldStat.BaseValue = comp.MaxEnergyShield;
                        cachedShieldStat.CurrentValue = comp.CurrentEnergyShield;
                    }

                    hasPushedInitialStats = true;
                    handler.onUpdate?.Invoke();
                }
                else if (pushToDevion)
                {
                    UnityEngine.Debug.Log($"<color=green>[PlayerStatService] PUSH to Devion Games: Heart.CurrentValue = {comp.CurrentLife}, Shield.CurrentValue = {comp.CurrentEnergyShield}</color>");
                    // Standard gameplay push - ONLY update CurrentValue to prevent runaway BaseValue feedback loop!
                    if (cachedHeartStat != null)
                    {
                        cachedHeartStat.CurrentValue = comp.CurrentLife;
                    }
                    
                    if (cachedManaStat != null)
                    {
                        cachedManaStat.CurrentValue = comp.CurrentMana;
                    }

                    if (cachedShieldStat != null)
                    {
                        cachedShieldStat.CurrentValue = comp.CurrentEnergyShield;
                    }

                    handler.onUpdate?.Invoke();
                }
                else
                {
                    // Standard gameplay pull - read the calculated final Value and CurrentValue
                    float oldLife = comp.CurrentLife;
                    float oldShield = comp.CurrentEnergyShield;

                    if (cachedHeartStat != null)
                    {
                        cachedHeartStat.BaseValue = 500f; // Force base max life to 500 to prevent clamping back to 48 from database/loaded values!
                        comp.MaxLife = cachedHeartStat.Value;
                        comp.CurrentLife = cachedHeartStat.CurrentValue;
                    }

                    if (cachedManaStat != null)
                    {
                        comp.MaxMana = cachedManaStat.Value;
                        comp.CurrentMana = cachedManaStat.CurrentValue;
                    }

                    if (cachedShieldStat != null)
                    {
                        comp.MaxEnergyShield = cachedShieldStat.Value;
                        comp.CurrentEnergyShield = cachedShieldStat.CurrentValue;
                    }

                    if (comp.CurrentLife != oldLife || comp.CurrentEnergyShield != oldShield)
                    {
                        UnityEngine.Debug.Log($"<color=yellow>[PlayerStatService] PULL - Stats changed in Devion! Heart: {oldLife} -> {comp.CurrentLife}, Shield: {oldShield} -> {comp.CurrentEnergyShield} (cachedHeart null? {cachedHeartStat == null}, cachedShield null? {cachedShieldStat == null})</color>");
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[PlayerStatService] Failed to sync with Devion Games Stat System: {ex.Message}");
            }
        }

        public void Clear()
        {
            var playerStat = playerStatFilter.GetRawEntities()[0];
            world.DelEntity(playerStat);
            cachedHandler = null;
            cachedHeartStat = null;
            cachedManaStat = null;
            cachedShieldStat = null;
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