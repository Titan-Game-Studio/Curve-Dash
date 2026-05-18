using System;

namespace STG.CurveDash
{
    public class EquipmentResolver : IEquipmentResolver
    {
        public LoadoutAssignment ResolveEquip(
            EquippableData currentLeft,
            EquippableData currentRight,
            EquippableData newItem)
        {
            return newItem switch
            {
                TwoHandedWeaponData twoH => new LoadoutAssignment
                    { LeftHand = twoH, RightHand = null, IsValid = true },

                BowData bow => new LoadoutAssignment
                    // Bow goes left; right hand kept as-is (SmartEquipService handles auto-pair)
                    { LeftHand = bow, RightHand = currentRight is OffHandData oh && oh.SubType == OffHandType.Arrow ? currentRight : null, IsValid = true },

                OneHandedWeaponData sword => ResolveOneHanded(sword, currentLeft, currentRight),

                OffHandData offhand when offhand.SubType == OffHandType.Arrow
                    => ResolveArrow(offhand, currentLeft, currentRight),

                OffHandData offhand when offhand.SubType == OffHandType.Shield
                    => ResolveShield(offhand, currentLeft, currentRight),

                _ => LoadoutAssignment.Invalid("Unknown equippable type")
            };
        }

        public LoadoutAssignment ResolveUnequip(
            EquippableData currentLeft,
            EquippableData currentRight,
            EquippableData removedItem)
        {
            if (removedItem == currentRight)
            {
                // SMART FIX Bug 2: nếu left có OneHandedWeapon → promote sang right
                var newRight = currentLeft is OneHandedWeaponData ? currentLeft : null;
                var newLeft  = currentLeft is OneHandedWeaponData ? null : currentLeft;
                return new LoadoutAssignment { LeftHand = newLeft, RightHand = newRight, IsValid = true };
            }
            if (removedItem == currentLeft)
                return new LoadoutAssignment { LeftHand = null, RightHand = currentRight, IsValid = true };

            return new LoadoutAssignment { LeftHand = currentLeft, RightHand = currentRight, IsValid = true };
        }

        private LoadoutAssignment ResolveOneHanded(
            OneHandedWeaponData sword, EquippableData left, EquippableData right)
        {
            if (right == null)
                return new LoadoutAssignment { LeftHand = left, RightHand = sword, IsValid = true };

            if (right is OneHandedWeaponData && left == null)
                return new LoadoutAssignment { LeftHand = sword, RightHand = right, IsValid = true };

            // Right bị chiếm → replace right
            return new LoadoutAssignment { LeftHand = left, RightHand = sword, IsValid = true };
        }

        private LoadoutAssignment ResolveArrow(
            OffHandData arrow, EquippableData left, EquippableData right)
        {
            // Arrow chỉ valid nếu left là Bow (SmartEquipService đảm bảo pre-load bow trước khi gọi resolver)
            if (left is BowData)
                return new LoadoutAssignment { LeftHand = left, RightHand = arrow, IsValid = true };

            return LoadoutAssignment.Invalid("No bow equipped or found in inventory");
        }

        private LoadoutAssignment ResolveShield(
            OffHandData shield, EquippableData left, EquippableData right)
        {
            // SMART FIX Bug 1: nếu left có OneHandedWeapon và right trống → move sword sang right
            if (left is OneHandedWeaponData leftSword && right == null)
                return new LoadoutAssignment { LeftHand = shield, RightHand = leftSword, IsValid = true };

            // 2H / Bow chiếm cả 2 tay → replace left, clear right
            if (left is TwoHandedWeaponData || left is BowData)
                return new LoadoutAssignment { LeftHand = shield, RightHand = null, IsValid = true };

            return new LoadoutAssignment { LeftHand = shield, RightHand = right, IsValid = true };
        }
    }
}
