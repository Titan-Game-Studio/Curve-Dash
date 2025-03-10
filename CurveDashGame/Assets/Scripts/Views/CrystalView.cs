using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    [RequireComponent(typeof(Rigidbody))]
    public class CrystalView : MonoBehaviour
    {
    }
    
    public class CrystalViewPool : MonoMemoryPool<CrystalView>
    {
    }
}