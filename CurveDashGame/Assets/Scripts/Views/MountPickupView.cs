using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class MountPickupView : MonoBehaviour
    {
        [HideInInspector]
        public int MountIndex;
        private GameObject spawnedVisual;
        
        public void Setup(int index, AssetManager assetManager)
        {
            MountIndex = index;
            if (spawnedVisual != null) Destroy(spawnedVisual);
            
            assetManager.LoadMountSkinAsync(index, prefab => 
            {
                if (this != null && prefab != null)
                {
                    spawnedVisual = Instantiate(prefab, transform);
                    
                    // Tự động tìm tâm của Mesh và dời về 0,0
                    var renderers = spawnedVisual.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        var bounds = renderers[0].bounds;
                        for (int i = 1; i < renderers.Length; i++)
                        {
                            bounds.Encapsulate(renderers[i].bounds);
                        }
                        
                        // Chuyển đổi center và min (đáy) về local space
                        Vector3 localCenter = spawnedVisual.transform.InverseTransformPoint(bounds.center);
                        Vector3 localMin = spawnedVisual.transform.InverseTransformPoint(bounds.min);
                        
                        // Căn giữa X và Z, nhưng để Y của đáy bằng 0 (chân chạm đất)
                        spawnedVisual.transform.localPosition = new Vector3(-localCenter.x, -localMin.y, -localCenter.z);
                    }
                    else
                    {
                        spawnedVisual.transform.localPosition = Vector3.zero;
                    }
                    
                    spawnedVisual.transform.localScale = Vector3.one * 0.5f; // Scale down for pickup
                }
            });
        }

        private void Update()
        {
            if (spawnedVisual != null)
            {
                spawnedVisual.transform.RotateAround(transform.position, Vector3.up, 100f * Time.deltaTime);
            }
        }
    }

    public class MountPickupViewPool : MemoryPool<MountPickupView> {}
}
