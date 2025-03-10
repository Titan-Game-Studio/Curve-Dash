using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    [RequireComponent(typeof(Rigidbody))]
    public class BlockPartView : MonoBehaviour
    {
    }
    
    public class BlockPartViewFactory : PlaceholderFactory<BlockPartView>
    {
    }
}