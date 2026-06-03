using Leopotam.EcsLite;
using UnityEngine;

namespace STG.CurveDash
{
    /// <summary>
    /// Single source of truth for the run's "area level" (a.k.a. dungeon depth). It drives how strong
    /// spawned monsters are and what item level / affix tiers freshly generated loot can roll.
    /// Currently the area level equals the player's character level; swap the formula here to retune
    /// progression (e.g. distance travelled or score) without touching any consumer.
    /// </summary>
    public class AreaLevelService
    {
        private readonly EcsFilter statFilter;
        private readonly EcsPool<PlayerStatComponent> statPool;

        public AreaLevelService(EcsWorld world)
        {
            statFilter = world.Filter<PlayerStatComponent>().End();
            statPool = world.GetPool<PlayerStatComponent>();
        }

        public int CurrentAreaLevel
        {
            get
            {
                foreach (var entity in statFilter)
                    return Mathf.Max(1, statPool.Get(entity).Level);
                return 1;
            }
        }
    }
}
