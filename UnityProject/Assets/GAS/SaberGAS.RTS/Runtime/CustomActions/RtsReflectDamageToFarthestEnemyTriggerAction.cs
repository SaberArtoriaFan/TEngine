using System;
using System.Collections.Generic;
using Herta;
using Saber.GAS.Actors;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;
using Saber.GAS.Tags;
using Saber.GAS.Triggers;
using Saber.GAS.RTS.Modules;
using Saber.GAS.RTS.Relations;
using Saber.GAS.RTS.Spatial;

namespace Saber.GAS.RTS.CustomActions
{
    /// <summary>
    /// “受伤后把伤害反给最远敌人”的自定义动作配置。
    /// </summary>
    public sealed class RtsReflectDamageToFarthestEnemyPayload
    {
        /// <summary>
        /// 反伤倍率。1 表示等额反伤。
        /// </summary>
        public FP ReflectionRatio { get; set; } = FP._1;

        /// <summary>
        /// 最大搜索距离。
        /// 小于等于 0 表示全图搜索。
        /// </summary>
        public FP MaxDistance { get; set; } = FP._0;
    }

    /// <summary>
    /// RTS 示例自定义 TriggerAction。
    /// 当拥有者受到伤害时，把伤害按倍率反给“最远的敌方单位”。
    /// </summary>
    public sealed class RtsReflectDamageToFarthestEnemyTriggerAction : ICombatCustomTriggerAction
    {
        /// <summary>
        /// 该自定义动作暴露给 TriggerActionDefinition 使用的稳定 Id。
        /// </summary>
        public const int ActionId = 1002;

        /// <summary>
        /// 标记反伤生成的重定向 Impact。
        /// </summary>
        private static readonly GameplayTag ReflectedImpactTag = new GameplayTag("rts.custom.reflect_farthest");

        private readonly IRtsSpatialQueryService _spatialQueryService = new RtsOctreeSpatialQueryService();
        private readonly RtsTeamRelationResolver _relationResolver = new RtsTeamRelationResolver();
        private readonly List<CombatActorState> _candidateBuffer = new List<CombatActorState>();

        /// <summary>
        /// 获取当前自定义动作的稳定 Id。
        /// </summary>
        public int CustomId => ActionId;

        /// <summary>
        /// 执行一次反伤逻辑。
        /// 仅在“拥有者是本次 Impact 目标”且存在负向资源变化时生效。
        /// </summary>
        public void ExecuteAction(CombatCustomTriggerActionExecutionContext context)
        {
            var triggerContext = context.TriggerContext;
            var worldState = context.WorldState;
            var ownerActor = context.OwnerActor;
            if (triggerContext == null ||
                worldState == null ||
                ownerActor == null ||
                triggerContext.RelatedAttempt == null ||
                triggerContext.RelatedImpact == null)
            {
                return;
            }

            var sourceImpact = triggerContext.RelatedImpact;
            if (sourceImpact.TargetActorId != ownerActor.ActorId)
            {
                return;
            }

            var payload = context.CustomPayload as RtsReflectDamageToFarthestEnemyPayload;
            var ratio = payload == null ? FP._1 : payload.ReflectionRatio;
            var maxDistance = payload == null ? FP._0 : payload.MaxDistance;
            if (ratio <= FP._0)
            {
                return;
            }

            if (!TryFindFarthestEnemy(worldState, ownerActor, maxDistance, out var farthestEnemy))
            {
                return;
            }

            var reflectedImpact = new CombatImpact(
                ownerActor.ActorId,
                farthestEnemy.ActorId,
                sourceImpact.AbilityId,
                worldState.CurrentTick,
                sourceImpact.Stage)
            {
                EffectId = sourceImpact.EffectId,
            };

            foreach (var tag in sourceImpact.Tags)
            {
                reflectedImpact.Tags.Add(tag);
            }

            reflectedImpact.Tags.Add(ReflectedImpactTag);

            var hasReflectedOperation = false;
            for (var operationIndex = 0; operationIndex < sourceImpact.Operations.Count; operationIndex++)
            {
                var operation = sourceImpact.Operations[operationIndex];
                if (operation.Type != CombatImpactOperationType.ResourceDelta || operation.Amount >= FP._0)
                {
                    continue;
                }

                var reflectedAmount = operation.Amount * ratio;
                if (reflectedAmount == FP._0)
                {
                    continue;
                }

                reflectedImpact.Operations.Add(new CombatImpactOperation
                {
                    Type = CombatImpactOperationType.ResourceDelta,
                    ResourceId = operation.ResourceId,
                    Amount = reflectedAmount,
                });
                hasReflectedOperation = true;
            }

            if (hasReflectedOperation)
            {
                triggerContext.RelatedAttempt.Impacts.Add(reflectedImpact);
            }
        }

        private bool TryFindFarthestEnemy(
            CombatWorldState worldState,
            CombatActorState ownerActor,
            FP maxDistance,
            out CombatActorState farthestEnemy)
        {
            farthestEnemy = null;
            var bestDistanceSquared = FP._0;
            var hasBest = false;

            if (maxDistance > FP._0)
            {
                _spatialQueryService.QueryActorsInRange(worldState, ownerActor.Position, maxDistance, _candidateBuffer);
                for (var i = 0; i < _candidateBuffer.Count; i++)
                {
                    EvaluateCandidate(ownerActor, _candidateBuffer[i], ref farthestEnemy, ref bestDistanceSquared, ref hasBest);
                }

                return hasBest;
            }

            foreach (var candidate in worldState.Actors)
            {
                EvaluateCandidate(ownerActor, candidate, ref farthestEnemy, ref bestDistanceSquared, ref hasBest);
            }

            return hasBest;
        }

        private void EvaluateCandidate(
            CombatActorState ownerActor,
            CombatActorState candidate,
            ref CombatActorState farthestEnemy,
            ref FP bestDistanceSquared,
            ref bool hasBest)
        {
            if (candidate == null ||
                !candidate.IsAlive ||
                candidate.ActorId == ownerActor.ActorId)
            {
                return;
            }

            var relation = _relationResolver.ResolveRelation(null, ownerActor, candidate);
            if (relation != CombatActorRelationFlags.Enemy)
            {
                return;
            }

            var distanceSquared = WorldPosition.DistanceSquared(ownerActor.Position, candidate.Position);
            if (!hasBest ||
                distanceSquared > bestDistanceSquared ||
                (distanceSquared == bestDistanceSquared && IsDeterministicallySmaller(candidate, farthestEnemy)))
            {
                bestDistanceSquared = distanceSquared;
                farthestEnemy = candidate;
                hasBest = true;
            }
        }

        private static bool IsDeterministicallySmaller(CombatActorState left, CombatActorState right)
        {
            if (left == null)
            {
                return false;
            }

            if (right == null)
            {
                return true;
            }

            return string.CompareOrdinal(left.ActorId.Value, right.ActorId.Value) < 0;
        }
    }
}
