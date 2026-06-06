using Leopotam.EcsLite;

namespace STG.CurveDash
{
    /// <summary>
    /// Lightweight, read-only accessor for the singleton <see cref="GameStateComponent"/>.
    /// Lets gameplay systems gate their per-frame logic on the current <see cref="GameState"/>
    /// (e.g. "only run while Playing") without depending on the heavier <see cref="GameSystem"/>,
    /// which would create a circular Zenject dependency (GameSystem already injects several systems).
    /// </summary>
    public class GameStateService
    {
        private readonly EcsFilter filter;
        private readonly EcsPool<GameStateComponent> pool;

        public GameStateService(EcsWorld world)
        {
            filter = world.Filter<GameStateComponent>().End();
            pool = world.GetPool<GameStateComponent>();
        }

        /// <summary>True once a GameStateComponent exists (scene has been initialized).</summary>
        public bool HasState => filter.GetEntitiesCount() > 0;

        /// <summary>
        /// Current game state. Falls back to <see cref="GameState.Title"/> before the
        /// scene's GameStateComponent is created, so systems stay safely paused at boot.
        /// </summary>
        public GameState Current
        {
            get
            {
                if (filter.GetEntitiesCount() == 0) return GameState.Title;
                return pool.Get(filter.GetRawEntities()[0]).State;
            }
        }

        /// <summary>True only while gameplay is actively running (the world is "live").</summary>
        public bool IsPlaying => Current == GameState.Playing;
    }
}
