using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class ItemPickupView : MonoBehaviour
    {
        [HideInInspector]
        public ItemData ItemToGive;

        // Rolled loot payload (weapons dropped by monsters). Empty for plain/base pickups.
        [HideInInspector] public System.Collections.Generic.List<StatModifier> RolledAffixes;
        [HideInInspector] public ItemRarity RolledRarity = ItemRarity.Normal;
        [HideInInspector] public int RolledItemLevel;

        private GameObject spawnedVisual;

        /// <summary>Attach pre-rolled affixes so they persist into the inventory/equipment on pickup.</summary>
        public void SetRolledLoot(ItemRarity rarity, int itemLevel, System.Collections.Generic.List<StatModifier> affixes)
        {
            RolledRarity = rarity;
            RolledItemLevel = itemLevel;
            RolledAffixes = affixes != null ? new System.Collections.Generic.List<StatModifier>(affixes) : null;
        }

        public void Setup(ItemData data)
        {
            ItemToGive = data;

            // Clear any stale rolled payload from a previous pooled use.
            RolledAffixes = null;
            RolledRarity = ItemRarity.Normal;
            RolledItemLevel = 0;

            if (spawnedVisual != null)
            {
                Destroy(spawnedVisual);
                spawnedVisual = null;
            }

            if (data != null)
            {
                // Set the name of the GameObject to the item's name
                string displayName = !string.IsNullOrEmpty(data.ItemName) ? data.ItemName : data.name;
                gameObject.name = displayName;

                GameObject modelPrefab = null;

#if UNITY_EDITOR
                if (data.Prefab != null && data.Prefab.editorAsset != null)
                {
                    modelPrefab = (GameObject)data.Prefab.editorAsset;
                }
                
                // Fallback to VisualModel for weapons if Prefab is not assigned in the editor yet
                if (modelPrefab == null && data is EquippableData equippable)
                {
                    modelPrefab = equippable.VisualModel;
                }
#endif

                if (modelPrefab != null)
                {
                    // Instant synchronous instantiation in Editor for flawless workflow
                    spawnedVisual = Instantiate(modelPrefab, transform);
                    ConfigureSpawnedVisual(spawnedVisual);
                }
                else if (data.Prefab != null && data.Prefab.RuntimeKeyIsValid())
                {
                    // Asynchronous instantiation in built player / runtime
                    data.Prefab.InstantiateAsync(transform).Completed += handle => {
                        if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                        {
                            // In case ItemPickupView was destroyed or Setup was called again before loading finished
                            if (this == null || ItemToGive != data)
                            {
                                if (handle.Result != null) Destroy(handle.Result);
                                return;
                            }

                            if (spawnedVisual != null) Destroy(spawnedVisual);
                            spawnedVisual = handle.Result;
                            ConfigureSpawnedVisual(spawnedVisual);
                        }
                        else
                        {
                            SpawnPlaceholder(data);
                        }
                    };
                }
                else
                {
                    SpawnPlaceholder(data);
                }

                // Guaranteed Trigger SphereCollider on root object for flawless pickup detection
                var triggerCollider = gameObject.GetComponent<SphereCollider>();
                if (triggerCollider == null) triggerCollider = gameObject.AddComponent<SphereCollider>();
                triggerCollider.isTrigger = true;
                triggerCollider.radius = 1.0f;
            }
        }

        private void ConfigureSpawnedVisual(GameObject visual)
        {
            if (visual == null) return;

            visual.transform.localScale = Vector3.one;

            // Remove all child colliders from instantiated 3D models so they don't block player movement
            var childColliders = visual.GetComponentsInChildren<Collider>();
            foreach (var cCollider in childColliders)
            {
                if (cCollider != null) Destroy(cCollider);
            }

            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
                Vector3 localCenter = visual.transform.InverseTransformPoint(bounds.center);
                visual.transform.localPosition = -localCenter;
            }
            else
            {
                visual.transform.localPosition = Vector3.zero;
            }
        }

        private void SpawnPlaceholder(ItemData data)
        {
            if (spawnedVisual != null) return;

            // Choose distinctive primitive shape based on item category with size 1.0
            PrimitiveType shapeType = PrimitiveType.Cube;
            Vector3 shapeScale = Vector3.one;
            Quaternion customRotation = Quaternion.identity;
            Color customColor = Color.white;
            bool hasCustomColor = false;

            if (data is GemItemData)
            {
                shapeType = PrimitiveType.Sphere;
                shapeScale = Vector3.one; // Sphere
                customColor = new Color(0.1f, 0.9f, 0.4f); // Emerald Green
                hasCustomColor = true;
            }
            else if (data is FlaskItemData)
            {
                shapeType = PrimitiveType.Capsule; // Capsule pill shape
                shapeScale = new Vector3(0.7f, 0.7f, 0.7f);
                customColor = new Color(0.9f, 0.1f, 0.2f); // Healing red
                hasCustomColor = true;
            }
            else if (data is CurrencyItemData)
            {
                shapeType = PrimitiveType.Cube; // Diamond
                shapeScale = Vector3.one;
                customRotation = Quaternion.Euler(45f, 45f, 0f);
                customColor = new Color(1.0f, 0.75f, 0.0f); // Gold
                hasCustomColor = true;
            }

            GameObject placeholder = GameObject.CreatePrimitive(shapeType);
            placeholder.transform.localScale = shapeScale;
            placeholder.transform.localRotation = customRotation;

            var placeholderCollider = placeholder.GetComponent<Collider>();
            if (placeholderCollider != null) Destroy(placeholderCollider);

            var renderer = placeholder.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color finalColor = Color.white;
                if (hasCustomColor)
                {
                    finalColor = customColor;
                }
                else
                {
                    switch (data.Rarity)
                    {
                        case ItemRarity.Magic: finalColor = new Color(0.2f, 0.6f, 1.0f); break;
                        case ItemRarity.Rare: finalColor = new Color(1.0f, 0.85f, 0.0f); break;
                        case ItemRarity.Unique: finalColor = new Color(1.0f, 0.4f, 0.0f); break;
                    }
                }
                renderer.material.color = finalColor;
            }
            
            spawnedVisual = placeholder;
            placeholder.transform.SetParent(transform, false);
            placeholder.transform.localPosition = Vector3.zero;
        }

        private void Update()
        {
            // Rotation disabled as requested
        }
    }

    public class ItemPickupViewPool : MemoryPool<ItemPickupView>
    {
    }
}
