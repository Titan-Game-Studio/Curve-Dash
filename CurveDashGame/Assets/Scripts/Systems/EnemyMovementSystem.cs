using Leopotam.EcsLite;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class EnemyMovementSystem : ITickable
    {
        private readonly EcsFilter _enemyFilter;
        private readonly EcsPool<EnemyComponent> _enemyPool;
        private readonly EcsPool<ViewLinkComponent> _viewLinkPool;

        public EnemyMovementSystem(EcsWorld world)
        {
            _enemyFilter = world.Filter<EnemyComponent>().Inc<ViewLinkComponent>().Exc<EnemyDeadEvent>().End();
            _enemyPool = world.GetPool<EnemyComponent>();
            _viewLinkPool = world.GetPool<ViewLinkComponent>();
        }

        public void Tick()
        {
            foreach (var entity in _enemyFilter)
            {
                ref var enemy = ref _enemyPool.Get(entity);
                ref var viewLink = ref _viewLinkPool.Get(entity);

                if (viewLink.Transform != null)
                {
                    // Quái vật di chuyển ngược lại hướng người chơi (hoặc về phía người chơi)
                    // Ở đây ta cho nó di chuyển theo hướng -Forward của nó hoặc hướng cố định
                    viewLink.Transform.Translate(Vector3.back * enemy.MoveSpeed * Time.deltaTime, Space.Self);
                }
            }
        }
    }
}
