using System;

namespace STG.CurveDash
{
    [Serializable]
    public class GemProgressEntry
    {
        public string AbilityKey;   // ability.name (ScriptableObject asset name) — unique per ability
        public int Level = 1;
        public long CurrentXP = 0;
    }
}
