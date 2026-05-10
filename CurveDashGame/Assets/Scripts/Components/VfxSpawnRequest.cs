using UnityEngine;

namespace STG.CurveDash
{
    /// <summary>
    /// ECS Component representing a request to spawn a VFX particle/effect.
    /// Used to route visual creation through the ECS lifecycle.
    /// </summary>
    public struct VfxSpawnRequest
    {
        public GameObject Prefab;
        public Vector3 Position;
        public Quaternion Rotation;
    }
}
