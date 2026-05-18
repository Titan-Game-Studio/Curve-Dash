// File: Assets/Scripts/Models/GemSocketResult.cs
using System;
namespace STG.CurveDash
{
    public enum GemSocketResultType { Socketed, Replaced, NoCompatibleSlot }

    public struct GemSocketResult
    {
        public GemSocketResultType ResultType;
        public string TargetItemName;   // e.g. "Long Sword", "Plate Vest"
        public string TargetSlotLabel;  // e.g. "MainHand Socket 2", "Body Socket 1"
        public AbilityData ReplacedAbility; // null if ResultType == Socketed
        public bool Success => ResultType != GemSocketResultType.NoCompatibleSlot;
    }
}
