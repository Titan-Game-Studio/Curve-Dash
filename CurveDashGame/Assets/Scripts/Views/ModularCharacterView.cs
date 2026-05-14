using UnityEngine;
using System.Collections.Generic;

namespace STG.CurveDash
{
    public class ModularCharacterView : MonoBehaviour
    {
        [Header("Roots")]
        public Transform armorPartsRoot;
        public Transform facePartsRoot;
        public Transform RightHandSlot;
        public Transform LeftHandSlot;

        public System.Action OnAttackHitEvent;

        // Unity Animation Event receiver
        public void OnAttackHit()
        {
            OnAttackHitEvent?.Invoke();
        }


        private Dictionary<EquipmentSlot, Transform> _slotRoots = new Dictionary<EquipmentSlot, Transform>();

        private void Awake()
        {
            InitializeSlots();
        }

        private void InitializeSlots()
        {
            if (armorPartsRoot == null)
            {
                // Auto-healing / Auto-finding root if unassigned in inspector!
                var candidate = transform.Find("Armor Parts") ?? transform.Find("armorPartsRoot") ?? transform.Find("Armor");
                if (candidate != null) armorPartsRoot = candidate;
                else
                {
                    // Quét toàn bộ con cháu để tìm HEADS hoặc CHESTS
                    var allTransforms = GetComponentsInChildren<Transform>(true);
                    foreach (var t in allTransforms)
                    {
                        if (t.name == "HEADS" && t.parent != null)
                        {
                            armorPartsRoot = t.parent;
                            break;
                        }
                    }
                }
            }

            if (armorPartsRoot == null)
            {
                Debug.LogError("[ModularCharacterView] ArmorPartsRoot is null and could not be auto-discovered!");
                return;
            }

            // Map standard PoE slots to the GanzSe prefab structure
            // HEADS, CHESTS, ARMS, LEGS are the expected child names
            _slotRoots[EquipmentSlot.Head] = armorPartsRoot.Find("HEADS") ?? armorPartsRoot.Find("Head") ?? armorPartsRoot.Find("HEAD");
            _slotRoots[EquipmentSlot.Body] = armorPartsRoot.Find("CHESTS") ?? armorPartsRoot.Find("Torso") ?? armorPartsRoot.Find("Chest") ?? armorPartsRoot.Find("BODY");
            _slotRoots[EquipmentSlot.Hands] = armorPartsRoot.Find("ARMS") ?? armorPartsRoot.Find("HANDS") ?? armorPartsRoot.Find("Arms");
            _slotRoots[EquipmentSlot.Feet] = armorPartsRoot.Find("LEGS") ?? armorPartsRoot.Find("FEET") ?? armorPartsRoot.Find("Legs");
            
            Debug.Log($"[ModularCharacterView] InitializeSlots complete. Head: {_slotRoots[EquipmentSlot.Head] != null}, Body: {_slotRoots[EquipmentSlot.Body] != null}");
        }

        public void SetPart(EquipmentSlot slot, int partIndex)
        {
            SetPartByName(slot, null, partIndex);
        }

        public void SetPartByName(EquipmentSlot slot, string partName, int fallbackIndex = -1)
        {
            if (_slotRoots.Count == 0) InitializeSlots();
            if (!_slotRoots.ContainsKey(slot) || _slotRoots[slot] == null)
            {
                Debug.LogWarning($"[ModularCharacterView] Slot {slot} root not found! Cannot equip {partName}");
                return;
            }

            Transform root = _slotRoots[slot];
            
            // Disable all children first
            foreach (Transform child in root)
            {
                child.gameObject.SetActive(false);
            }

            bool found = false;
            if (!string.IsNullOrEmpty(partName))
            {
                foreach (Transform child in root)
                {
                    if (child.name.Equals(partName, System.StringComparison.OrdinalIgnoreCase) || 
                        child.name.StartsWith(partName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        child.gameObject.SetActive(true);
                        found = true;
                        Debug.Log($"<color=lime>[ModularCharacterView] Successfully enabled mesh part by name: '{child.name}' under '{root.name}'</color>");
                        break;
                    }
                }
            }

            if (!found && fallbackIndex >= 0 && fallbackIndex < root.childCount)
            {
                root.GetChild(fallbackIndex).gameObject.SetActive(true);
                found = true;
                Debug.Log($"<color=yellow>[ModularCharacterView] Fallback to index {fallbackIndex} under '{root.name}'</color>");
            }

            if (slot == EquipmentSlot.Head)
            {
                ToggleFaceVisibility(!found); // Show face if no helmet
            }
        }

        private void ToggleFaceVisibility(bool visible)
        {
            if (facePartsRoot != null)
            {
                facePartsRoot.gameObject.SetActive(visible);
            }
        }

        public void ClearAllParts()
        {
            foreach (var root in _slotRoots.Values)
            {
                if (root == null) continue;
                foreach (Transform child in root)
                {
                    child.gameObject.SetActive(false);
                }
            }
            ToggleFaceVisibility(true);
        }
    }
}
