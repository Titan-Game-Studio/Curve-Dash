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

    // Manages the 3 flask slots on a Belt item, auto-using flasks like POE.
    // Charges regenerate at 1 charge/sec. Flask is auto-used when its condition is met.
    public class BeltFlaskService
    {
        private BeltItemData _equippedBelt;
        private readonly List<BeltFlaskActiveState> _flaskStates = new List<BeltFlaskActiveState>();

        public bool HasActiveBelt => _equippedBelt != null;
        public IReadOnlyList<BeltFlaskActiveState> FlaskStates => _flaskStates;

        public void SetBelt(BeltItemData belt)
        {
            _equippedBelt = belt;
            _flaskStates.Clear();

            if (belt == null) return;

            foreach (var flask in belt.FlaskSlots)
            {
                if (flask == null) continue;
                _flaskStates.Add(new BeltFlaskActiveState
                {
                    Flask = flask,
                    CurrentCharges = flask.MaxCharges,
                    TimeRemaining = 0f,
                    IsActive = false
                });
            }
        }

        // Returns combined movement speed multiplier from all active utility flasks.
        public float GetSpeedModifier()
        {
            float mod = 1f;
            foreach (var state in _flaskStates)
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
            foreach (var state in _flaskStates)
            {
                if (state.IsActive && state.Flask.FlaskType == FlaskType.Utility && state.Flask.AttackSpeedModifier != 1f)
                    mod *= state.Flask.AttackSpeedModifier;
            }
            return mod;
        }

        // Tick all flask states. Returns total life healing to apply this frame.
        // Call every Update frame with current player health values for condition checking.
        public float Tick(float deltaTime, float currentHealth, float maxHealth)
        {
            if (_equippedBelt == null) return 0f;

            float healingThisTick = 0f;

            foreach (var state in _flaskStates)
            {
                if (state.Flask == null) continue;

                // Tick active flask effects
                if (state.IsActive)
                {
                    state.TimeRemaining -= deltaTime;
                    if (state.Flask.FlaskType == FlaskType.Life)
                    {
                        float healPerSec = state.Flask.RecoveryAmount / Mathf.Max(state.Flask.Duration, 0.01f);
                        healingThisTick += healPerSec * deltaTime;
                    }
                    if (state.TimeRemaining <= 0f)
                        state.IsActive = false;
                }

                // Auto-use when charges are sufficient and condition is met
                if (!state.IsActive && state.CurrentCharges >= state.Flask.ChargesUsedPerUse)
                {
                    if (ShouldAutoUse(state.Flask, currentHealth, maxHealth))
                    {
                        state.CurrentCharges -= state.Flask.ChargesUsedPerUse;
                        state.TimeRemaining = state.Flask.Duration;
                        state.IsActive = true;
                    }
                }

                // Recharge 1 charge/sec
                if (state.CurrentCharges < state.Flask.MaxCharges)
                    state.CurrentCharges = Mathf.Min(state.Flask.MaxCharges, state.CurrentCharges + deltaTime);
            }

            return healingThisTick;
        }

        private static bool ShouldAutoUse(FlaskItemData flask, float currentHealth, float maxHealth)
        {
            switch (flask.AutoUseCondition)
            {
                case FlaskAutoUseCondition.WhenInjured:
                    return maxHealth > 0f && currentHealth < maxHealth * 0.6f;
                case FlaskAutoUseCondition.WhenFullCharges:
                    return true; // condition already checked (charges >= ChargesUsedPerUse)
                case FlaskAutoUseCondition.None:
                default:
                    return false;
            }
        }
    }
}
