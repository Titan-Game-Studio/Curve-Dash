using System;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

namespace STG.CurveDash.Views
{
    [RequireComponent(typeof(Rigidbody))]
    public class ObstacleView : MonoBehaviour
    {
        [Inject] private AssetManager _assetManager;

        private void Start()
        {
            if (_assetManager.ObstacleCount > 0)
            {
                int index = Random.Range(0, _assetManager.ObstacleCount);
                _assetManager.LoadObstacleAsync(index, prefab => { 
                    if (this != null) Instantiate(prefab, transform); 
                });
            }
        }
    }
    
    public class ObstacleViewPool : MonoMemoryPool<ObstacleView>
    {
    }
}