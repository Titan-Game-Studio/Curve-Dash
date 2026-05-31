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
                // Song kiếm: bỏ tay phải → promote tay trái lên tay phải
                var newRight = currentLeft is OneHandedWeaponData ? currentLeft : null;
                var newLeft  = currentLeft is OneHandedWeaponData ? null : currentLeft;
                return new LoadoutAssignment { LeftHand = newLeft, RightHand = newRight, IsValid = true };
            }
            if (removedItem == currentLeft)
            {
                // Bỏ Cung → Tên ở tay phải không còn ý nghĩa, phải xóa luôn
                var newRight = (currentLeft is BowData && currentRight is OffHandData oh && oh.SubType == OffHandType.Arrow)
                    ? null
                    : currentRight;
                return new LoadoutAssignment { LeftHand = null, RightHand = newRight, IsValid = true };
            }

            return new LoadoutAssignment { LeftHand = currentLeft, RightHand = currentRight, IsValid = true };
        }

        private LoadoutAssignment ResolveOneHanded(
            OneHandedWeaponData sword, EquippableData left, EquippableData right)
        {
            // Vũ khí 2 tay và Cung đều không thể dùng chung với kiếm 1 tay — clear cả hai
            // (Cung chỉ pair với Tên, không pair với Kiếm)
            if (left is TwoHandedWeaponData || left is BowData)
                left = null;

            // Nếu tay phải trống, ưu tiên tay phải
            if (right == null)
                return new LoadoutAssignment { LeftHand = left, RightHand = sword, IsValid = true };

            // Nếu tay phải đang cầm khiên, đẩy khiên sang tay trái (nếu tay trái trống) và cầm kiếm tay phải
            if (right is OffHandData off && off.SubType == OffHandType.Shield && left == null)
                return new LoadoutAssignment { LeftHand = right, RightHand = sword, IsValid = true };

            // Nếu tay phải đang cầm kiếm, và tay trái trống → Song kiếm
            if (right is OneHandedWeaponData && left == null)
                return new LoadoutAssignment { LeftHand = sword, RightHand = right, IsValid = true };

            // Mặc định thay thế tay phải
            return new LoadoutAssignment { LeftHand = left, RightHand = sword, IsValid = true };
        }

        private LoadoutAssignment ResolveArrow(
            OffHandData arrow, EquippableData left, EquippableData right)
        {
            // Arrow luôn đi với Bow ở tay trái
            if (left is BowData)
                return new LoadoutAssignment { LeftHand = left, RightHand = arrow, IsValid = true };

            return LoadoutAssignment.Invalid("No bow equipped");
        }

        private LoadoutAssignment ResolveShield(
            OffHandData shield, EquippableData left, EquippableData right)
        {
            // Nếu tay trái đang cầm Cung hoặc Vũ khí 2 tay → xóa cả hai tay
            // (Cung mang theo Tên ở tay phải; Vũ khí 2 tay chiếm cả hai tay)
            if (left is BowData || left is TwoHandedWeaponData)
                return new LoadoutAssignment { LeftHand = shield, RightHand = null, IsValid = true };

            // Nếu tay phải đang cầm Tên nhưng không còn Cung → Tên vô nghĩa, xóa đi
            var newRight = (right is OffHandData oh && oh.SubType == OffHandType.Arrow) ? null : right;

            // Shield luôn đặt vào tay trái, giữ nguyên tay phải (nếu là kiếm 1 tay)
            return new LoadoutAssignment { LeftHand = shield, RightHand = newRight, IsValid = true };
        }
    }
}
