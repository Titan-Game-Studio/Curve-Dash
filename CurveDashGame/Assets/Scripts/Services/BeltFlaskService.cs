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

        // Raised whenever the set of currently-active flask buffs may have changed: a flask activated or
        // expired, or one was equipped/unequipped. Consumers (PlayerView) re-apply the aggregated buffs to
        // the character sheet on this signal, so application is event-driven, not polled every frame.
        public event System.Action ActiveBuffsChanged;
        private bool _activeSetDirty;

        public void SetBelt(BeltItemData belt)
        {
            _equippedBelt = belt;
            _beltFlaskStates.Clear();

            if (belt != null)
            {
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

            // A belt swap can drop active flasks → re-apply the (now smaller) active-buff set immediately.
            ActiveBuffsChanged?.Invoke();
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
            // New flask starts inactive (no buff yet), but signal anyway so consumers stay in sync.
            ActiveBuffsChanged?.Invoke();
        }

        public void RemoveEquipmentFlask(FlaskItemData flask)
        {
            if (flask == null) return;
            // Fire immediately (not via Tick) so an unequipped-while-active flask's buff is cleared even when
            // it was the last flask — once HasAnyFlask is false, Tick stops running and would never signal.
            if (_equipmentFlaskStates.RemoveAll(s => s.Flask == flask) > 0)
                ActiveBuffsChanged?.Invoke();
        }

        // Aggregates the stat modifiers of every CURRENTLY-ACTIVE flask. This is the single, data-driven
        // source of flask buffs: sheet stats are pushed to the character sheet by the consumer (via
        // AffixStatMapper), while ECS systems read specific StatTypes via SumActiveValue. A new flask effect
        // needs only data — a Buffs entry on the asset — and no new code anywhere.
        public List<StatModifier> GetActiveModifiers()
        {
            var result = new List<StatModifier>();
            CollectActiveModifiers(_beltFlaskStates, result);
            CollectActiveModifiers(_equipmentFlaskStates, result);
            return result;
        }

        private static void CollectActiveModifiers(List<BeltFlaskActiveState> states, List<StatModifier> dest)
        {
            foreach (var state in states)
            {
                if (!state.IsActive || state.Flask == null || state.Flask.Buffs == null) continue;
                foreach (var mod in state.Flask.Buffs)
                    if (mod != null) dest.Add(mod);
            }
        }

        // Summed Value of one StatType across all active flasks. Used by ECS systems (movement/combat) that
        // consume a stat directly instead of from the Devion sheet — e.g. AddedMovementSpeed (PlayerMovement)
        // and IncreasedAttackSpeed (Combat). Returns 0 when no active flask grants it.
        public float SumActiveValue(StatType type)
        {
            return SumActiveValue(_beltFlaskStates, type) + SumActiveValue(_equipmentFlaskStates, type);
        }

        private static float SumActiveValue(List<BeltFlaskActiveState> states, StatType type)
        {
            float sum = 0f;
            foreach (var state in states)
            {
                if (!state.IsActive || state.Flask == null || state.Flask.Buffs == null) continue;
                foreach (var mod in state.Flask.Buffs)
                    if (mod != null && mod.Type == type) sum += mod.Value;
            }
            return sum;
        }

        // Tick all flask states. Returns (lifeHeal, manaRestore) to apply this frame.
        public (float lifeHeal, float manaRestore) Tick(float deltaTime, float currentHealth, float maxHealth, float currentMana, float maxMana)
        {
            float lifeHeal = 0f;
            float manaRestore = 0f;

            TickList(_beltFlaskStates, deltaTime, currentHealth, maxHealth, currentMana, maxMana, ref lifeHeal, ref manaRestore);
            TickList(_equipmentFlaskStates, deltaTime, currentHealth, maxHealth, currentMana, maxMana, ref lifeHeal, ref manaRestore);

            // Fire once per frame after both lists ticked, so a flask flipping active/inactive this frame
            // re-applies the aggregated buffs exactly once (avoids mutating subscribers mid-iteration).
            if (_activeSetDirty)
            {
                _activeSetDirty = false;
                ActiveBuffsChanged?.Invoke();
            }

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
                    {
                        state.IsActive = false;
                        _activeSetDirty = true; // effect expired → buffs must be removed
                    }
                }

                if (!state.IsActive && state.CurrentCharges >= state.Flask.ChargesUsedPerUse)
                {
                    if (ShouldAutoUse(state.Flask, currentHealth, maxHealth, currentMana, maxMana))
                    {
                        state.CurrentCharges -= state.Flask.ChargesUsedPerUse;
                        state.TimeRemaining = state.Flask.Duration;
                        state.IsActive = true;
                        _activeSetDirty = true; // effect started → buffs must be applied
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
