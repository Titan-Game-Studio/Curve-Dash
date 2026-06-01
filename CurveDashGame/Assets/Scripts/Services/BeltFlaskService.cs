using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    public class BeltFlaskActiveState
    {
        public FlaskItemData Flask;
        public float CurrentCharges;
        public float TimeRemaining;
        public bool IsActive;
    }

    // Manages flask auto-use (POE-style) for both belt-linked and equipment-slot flasks.
    public class BeltFlaskService
    {
        private BeltItemData _equippedBelt;
        private readonly List<BeltFlaskActiveState> _beltFlaskStates = new List<BeltFlaskActiveState>();
        private readonly List<BeltFlaskActiveState> _equipmentFlaskStates = new List<BeltFlaskActiveState>();

        public bool HasActiveBelt => _equippedBelt != null;
        public bool HasAnyFlask => _beltFlaskStates.Count > 0 || _equipmentFlaskStates.Count > 0;

        public IReadOnlyList<BeltFlaskActiveState> FlaskStates => _beltFlaskStates;

        public void SetBelt(BeltItemData belt)
        {
            _equippedBelt = belt;
            _beltFlaskStates.Clear();

            if (belt == null) return;

            foreach (var flask in belt.FlaskSlots)
            {
                if (flask == null) continue;
                _beltFlaskStates.Add(new BeltFlaskActiveState
                {
                    Flask = flask,
                    CurrentCharges = flask.MaxCharges,
                    TimeRemaining = 0f,
                    IsActive = false
                });
            }
        }

        // Restore ChargesGainedOnKill to every flask — call when an enemy is killed.
        public void OnEnemyKilled()
        {
            RestoreKillCharges(_beltFlaskStates);
            RestoreKillCharges(_equipmentFlaskStates);
        }

        // Instantly fill all flasks to MaxCharges — call on game start / checkpoint.
        public void RefillAll()
        {
            RefillList(_beltFlaskStates);
            RefillList(_equipmentFlaskStates);
        }

        private static void RestoreKillCharges(List<BeltFlaskActiveState> states)
        {
            foreach (var state in states)
            {
                if (state.Flask == null) continue;
                state.CurrentCharges = Mathf.Min(state.Flask.MaxCharges,
                    state.CurrentCharges + state.Flask.ChargesGainedOnKill);
            }
        }

        private static void RefillList(List<BeltFlaskActiveState> states)
        {
            foreach (var state in states)
            {
                if (state.Flask == null) continue;
                state.CurrentCharges = state.Flask.MaxCharges;
            }
        }

        public void AddEquipmentFlask(FlaskItemData flask)
        {
            if (flask == null) return;
            // Avoid duplicates
            if (_equipmentFlaskStates.Exists(s => s.Flask == flask)) return;
            _equipmentFlaskStates.Add(new BeltFlaskActiveState
            {
                Flask = flask,
                CurrentCharges = flask.MaxCharges,
                TimeRemaining = 0f,
                IsActive = false
            });
        }

        public void RemoveEquipmentFlask(FlaskItemData flask)
        {
            if (flask == null) return;
            _equipmentFlaskStates.RemoveAll(s => s.Flask == flask);
        }

        // Returns combined movement speed multiplier from all active utility flasks.
        public float GetSpeedModifier()
        {
            float mod = 1f;
            foreach (var state in _beltFlaskStates)
            {
                if (state.IsActive && state.Flask.FlaskType == FlaskType.Utility && state.Flask.SpeedModifier != 1f)
                    mod *= state.Flask.SpeedModifier;
            }
            foreach (var state in _equipmentFlaskStates)
            {
                if (state.IsActive && state.Flask.FlaskType == FlaskType.Utility && state.Flask.SpeedModifier != 1f)
                    mod *= state.Flask.SpeedModifier;
            }
            return mod;
        }

        // Returns combined attack speed multiplier from all active utility flasks.
        public float GetAttackSpeedModifier()
        {
            float mod = 1f;
            foreach (var state in _beltFlaskStates)
            {
                if (state.IsActive && state.Flask.FlaskType == FlaskType.Utility && state.Flask.AttackSpeedModifier != 1f)
                    mod *= state.Flask.AttackSpeedModifier;
            }
            foreach (var state in _equipmentFlaskStates)
            {
                if (state.IsActive && state.Flask.FlaskType == FlaskType.Utility && state.Flask.AttackSpeedModifier != 1f)
                    mod *= state.Flask.AttackSpeedModifier;
            }
            return mod;
        }

        // Tick all flask states. Returns (lifeHeal, manaRestore) to apply this frame.
        public (float lifeHeal, float manaRestore) Tick(float deltaTime, float currentHealth, float maxHealth, float currentMana, float maxMana)
        {
            float lifeHeal = 0f;
            float manaRestore = 0f;

            TickList(_beltFlaskStates, deltaTime, currentHealth, maxHealth, currentMana, maxMana, ref lifeHeal, ref manaRestore);
            TickList(_equipmentFlaskStates, deltaTime, currentHealth, maxHealth, currentMana, maxMana, ref lifeHeal, ref manaRestore);

            return (lifeHeal, manaRestore);
        }

        private void TickList(List<BeltFlaskActiveState> states, float deltaTime,
            float currentHealth, float maxHealth, float currentMana, float maxMana,
            ref float lifeHeal, ref float manaRestore)
        {
            foreach (var state in states)
            {
                if (state.Flask == null) continue;

                if (state.IsActive)
                {
                    state.TimeRemaining -= deltaTime;

                    float recoverPerSec = state.Flask.RecoveryAmount / Mathf.Max(state.Flask.Duration, 0.01f);
                    if (state.Flask.FlaskType == FlaskType.Life)
                        lifeHeal += recoverPerSec * deltaTime;
                    else if (state.Flask.FlaskType == FlaskType.Mana)
                        manaRestore += recoverPerSec * deltaTime;

                    if (state.TimeRemaining <= 0f)
                        state.IsActive = false;
                }

                if (!state.IsActive && state.CurrentCharges >= state.Flask.ChargesUsedPerUse)
                {
                    if (ShouldAutoUse(state.Flask, currentHealth, maxHealth, currentMana, maxMana))
                    {
                        state.CurrentCharges -= state.Flask.ChargesUsedPerUse;
                        state.TimeRemaining = state.Flask.Duration;
                        state.IsActive = true;
                    }
                }

                if (state.CurrentCharges < state.Flask.MaxCharges)
                    state.CurrentCharges = Mathf.Min(state.Flask.MaxCharges, state.CurrentCharges + deltaTime);
            }
        }

        private static bool ShouldAutoUse(FlaskItemData flask, float currentHealth, float maxHealth, float currentMana, float maxMana)
        {
            switch (flask.AutoUseCondition)
            {
                case FlaskAutoUseCondition.WhenInjured:
                    return maxHealth > 0f && currentHealth < maxHealth * 0.6f;
                case FlaskAutoUseCondition.WhenManaLow:
                    return maxMana > 0f && currentMana < maxMana * 0.4f;
                case FlaskAutoUseCondition.WhenFullCharges:
                    return true;
                case FlaskAutoUseCondition.WhenHitRareOrUnique:
                    // Fallback: trigger based on flask type since rare/unique detection isn't available
                    if (flask.FlaskType == FlaskType.Life)
                        return maxHealth > 0f && currentHealth < maxHealth * 0.6f;
                    if (flask.FlaskType == FlaskType.Mana)
                        return maxMana > 0f && currentMana < maxMana * 0.4f;
                    return false;
                case FlaskAutoUseCondition.None:
                default:
                    return false;
            }
        }
    }
}
