using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    [RequireComponent(typeof(Rigidbody))]
    public class BallView : MonoBehaviour
    {
    }
    
    public class BallViewFactory : PlaceholderFactory<BallView>
    {
    }
}