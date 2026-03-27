using System.Collections.Generic;
using Herta;
using Saber.GAS.Foundation;
using Saber.GAS.Pooling;
using Saber.GAS.Runtime;
using Saber.GAS.Tags;

namespace Saber.GAS.Projectiles
{
    /// <summary>
    /// Defines how a projectile resolves its destination each tick.
    /// </summary>
    public enum CombatProjectileTrackingMode
    {
        /// <summary>
        /// Fly toward a fixed world point captured at spawn time.
        /// </summary>
        FixedPoint = 0,
        /// <summary>
        /// Continuously track a target actor's current position.
        /// </summary>
        TrackActor = 1,
    }

    /// <summary>
    /// Immutable-style spawn payload built from authoring data and attached to impact operations.
    /// </summary>
    public sealed class CombatProjectileSpawnDefinition
    {
        public CombatProjectileSpawnDefinition()
        {
            ImpactTags = new GameplayTagContainer();
            ImpactOperations = new List<CombatImpactOperation>();
            SpeedPerTick = FP._1;
            HitRadius = FP._0;
            MaxLifetimeTicks = 1;
        }

        /// <summary>
        /// Optional logical name for debugging and telemetry.
        /// </summary>
        public string Name { get; set; }

        public CombatProjectileTrackingMode TrackingMode { get; set; }

        public FP SpeedPerTick { get; set; }

        public FP HitRadius { get; set; }

        public long MaxLifetimeTicks { get; set; }

        public GameplayTagContainer ImpactTags { get; }

        /// <summary>
        /// Impact operations executed when the projectile hits.
        /// </summary>
        public IList<CombatImpactOperation> ImpactOperations { get; }
    }

    /// <summary>
    /// Runtime projectile entity stored in world state and included in snapshots.
    /// </summary>
    public sealed class CombatProjectileState : ICombatPoolable
    {
        public CombatProjectileState()
        {
            ImpactTags = new GameplayTagContainer();
            ImpactOperations = new List<CombatImpactOperation>();
        }

        public ProjectileInstanceId InstanceId { get; set; }

        public ActorId SourceActorId { get; set; }

        public ActorId TargetActorId { get; set; }

        public AbilityId AbilityId { get; set; }

        public EffectId SourceEffectId { get; set; }

        public ActionExecutionStage Stage { get; set; }

        public CombatProjectileTrackingMode TrackingMode { get; set; }

        public WorldPosition Position { get; set; }

        public WorldPosition FixedTargetPoint { get; set; }

        public bool HasFixedTargetPoint { get; set; }

        public WorldPosition LastResolvedTargetPoint { get; set; }

        public FP SpeedPerTick { get; set; }

        public FP HitRadius { get; set; }

        public SimulationTick SpawnTick { get; set; }

        public SimulationTick ExpireTick { get; set; }

        public GameplayTagContainer ImpactTags { get; }

        public IList<CombatImpactOperation> ImpactOperations { get; }

        /// <summary>
        /// Resets all runtime fields so this instance can be safely reused by object pools.
        /// </summary>
        public void ResetForPool()
        {
            InstanceId = ProjectileInstanceId.Empty;
            SourceActorId = ActorId.Empty;
            TargetActorId = ActorId.Empty;
            AbilityId = AbilityId.Empty;
            SourceEffectId = EffectId.Empty;
            Stage = ActionExecutionStage.Execute;
            TrackingMode = CombatProjectileTrackingMode.FixedPoint;
            Position = WorldPosition.Zero;
            FixedTargetPoint = WorldPosition.Zero;
            HasFixedTargetPoint = false;
            LastResolvedTargetPoint = WorldPosition.Zero;
            SpeedPerTick = FP._0;
            HitRadius = FP._0;
            SpawnTick = SimulationTick.Zero;
            ExpireTick = SimulationTick.Zero;
            ImpactTags.Clear();
            ImpactOperations.Clear();
        }
    }
}
