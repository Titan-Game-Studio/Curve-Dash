using System;

namespace STG.CurveDash
{
    public interface IEquipmentResolver
    {
        LoadoutAssignment ResolveEquip(
            EquippableData currentLeft,
            EquippableData currentRight,
            EquippableData newItem);

        LoadoutAssignment ResolveUnequip(
            EquippableData currentLeft,
            EquippableData currentRight,
            EquippableData removedItem);
    }
}
