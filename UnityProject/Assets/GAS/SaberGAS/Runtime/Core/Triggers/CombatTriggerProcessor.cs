using System.Collections.Generic;
using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;
using Saber.GAS.Tags;

namespace Saber.GAS.Triggers
{
    /// <summary>
    /// Trigger 系统的统一执行器。
    /// 它负责收集候选 Trigger、按确定性顺序排序、过滤条件，并把动作回流到 CombatRuntime。
    /// </summary>
    internal sealed class CombatTriggerProcessor
    {
        /// <summary>
        /// 限制 Trigger 连锁执行深度，避免无限递归。
        /// </summary>
        private const int MaxExecutionDepth = 16;
        /// <summary>
        /// 缓存所属 CombatRuntime。
        /// </summary>
        private readonly CombatRuntime _runtime;
        private readonly CompositeCombatTriggerActionDescriptorRegistry _actionDescriptorRegistry;
        /// <summary>
        /// 记录当前递归执行深度。
        /// </summary>
        private int _executionDepth;

        /// <summary>
        /// 使用 CombatRuntime 创建 Trigger 执行器。
        /// </summary>
        public CombatTriggerProcessor(CombatRuntime runtime)
        {
            _runtime = runtime;
            _actionDescriptorRegistry = runtime == null ? null : runtime.TriggerActionDescriptorRegistry;
        }

        /// <summary>
        /// 处理一次 Trigger 上下文。
        /// 为避免递归连锁失控，这里带有最大执行深度保护。
        /// </summary>
        public void Process(CombatTriggerContext context)
        {
            if (context == null)
            {
                return;
            }

            if (_executionDepth >= MaxExecutionDepth)
            {
                return;
            }

            var candidates = new List<TriggerCandidate>();
            CollectCandidates(context, candidates);
            if (candidates.Count == 0)
            {
                return;
            }

            candidates.Sort(CompareCandidates);

            _executionDepth += 1;
            try
            {
                for (var i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    var trigger = candidate.TriggerInstance;
                    if (trigger == null || !trigger.CanTrigger(context.CurrentTick))
                    {
                        continue;
                    }

                    if (!MatchesTrigger(candidate, context))
                    {
                        continue;
                    }

                    trigger.RegisterTriggered(context.CurrentTick);
                    ExecuteTrigger(context, candidate);
                }
            }
            finally
            {
                _executionDepth -= 1;
            }
        }

        /// <summary>
        /// 收集当前上下文可能命中的所有 Trigger 候选。
        /// 收集范围包含 Actor、ActiveEffect、ActiveAbilityInstance 以及世界级 Trigger。
        /// </summary>
        private void CollectCandidates(CombatTriggerContext context, IList<TriggerCandidate> results)
        {
            CombatActorState ownerActor = null;
            if (!context.OwnerActorId.IsEmpty && _runtime.WorldState.TryGetActor(context.OwnerActorId, out ownerActor))
            {
                AddActorCandidates(ownerActor, results);
            }

            _runtime.TriggerRegistry.Collect(context, results);

            if (context.SourceActiveEffect != null && !OwnerContainsEffect(ownerActor, context.SourceActiveEffect))
            {
                AddEffectCandidates(ownerActor, context.SourceActiveEffect, results);
            }

            if (context.SourceAbilityInstance != null && !OwnerContainsAbility(ownerActor, context.SourceAbilityInstance))
            {
                AddAbilityCandidates(ownerActor, context.SourceAbilityInstance, results);
            }

            for (var i = 0; i < _runtime.WorldState.GlobalTriggers.Count; i++)
            {
                results.Add(new TriggerCandidate
                {
                    OwnerActor = null,
                    SourceKind = TriggerSourceKind.Global,
                    SourceEffect = null,
                    SourceAbilityInstance = null,
                    TriggerInstance = _runtime.WorldState.GlobalTriggers[i],
                });
            }
        }

        /// <summary>
        /// 按优先级、拥有者、来源类型和 TriggerId 对候选排序。
        /// </summary>
        private static int CompareCandidates(TriggerCandidate left, TriggerCandidate right)
        {
            var leftPriority = left.TriggerInstance?.Definition?.Priority ?? 0;
            var rightPriority = right.TriggerInstance?.Definition?.Priority ?? 0;
            var priorityCompare = rightPriority.CompareTo(leftPriority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            var ownerCompare = string.CompareOrdinal(
                left.OwnerActor == null ? string.Empty : left.OwnerActor.ActorId.Value,
                right.OwnerActor == null ? string.Empty : right.OwnerActor.ActorId.Value);
            if (ownerCompare != 0)
            {
                return ownerCompare;
            }

            var sourceCompare = ((int)left.SourceKind).CompareTo((int)right.SourceKind);
            if (sourceCompare != 0)
            {
                return sourceCompare;
            }

            return string.CompareOrdinal(
                left.TriggerInstance?.Definition?.Id.Value ?? string.Empty,
                right.TriggerInstance?.Definition?.Id.Value ?? string.Empty);
        }

        /// <summary>
        /// 收集一个 Actor 本地拥有的 Trigger 候选。
        /// </summary>
        private void AddActorCandidates(CombatActorState ownerActor, IList<TriggerCandidate> results)
        {
            for (var i = 0; i < ownerActor.ActiveTriggers.Count; i++)
            {
                var triggerInstance = ownerActor.ActiveTriggers[i];
                if (!IsOwnerLocalTrigger(triggerInstance))
                {
                    continue;
                }

                results.Add(new TriggerCandidate
                {
                    OwnerActor = ownerActor,
                    SourceKind = TriggerSourceKind.Actor,
                    TriggerInstance = triggerInstance,
                });
            }

            for (var i = 0; i < ownerActor.ActiveEffects.Count; i++)
            {
                var activeEffect = ownerActor.ActiveEffects[i];
                AddEffectCandidates(ownerActor, activeEffect, results);
            }

            for (var i = 0; i < ownerActor.ActiveAbilityInstances.Count; i++)
            {
                var abilityInstance = ownerActor.ActiveAbilityInstances[i];
                AddAbilityCandidates(ownerActor, abilityInstance, results);
            }
        }

        /// <summary>
        /// 收集某个持续效果上的本地 Trigger 候选。
        /// </summary>
        private static void AddEffectCandidates(CombatActorState ownerActor, ActiveEffect activeEffect, IList<TriggerCandidate> results)
        {
            for (var triggerIndex = 0; triggerIndex < activeEffect.ActiveTriggers.Count; triggerIndex++)
            {
                var triggerInstance = activeEffect.ActiveTriggers[triggerIndex];
                if (!IsOwnerLocalTrigger(triggerInstance))
                {
                    continue;
                }

                results.Add(new TriggerCandidate
                {
                    OwnerActor = ownerActor,
                    SourceKind = TriggerSourceKind.Effect,
                    SourceEffect = activeEffect,
                    TriggerInstance = triggerInstance,
                });
            }
        }

        /// <summary>
        /// 收集某个技能实例上的本地 Trigger 候选。
        /// </summary>
        private static void AddAbilityCandidates(CombatActorState ownerActor, ActiveAbilityInstance abilityInstance, IList<TriggerCandidate> results)
        {
            for (var triggerIndex = 0; triggerIndex < abilityInstance.ActiveTriggers.Count; triggerIndex++)
            {
                var triggerInstance = abilityInstance.ActiveTriggers[triggerIndex];
                if (!IsOwnerLocalTrigger(triggerInstance))
                {
                    continue;
                }

                results.Add(new TriggerCandidate
                {
                    OwnerActor = ownerActor,
                    SourceKind = TriggerSourceKind.Ability,
                    SourceAbilityInstance = abilityInstance,
                    TriggerInstance = triggerInstance,
                });
            }
        }

        /// <summary>
        /// 判断一个 Trigger 是否属于 OwnerOnly 本地收集模式。
        /// </summary>
        private static bool IsOwnerLocalTrigger(ActiveTriggerInstance triggerInstance)
        {
            return triggerInstance != null &&
                   triggerInstance.Definition != null &&
                   triggerInstance.Definition.CollectionMode == TriggerCollectionMode.OwnerOnly;
        }

        /// <summary>
        /// 判断某个效果实例是否已经存在于拥有者身上。
        /// </summary>
        private static bool OwnerContainsEffect(CombatActorState ownerActor, ActiveEffect activeEffect)
        {
            if (ownerActor == null)
            {
                return false;
            }

            for (var i = 0; i < ownerActor.ActiveEffects.Count; i++)
            {
                if (object.ReferenceEquals(ownerActor.ActiveEffects[i], activeEffect))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断某个技能实例是否已经存在于拥有者身上。
        /// </summary>
        private static bool OwnerContainsAbility(CombatActorState ownerActor, ActiveAbilityInstance abilityInstance)
        {
            if (ownerActor == null)
            {
                return false;
            }

            for (var i = 0; i < ownerActor.ActiveAbilityInstances.Count; i++)
            {
                if (object.ReferenceEquals(ownerActor.ActiveAbilityInstances[i], abilityInstance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 对单个 Trigger 候选做完整匹配。
        /// 只有事件、时机、Tag、阵营关系、资源阈值与生存状态都满足时才允许执行。
        /// </summary>
        private bool MatchesTrigger(TriggerCandidate candidate, CombatTriggerContext context)
        {
            var definition = candidate.TriggerInstance.Definition;
            if (definition == null)
            {
                return false;
            }

            if (!MatchesEvent(definition, context))
            {
                return false;
            }

            if (!MatchesTiming(definition, context))
            {
                return false;
            }

            if (!MatchesCollectionMode(candidate, context, definition))
            {
                return false;
            }

            if (!MatchesOwnerState(candidate, definition))
            {
                return false;
            }

            if (!MatchesActorTags(context.InstigatorActorId, definition.RequiredInstigatorTags, definition.BlockedInstigatorTags))
            {
                return false;
            }

            if (!MatchesActorTags(context.TargetActorId, definition.RequiredTargetTags, definition.BlockedTargetTags))
            {
                return false;
            }

            if (!MatchesAbilityTags(context.RelatedAbility, definition.RequiredIncomingAbilityTags, definition.BlockedIncomingAbilityTags))
            {
                return false;
            }

            if (!MatchesEffectTags(context.RelatedEffect, definition.RequiredIncomingEffectTags, definition.BlockedIncomingEffectTags))
            {
                return false;
            }

            if (!MatchesImpactTags(context.RelatedImpact, definition.RequiredImpactTags, definition.BlockedImpactTags))
            {
                return false;
            }

            if (!MatchesRelation(candidate.OwnerActor, context.TargetActorId, definition.TargetRelationFilter))
            {
                return false;
            }

            if (!MatchesRelation(candidate.OwnerActor, context.InstigatorActorId, definition.InstigatorRelationFilter))
            {
                return false;
            }

            if (!MatchesLegacyRelation(candidate.OwnerActor, context, definition.RelationFilter, definition))
            {
                return false;
            }

            if (!MatchesLifeState(candidate, context, definition))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 判断 Trigger 是否命中当前事件类型。
        /// </summary>
        private static bool MatchesEvent(TriggerDefinition definition, CombatTriggerContext context)
        {
            if (definition.EventKind == context.EventKind)
            {
                return true;
            }

            if (definition.EventKind != CombatTriggerEventKind.OnResourceThresholdCrossed ||
                context.EventKind != CombatTriggerEventKind.OnResourceChanged)
            {
                return false;
            }

            var threshold = definition.Threshold;
            if (threshold == null || threshold.Direction == TriggerThresholdDirection.None)
            {
                return false;
            }

            if (threshold.ResourceId != context.ResourceId)
            {
                return false;
            }

            if (threshold.Direction == TriggerThresholdDirection.CrossBelowOrEqual)
            {
                return context.PreviousResourceValue > threshold.Value && context.CurrentResourceValue <= threshold.Value;
            }

            if (threshold.Direction == TriggerThresholdDirection.CrossAboveOrEqual)
            {
                return context.PreviousResourceValue < threshold.Value && context.CurrentResourceValue >= threshold.Value;
            }

            return false;
        }

        /// <summary>
        /// 判断 Trigger 是否命中当前执行时机。
        /// </summary>
        private static bool MatchesTiming(TriggerDefinition definition, CombatTriggerContext context)
        {
            return definition.Timing == CombatTriggerTiming.Manual || definition.Timing == context.Timing;
        }

        /// <summary>
        /// 判断拥有者自身的 Tag 状态是否满足 Trigger 条件。
        /// </summary>
        private bool MatchesOwnerState(TriggerCandidate candidate, TriggerDefinition definition)
        {
            IEnumerable<GameplayTag> ownerTags = null;
            if (candidate.OwnerActor != null)
            {
                ownerTags = candidate.OwnerActor.Tags;
            }
            else
            {
                ownerTags = _runtime.WorldState.WorldTags;
            }

            return MatchesTags(ownerTags, definition.RequiredOwnerTags, definition.BlockedOwnerTags);
        }

        /// <summary>
        /// 按收集模式校验观察目标与距离条件。
        /// </summary>
        private bool MatchesCollectionMode(TriggerCandidate candidate, CombatTriggerContext context, TriggerDefinition definition)
        {
            switch (definition.CollectionMode)
            {
                case TriggerCollectionMode.OwnerOnly:
                    return true;
                case TriggerCollectionMode.ObserveTarget:
                    return MatchesObservedActor(candidate.OwnerActor, context.TargetActorId, definition.ObserverMaxDistance);
                case TriggerCollectionMode.ObserveInstigator:
                    return MatchesObservedActor(candidate.OwnerActor, context.InstigatorActorId, definition.ObserverMaxDistance);
                default:
                    return true;
            }
        }

        /// <summary>
        /// 判断某个 Actor 的 Tag 是否满足指定要求。
        /// </summary>
        private bool MatchesActorTags(ActorId actorId, GameplayTagContainer requiredTags, GameplayTagContainer blockedTags)
        {
            if ((requiredTags == null || requiredTags.Count == 0) &&
                (blockedTags == null || blockedTags.Count == 0))
            {
                return true;
            }

            CombatActorState actor;
            if (!_runtime.WorldState.TryGetActor(actorId, out actor))
            {
                return requiredTags == null || requiredTags.Count == 0;
            }

            return MatchesTags(actor.Tags, requiredTags, blockedTags);
        }

        /// <summary>
        /// 判断技能 Tag 是否满足指定要求。
        /// </summary>
        private static bool MatchesAbilityTags(AbilityDefinition ability, GameplayTagContainer requiredTags, GameplayTagContainer blockedTags)
        {
            if ((requiredTags == null || requiredTags.Count == 0) &&
                (blockedTags == null || blockedTags.Count == 0))
            {
                return true;
            }

            if (ability == null)
            {
                return requiredTags == null || requiredTags.Count == 0;
            }

            return MatchesTags(ability.AbilityTags, requiredTags, blockedTags);
        }

        /// <summary>
        /// 判断效果 Tag 是否满足指定要求。
        /// </summary>
        private static bool MatchesEffectTags(EffectDefinition effect, GameplayTagContainer requiredTags, GameplayTagContainer blockedTags)
        {
            if ((requiredTags == null || requiredTags.Count == 0) &&
                (blockedTags == null || blockedTags.Count == 0))
            {
                return true;
            }

            if (effect == null)
            {
                return requiredTags == null || requiredTags.Count == 0;
            }

            return MatchesTags(effect.EffectTags, requiredTags, blockedTags);
        }

        /// <summary>
        /// 判断 Impact Tag 是否满足指定要求。
        /// </summary>
        private static bool MatchesImpactTags(CombatImpact impact, GameplayTagContainer requiredTags, GameplayTagContainer blockedTags)
        {
            if ((requiredTags == null || requiredTags.Count == 0) &&
                (blockedTags == null || blockedTags.Count == 0))
            {
                return true;
            }

            if (impact == null)
            {
                return requiredTags == null || requiredTags.Count == 0;
            }

            return MatchesTags(impact.Tags, requiredTags, blockedTags);
        }

        /// <summary>
        /// 用统一逻辑校验一组 Tag 是否满足必需与屏蔽条件。
        /// </summary>
        private static bool MatchesTags(IEnumerable<GameplayTag> tags, GameplayTagContainer requiredTags, GameplayTagContainer blockedTags)
        {
            var container = tags as GameplayTagContainer ?? (tags == null ? new GameplayTagContainer() : new GameplayTagContainer(tags));
            if (requiredTags != null && !container.ContainsAll(requiredTags))
            {
                return false;
            }

            if (blockedTags != null && container.ContainsAny(blockedTags))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 兼容旧版单关系过滤字段的匹配逻辑。
        /// </summary>
        private bool MatchesLegacyRelation(
            CombatActorState ownerActor,
            CombatTriggerContext context,
            CombatActorRelationFlags relationFilter,
            TriggerDefinition definition)
        {
            if (relationFilter == CombatActorRelationFlags.Any)
            {
                return true;
            }

            if (definition.TargetRelationFilter != CombatActorRelationFlags.Any ||
                definition.InstigatorRelationFilter != CombatActorRelationFlags.Any)
            {
                return true;
            }

            if (ownerActor == null)
            {
                return false;
            }

            var relatedActorId = !context.InstigatorActorId.IsEmpty
                ? context.InstigatorActorId
                : context.TargetActorId;

            return MatchesRelation(ownerActor, relatedActorId, relationFilter);
        }

        /// <summary>
        /// 判断拥有者与关联单位是否满足指定阵营关系。
        /// </summary>
        private bool MatchesRelation(
            CombatActorState ownerActor,
            ActorId relatedActorId,
            CombatActorRelationFlags relationFilter)
        {
            return _runtime.MatchesActorRelation(ownerActor, relatedActorId, relationFilter);
        }

        /// <summary>
        /// 判断观察者收集模式下的被观察对象与距离条件。
        /// </summary>
        private bool MatchesObservedActor(CombatActorState ownerActor, ActorId observedActorId, FP maxDistance)
        {
            if (ownerActor == null || observedActorId.IsEmpty)
            {
                return false;
            }

            CombatActorState observedActor;
            if (!_runtime.WorldState.TryGetActor(observedActorId, out observedActor))
            {
                return false;
            }

            if (maxDistance <= FP._0)
            {
                return true;
            }

            var maxDistanceSquared = maxDistance * maxDistance;
            return WorldPosition.DistanceSquared(ownerActor.Position, observedActor.Position) <= maxDistanceSquared;
        }

        /// <summary>
        /// 判断当前事件涉及的源和目标存活状态是否允许 Trigger 生效。
        /// </summary>
        private bool MatchesLifeState(TriggerCandidate candidate, CombatTriggerContext context, TriggerDefinition definition)
        {
            if (definition.DisableWhenSourceDead && candidate.OwnerActor != null && !candidate.OwnerActor.IsAlive)
            {
                return false;
            }

            if (!definition.DisableWhenTargetDead || context.TargetActorId.IsEmpty)
            {
                return true;
            }

            CombatActorState targetActor;
            if (!_runtime.WorldState.TryGetActor(context.TargetActorId, out targetActor))
            {
                return false;
            }

            return targetActor.IsAlive;
        }

        /// <summary>
        /// 执行 Trigger 命中的动作，并在必要时消费或移除来源 Effect。
        /// </summary>
        private void ExecuteTrigger(CombatTriggerContext context, TriggerCandidate candidate)
        {
            var definition = candidate.TriggerInstance.Definition;
            var action = definition.Action;
            if (action == null || action.Kind == TriggerActionKind.None)
            {
                return;
            }

            _actionDescriptorRegistry?.TryExecute(new CombatTriggerActionExecutionContext(
                _runtime,
                context,
                candidate,
                action));

            if (definition.RemoveSourceEffectOnTrigger && candidate.SourceEffect != null && candidate.OwnerActor != null)
            {
                _runtime.RemoveEffectDirect(candidate.OwnerActor, candidate.SourceEffect);
                return;
            }

            if (definition.ConsumeSourceEffectOnTrigger && candidate.SourceEffect != null && candidate.OwnerActor != null)
            {
                _runtime.ConsumeEffectStackDirect(candidate.OwnerActor, candidate.SourceEffect);
            }
        }

        /// <summary>
        /// 触发一个标准 Ability 激活流程。
        /// 适合“受击反击”“死亡后施放技能”这类玩法。
        /// </summary>
        private void ExecuteAbilityAction(CombatTriggerContext context, TriggerCandidate candidate, TriggerActionDefinition action)
        {
            var sourceActorId = ResolveActorReference(action.SourceActor, candidate, context);
            if (sourceActorId.IsEmpty || action.TriggeredAbilityId.IsEmpty)
            {
                return;
            }

            var targetActorId = ResolveActorReference(action.TargetActor, candidate, context);
            _runtime.ExecuteTriggeredAbility(sourceActorId, action.TriggeredAbilityId, targetActorId, null, action.CustomPayload);
        }

        /// <summary>
        /// 触发一个标准 Effect 施加流程。
        /// 适合“低血自动开盾”“队友死亡后狂暴”这类玩法。
        /// </summary>
        private void ExecuteApplyEffectAction(CombatTriggerContext context, TriggerCandidate candidate, TriggerActionDefinition action)
        {
            var sourceActorId = ResolveActorReference(action.SourceActor, candidate, context);
            var targetActorId = ResolveActorReference(action.TargetActor, candidate, context);
            if (targetActorId.IsEmpty || action.EffectDefinition == null)
            {
                return;
            }

            _runtime.ApplyTriggerEffect(action.EffectDefinition, sourceActorId, targetActorId, 1);
        }

        /// <summary>
        /// 按 EffectId 直接移除目标身上的效果。
        /// </summary>
        private void ExecuteRemoveEffectByIdAction(CombatTriggerContext context, TriggerCandidate candidate, TriggerActionDefinition action)
        {
            var targetActorId = ResolveActorReference(action.TargetActor, candidate, context);
            if (targetActorId.IsEmpty || action.EffectId.IsEmpty)
            {
                return;
            }

            _runtime.RemoveEffectsByIdDirect(targetActorId, action.EffectId);
        }

        /// <summary>
        /// 按 Tag 批量清理目标身上的效果。
        /// 可用于净化、驱散或某些被动反制。
        /// </summary>
        private void ExecuteRemoveEffectsByTagAction(CombatTriggerContext context, TriggerCandidate candidate, TriggerActionDefinition action)
        {
            var targetActorId = ResolveActorReference(action.TargetActor, candidate, context);
            if (targetActorId.IsEmpty || string.IsNullOrWhiteSpace(action.EffectTag.Value))
            {
                return;
            }

            _runtime.RemoveEffectsByTagDirect(targetActorId, action.EffectTag);
        }

        /// <summary>
        /// 向当前 Impact 追加一个新的操作模板副本。
        /// </summary>
        private static void ExecuteAddImpactOperationAction(CombatTriggerContext context, TriggerActionDefinition action)
        {
            if (context.RelatedImpact == null || action.OperationTemplate == null)
            {
                return;
            }

            context.RelatedImpact.Operations.Add(CloneOperation(action.OperationTemplate));
        }

        /// <summary>
        /// 按倍率改写当前 Impact 的数值操作。
        /// </summary>
        private static void ExecuteModifyImpactMagnitudeAction(CombatTriggerContext context, TriggerActionDefinition action)
        {
            if (context.RelatedImpact == null)
            {
                return;
            }

            if (context.RelatedOperation != null && context.RelatedOperation.Type == CombatImpactOperationType.ResourceDelta)
            {
                context.RelatedOperation.Amount = context.RelatedOperation.Amount * action.MagnitudeMultiplier;
                return;
            }

            for (var i = 0; i < context.RelatedImpact.Operations.Count; i++)
            {
                var operation = context.RelatedImpact.Operations[i];
                if (operation.Type != CombatImpactOperationType.ResourceDelta)
                {
                    continue;
                }

                operation.Amount = operation.Amount * action.MagnitudeMultiplier;
            }
        }

        /// <summary>
        /// 直接调整资源值，并补发 OnResourceChanged 触发窗口。
        /// </summary>
        private void ExecuteModifyResourceAction(CombatTriggerContext context, TriggerCandidate candidate, TriggerActionDefinition action, bool negate)
        {
            var targetActorId = ResolveActorReference(action.TargetActor, candidate, context);
            if (targetActorId.IsEmpty || action.ResourceId.IsEmpty)
            {
                return;
            }

            var delta = negate ? -action.ResourceAmount : action.ResourceAmount;
            _runtime.ModifyResourceDirect(targetActorId, action.ResourceId, delta, context, candidate.OwnerActor);
        }

        /// <summary>
        /// 在运行时为单位增删 Tag。
        /// 这类动作只改状态，不直接派生新的 Ability 或 Effect。
        /// </summary>
        private void ExecuteModifyTagAction(CombatTriggerContext context, TriggerCandidate candidate, TriggerActionDefinition action, bool add)
        {
            var targetActorId = ResolveActorReference(action.TargetActor, candidate, context);
            if (targetActorId.IsEmpty || string.IsNullOrWhiteSpace(action.Tag.Value))
            {
                return;
            }

            if (add)
            {
                _runtime.AddRuntimeTagDirect(targetActorId, action.Tag);
            }
            else
            {
                _runtime.RemoveRuntimeTagDirect(targetActorId, action.Tag);
            }
        }

        /// <summary>
        /// 取消目标单位当前正在运行的指定 Ability。
        /// </summary>
        private void ExecuteCancelAbilityAction(CombatTriggerContext context, TriggerCandidate candidate, TriggerActionDefinition action)
        {
            var targetActorId = ResolveActorReference(action.TargetActor, candidate, context);
            if (targetActorId.IsEmpty || action.AbilityToCancelId.IsEmpty)
            {
                return;
            }

            _runtime.CancelAbilityByIdDirect(targetActorId, action.AbilityToCancelId, "Cancelled by trigger.");
        }

        /// <summary>
        /// 发出一个轻量级表现事件。
        /// 适合驱动外层表现层或调试日志，不直接改变战斗数值状态。
        /// </summary>
        private void ExecuteEmitCueAction(CombatTriggerContext context, TriggerCandidate candidate, TriggerActionDefinition action)
        {
            var targetActorId = ResolveActorReference(action.TargetActor, candidate, context);
            if (targetActorId.IsEmpty)
            {
                targetActorId = ResolveActorReference(TriggerActorReference.Owner, candidate, context);
            }

            _runtime.EmitCueDirect(targetActorId, action.CueName);
        }

        /// <summary>
        /// 把当前 Impact 的一部分重定向给另一个单位承受。
        /// </summary>
        private void ExecuteSplitImpactAction(CombatTriggerContext context, TriggerCandidate candidate, TriggerActionDefinition action)
        {
            var targetActorId = ResolveActorReference(action.TargetActor, candidate, context);
            if (targetActorId.IsEmpty)
            {
                return;
            }

            _runtime.SplitImpactToActorDirect(context, targetActorId, action.MagnitudeMultiplier, action.Tag);
        }

        /// <summary>
        /// 依据当前 Impact 的规模折算一笔资源变化。
        /// </summary>
        private void ExecuteAddResourceFromImpactAction(CombatTriggerContext context, TriggerCandidate candidate, TriggerActionDefinition action)
        {
            var targetActorId = ResolveActorReference(action.TargetActor, candidate, context);
            if (targetActorId.IsEmpty || action.ResourceId.IsEmpty)
            {
                return;
            }

            _runtime.AddResourceFromImpactDirect(context, targetActorId, action.ResourceId, action.MagnitudeMultiplier, candidate.OwnerActor);
        }

        /// <summary>
        /// 把自定义 TriggerAction 转交给 Runtime 的注册表派发链执行。
        /// </summary>
        private void ExecuteCustomAction(CombatTriggerContext context, TriggerCandidate candidate, TriggerActionDefinition action)
        {
            _runtime.ExecuteCustomTriggerAction(new CombatCustomTriggerActionExecutionContext(
                _runtime,
                context,
                action,
                candidate.OwnerActor,
                candidate.SourceKind,
                candidate.SourceEffect,
                candidate.SourceAbilityInstance));
        }

        /// <summary>
        /// 把 TriggerActorReference 解析为实际的 ActorId。
        /// </summary>
        private static ActorId ResolveActorReference(TriggerActorReference reference, TriggerCandidate candidate, CombatTriggerContext context)
        {
            switch (reference)
            {
                case TriggerActorReference.Owner:
                    return candidate.OwnerActor == null ? context.OwnerActorId : candidate.OwnerActor.ActorId;
                case TriggerActorReference.Instigator:
                    return context.InstigatorActorId;
                case TriggerActorReference.Target:
                    return context.TargetActorId;
                default:
                    return ActorId.Empty;
            }
        }

        /// <summary>
        /// 克隆一份 Impact 操作模板，避免运行时直接复用共享定义对象。
        /// </summary>
        private static CombatImpactOperation CloneOperation(CombatImpactOperation source)
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
                    : new EffectSpec(source.EffectSpec.Definition, source.EffectSpec.SourceActorId, source.EffectSpec.TargetActorId, source.EffectSpec.AppliedTick, source.EffectSpec.Stacks),
                CueName = source.CueName,
                Payload = source.Payload,
            };
        }
    }
}
