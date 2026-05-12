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
            // Convert any local colliders on the ObstacleView itself to triggers
            foreach (var col in GetComponents<Collider>())
            {
                col.isTrigger = true;
            }

            if (_assetManager.ObstacleCount > 0)
            {
                int index = Random.Range(0, _assetManager.ObstacleCount);
                _assetManager.LoadObstacleAsync(index, prefab => { 
                    if (this != null) 
                    {
                        GameObject child = Instantiate(prefab, transform); 
                        
                        // Automatically convert all loaded child colliders to triggers so player runs straight through!
                        foreach (var col in child.GetComponentsInChildren<Collider>(true))
                        {
                            col.isTrigger = true;
                        }
                    }
                });
            }
        }
    }
    
    public class ObstacleViewPool : MonoMemoryPool<ObstacleView>
    {
    }
}