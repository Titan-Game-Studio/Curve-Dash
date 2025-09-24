using System;
using UnityEngine;
using Zenject;
using Random = System.Random;

namespace STG.CurveDash
{
    
    [RequireComponent(typeof(Rigidbody))]
    public class BallView : MonoBehaviour
    {
        [SerializeField] private GameObject[] VFXTrailGOs;
        private GameObject VFXTrailGO;

        private void OnEnable()
        {
            int ranIndex = UnityEngine.Random.Range(0, VFXTrailGOs.Length);
            VFXTrailGO = VFXTrailGOs[ranIndex];
            VFXTrailGO.SetActive(true);
        }

        private void OnDisable()
        {
            VFXTrailGO.SetActive(false);
        }
    }
    
    public class BallViewFactory : PlaceholderFactory<BallView>
    {
    }
}