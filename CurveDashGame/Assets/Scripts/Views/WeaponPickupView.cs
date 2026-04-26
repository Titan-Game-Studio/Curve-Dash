using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class WeaponPickupView : MonoBehaviour
    {
        [HideInInspector]
        public WeaponData WeaponToGive;
        
        private GameObject spawnedVisual;

        public void Setup(WeaponData data)
        {
            WeaponToGive = data;

            // Clear old visual if pooling
            if (spawnedVisual != null)
            {
                Destroy(spawnedVisual);
            }

            if (data != null && data.RightHandModel != null)
            {
                spawnedVisual = Instantiate(data.RightHandModel, transform);
                
                // Tự động tìm tâm của Mesh và dời về 0,0
                var renderers = spawnedVisual.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                    // Dời model sao cho tâm bounds trùng với vị trí gốc của cha
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
                // Xoay quanh tâm của cha (vị trí đã được căn giữa mesh)
                spawnedVisual.transform.RotateAround(transform.position, Vector3.up, 100f * Time.deltaTime);
            }
        }
    }

    public class WeaponPickupViewPool : MemoryPool<WeaponPickupView>
    {
    }
}
