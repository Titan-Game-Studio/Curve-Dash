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
            // TODO: Refacter.
            int index = Random.Range(0, 7);
            _assetManager.LoadObstacleAsync(index, prefab => { Instantiate(prefab, transform); });
        }
    }
    
    public class ObstacleViewPool : MonoMemoryPool<ObstacleView>
    {
    }
}