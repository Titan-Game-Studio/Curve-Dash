using System;

namespace STG.CurveDash
{
    public class SmartEquipService
    {
        private readonly IEquipmentResolver _resolver;
        private readonly IInventoryScanner _scanner;

        public SmartEquipService(IEquipmentResolver resolver, IInventoryScanner scanner)
        {
            _resolver = resolver;
            _scanner = scanner;
        }

        public LoadoutAssignment SmartEquip(
            EquippableData currentLeft,
            EquippableData currentRight,
            EquippableData newItem)
        {
            // PRE-STEP: Arrow cần Bow; nếu left không có Bow → tìm trong inventory
            var (adjustedLeft, adjustedRight) = PreloadRequiredPartner(currentLeft, currentRight, newItem);

            // RESOLVE: tính hand assignment
            var assignment = _resolver.ResolveEquip(adjustedLeft, adjustedRight, newItem);
            if (!assignment.IsValid) return assignment;

            // POST-STEP: nếu slot đối tác vẫn trống sau resolve → tìm partner trong inventory
            return AutoFillEmptyPartnerSlot(assignment, newItem);
        }

        public LoadoutAssignment SmartUnequip(
            EquippableData currentLeft,
            EquippableData currentRight,
            EquippableData removedItem)
            => _resolver.ResolveUnequip(currentLeft, currentRight, removedItem);

        // --- Private ---

        private (EquippableData left, EquippableData right) PreloadRequiredPartner(
            EquippableData left, EquippableData right, EquippableData newItem)
        {
            // Chỉ Arrow yêu cầu partner bắt buộc (Bow)
            if (newItem is OffHandData arrow && arrow.SubType == OffHandType.Arrow
                && left is not BowData)
            {
                var bow = _scanner.FindFirstBow();
                if (bow != null) left = bow;
            }
            return (left, right);
        }

        private LoadoutAssignment AutoFillEmptyPartnerSlot(LoadoutAssignment assignment, EquippableData newItem)
        {
            switch (newItem)
            {
                // Bow → scan for Arrow nếu right trống
                case BowData when assignment.RightHand == null:
                    assignment.RightHand = _scanner.FindFirstArrow();
                    break;

                // Arrow: partner (Bow) đã được preload, không cần xử lý thêm

                // One-Handed Sword → scan for Shield nếu left trống
                case OneHandedWeaponData when assignment.LeftHand == null:
                    assignment.LeftHand = _scanner.FindFirstShield();
                    break;

                // Shield → scan for Sword nếu right trống
                case OffHandData offhand when offhand.SubType == OffHandType.Shield
                    && assignment.RightHand == null:
                    assignment.RightHand = _scanner.FindFirstOneHandedWeapon();
                    break;
            }
            return assignment;
        }
    }
}
