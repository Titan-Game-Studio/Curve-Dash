using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class ShieldView : MonoBehaviour
    {
    }

    public class ShieldViewPool : MonoMemoryPool<ShieldView>
    {
        protected override void OnSpawned(ShieldView item)
        {
            item.gameObject.SetActive(true);
            base.OnSpawned(item);
        }

        protected override void OnDespawned(ShieldView item)
        {
            item.gameObject.SetActive(false);
            base.OnDespawned(item);
        }
    }
}
