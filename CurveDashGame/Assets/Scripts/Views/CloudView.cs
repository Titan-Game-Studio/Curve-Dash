using UnityEngine;
using Zenject;

namespace STG.CurveDash.Views
{
    public class CloudView : MonoBehaviour
    {
        [Inject] private AssetManager _assetManager;

        private void Start()
        {
            if (_assetManager.CloudCount > 0)
            {
                int index = Random.Range(0, _assetManager.CloudCount);
                _assetManager.LoadCloudAsync(index, prefab => { 
                    if (this != null) Instantiate(prefab, transform); 
                });
            }
        }
    }
    
    public class CloudViewPool : MonoMemoryPool<CloudView>
    {
    }
}
