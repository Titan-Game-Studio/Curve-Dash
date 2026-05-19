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
            // RESOLVE: tính hand assignment
            return _resolver.ResolveEquip(currentLeft, currentRight, newItem);
        }

        public LoadoutAssignment SmartUnequip(
            EquippableData currentLeft,
            EquippableData currentRight,
            EquippableData removedItem)
            => _resolver.ResolveUnequip(currentLeft, currentRight, removedItem);
    }
}
