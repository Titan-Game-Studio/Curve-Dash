using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    /// <summary>
    /// Lightweight, allocation-free bridge that exposes the player's currently-active self-buffs
    /// (looping auras + active flask effects) to UI that lives outside the Zenject context — namely
    /// <see cref="CharacterStatsPanel"/>, which self-instantiates and can't be injected.
    ///
    /// The systems that OWN each buff push a live snapshot here every frame (<see cref="AuraSystem"/>
    /// for auras, <see cref="BeltFlaskService"/> for flasks); the panel simply reads the lists when it
    /// refreshes. The backing lists are reused, so per-frame updates don't allocate.
    /// </summary>
    public static class ActiveBuffTracker
    {
        public struct AuraBuff
        {
            public string Name;
            public Sprite Icon;
            public PoEElementType Element;
            public float Remaining;   // seconds left in the current cast before the aura auto-recasts
            public float Duration;    // full (effective) cast length, for the depleting time bar
        }

        public struct FlaskBuff
        {
            public string Name;
            public Sprite Icon;
            public FlaskType Type;
            public float Remaining;             // seconds left on the active effect
            public float Duration;              // full effect length (for the depleting time bar)
            public List<StatModifier> Buffs;    // stat modifiers granted while active (may be null/empty)
        }

        public static readonly List<AuraBuff> Auras = new List<AuraBuff>();
        public static readonly List<FlaskBuff> Flasks = new List<FlaskBuff>();

        public static bool HasAny => Auras.Count > 0 || Flasks.Count > 0;
    }
}
