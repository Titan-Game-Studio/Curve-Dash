using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class BlockView : MonoBehaviour
    {
    }

    public class BlockViewPool : MonoMemoryPool<BlockView>
    {
    }
}