using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class WeaponPickupView : MonoBehaviour
    {
        [HideInInspector]
        public EquippableData ItemToGive;
        
        private GameObject spawnedVisual;

        public void Setup(EquippableData data)
        {
            ItemToGive = data;

            if (spawnedVisual != null)
            {
                Destroy(spawnedVisual);
            }

            if (data != null && data.VisualModel != null)
            {
                spawnedVisual = Instantiate(data.VisualModel, transform);

                
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
        }

        private void Update()
        {
            if (spawnedVisual != null)
            {
                spawnedVisual.transform.RotateAround(transform.position, Vector3.up, 100f * Time.deltaTime);
            }
        }
    }

    public class WeaponPickupViewPool : MemoryPool<WeaponPickupView>
    {
    }
}
