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


        private Dictionary<EquipmentSlot, Transform> _slotRoots = new Dictionary<EquipmentSlot, Transform>();

        private void Awake()
        {
            InitializeSlots();
        }

        private void InitializeSlots()
        {
            if (armorPartsRoot == null) return;

            // Map standard PoE slots to the GanzSe prefab structure
            // HEADS, CHESTS, HANDS, LEGS are the expected child names
            _slotRoots[EquipmentSlot.Head] = armorPartsRoot.Find("HEADS");
            _slotRoots[EquipmentSlot.Body] = armorPartsRoot.Find("CHESTS");
            _slotRoots[EquipmentSlot.Hands] = armorPartsRoot.Find("HANDS");
            _slotRoots[EquipmentSlot.Feet] = armorPartsRoot.Find("LEGS");
        }

        public void SetPart(EquipmentSlot slot, int partIndex)
        {
            if (!_slotRoots.ContainsKey(slot) || _slotRoots[slot] == null) return;

            Transform root = _slotRoots[slot];
            
            // Disable all children first
            foreach (Transform child in root)
            {
                child.gameObject.SetActive(false);
            }

            // Enable the specific index if it exists
            if (partIndex >= 0 && partIndex < root.childCount)
            {
                root.GetChild(partIndex).gameObject.SetActive(true);
            }
            
            // Special logic for helmet/face toggle if needed
            if (slot == EquipmentSlot.Head)
            {
                ToggleFaceVisibility(partIndex < 0); // Show face if no helmet
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
