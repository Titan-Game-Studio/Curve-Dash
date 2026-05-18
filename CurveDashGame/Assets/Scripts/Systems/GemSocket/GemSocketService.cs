// File: Assets/Scripts/Systems/GemSocket/GemSocketService.cs
using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    public class GemSocketService : IGemSocketService
    {
        // Order of slot search for empty sockets
        private static readonly EquipmentSlot[] _socketSearchOrder = new[]
        {
            EquipmentSlot.MainHand,
            EquipmentSlot.Body,
            EquipmentSlot.Head,
            EquipmentSlot.Hands,
            EquipmentSlot.Feet,
            EquipmentSlot.OffHand
        };

        public GemSocketResult TrySocketGem(GemItemData gem, IEquippedLoadout loadout)
        {
            // 1. Try to find empty slot
            Log("Starting empty slot search");
            foreach (var slot in _socketSearchOrder)
            {
                // Weapon slot (MainHand) – use weapon dynamic abilities
                if (slot == EquipmentSlot.MainHand)
                {
                    var weapon = loadout.GetWeapon();
                    if (weapon != null && weapon.BaseData != null)
                    {
                        int max = weapon.BaseData.MaxSockets;
                        Log($"Weapon {weapon.BaseData.ItemName} sockets used: {weapon.DynamicAbilities.Count}/{max}");
                        if (weapon.DynamicAbilities.Count < max)
                        {
                            weapon.DynamicAbilities.Add(gem.EmbeddedAbility);
                            Log($"Socketed gem into weapon slot. Total now: {weapon.DynamicAbilities.Count}");
                            return new GemSocketResult
                            {
                                ResultType = GemSocketResultType.Socketed,
                                TargetItemName = weapon.BaseData.ItemName,
                                TargetSlotLabel = "MainHand Socket " + (weapon.DynamicAbilities.Count),
                                ReplacedAbility = null
                            };
                        }
                    }
                }
                else // Armor slots
                {
                    var current = loadout.GetArmorRuntimeSockets(slot);
                    int max = loadout.GetArmorMaxSockets(slot);
                    Log($"Armor slot {slot} sockets used: {current.Count}/{max}");
                    if (current.Count < max)
                    {
                        loadout.AddArmorAbility(slot, gem.EmbeddedAbility);
                        Log($"Socketed gem into armor slot {slot}. Total now: {current.Count + 1}");
                        return new GemSocketResult
                        {
                            ResultType = GemSocketResultType.Socketed,
                            TargetItemName = loadout.GetEquippedItemName(slot),
                            TargetSlotLabel = slot + " Socket " + (current.Count + 1),
                            ReplacedAbility = null
                        };
                    }
                }
            }

            // 2. No empty slot – try to replace first ability of same type (Support/Active)
            Log("No empty slots found, attempting replacement");
            foreach (var slot in _socketSearchOrder)
            {
                // Weapon replace
                if (slot == EquipmentSlot.MainHand)
                {
                    var weapon = loadout.GetWeapon();
                    if (weapon != null && weapon.DynamicAbilities.Count > 0)
                    {
                        for (int i = 0; i < weapon.DynamicAbilities.Count; i++)
                        {
                            var existing = weapon.DynamicAbilities[i];
                            if (IsSupport(existing) == IsGemSupport(gem))
                            {
                                var replaced = existing;
                                Log($"Replacing weapon ability at slot {i+1} with gem {gem.name}");
                                weapon.DynamicAbilities[i] = gem.EmbeddedAbility;
                                return new GemSocketResult
                                {
                                    ResultType = GemSocketResultType.Replaced,
                                    TargetItemName = weapon.BaseData.ItemName,
                                    TargetSlotLabel = "MainHand Socket " + (i + 1),
                                    ReplacedAbility = replaced
                                };
                            }
                        }
                    }
                }
                else // Armor replace
                {
                    var list = loadout.GetArmorRuntimeSockets(slot);
                    for (int i = 0; i < list.Count; i++)
                    {
                        var existing = list[i];
                        if (IsSupport(existing) == IsGemSupport(gem))
                        {
                            var replaced = existing;
                            Log($"Replacing armor ability in slot {slot} socket {i+1} with gem {gem.name}");
                            loadout.RemoveArmorAbility(slot, existing);
                            loadout.AddArmorAbility(slot, gem.EmbeddedAbility);
                            return new GemSocketResult
                            {
                                ResultType = GemSocketResultType.Replaced,
                                TargetItemName = loadout.GetEquippedItemName(slot),
                                TargetSlotLabel = slot + " Socket " + (i + 1),
                                ReplacedAbility = replaced
                            };
                        }
                    }
                }
            }

            // 3. Nothing compatible
            Log("No compatible slot found for gem");
            return new GemSocketResult
            {
                ResultType = GemSocketResultType.NoCompatibleSlot,
                TargetItemName = string.Empty,
                TargetSlotLabel = string.Empty,
                ReplacedAbility = null
            };
        }

        // Helper to decide if an ability is a Support gem
        private static bool IsSupport(AbilityData ability) => ability is SupportAbilityData;
        private static bool IsGemSupport(GemItemData gem) => gem.GemType == GemType.Support;

        // Centralized debug logger for this service
        private static void Log(string message)
        {
            Debug.Log($"[GemSocketService] {message}");
        }
    }
}
