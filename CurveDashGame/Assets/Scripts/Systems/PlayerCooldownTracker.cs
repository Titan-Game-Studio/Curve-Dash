using UnityEngine;

namespace STG.CurveDash
{
    /// <summary>
    /// Live attack-cooldown state published by <see cref="CombatSystem"/> for cooldown UI. In this
    /// auto-attack combat model the "cooldown" is the cadence between attacks (<c>1 / attack speed</c>),
    /// which is also exactly how often the socketed active skill fires — so the action bar visualises it
    /// as a radial sweep over each skill slot. Exposed statically so the (non-injected) action-bar overlay
    /// can read it without going through Zenject.
    /// </summary>
    public static class PlayerCooldownTracker
    {
        /// <summary>Seconds left until the next attack/cast is ready.</summary>
        public static float Remaining { get; private set; }

        /// <summary>Full cooldown length of the current cadence (1 / attack speed).</summary>
        public static float Total { get; private set; }

        /// <summary>0 = ready, 1 = just fired. Drives the radial fill amount.</summary>
        public static float Fraction =>
            (Total > 0.0001f && Remaining > 0f) ? Mathf.Clamp01(Remaining / Total) : 0f;

        public static bool OnCooldown => Fraction > 0f;

        public static void Report(float remaining, float total)
        {
            Remaining = remaining > 0f ? remaining : 0f;
            Total = total > 0f ? total : 0f;
        }

        /// <summary>Clear when the world is frozen (Title / Game Over) so the UI shows no stale sweep.</summary>
        public static void Reset()
        {
            Remaining = 0f;
            Total = 0f;
        }
    }
}
