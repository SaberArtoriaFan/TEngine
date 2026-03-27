using Herta;
using Saber.GAS.Actors;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;

namespace Saber.GAS.Projectiles
{
    /// <summary>
    /// Handles projectile spawn operations and deterministic projectile simulation.
    /// </summary>
    public sealed class CombatProjectileModule : ICombatRuleModule, ICombatImpactResolver
    {
        public void Initialize(CombatRuntime runtime)
        {
        }

        public void Shutdown(CombatRuntime runtime)
        {
        }

        public void EvaluateAbilityAttempt(CombatActionAttempt attempt, CombatWorldState worldState)
        {
        }

        public bool TryBlockEffectApplication(
            CombatActorState targetActor,
            EffectDefinition effectDefinition,
            CombatWorldState worldState,
            out string reason)
        {
            reason = null;
            return false;
        }

        public bool HasRuntimeEffectPayload(EffectDefinition effectDefinition)
        {
            return false;
        }

        public void OnEffectApplied(CombatActorState targetActor, ActiveEffect activeEffect, CombatRuntime runtime)
        {
        }

        public void OnEffectRefreshed(CombatActorState targetActor, ActiveEffect activeEffect, CombatRuntime runtime)
        {
        }

        public void OnEffectRemoving(CombatActorState targetActor, ActiveEffect activeEffect, CombatRuntime runtime)
        {
        }

        public void OnTick(CombatRuntime runtime)
        {
            if (runtime == null)
            {
                return;
            }

            var projectiles = runtime.WorldState.ProjectileStates;
            for (var i = projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = projectiles[i];
                if (projectile == null)
                {
                    projectiles.RemoveAt(i);
                    continue;
                }

                if (!TryResolveTargetPoint(projectile, runtime.WorldState, out var targetPoint))
                {
                    ExpireProjectile(runtime, projectiles, i, projectile);
                    continue;
                }

                if (HasReachedTarget(projectile.Position, targetPoint, projectile.HitRadius))
                {
                    HitProjectile(runtime, projectiles, i, projectile, targetPoint);
                    continue;
                }

                if (IsExpired(projectile, runtime.WorldState.CurrentTick))
                {
                    ExpireProjectile(runtime, projectiles, i, projectile);
                    continue;
                }

                AdvanceProjectile(projectile, targetPoint);
                if (HasReachedTarget(projectile.Position, targetPoint, projectile.HitRadius))
                {
                    HitProjectile(runtime, projectiles, i, projectile, targetPoint);
                    continue;
                }

                if (IsExpired(projectile, runtime.WorldState.CurrentTick))
                {
                    ExpireProjectile(runtime, projectiles, i, projectile);
                }
            }
        }

        public void ProcessResourceDelta(CombatResourceDeltaContext context)
        {
        }

        public bool CanResolve(CombatImpactOperation operation)
        {
            return operation != null && operation.Type == CombatImpactOperationType.SpawnProjectile;
        }

        public void Resolve(CombatActionAttempt attempt, CombatImpact impact, CombatImpactOperation operation, CombatRuntime runtime)
        {
            if (runtime == null || impact == null || operation?.Projectile == null)
            {
                return;
            }

            CombatActorState sourceActor;
            if (!runtime.WorldState.TryGetActor(impact.SourceActorId, out sourceActor))
            {
                return;
            }

            var initialTargetPoint = ResolveInitialTargetPoint(attempt, impact, operation.Projectile, runtime.WorldState);
            if (!initialTargetPoint.HasValue)
            {
                return;
            }

            var projectile = runtime.RentProjectileState();
            projectile.ResetForPool();
            projectile.InstanceId = runtime.WorldState.CreateProjectileInstanceId();
            projectile.SourceActorId = impact.SourceActorId;
            projectile.TargetActorId = impact.TargetActorId;
            projectile.AbilityId = impact.AbilityId;
            projectile.SourceEffectId = impact.EffectId;
            projectile.Stage = impact.Stage;
            projectile.TrackingMode = impact.TargetActorId.IsEmpty
                ? CombatProjectileTrackingMode.FixedPoint
                : operation.Projectile.TrackingMode;
            projectile.Position = sourceActor.Position;
            projectile.FixedTargetPoint = initialTargetPoint.Value;
            projectile.HasFixedTargetPoint = true;
            projectile.LastResolvedTargetPoint = initialTargetPoint.Value;
            projectile.SpeedPerTick = operation.Projectile.SpeedPerTick;
            projectile.HitRadius = operation.Projectile.HitRadius;
            projectile.SpawnTick = runtime.WorldState.CurrentTick;
            projectile.ExpireTick = runtime.WorldState.CurrentTick + operation.Projectile.MaxLifetimeTicks;
            projectile.ImpactTags.AddRange(impact.Tags);
            projectile.ImpactTags.AddRange(operation.Projectile.ImpactTags);

            for (var operationIndex = 0; operationIndex < operation.Projectile.ImpactOperations.Count; operationIndex++)
            {
                projectile.ImpactOperations.Add(CloneImpactOperation(operation.Projectile.ImpactOperations[operationIndex]));
            }

            runtime.WorldState.AddProjectile(projectile);
            runtime.PublishEvent(CombatEventKind.ProjectileSpawned, impact.SourceActorId, projectile.InstanceId.ToString());
        }

        private static WorldPosition? ResolveInitialTargetPoint(
            CombatActionAttempt attempt,
            CombatImpact impact,
            CombatProjectileSpawnDefinition definition,
            CombatWorldState worldState)
        {
            if (definition != null &&
                definition.TrackingMode == CombatProjectileTrackingMode.TrackActor &&
                !impact.TargetActorId.IsEmpty &&
                worldState.TryGetActor(impact.TargetActorId, out var trackedActor))
            {
                return trackedActor.Position;
            }

            var targetPoint = attempt?.Request?.TargetData?.TargetPoint;
            if (targetPoint.HasValue)
            {
                return targetPoint.Value;
            }

            if (!impact.TargetActorId.IsEmpty && worldState.TryGetActor(impact.TargetActorId, out var targetActor))
            {
                return targetActor.Position;
            }

            return null;
        }

        private static bool TryResolveTargetPoint(CombatProjectileState projectile, CombatWorldState worldState, out WorldPosition targetPoint)
        {
            targetPoint = projectile == null ? WorldPosition.Zero : projectile.LastResolvedTargetPoint;
            if (projectile == null)
            {
                return false;
            }

            // TrackActor projectiles require a live actor target. If the target disappears,
            // the projectile should expire instead of falling back to a fixed point.
            if (projectile.TrackingMode == CombatProjectileTrackingMode.TrackActor)
            {
                if (projectile.TargetActorId.IsEmpty)
                {
                    return false;
                }

                if (worldState.TryGetActor(projectile.TargetActorId, out var trackedActor))
                {
                    projectile.LastResolvedTargetPoint = trackedActor.Position;
                    targetPoint = trackedActor.Position;
                    return true;
                }

                return false;
            }

            if (!projectile.HasFixedTargetPoint)
            {
                return false;
            }

            targetPoint = projectile.FixedTargetPoint;
            projectile.LastResolvedTargetPoint = projectile.FixedTargetPoint;
            return true;
        }

        private static void AdvanceProjectile(CombatProjectileState projectile, WorldPosition targetPoint)
        {
            if (projectile == null || projectile.SpeedPerTick <= FP._0)
            {
                return;
            }

            var deltaX = targetPoint.X - projectile.Position.X;
            var deltaY = targetPoint.Y - projectile.Position.Y;
            var deltaZ = targetPoint.Z - projectile.Position.Z;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ);
            if (distanceSquared <= FP._0)
            {
                projectile.Position = targetPoint;
                return;
            }

            var distance = FPMath.Sqrt(distanceSquared);
            if (distance <= projectile.SpeedPerTick)
            {
                projectile.Position = targetPoint;
                return;
            }

            var ratio = projectile.SpeedPerTick / distance;
            projectile.Position = new WorldPosition(
                projectile.Position.X + (deltaX * ratio),
                projectile.Position.Y + (deltaY * ratio),
                projectile.Position.Z + (deltaZ * ratio));
        }

        private static bool HasReachedTarget(WorldPosition position, WorldPosition targetPoint, FP hitRadius)
        {
            var radius = hitRadius < FP._0 ? FP._0 : hitRadius;
            return WorldPosition.DistanceSquared(position, targetPoint) <= (radius * radius);
        }

        private static bool IsExpired(CombatProjectileState projectile, SimulationTick currentTick)
        {
            return projectile == null ||
                (projectile.ExpireTick > SimulationTick.Zero && currentTick > projectile.ExpireTick);
        }

        private static void ExpireProjectile(
            CombatRuntime runtime,
            System.Collections.Generic.IList<CombatProjectileState> projectiles,
            int index,
            CombatProjectileState projectile)
        {
            projectiles.RemoveAt(index);
            runtime.PublishEvent(CombatEventKind.ProjectileExpired, projectile.SourceActorId, projectile.InstanceId.ToString());
            runtime.ReturnProjectileState(projectile);
        }

        private static void HitProjectile(
            CombatRuntime runtime,
            System.Collections.Generic.IList<CombatProjectileState> projectiles,
            int index,
            CombatProjectileState projectile,
            WorldPosition targetPoint)
        {
            var targetActorId = projectile.TargetActorId;
            if (!targetActorId.IsEmpty && !runtime.WorldState.TryGetActor(targetActorId, out _))
            {
                targetActorId = ActorId.Empty;
            }

            // Route hit payload back through the standard impact pipeline so
            // mutators, semantics, and triggers keep a single execution path.
            runtime.ResolveImpactOperationsDirect(
                projectile.SourceActorId,
                targetActorId,
                targetPoint,
                projectile.AbilityId,
                projectile.SourceEffectId,
                projectile.Stage,
                projectile.ImpactTags,
                projectile.ImpactOperations);

            projectiles.RemoveAt(index);
            runtime.PublishEvent(CombatEventKind.ProjectileHit, projectile.SourceActorId, projectile.InstanceId.ToString());
            runtime.ReturnProjectileState(projectile);
        }

        private static CombatImpactOperation CloneImpactOperation(CombatImpactOperation source)
        {
            if (source == null)
            {
                return null;
            }

            return new CombatImpactOperation
            {
                Type = source.Type,
                ResourceId = source.ResourceId,
                EffectId = source.EffectId,
                Tag = source.Tag,
                Amount = source.Amount,
                EffectSpec = source.EffectSpec == null
                    ? null
                    : new EffectSpec(
                        source.EffectSpec.Definition,
                        source.EffectSpec.SourceActorId,
                        source.EffectSpec.TargetActorId,
                        source.EffectSpec.AppliedTick,
                        source.EffectSpec.Stacks),
                CueName = source.CueName,
                Projectile = CloneProjectileDefinition(source.Projectile),
                Payload = source.Payload,
            };
        }

        private static CombatProjectileSpawnDefinition CloneProjectileDefinition(CombatProjectileSpawnDefinition source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new CombatProjectileSpawnDefinition
            {
                Name = source.Name,
                TrackingMode = source.TrackingMode,
                SpeedPerTick = source.SpeedPerTick,
                HitRadius = source.HitRadius,
                MaxLifetimeTicks = source.MaxLifetimeTicks,
            };

            clone.ImpactTags.AddRange(source.ImpactTags);
            for (var operationIndex = 0; operationIndex < source.ImpactOperations.Count; operationIndex++)
            {
                clone.ImpactOperations.Add(CloneImpactOperation(source.ImpactOperations[operationIndex]));
            }

            return clone;
        }
    }
}
