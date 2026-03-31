using Herta;
using Saber.GAS.Actors;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;
using Saber.GAS.RTS.Modules;

namespace Saber.GAS.RTS.Spatial
{
    /// <summary>
    /// 以八叉树为底层索引的 RTS 空间查询服务。
    /// </summary>
    public sealed class RtsOctreeSpatialQueryService : IRtsSpatialQueryService
    {
        private readonly RtsOctreeSpatialIndex _index;
        private CombatWorldState _cachedWorldState;
        private SimulationTick _cachedTick;
        private bool _hasBuilt;

        public RtsOctreeSpatialQueryService()
            : this(new RtsSpatialQueryOptions())
        {
        }

        public RtsOctreeSpatialQueryService(RtsSpatialQueryOptions options)
        {
            _index = new RtsOctreeSpatialIndex(options);
        }

        /// <summary>
        /// 手动重建当前世界的空间索引。
        /// 建议在同 Tick 内频繁改动单位位置后主动调用一次。
        /// </summary>
        public void Rebuild(CombatWorldState worldState)
        {
            if (worldState == null)
            {
                _index.Rebuild(null);
                _cachedWorldState = null;
                _cachedTick = SimulationTick.Zero;
                _hasBuilt = false;
                return;
            }

            _index.Rebuild(worldState.Actors);
            _cachedWorldState = worldState;
            _cachedTick = worldState.CurrentTick;
            _hasBuilt = true;
        }

        public void QueryActorsInRange(CombatWorldState worldState, WorldPosition center, FP radius, System.Collections.Generic.IList<CombatActorState> results)
        {
            if (results == null)
            {
                return;
            }

            if (worldState == null)
            {
                results.Clear();
                return;
            }

            EnsureIndexReady(worldState);
            _index.QuerySphere(center, radius, results);
        }

        public bool TryFindNearestActor(CombatWorldState worldState, WorldPosition center, FP maxDistance, out CombatActorState nearestActor)
        {
            nearestActor = null;
            if (worldState == null)
            {
                return false;
            }

            EnsureIndexReady(worldState);
            return _index.TryFindNearest(center, maxDistance, ActorId.Empty, out nearestActor);
        }

        public bool TryFindNearestActor(CombatWorldState worldState, CombatActorState sourceActor, FP maxDistance, out CombatActorState nearestActor)
        {
            nearestActor = null;
            if (worldState == null || sourceActor == null)
            {
                return false;
            }

            EnsureIndexReady(worldState);
            return _index.TryFindNearest(sourceActor.Position, maxDistance, sourceActor.ActorId, out nearestActor);
        }

        private void EnsureIndexReady(CombatWorldState worldState)
        {
            if (!_hasBuilt ||
                !ReferenceEquals(_cachedWorldState, worldState) ||
                !_cachedTick.Equals(worldState.CurrentTick))
            {
                Rebuild(worldState);
            }
        }
    }
}
