// File: Assets/Scripts/Systems/GemSocket/IEquippedLoadout.cs
namespace STG.CurveDash
{
    using System.Collections.Generic;

    public interface IEquippedLoadout
    {
        // Weapon
        WeaponInstance GetWeapon();
        EquippableData GetRightHandItem();
        EquippableData GetLeftHandItem();

        // Armor — runtime sockets (không đọc từ ScriptableObject)
        List<AbilityData> GetArmorRuntimeSockets(EquipmentSlot slot);
        int GetArmorMaxSockets(EquipmentSlot slot);
        void AddArmorAbility(EquipmentSlot slot, AbilityData ability);
        void RemoveArmorAbility(EquipmentSlot slot, AbilityData ability);

        // Tên display của item đang trang bị tại slot (dùng cho log/UI)
        string GetEquippedItemName(EquipmentSlot slot);
    }
}
