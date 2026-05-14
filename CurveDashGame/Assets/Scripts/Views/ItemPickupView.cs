using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class ItemPickupView : MonoBehaviour
    {
        [HideInInspector]
        public ItemData ItemToGive;
        
        private GameObject spawnedVisual;

        public void Setup(ItemData data)
        {
            ItemToGive = data;

            if (spawnedVisual != null)
            {
                Destroy(spawnedVisual);
            }

            if (data != null)
            {
                // Set the name of the GameObject to the item's name
                string displayName = !string.IsNullOrEmpty(data.ItemName) ? data.ItemName : data.name;
                gameObject.name = displayName;

                GameObject modelPrefab = null;
                if (data is EquippableData equippable)
                {
                    modelPrefab = equippable.VisualModel;
                }

                if (modelPrefab == null && data.DevionAdapter != null)
                {
                    modelPrefab = data.DevionAdapter.Prefab;
                }

                if (modelPrefab != null)
                {
                    spawnedVisual = Instantiate(modelPrefab, transform);
                    
                    // Set scale to around 0.5f for 3D prefabs as well to keep them visible
                    spawnedVisual.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

                    var renderers = spawnedVisual.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        var bounds = renderers[0].bounds;
                        for (int i = 1; i < renderers.Length; i++)
                        {
                            bounds.Encapsulate(renderers[i].bounds);
                        }
                        Vector3 localCenter = spawnedVisual.transform.InverseTransformPoint(bounds.center);
                        spawnedVisual.transform.localPosition = -localCenter;
                    }
                    else
                    {
                        spawnedVisual.transform.localPosition = Vector3.zero;
                    }
                }
                else
                {
                    // Choose distinctive primitive shape based on item category with size around 0.5
                    PrimitiveType shapeType = PrimitiveType.Cube;
                    Vector3 shapeScale = new Vector3(0.5f, 0.5f, 0.5f);
                    Quaternion customRotation = Quaternion.identity;
                    Color customColor = Color.white;
                    bool hasCustomColor = false;

                    if (data is GemItemData)
                    {
                        shapeType = PrimitiveType.Sphere;
                        shapeScale = new Vector3(0.5f, 0.5f, 0.5f); // Sphere
                        customColor = new Color(0.1f, 0.9f, 0.4f); // Beautiful Emerald Green
                        hasCustomColor = true;
                    }
                    else if (data is FlaskItemData)
                    {
                        shapeType = PrimitiveType.Capsule; // Capsule pill shape
                        shapeScale = new Vector3(0.35f, 0.35f, 0.35f); // Beautiful pill proportion (capsule is naturally taller)
                        customColor = new Color(0.9f, 0.1f, 0.2f); // Healing red potion
                        hasCustomColor = true;
                    }
                    else if (data is CurrencyItemData)
                    {
                        shapeType = PrimitiveType.Cube; // Cube standing on vertex (diamond)
                        shapeScale = new Vector3(0.5f, 0.5f, 0.5f);
                        customRotation = Quaternion.Euler(45f, 45f, 0f); // Rotate X and Y to point a corner straight down
                        customColor = new Color(1.0f, 0.75f, 0.0f); // Bright Gold
                        hasCustomColor = true;
                    }
                    else if (data is ArmorItemData)
                    {
                        shapeType = PrimitiveType.Cube;
                        shapeScale = new Vector3(0.5f, 0.5f, 0.5f);
                        customColor = new Color(0.5f, 0.5f, 0.5f); // Iron/Steel Gray
                        hasCustomColor = true;
                    }

                    GameObject placeholder = GameObject.CreatePrimitive(shapeType);
                    placeholder.transform.localScale = shapeScale;
                    placeholder.transform.localRotation = customRotation;

                    // Remove collider from the spawned visual to prevent physics overlap issues
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
                                case ItemRarity.Magic: finalColor = new Color(0.2f, 0.6f, 1.0f); break; // Beautiful Blue
                                case ItemRarity.Rare: finalColor = new Color(1.0f, 0.85f, 0.0f); break; // Glorious Yellow/Gold
                                case ItemRarity.Unique: finalColor = new Color(1.0f, 0.4f, 0.0f); break; // Epic Orange
                            }
                        }
                        
                        renderer.material.color = finalColor;
                    }
                    
                    spawnedVisual = placeholder;
                    placeholder.transform.SetParent(transform, false);
                    placeholder.transform.localPosition = Vector3.zero;
                }
            }
        }

        private void Update()
        {
            if (spawnedVisual != null)
            {
                // Rotate to make it look active and premium
                spawnedVisual.transform.Rotate(Vector3.up, 100f * Time.deltaTime, Space.World);
            }
        }
    }

    public class ItemPickupViewPool : MemoryPool<ItemPickupView>
    {
    }
}
