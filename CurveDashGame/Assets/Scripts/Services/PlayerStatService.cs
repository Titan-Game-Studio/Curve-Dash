using System;
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
            return ref playerStatPool.Get(playerStat);
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
            playerStatComponent.CurrentLife = Mathf.Min(playerStatComponent.MaxLife, playerStatComponent.CurrentLife + amount);
        }

        public void AddMana(float amount)
        {
            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var playerStatComponent = ref playerStatPool.Get(playerStat);
            playerStatComponent.CurrentMana = Mathf.Min(playerStatComponent.MaxMana, playerStatComponent.CurrentMana + amount);
        }

        public void AddEnergyShield(float amount)
        {
            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var playerStatComponent = ref playerStatPool.Get(playerStat);
            playerStatComponent.CurrentEnergyShield = Mathf.Min(playerStatComponent.MaxEnergyShield, playerStatComponent.CurrentEnergyShield + amount);
        }

        public void TakeDamage(float damage, Action onDeath = null)
        {
            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var playerStatComponent = ref playerStatPool.Get(playerStat);
            
            // 1. Energy Shield absorbs damage first
            if (playerStatComponent.CurrentEnergyShield > 0)
            {
                if (playerStatComponent.CurrentEnergyShield >= damage)
                {
                    playerStatComponent.CurrentEnergyShield -= damage;
                    damage = 0;
                }
                else
                {
                    damage -= playerStatComponent.CurrentEnergyShield;
                    playerStatComponent.CurrentEnergyShield = 0;
                }
            }

            // 2. Excess damage goes to Life
            if (damage > 0)
            {
                playerStatComponent.CurrentLife -= damage;
            }

            if (playerStatComponent.CurrentLife <= 0)
            {
                playerStatComponent.CurrentLife = 0;
                onDeath?.Invoke();
            }
        }

        public void GameStart(int level)
        {
            var playerStat = world.NewEntity();

            ref var playerStatComponent = ref playerStatPool.Add(playerStat);
            playerStatComponent.Level = level;
            playerStatComponent.Score = 0;
            playerStatComponent.Gold = 0;
            playerStatComponent.MaxLife = 100f;
            playerStatComponent.CurrentLife = 100f;
            playerStatComponent.MaxMana = 50f;
            playerStatComponent.CurrentMana = 50f;
            playerStatComponent.MaxEnergyShield = 30f;
            playerStatComponent.CurrentEnergyShield = 30f;
            playerStatComponent.InvincibleTimer = 0f;
            RestoreResult(ref playerStatComponent);
        }

        public void GameEnd()
        {
            // new record
            var playerStat = playerStatFilter.GetRawEntities()[0];
            ref var playerStatComponent = ref playerStatPool.Get(playerStat);
            if (playerStatComponent.Score > playerStatComponent.HighScore)
                playerStatComponent.HighScore = playerStatComponent.Score;

            StoreResult(playerStatComponent);
        }

        public void Clear()
        {
            var playerStat = playerStatFilter.GetRawEntities()[0];
            world.DelEntity(playerStat);
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