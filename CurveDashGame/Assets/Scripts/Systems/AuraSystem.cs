using Leopotam.EcsLite;
using UnityEngine;
using Zenject;
using System.Collections.Generic;

namespace STG.CurveDash
{
    /// <summary>
    /// Handles Aura abilities as persistent, duration-based self-buffs.
    /// Unlike active skills, auras are NOT cast when the player hits a monster.
    /// Instead, while an aura is equipped (on the weapon or armor) this system casts it,
    /// keeps it active for its <see cref="PoEAbility.Duration"/>, and recasts it once the
    /// effect expires — looping for as long as the aura stays equipped.
    /// </summary>
    public class AuraSystem : ITickable
    {
        private readonly EcsWorld world;

        private readonly EcsFilter playerFilter;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        private readonly GameStateService gameState;

        // Remaining effect time per currently-equipped aura. <= 0 means "recast this frame".
        private readonly Dictionary<PoEAbility, float> _remaining = new Dictionary<PoEAbility, float>();

        // Reused per tick to detect auras that got unequipped so we can drop their timers.
        private readonly List<PoEAbility> _activeAuras = new List<PoEAbility>();
        private readonly List<PoEAbility> _staleAuras = new List<PoEAbility>();

        public AuraSystem(EcsWorld world, GameStateService gameState)
        {
            this.world = world;
            this.gameState = gameState;

            playerFilter = world.Filter<PlayerComponent>().Inc<PlayerCombatComponent>().Inc<ViewLinkComponent>().End();
            viewLinkPool = world.GetPool<ViewLinkComponent>();
        }

        public void Tick()
        {
            // Auras are gameplay self-buffs — don't cast their VFX while the world is frozen.
            if (!gameState.IsPlaying) return;

            float dt = Time.deltaTime;

            foreach (var playerEntity in playerFilter)
            {
                ref var playerView = ref viewLinkPool.Get(playerEntity);
                if (playerView.Transform == null) continue;

                var playerGo = playerView.Transform.gameObject;
                var playerViewComponent = playerView.Transform.GetComponent<PlayerView>();

                // Collect every aura currently equipped (weapon sockets + armor sockets).
                _activeAuras.Clear();
                CollectAuras(playerViewComponent, _activeAuras);

                // Tick / recast each equipped aura.
                foreach (var aura in _activeAuras)
                {
                    if (!_remaining.TryGetValue(aura, out float timeLeft))
                    {
                        // Newly equipped aura → cast immediately.
                        timeLeft = 0f;
                    }

                    timeLeft -= dt;

                    if (timeLeft <= 0f)
                    {
                        // Effect expired (or first time) → recast at the player's position.
                        aura.Execute(playerGo, playerView.Transform.position);
                        // Duration modifiers (the aura's own SelfModifiers + compatible support gems) extend it.
                        timeLeft = Mathf.Max(0.1f, aura.Duration * EffectiveDurationMultiplier(aura, playerViewComponent));
                    }

                    _remaining[aura] = timeLeft;
                }

                // Forget timers for auras that are no longer equipped.
                _staleAuras.Clear();
                foreach (var kvp in _remaining)
                {
                    if (!_activeAuras.Contains(kvp.Key))
                        _staleAuras.Add(kvp.Key);
                }
                foreach (var stale in _staleAuras)
                    _remaining.Remove(stale);
            }
        }

        // Combined Duration multiplier: the aura's own SelfModifiers plus every compatible socketed
        // support gem (weapon + armor). 1.0 when nothing modifies duration.
        private float EffectiveDurationMultiplier(PoEAbility aura, PlayerView pv)
        {
            float mult = SupportAbilityData.Multiplier(aura.SelfModifiers, SupportStat.Duration);
            if (pv == null) return mult;

            if (pv.CurrentWeaponInstance != null)
            {
                var weaponAbilities = pv.CurrentWeaponInstance.GetAbilities();
                if (weaponAbilities != null)
                {
                    foreach (var ab in weaponAbilities)
                        if (ab is SupportAbilityData s && s.IsCompatible(aura)) mult *= s.GetDurationMultiplier();
                }
            }
            foreach (var ab in pv.GetEquippedArmorAbilities())
                if (ab is SupportAbilityData s && s.IsCompatible(aura)) mult *= s.GetDurationMultiplier();

            return mult;
        }

        private void CollectAuras(PlayerView playerViewComponent, List<PoEAbility> result)
        {
            if (playerViewComponent == null) return;

            if (playerViewComponent.CurrentWeaponInstance != null)
            {
                var weaponAbilities = playerViewComponent.CurrentWeaponInstance.GetAbilities();
                if (weaponAbilities != null)
                {
                    foreach (var ab in weaponAbilities)
                    {
                        if (ab is PoEAbility active && active.SkillType == PoEAbilityType.Aura && !result.Contains(active))
                            result.Add(active);
                    }
                }
            }

            foreach (var ab in playerViewComponent.GetEquippedArmorAbilities())
            {
                if (ab is PoEAbility active && active.SkillType == PoEAbilityType.Aura && !result.Contains(active))
                    result.Add(active);
            }
        }
    }
}
