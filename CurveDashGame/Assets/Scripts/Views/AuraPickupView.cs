using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class AuraPickupView : MonoBehaviour
    {
        [HideInInspector]
        public int AuraIndex;
        private GameObject spawnedVisual;
        
        public void Setup(int index, AssetManager assetManager)
        {
            AuraIndex = index;
            if (spawnedVisual != null) Destroy(spawnedVisual);
            
            assetManager.LoadAuraAsync(index, prefab => 
            {
                if (this != null && prefab != null)
                {
                    spawnedVisual = Instantiate(prefab, transform);
                    // Lệch tâm một chút để tạo hiệu ứng đẹp khi xoay
                    spawnedVisual.transform.localPosition = new Vector3(0.5f, 0, 0);
                    spawnedVisual.transform.localScale = Vector3.one * 0.5f; 
                }
            });
        }

        private void Update()
        {
            // Xoay thằng cha để trail bay vòng vòng
            transform.Rotate(Vector3.up, 200f * Time.deltaTime);
        }
    }

    public class AuraPickupViewPool : MemoryPool<AuraPickupView> {}
}
