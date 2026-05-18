using System;

namespace STG.CurveDash
{
    public struct LoadoutAssignment
    {
        public EquippableData LeftHand;   // null = clear slot
        public EquippableData RightHand;  // null = clear slot
        public bool IsValid;
        public string InvalidReason;

        public static LoadoutAssignment Invalid(string reason) =>
            new LoadoutAssignment { IsValid = false, InvalidReason = reason };
    }
}
