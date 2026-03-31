using System;
using System.Collections.Generic;
using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;
using Saber.GAS.RTS.Modules;
using Saber.GAS.RTS.Relations;

namespace Saber.GAS.RTS.Spatial
{
    /// <summary>
    /// 面向 RTS 的目标解析器：
    /// - Area: 使用八叉树按半径选择目标。
    /// - Actor: 在未指定显式目标列表时，支持“最近单位”自动选择。
    /// 其他模式回退到默认解析器。
    /// </summary>
    public sealed class RtsSpatialTargetingResolver : ITargetingResolver
    {
        private readonly IRtsSpatialQueryService _spatialQueryService;
        private readonly ICombatActorRelationResolver _relationResolver;
        private readonly ITargetingResolver _fallbackResolver;
        private readonly List<CombatActorState> _candidateBuffer = new List<CombatActorState>();

        /// <summary>
        /// 获取或设置 Area 目标默认半径解析器。
        /// 默认返回 ability.Targeting.MaxRange。
        /// </summary>
        public Func<AbilityDefinition, FP> AreaRadiusResolver { get; set; } = ability => ability == null ? FP._0 : ability.Targeting.MaxRange;

        public RtsSpatialTargetingResolver(
            IRtsSpatialQueryService spatialQueryService,
            ICombatActorRelationResolver relationResolver,
            ITargetingResolver fallbackResolver = null)
        {
            _spatialQueryService = spatialQueryService ?? throw new ArgumentNullException(nameof(spatialQueryService));
            _relationResolver = relationResolver ?? new RtsTeamRelationResolver();
            _fallbackResolver = fallbackResolver ?? new DefaultTargetingResolver();
        }

        public void ResolveTargets(
            CombatWorldState worldState,
            CombatActorState source,
            AbilityDefinition ability,
            AbilityTargetData targetData,
            IList<CombatActorState> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();

            if (worldState == null || source == null || ability == null)
            {
                return;
            }

            if (ability.Targeting.Kind == AbilityTargetKind.Area)
            {
                ResolveAreaTargets(worldState, source, ability, targetData, results);
                return;
            }

            if (ability.Targeting.Kind == AbilityTargetKind.Actor &&
                (targetData == null || targetData.TargetActorIds.Count == 0))
            {
                ResolveNearestActorTargets(worldState, source, ability, targetData, results);
                return;
            }

            _fallbackResolver.ResolveTargets(worldState, source, ability, targetData, results);
        }

        private void ResolveAreaTargets(
            CombatWorldState worldState,
            CombatActorState source,
            AbilityDefinition ability,
            AbilityTargetData targetData,
            IList<CombatActorState> results)
        {
            if (targetData == null || !targetData.TargetPoint.HasValue)
            {
                return;
            }

            var radius = AreaRadiusResolver == null ? FP._0 : AreaRadiusResolver(ability);
            if (radius <= FP._0)
            {
                return;
            }

            _spatialQueryService.QueryActorsInRange(worldState, targetData.TargetPoint.Value, radius, _candidateBuffer);
            for (var i = 0; i < _candidateBuffer.Count; i++)
            {
                var candidate = _candidateBuffer[i];
                if (!CanTarget(worldState, source, candidate, ability))
                {
                    continue;
                }

                results.Add(candidate);
            }
        }

        private void ResolveNearestActorTargets(
            CombatWorldState worldState,
            CombatActorState source,
            AbilityDefinition ability,
            AbilityTargetData targetData,
            IList<CombatActorState> results)
        {
            var searchCenter = targetData != null && targetData.TargetPoint.HasValue
                ? targetData.TargetPoint.Value
                : source.Position;

            CombatActorState nearestActor = null;
            var nearestDistanceSquared = FP.MaxValue;

            var maxRange = ability.Targeting.MaxRange;
            if (maxRange > FP._0)
            {
                _spatialQueryService.QueryActorsInRange(worldState, searchCenter, maxRange, _candidateBuffer);
                for (var i = 0; i < _candidateBuffer.Count; i++)
                {
                    TryPromoteNearest(worldState, source, ability, searchCenter, _candidateBuffer[i], ref nearestActor, ref nearestDistanceSquared);
                }
            }
            else
            {
                foreach (var candidate in worldState.Actors)
                {
                    TryPromoteNearest(worldState, source, ability, searchCenter, candidate, ref nearestActor, ref nearestDistanceSquared);
                }
            }

            if (nearestActor != null)
            {
                results.Add(nearestActor);
            }
        }

        private void TryPromoteNearest(
            CombatWorldState worldState,
            CombatActorState source,
            AbilityDefinition ability,
            WorldPosition searchCenter,
            CombatActorState candidate,
            ref CombatActorState nearestActor,
            ref FP nearestDistanceSquared)
        {
            if (!CanTarget(worldState, source, candidate, ability))
            {
                return;
            }

            var distanceSquared = WorldPosition.DistanceSquared(searchCenter, candidate.Position);
            if (distanceSquared >= nearestDistanceSquared)
            {
                return;
            }

            nearestDistanceSquared = distanceSquared;
            nearestActor = candidate;
        }

        private bool CanTarget(CombatWorldState worldState, CombatActorState source, CombatActorState target, AbilityDefinition ability)
        {
            if (source == null || target == null || ability == null)
            {
                return false;
            }

            if (!target.Tags.ContainsAll(ability.Targeting.RequiredTargetTags))
            {
                return false;
            }

            if (target.Tags.ContainsAny(ability.Targeting.BlockedTargetTags))
            {
                return false;
            }

            return MatchesTargetFlags(worldState, source, target, ability.Targeting.AllowedFlags);
        }

        private bool MatchesTargetFlags(CombatWorldState worldState, CombatActorState sourceActor, CombatActorState targetActor, AbilityTargetFlags flags)
        {
            if (flags == AbilityTargetFlags.None)
            {
                return true;
            }

            var requiredRelations = GetRequiredTargetRelations(flags);
            if (requiredRelations != CombatActorRelationFlags.None)
            {
                var relation = _relationResolver.ResolveRelation(worldState, sourceActor, targetActor);
                if ((requiredRelations & relation) == 0)
                {
                    return false;
                }
            }

            var hasLifeConstraint =
                (flags & AbilityTargetFlags.Dead) != 0 ||
                (flags & AbilityTargetFlags.Alive) != 0;

            if (!hasLifeConstraint)
            {
                return true;
            }

            var lifeMatched = false;
            if ((flags & AbilityTargetFlags.Dead) != 0 && !targetActor.IsAlive)
            {
                lifeMatched = true;
            }

            if ((flags & AbilityTargetFlags.Alive) != 0 && targetActor.IsAlive)
            {
                lifeMatched = true;
            }

            return lifeMatched;
        }

        private static CombatActorRelationFlags GetRequiredTargetRelations(AbilityTargetFlags flags)
        {
            var relations = CombatActorRelationFlags.None;

            if ((flags & AbilityTargetFlags.Self) != 0)
            {
                relations |= CombatActorRelationFlags.Self;
            }

            if ((flags & AbilityTargetFlags.Ally) != 0)
            {
                relations |= CombatActorRelationFlags.Ally;
            }

            if ((flags & AbilityTargetFlags.Enemy) != 0)
            {
                relations |= CombatActorRelationFlags.Enemy;
            }

            if ((flags & AbilityTargetFlags.Neutral) != 0)
            {
                relations |= CombatActorRelationFlags.Neutral;
            }

            return relations;
        }
    }
}
