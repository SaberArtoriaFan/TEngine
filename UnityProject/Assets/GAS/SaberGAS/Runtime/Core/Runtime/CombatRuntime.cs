using System.Collections.Generic;
using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Attributes;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Pooling;
using Saber.GAS.Tags;
using Saber.GAS.Triggers;

namespace Saber.GAS.Runtime
{
    /// <summary>
    /// Saber.GAS 的核心执行器，负责驱动能力、效果、资源、标签与 Tick 流转。
    /// </summary>
    public sealed class CombatRuntime
    {
        /// <summary>
        /// 行动尝试进入正式结算前需要通过的外部闸门集合。
        /// </summary>
        private readonly IReadOnlyList<ICombatActionGate> _actionGates;
        /// <summary>
        /// 统一接收战斗事件广播的事件汇聚端。
        /// </summary>
        private readonly ICombatEventSink _eventSink;
        /// <summary>
        /// 在 Impact 结算前参与改写内容的变异器集合。
        /// </summary>
        private readonly IReadOnlyList<ICombatImpactMutator> _impactMutators;
        /// <summary>
        /// 负责把 ImpactOperation 解释成最终落地结果的解析器集合。
        /// </summary>
        private readonly IReadOnlyList<ICombatImpactResolver> _impactResolvers;
        /// <summary>
        /// 提供行动资格、阶段收尾等模式规则的查询接口。
        /// </summary>
        private readonly ICombatModeRules _modeRules;
        /// <summary>
        /// 负责解析 Actor 之间抽象关系的关系查询器。
        /// </summary>
        private readonly ICombatActorRelationResolver _relationResolver;
        /// <summary>
        /// Runtime 内部复用的对象池集合。
        /// </summary>
        private readonly CombatRuntimePools _pools;
        /// <summary>
        /// 语义层或业务层挂入 Core 的规则模块集合。
        /// </summary>
        private readonly IReadOnlyList<ICombatRuleModule> _ruleModules;
        /// <summary>
        /// 统一处理技能目标解析的目标查询器。
        /// </summary>
        private readonly ITargetingResolver _targetingResolver;
        /// <summary>
        /// 用于收集观察者 Trigger 的全局注册索引。
        /// </summary>
        private readonly CombatTriggerRegistry _triggerRegistry;
        /// <summary>
        /// 承担 Trigger 候选筛选、排序与执行的处理器。
        /// </summary>
        private readonly CombatTriggerProcessor _triggerProcessor;
        /// <summary>
        /// 承担自定义 TriggerAction 的查找与派发。
        /// </summary>
        private readonly CompositeCombatCustomTriggerActionRegistry _customTriggerActionRegistry;

        /// <summary>
        /// 使用世界状态和运行时配置创建核心战斗执行器。
        /// </summary>
        public CombatRuntime(CombatWorldState worldState, CombatRuntimeOptions options = null)
        {
            var runtimeOptions = options ?? new CombatRuntimeOptions();

            WorldState = worldState;
            _modeRules = runtimeOptions.ModeRules ?? new DefaultCombatModeRules();
            _relationResolver = runtimeOptions.RelationResolver ?? new FallbackCombatActorRelationResolver();
            _targetingResolver = runtimeOptions.TargetingResolver ?? new DefaultTargetingResolver();
            _eventSink = runtimeOptions.EventSink ?? new NullCombatEventSink();
            var assemblyExtensions = new CompositeCombatAssemblyExtensions(
                CombatAssemblyExtensionRegistryHub.Snapshot(),
                runtimeOptions.ActionGates,
                runtimeOptions.ImpactMutators,
                runtimeOptions.ImpactResolvers,
                runtimeOptions.RuleModules,
                null);
            _actionGates = assemblyExtensions.ActionGates;
            _impactMutators = assemblyExtensions.ImpactMutators;
            _impactResolvers = assemblyExtensions.ImpactResolvers;
            _ruleModules = assemblyExtensions.RuleModules;
            _customTriggerActionRegistry = new CompositeCombatCustomTriggerActionRegistry(CollectCustomTriggerActionRegistries(runtimeOptions.CustomTriggerActionRegistries));
            _pools = new CombatRuntimePools();
            _triggerRegistry = new CombatTriggerRegistry();
            _triggerProcessor = new CombatTriggerProcessor(this);
            RebuildTriggerRegistry();
            InitializeRuleModules();
        }

        /// <summary>
        /// 使用简化依赖参数创建核心战斗执行器。
        /// </summary>
        public CombatRuntime(
            CombatWorldState worldState,
            ICombatModeRules modeRules = null,
            ITargetingResolver targetingResolver = null,
            ICombatActorRelationResolver relationResolver = null,
            ICombatEventSink eventSink = null)
            : this(
                worldState,
                new CombatRuntimeOptions
                {
                    ModeRules = modeRules,
                    RelationResolver = relationResolver,
                    TargetingResolver = targetingResolver,
                    EventSink = eventSink,
                })
        {
        }

        /// <summary>
        /// 当前 Runtime 驱动的战斗世界状态。
        /// </summary>
        public CombatWorldState WorldState { get; }

        /// <summary>
        /// Runtime 内部维护的观察者 Trigger 注册表。
        /// </summary>
        internal CombatTriggerRegistry TriggerRegistry => _triggerRegistry;

        /// <summary>
        /// Runtime 当前使用的 Actor 关系解析器。
        /// </summary>
        internal ICombatActorRelationResolver RelationResolver => _relationResolver;

        /// <summary>
        /// 执行一次自定义 TriggerAction。
        /// Custom 分支最终都会汇流到这里，由组合注册表完成查找和调用。
        /// </summary>
        internal bool ExecuteCustomTriggerAction(CombatCustomTriggerActionExecutionContext context)
        {
            return _customTriggerActionRegistry.TryExecute(context);
        }

        /// <summary>
        /// 向 Trigger 系统投递一次结算上下文。
        /// 这是 Runtime 与 CombatTriggerProcessor 之间的统一入口。
        /// </summary>
        public void RaiseTriggerEvent(CombatTriggerContext context)
        {
            if (context == null)
            {
                return;
            }

            if (context.CurrentTick.Equals(SimulationTick.Zero) && WorldState.CurrentTick > SimulationTick.Zero)
            {
                context.CurrentTick = WorldState.CurrentTick;
            }

            _triggerProcessor.Process(context);
        }

        /// <summary>
        /// 给指定 Actor 挂一个运行时 Trigger。
        /// 适合临时被动、装备效果或关卡规则在战斗中动态注入。
        /// </summary>
        public bool AddActorTrigger(ActorId actorId, TriggerDefinition triggerDefinition)
        {
            CombatActorState actor;
            if (triggerDefinition == null || !WorldState.TryGetActor(actorId, out actor))
            {
                return false;
            }

            var triggerInstance = RentTriggerInstance(triggerDefinition);
            actor.ActiveTriggers.Add(triggerInstance);
            RegisterActorTriggerInstance(actor, triggerInstance);
            return true;
        }

        /// <summary>
        /// 从指定 Actor 身上移除一个运行时 Trigger。
        /// </summary>
        public bool RemoveActorTrigger(ActorId actorId, TriggerId triggerId)
        {
            CombatActorState actor;
            if (triggerId.IsEmpty || !WorldState.TryGetActor(actorId, out actor))
            {
                return false;
            }

            for (var i = actor.ActiveTriggers.Count - 1; i >= 0; i--)
            {
                var trigger = actor.ActiveTriggers[i];
                if (trigger.Definition == null || trigger.Definition.Id != triggerId)
                {
                    continue;
                }

                actor.ActiveTriggers.RemoveAt(i);
                _triggerRegistry.Unregister(trigger);
                _pools.ActiveTriggers.Return(trigger);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 添加一个世界级 Trigger。
        /// 适合地形规则、模式规则或整局共享的全局反应逻辑。
        /// </summary>
        public void AddGlobalTrigger(TriggerDefinition triggerDefinition)
        {
            if (triggerDefinition == null)
            {
                return;
            }

            WorldState.GlobalTriggers.Add(RentTriggerInstance(triggerDefinition));
        }

        /// <summary>
        /// 按 TriggerId 移除一个世界级 Trigger。
        /// </summary>
        public bool RemoveGlobalTrigger(TriggerId triggerId)
        {
            if (triggerId.IsEmpty)
            {
                return false;
            }

            for (var i = WorldState.GlobalTriggers.Count - 1; i >= 0; i--)
            {
                var trigger = WorldState.GlobalTriggers[i];
                if (trigger.Definition == null || trigger.Definition.Id != triggerId)
                {
                    continue;
                }

                WorldState.GlobalTriggers.RemoveAt(i);
                _pools.ActiveTriggers.Return(trigger);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 设置 Actor 的生存状态，并补发死亡、击杀、队友死亡等触发窗口。
        /// 这样死亡相关被动不需要额外监听外部事件。
        /// </summary>
        public bool SetActorAliveState(
            ActorId actorId,
            bool isAlive,
            ActorId instigatorActorId = default(ActorId),
            AbilityId relatedAbilityId = default(AbilityId))
        {
            CombatActorState actor;
            if (!WorldState.TryGetActor(actorId, out actor))
            {
                return false;
            }

            if (actor.IsAlive == isAlive)
            {
                return true;
            }

            actor.IsAlive = isAlive;
            if (isAlive)
            {
                return true;
            }

            AbilityDefinition relatedAbility;
            WorldState.TryGetAbility(relatedAbilityId, out relatedAbility);

            ProcessOwnedTriggerEvent(
                CombatTriggerEventKind.OnDeath,
                CombatTriggerTiming.ImmediatePostResolve,
                actorId,
                instigatorActorId,
                actorId,
                relatedAbility,
                null,
                null,
                null,
                null,
                null,
                null);

            if (!instigatorActorId.IsEmpty)
            {
                ProcessOwnedTriggerEvent(
                    CombatTriggerEventKind.OnKill,
                    CombatTriggerTiming.ImmediatePostResolve,
                    instigatorActorId,
                    instigatorActorId,
                    actorId,
                    relatedAbility,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            foreach (var otherActor in WorldState.Actors)
            {
                if (otherActor.ActorId == actor.ActorId ||
                    !MatchesActorRelation(otherActor, actor, CombatActorRelationFlags.Ally))
                {
                    continue;
                }

                ProcessOwnedTriggerEvent(
                    CombatTriggerEventKind.OnAllyDeath,
                    CombatTriggerTiming.ImmediatePostResolve,
                    otherActor.ActorId,
                    instigatorActorId,
                    actor.ActorId,
                    relatedAbility,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            return true;
        }

        /// <summary>
        /// 创建并注册一个运行时 actor，优先复用对象池中的实例。
        /// </summary>
        public CombatActorState AddActor(ActorId actorId)
        {
            RemoveActor(actorId);

            var actor = _pools.ActorStates.Rent();
            actor.Initialize(actorId);
            WorldState.AddActor(actor);
            return actor;
        }

        /// <summary>
        /// 将 actor 从世界中移除并回收其内部运行时状态。
        /// </summary>
        public bool RemoveActor(ActorId actorId)
        {
            CombatActorState actor;
            if (!WorldState.RemoveActor(actorId, out actor))
            {
                return false;
            }

            ReleaseActor(actor);
            return true;
        }

        /// <summary>
        /// 关闭本次战斗运行时，并回收所有池化对象。
        /// </summary>
        public void Shutdown()
        {
            // A battle-close shutdown must first reclaim live actor-owned state back into pools,
            // then clear the world so large scenes do not pin pooled memory via actor references.
            ShutdownRuleModules();

            foreach (var actor in WorldState.Actors)
            {
                ReleaseActor(actor);
            }

            WorldState.ClearActors();
            ReleaseGlobalTriggers();
            _pools.Reset();
        }

        /// <summary>
        /// 尝试执行一次能力激活请求。
        /// </summary>
        public AbilityActivationResult TryActivate(AbilityActivationRequest request)
        {
            var attempt = BuildActivationAttempt(request);
            if (attempt.IsBlocked)
            {
                return FailAttempt(attempt);
            }

            CommitActivationCostAndCooldown(attempt.SourceActor, attempt.Ability);

            var record = new AbilityExecutionRecord
            {
                AbilityId = attempt.Ability.Id,
                SourceActorId = attempt.SourceActor.ActorId,
                ExecutionTick = WorldState.CurrentTick,
            };

            if (ShouldCreateLifecycle(attempt.Ability))
            {
                StartAbilityLifecycle(attempt, record);
                _eventSink.Publish(new CombatEvent(CombatEventKind.AbilityActivated, attempt.SourceActor.ActorId, attempt.Ability.Name ?? attempt.Ability.Id.Value));
                ProcessOwnedTriggerEvent(
                    CombatTriggerEventKind.AfterAttemptSucceeded,
                    CombatTriggerTiming.EndOfStage,
                    attempt.SourceActor.ActorId,
                    attempt.SourceActor.ActorId,
                    attempt.Targets.Count > 0 ? attempt.Targets[0].ActorId : ActorId.Empty,
                    attempt.Ability,
                    null,
                    attempt,
                    null,
                    null,
                    null,
                    null);
                return AbilityActivationResult.Success(record, attempt);
            }

            QueueImpactsForEffects(attempt, attempt.SourceActor, attempt.Ability, attempt.Targets, attempt.Ability.Effects, ActionExecutionStage.Execute);
            ResolveAttemptImpacts(attempt, record);
            _eventSink.Publish(new CombatEvent(CombatEventKind.AbilityActivated, attempt.SourceActor.ActorId, attempt.Ability.Name ?? attempt.Ability.Id.Value));
            ProcessOwnedTriggerEvent(
                CombatTriggerEventKind.AfterAttemptSucceeded,
                CombatTriggerTiming.EndOfStage,
                attempt.SourceActor.ActorId,
                attempt.SourceActor.ActorId,
                attempt.Targets.Count > 0 ? attempt.Targets[0].ActorId : ActorId.Empty,
                attempt.Ability,
                null,
                attempt,
                null,
                null,
                null,
                null);
            return AbilityActivationResult.Success(record, attempt);
        }

        /// <summary>
        /// 尝试取消一个正在运行的能力实例。
        /// </summary>
        public bool TryCancelAbilityInstance(ActorId actorId, AbilityInstanceId instanceId, string reason)
        {
            CombatActorState actor;
            if (!WorldState.TryGetActor(actorId, out actor))
            {
                return false;
            }

            for (var i = actor.ActiveAbilityInstances.Count - 1; i >= 0; i--)
            {
                var instance = actor.ActiveAbilityInstances[i];
                if (instance.InstanceId != instanceId)
                {
                    continue;
                }

                CancelAbilityInstance(actor, instance, reason ?? "Cancelled.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 推进一帧逻辑 Tick，处理再生、能力实例、持续效果与模式回调。
        /// </summary>
        public void Tick()
        {
            WorldState.CurrentTick = WorldState.CurrentTick.Next();
            EnsurePassiveAbilities();

            foreach (var actor in WorldState.Actors)
            {
                actor.Resources.RegenerateAll();
            }

            foreach (var actor in WorldState.Actors)
            {
                ProcessAbilityInstances(actor);
                ProcessEffects(actor);
                ProcessOwnedTriggerEvent(
                    CombatTriggerEventKind.OnTick,
                    CombatTriggerTiming.EndOfStage,
                    actor.ActorId,
                    ActorId.Empty,
                    actor.ActorId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            RaiseTriggerEvent(new CombatTriggerContext
            {
                CurrentTick = WorldState.CurrentTick,
                EventKind = CombatTriggerEventKind.OnTick,
                Timing = CombatTriggerTiming.EndOfStage,
            });
            _modeRules.OnPostTick(WorldState);
            _eventSink.Publish(new CombatEvent(CombatEventKind.TickAdvanced, ActorId.Empty, WorldState.CurrentTick.ToString()));
        }

        /// <summary>
        /// 构造一次激活 attempt，并完成前置阻断与选目标校验。
        /// </summary>
        private CombatActionAttempt BuildActivationAttempt(AbilityActivationRequest request)
        {
            CombatActorState sourceActor = null;
            AbilityDefinition ability = null;

            if (request != null)
            {
                WorldState.TryGetActor(request.SourceActorId, out sourceActor);
                WorldState.TryGetAbility(request.AbilityId, out ability);
            }

            var executionContext = new AbilityExecutionContext(request, ability, WorldState.CurrentTick);
            var attempt = new CombatActionAttempt(request, sourceActor, ability, executionContext, ActionExecutionStage.Activation);

            if (request == null)
            {
                attempt.AddBlock(ActionBlockKind.InvalidRequest, "Activation request is null.");
                return attempt;
            }

            if (sourceActor == null)
            {
                attempt.AddBlock(ActionBlockKind.SourceState, "Source actor not found.");
            }

            if (ability == null)
            {
                attempt.AddBlock(ActionBlockKind.AbilityState, "Ability definition not found.");
            }

            if (attempt.IsBlocked)
            {
                return attempt;
            }

            // Target resolution happens after built-in blocks so we do not spend resolver work
            // on requests that are already invalid at the source / ability / cooldown layer.
            EvaluateBuiltInBlocks(attempt);

            if (!attempt.IsBlocked)
            {
                _targetingResolver.ResolveTargets(WorldState, sourceActor, ability, request.TargetData, attempt.Targets);

                string reason;
                if (!ValidateTargets(sourceActor, ability, request.TargetData, attempt.Targets, out reason))
                {
                    attempt.AddBlock(ActionBlockKind.Targeting, reason);
                }
            }

            for (var i = 0; i < _actionGates.Count; i++)
            {
                _actionGates[i].Evaluate(attempt, WorldState);
            }

            return attempt;
        }

        /// <summary>
        /// 执行运行时内建的阻断规则。
        /// </summary>
        private void EvaluateBuiltInBlocks(CombatActionAttempt attempt)
        {
            var sourceActor = attempt.SourceActor;
            var ability = attempt.Ability;
            var request = attempt.Request;

            if (!sourceActor.HasAbility(ability.Id))
            {
                attempt.AddBlock(ActionBlockKind.AbilityState, "Source actor does not own the ability.");
            }

            if (ability.ActivationMode == AbilityActivationMode.Passive)
            {
                attempt.AddBlock(ActionBlockKind.Lifecycle, "Passive abilities cannot be manually activated.");
            }

            string reason;
            if (!_modeRules.CanActorAct(WorldState, sourceActor, ability, out reason))
            {
                attempt.AddBlock(ActionBlockKind.ModeRule, reason ?? "Current combat mode blocked the action.");
            }

            if (!sourceActor.Tags.ContainsAll(ability.ActivationRequiredTags))
            {
                attempt.AddBlock(ActionBlockKind.SourceState, "Source actor does not satisfy required tags.");
            }

            if (sourceActor.Tags.ContainsAny(ability.ActivationBlockedTags))
            {
                attempt.AddBlock(ActionBlockKind.SourceState, "Source actor is blocked by activation tags.");
            }

            if (!sourceActor.Tags.ContainsAll(ability.Targeting.RequiredSourceTags))
            {
                attempt.AddBlock(ActionBlockKind.Targeting, "Source actor does not satisfy source targeting tags.");
            }

            if (sourceActor.Tags.ContainsAny(ability.Targeting.BlockedSourceTags))
            {
                attempt.AddBlock(ActionBlockKind.Targeting, "Source actor is blocked by source targeting tags.");
            }

            if (sourceActor.IsAbilityOnCooldown(ability.Id, WorldState.CurrentTick))
            {
                attempt.AddBlock(ActionBlockKind.Cooldown, "Ability is on cooldown.");
            }

            if (!sourceActor.Resources.CanAfford(ability.Costs))
            {
                attempt.AddBlock(ActionBlockKind.Cost, "Source actor cannot afford the resource cost.");
            }

            if (request.RequestTick > WorldState.CurrentTick)
            {
                attempt.AddBlock(ActionBlockKind.InvalidRequest, "Activation request tick cannot be in the future for direct execution.");
            }

            for (var moduleIndex = 0; moduleIndex < _ruleModules.Count; moduleIndex++)
            {
                _ruleModules[moduleIndex].EvaluateAbilityAttempt(attempt, WorldState);
            }
        }

        /// <summary>
        /// 在能力激活成功后真正扣除资源并写入冷却。
        /// </summary>
        private void CommitActivationCostAndCooldown(CombatActorState sourceActor, AbilityDefinition ability)
        {
            sourceActor.Resources.Spend(ability.Costs);

            if (ability.Cooldown.DurationTicks > 0)
            {
                sourceActor.SetCooldown(ability.Id, WorldState.CurrentTick + ability.Cooldown.DurationTicks);
            }
        }

        /// <summary>
        /// 判断该能力是否需要保留运行时生命周期实例。
        /// </summary>
        private bool ShouldCreateLifecycle(AbilityDefinition ability)
        {
            if (ability.ActivationMode != AbilityActivationMode.Instant)
            {
                return true;
            }

            if (ability.CastDurationTicks > 0 || ability.ActiveDurationTicks > 0 || ability.IntervalTicks > 0)
            {
                return true;
            }

            if (ability.PeriodicEffects.Count > 0 || ability.EndEffects.Count > 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 启动一个长生命周期能力实例。
        /// </summary>
        private void StartAbilityLifecycle(CombatActionAttempt attempt, AbilityExecutionRecord record)
        {
            // Long-lived abilities keep a runtime instance so cast, active, periodic and end
            // stages can be advanced by ticks instead of being flattened into one activation.
            var instance = RentAbilityInstance(
                WorldState.CreateAbilityInstanceId(),
                attempt.SourceActor.ActorId,
                attempt.Ability,
                attempt.Request.TargetData,
                WorldState.CurrentTick);

            attempt.AbilityInstance = instance;
            attempt.SourceActor.ActiveAbilityInstances.Add(instance);
            RegisterAbilityTriggerInstances(attempt.SourceActor, instance);
            record.StartedAbilityInstanceId = instance.InstanceId;

            _eventSink.Publish(new CombatEvent(CombatEventKind.AbilityStarted, attempt.SourceActor.ActorId, attempt.Ability.Name ?? attempt.Ability.Id.Value));
            ProcessOwnedTriggerEvent(
                CombatTriggerEventKind.OnAbilityStarted,
                CombatTriggerTiming.EndOfStage,
                attempt.SourceActor.ActorId,
                attempt.SourceActor.ActorId,
                attempt.Targets.Count > 0 ? attempt.Targets[0].ActorId : ActorId.Empty,
                attempt.Ability,
                null,
                attempt,
                null,
                null,
                null,
                instance);

            if (instance.State == ActiveAbilityState.Active)
            {
                ActivateAbilityInstance(attempt.SourceActor, instance, record);

                if (!ShouldAbilityRemainActive(instance))
                {
                    CompleteAbilityInstance(attempt.SourceActor, instance);
                }
            }
        }

        /// <summary>
        /// 自动确保需要常驻的被动能力实例被创建出来。
        /// </summary>
        private void EnsurePassiveAbilities()
        {
            foreach (var actor in WorldState.Actors)
            {
                for (var i = 0; i < actor.GrantedAbilities.Count; i++)
                {
                    AbilityDefinition ability;
                    if (!WorldState.TryGetAbility(actor.GrantedAbilities[i], out ability))
                    {
                        continue;
                    }

                    if (ability.ActivationMode != AbilityActivationMode.Passive || !ability.AutoActivatePassive)
                    {
                        continue;
                    }

                    if (HasActiveAbilityInstance(actor, ability.Id))
                    {
                        continue;
                    }

                    var instance = RentAbilityInstance(
                        WorldState.CreateAbilityInstanceId(),
                        actor.ActorId,
                        ability,
                        null,
                        WorldState.CurrentTick);

                    actor.ActiveAbilityInstances.Add(instance);
                    RegisterAbilityTriggerInstances(actor, instance);
                    _eventSink.Publish(new CombatEvent(CombatEventKind.AbilityStarted, actor.ActorId, ability.Name ?? ability.Id.Value));
                    ProcessOwnedTriggerEvent(
                        CombatTriggerEventKind.OnAbilityStarted,
                        CombatTriggerTiming.EndOfStage,
                        actor.ActorId,
                        actor.ActorId,
                        actor.ActorId,
                        ability,
                        null,
                        null,
                        null,
                        null,
                        null,
                        instance);
                    ActivateAbilityInstance(actor, instance, null);
                }
            }
        }

        /// <summary>
        /// 推进单位身上的所有能力实例。
        /// </summary>
        private void ProcessAbilityInstances(CombatActorState actor)
        {
            for (var i = actor.ActiveAbilityInstances.Count - 1; i >= 0; i--)
            {
                var instance = actor.ActiveAbilityInstances[i];

                if (instance.Ability.ActivationMode == AbilityActivationMode.Passive && !actor.HasAbility(instance.Ability.Id))
                {
                    CancelAbilityInstance(actor, instance, "Passive ability was revoked.");
                    continue;
                }

                if (instance.Ability.CancelOnSourceDeath && !actor.IsAlive)
                {
                    CancelAbilityInstance(actor, instance, "Source actor died.");
                    continue;
                }

                if (instance.ShouldResolveCast(WorldState.CurrentTick))
                {
                    instance.EnterActive(WorldState.CurrentTick);
                    ActivateAbilityInstance(actor, instance, null);

                    if (!ShouldAbilityRemainActive(instance))
                    {
                        CompleteAbilityInstance(actor, instance);
                    }

                    continue;
                }

                if (instance.State != ActiveAbilityState.Active)
                {
                    continue;
                }

                while (instance.ShouldTriggerPeriodic(WorldState.CurrentTick))
                {
                    ExecuteAbilityStage(actor, instance, instance.Ability.PeriodicEffects, ActionExecutionStage.Periodic, null);
                    instance.AdvancePeriodic();

                    if (instance.State != ActiveAbilityState.Active)
                    {
                        break;
                    }
                }

                if (instance.State == ActiveAbilityState.Active && instance.ShouldExpire(WorldState.CurrentTick))
                {
                    CompleteAbilityInstance(actor, instance);
                }
            }
        }

        /// <summary>
        /// 在能力实例进入 Active 阶段后执行初次激活逻辑。
        /// </summary>
        private void ActivateAbilityInstance(CombatActorState actor, ActiveAbilityInstance instance, AbilityExecutionRecord record)
        {
            ApplyAbilityActiveTags(actor, instance);

            if (!instance.InitialExecutionCompleted)
            {
                if (instance.Ability.ExecuteEffectsOnActivate)
                {
                    ExecuteAbilityStage(actor, instance, instance.Ability.Effects, ActionExecutionStage.Execute, record);
                }

                instance.InitialExecutionCompleted = true;
            }
        }

        /// <summary>
        /// 执行能力生命周期中的某个阶段，并为该阶段构造 attempt / impact。
        /// </summary>
        private void ExecuteAbilityStage(
            CombatActorState actor,
            ActiveAbilityInstance instance,
            IList<EffectDefinition> effects,
            ActionExecutionStage stage,
            AbilityExecutionRecord record)
        {
            if (effects == null || effects.Count == 0)
            {
                return;
            }

            // Ability instances only store locked targets. We materialize a temporary target data
            // object here, feed it through targeting/impact generation, then immediately recycle it.
            var targetData = _pools.AbilityTargetDataItems.Rent();
            instance.PopulateTargetData(targetData);

            var request = new AbilityActivationRequest
            {
                SourceActorId = actor.ActorId,
                AbilityId = instance.Ability.Id,
                RequestTick = WorldState.CurrentTick,
                TargetData = targetData,
            };

            var executionContext = new AbilityExecutionContext(request, instance.Ability, WorldState.CurrentTick);
            var attempt = new CombatActionAttempt(request, actor, instance.Ability, executionContext, stage)
            {
                AbilityInstance = instance,
            };

            try
            {
                _targetingResolver.ResolveTargets(WorldState, actor, instance.Ability, request.TargetData, attempt.Targets);
                QueueImpactsForEffects(attempt, actor, instance.Ability, attempt.Targets, effects, stage);
                ResolveAttemptImpacts(attempt, record);
            }
            finally
            {
                _pools.AbilityTargetDataItems.Return(targetData);
            }
        }

        /// <summary>
        /// 正常结束一个能力实例。
        /// </summary>
        private void CompleteAbilityInstance(CombatActorState actor, ActiveAbilityInstance instance)
        {
            if (actor == null || instance == null || instance.Ability == null)
            {
                return;
            }

            var abilityName = instance.Ability.Name ?? instance.Ability.Id.Value;
            ExecuteAbilityStage(actor, instance, instance.Ability.EndEffects, ActionExecutionStage.End, null);
            RemoveAbilityActiveTags(actor, instance);
            instance.MarkCompleted();
            actor.ActiveAbilityInstances.Remove(instance);
            ProcessOwnedTriggerEvent(
                CombatTriggerEventKind.OnAbilityCompleted,
                CombatTriggerTiming.EndOfStage,
                actor.ActorId,
                actor.ActorId,
                actor.ActorId,
                instance.Ability,
                null,
                null,
                null,
                null,
                null,
                instance);
            _eventSink.Publish(new CombatEvent(CombatEventKind.AbilityCompleted, actor.ActorId, abilityName));
            ReleaseActiveAbilityInstance(instance);
        }

        /// <summary>
        /// 取消一个能力实例。
        /// </summary>
        private void CancelAbilityInstance(CombatActorState actor, ActiveAbilityInstance instance, string reason)
        {
            if (actor == null || instance == null || instance.Ability == null)
            {
                return;
            }

            ExecuteAbilityStage(actor, instance, instance.Ability.EndEffects, ActionExecutionStage.End, null);
            RemoveAbilityActiveTags(actor, instance);
            instance.MarkCancelled();
            actor.ActiveAbilityInstances.Remove(instance);
            ProcessOwnedTriggerEvent(
                CombatTriggerEventKind.OnAbilityCancelled,
                CombatTriggerTiming.EndOfStage,
                actor.ActorId,
                actor.ActorId,
                actor.ActorId,
                instance.Ability,
                null,
                null,
                null,
                null,
                null,
                instance);
            _eventSink.Publish(new CombatEvent(CombatEventKind.AbilityCancelled, actor.ActorId, reason));
            ReleaseActiveAbilityInstance(instance);
        }

        /// <summary>
        /// 判断单位当前是否已有同一技能的运行中实例。
        /// </summary>
        private bool HasActiveAbilityInstance(CombatActorState actor, AbilityId abilityId)
        {
            for (var i = 0; i < actor.ActiveAbilityInstances.Count; i++)
            {
                if (actor.ActiveAbilityInstances[i].Ability.Id == abilityId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断一个技能实例是否还应继续保留在运行时列表中。
        /// </summary>
        private bool ShouldAbilityRemainActive(ActiveAbilityInstance instance)
        {
            if (instance.Ability.ActivationMode == AbilityActivationMode.Passive)
            {
                return true;
            }

            if (instance.Ability.ActivationMode == AbilityActivationMode.Channeled)
            {
                return true;
            }

            if (instance.Ability.ActiveDurationTicks > 0)
            {
                return true;
            }

            if (instance.Ability.IntervalTicks > 0 && instance.Ability.PeriodicEffects.Count > 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 在技能实例激活期间给施法者附加 GrantedTagsWhileActive。
        /// </summary>
        private void ApplyAbilityActiveTags(CombatActorState actor, ActiveAbilityInstance instance)
        {
            foreach (var tag in instance.Ability.GrantedTagsWhileActive)
            {
                actor.AddTagReference(tag);
            }
        }

        /// <summary>
        /// 在技能实例结束时移除激活期附加的 Tag。
        /// </summary>
        private void RemoveAbilityActiveTags(CombatActorState actor, ActiveAbilityInstance instance)
        {
            foreach (var tag in instance.Ability.GrantedTagsWhileActive)
            {
                actor.RemoveTagReference(tag);
            }
        }

        /// <summary>
        /// 根据 effect 定义为本次 attempt 生成结构化 impacts。
        /// </summary>
        private void QueueImpactsForEffects(
            CombatActionAttempt attempt,
            CombatActorState sourceActor,
            AbilityDefinition ability,
            IList<CombatActorState> targets,
            IList<EffectDefinition> effects,
            ActionExecutionStage stage)
        {
            if (effects == null || effects.Count == 0)
            {
                return;
            }

            // The pipeline intentionally materializes impacts first and resolves them later.
            // This keeps mutation / blocking / replay hooks operating on a stable data model.
            for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                var target = targets[targetIndex];

                for (var effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                {
                    var effectDefinition = effects[effectIndex];
                    if (!CanApplyEffectToTarget(target, effectDefinition))
                    {
                        continue;
                    }

                    var impact = new CombatImpact(sourceActor.ActorId, target.ActorId, ability.Id, WorldState.CurrentTick, stage)
                    {
                        EffectId = effectDefinition.Id,
                    };
                    impact.Tags.AddRange(ability.AbilityTags);
                    impact.Tags.AddRange(effectDefinition.EffectTags);

                    foreach (var tag in effectDefinition.RemovedTargetEffectTags)
                    {
                        impact.Operations.Add(new CombatImpactOperation
                        {
                            Type = CombatImpactOperationType.RemoveEffectsByTag,
                            Tag = tag,
                        });
                    }

                    for (var removedEffectIndex = 0; removedEffectIndex < effectDefinition.RemovedTargetEffectIds.Count; removedEffectIndex++)
                    {
                        impact.Operations.Add(new CombatImpactOperation
                        {
                            Type = CombatImpactOperationType.RemoveEffectById,
                            EffectId = effectDefinition.RemovedTargetEffectIds[removedEffectIndex],
                        });
                    }

                    for (var deltaIndex = 0; deltaIndex < effectDefinition.InstantResourceDeltas.Count; deltaIndex++)
                    {
                        var delta = effectDefinition.InstantResourceDeltas[deltaIndex];
                        impact.Operations.Add(new CombatImpactOperation
                        {
                            Type = CombatImpactOperationType.ResourceDelta,
                            ResourceId = delta.ResourceId,
                            Amount = delta.Amount,
                        });
                    }

                    if (effectDefinition.DurationPolicy != EffectDurationPolicy.Instant ||
                        effectDefinition.AttributeModifiers.Count > 0 ||
                        effectDefinition.PeriodicResourceDeltas.Count > 0 ||
                        effectDefinition.GrantedTags.Count > 0 ||
                        HasRuntimeEffectPayload(effectDefinition))
                    {
                        impact.Operations.Add(new CombatImpactOperation
                        {
                            Type = CombatImpactOperationType.ApplyEffect,
                            EffectSpec = new EffectSpec(effectDefinition, sourceActor.ActorId, target.ActorId, WorldState.CurrentTick),
                        });
                    }

                    if (impact.Operations.Count > 0)
                    {
                        attempt.Impacts.Add(impact);
                    }
                }
            }
        }

        /// <summary>
        /// 对 attempt 中已经生成的 impacts 做 mutate 与 resolve。
        /// </summary>
        private void ResolveAttemptImpacts(CombatActionAttempt attempt, AbilityExecutionRecord record)
        {
            // Mutators run before built-in resolution so rule systems can rewrite magnitudes,
            // add custom operations, or strip operations without patching runtime core logic.
            for (var impactIndex = 0; impactIndex < attempt.Impacts.Count; impactIndex++)
            {
                var impact = attempt.Impacts[impactIndex];

                for (var mutatorIndex = 0; mutatorIndex < _impactMutators.Count; mutatorIndex++)
                {
                    _impactMutators[mutatorIndex].Mutate(attempt, impact, WorldState);
                }

                ProcessOwnedTriggerEvent(
                    CombatTriggerEventKind.BeforeImpactResolve,
                    CombatTriggerTiming.ImmediatePreResolve,
                    impact.TargetActorId,
                    impact.SourceActorId,
                    impact.TargetActorId,
                    attempt.Ability,
                    ResolveRelatedEffectDefinition(attempt, impact),
                    attempt,
                    impact,
                    null,
                    null,
                    attempt.AbilityInstance);

                ProcessOwnedTriggerEvent(
                    CombatTriggerEventKind.BeforeImpactResolve,
                    CombatTriggerTiming.ImmediatePreResolve,
                    impact.SourceActorId,
                    impact.SourceActorId,
                    impact.TargetActorId,
                    attempt.Ability,
                    ResolveRelatedEffectDefinition(attempt, impact),
                    attempt,
                    impact,
                    null,
                    null,
                    attempt.AbilityInstance);

                for (var operationIndex = 0; operationIndex < impact.Operations.Count; operationIndex++)
                {
                    var operation = impact.Operations[operationIndex];
                    if (TryResolveCustomImpact(attempt, impact, operation))
                    {
                        continue;
                    }

                    ResolveBuiltInImpact(attempt, impact, operation, record);
                }

                if (record != null)
                {
                    record.AppliedImpacts.Add(impact);
                }

                ProcessOwnedTriggerEvent(
                    CombatTriggerEventKind.AfterImpactResolved,
                    CombatTriggerTiming.ImmediatePostResolve,
                    impact.TargetActorId,
                    impact.SourceActorId,
                    impact.TargetActorId,
                    attempt.Ability,
                    ResolveRelatedEffectDefinition(attempt, impact),
                    attempt,
                    impact,
                    null,
                    null,
                    attempt.AbilityInstance);

                ProcessOwnedTriggerEvent(
                    CombatTriggerEventKind.AfterImpactResolved,
                    CombatTriggerTiming.ImmediatePostResolve,
                    impact.SourceActorId,
                    impact.SourceActorId,
                    impact.TargetActorId,
                    attempt.Ability,
                    ResolveRelatedEffectDefinition(attempt, impact),
                    attempt,
                    impact,
                    null,
                    null,
                    attempt.AbilityInstance);
            }
        }

        /// <summary>
        /// 尝试把自定义 ImpactOperation 交给外部 Resolver 处理。
        /// </summary>
        private bool TryResolveCustomImpact(CombatActionAttempt attempt, CombatImpact impact, CombatImpactOperation operation)
        {
            for (var resolverIndex = 0; resolverIndex < _impactResolvers.Count; resolverIndex++)
            {
                var resolver = _impactResolvers[resolverIndex];
                if (!resolver.CanResolve(operation))
                {
                    continue;
                }

                resolver.Resolve(attempt, impact, operation, this);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 处理 Core 内建支持的 ImpactOperation。
        /// </summary>
        private void ResolveBuiltInImpact(
            CombatActionAttempt attempt,
            CombatImpact impact,
            CombatImpactOperation operation,
            AbilityExecutionRecord record)
        {
            CombatActorState targetActor;

            switch (operation.Type)
            {
                case CombatImpactOperationType.ResourceDelta:
                    if (!WorldState.TryGetActor(impact.TargetActorId, out targetActor))
                    {
                        return;
                    }

                    CombatActorState sourceActor;
                    WorldState.TryGetActor(impact.SourceActorId, out sourceActor);

                    var adjustedAmount = ApplyRuleModulesToResourceDelta(
                        CombatResourceDeltaSourceKind.ImpactOperation,
                        sourceActor,
                        targetActor,
                        ResolveRelatedEffectDefinition(attempt, impact),
                        null,
                        attempt,
                        impact,
                        operation,
                        operation.ResourceId,
                        operation.Amount,
                        out var consumeDefaultApply);
                    if (consumeDefaultApply)
                    {
                        operation.Amount = adjustedAmount;
                        return;
                    }

                    var resourceValue = targetActor.Resources.GetOrCreate(operation.ResourceId);
                    var previousValue = resourceValue.Current;
                    resourceValue.Add(adjustedAmount);
                    operation.Amount = adjustedAmount;
                    _eventSink.Publish(new CombatEvent(CombatEventKind.ResourceChanged, targetActor.ActorId, operation.ResourceId.Value));

                    ProcessOwnedTriggerEvent(
                        CombatTriggerEventKind.OnResourceChanged,
                        CombatTriggerTiming.ImmediatePostResolve,
                        targetActor.ActorId,
                        impact.SourceActorId,
                        targetActor.ActorId,
                        attempt.Ability,
                        ResolveRelatedEffectDefinition(attempt, impact),
                        attempt,
                        impact,
                        operation,
                        null,
                        attempt.AbilityInstance,
                        operation.ResourceId,
                        previousValue,
                        resourceValue.Current,
                        null);

                    ProcessOwnedTriggerEvent(
                        CombatTriggerEventKind.OnResourceChanged,
                        CombatTriggerTiming.ImmediatePostResolve,
                        impact.SourceActorId,
                        impact.SourceActorId,
                        targetActor.ActorId,
                        attempt.Ability,
                        ResolveRelatedEffectDefinition(attempt, impact),
                        attempt,
                        impact,
                        operation,
                        null,
                        attempt.AbilityInstance,
                        operation.ResourceId,
                        previousValue,
                        resourceValue.Current,
                        null);
                    return;

                case CombatImpactOperationType.ApplyEffect:
                    if (!WorldState.TryGetActor(impact.TargetActorId, out targetActor))
                    {
                        return;
                    }

                    ApplyEffectSpec(targetActor, operation.EffectSpec, record);
                    return;

                case CombatImpactOperationType.RemoveEffectsByTag:
                    if (!WorldState.TryGetActor(impact.TargetActorId, out targetActor))
                    {
                        return;
                    }

                    RemoveEffectsByGrantedTag(targetActor, operation.Tag);
                    return;

                case CombatImpactOperationType.RemoveEffectById:
                    if (!WorldState.TryGetActor(impact.TargetActorId, out targetActor))
                    {
                        return;
                    }

                    RemoveEffectsById(targetActor, operation.EffectId);
                    return;

                case CombatImpactOperationType.Cue:
                    _eventSink.Publish(new CombatEvent(CombatEventKind.EffectApplied, impact.TargetActorId, operation.CueName ?? impact.EffectId.Value));
                    return;
            }
        }

        /// <summary>
        /// 将一个 effectSpec 应用到目标身上，自动区分瞬时效果与持续效果。
        /// </summary>
        private void ApplyEffectSpec(CombatActorState targetActor, EffectSpec spec, AbilityExecutionRecord record)
        {
            ProcessOwnedTriggerEvent(
                CombatTriggerEventKind.BeforeEffectApplied,
                CombatTriggerTiming.ImmediatePreResolve,
                targetActor.ActorId,
                spec.SourceActorId,
                targetActor.ActorId,
                null,
                spec.Definition,
                null,
                null,
                null,
                null,
                null);

            ProcessOwnedTriggerEvent(
                CombatTriggerEventKind.BeforeEffectApplied,
                CombatTriggerTiming.ImmediatePreResolve,
                spec.SourceActorId,
                spec.SourceActorId,
                targetActor.ActorId,
                null,
                spec.Definition,
                null,
                null,
                null,
                null,
                null);

            if (spec.Definition.DurationPolicy == EffectDurationPolicy.Instant &&
                spec.Definition.InstantResourceDeltas.Count == 0 &&
                spec.Definition.AttributeModifiers.Count == 0 &&
                spec.Definition.PeriodicResourceDeltas.Count == 0 &&
                spec.Definition.GrantedTags.Count == 0 &&
                !HasRuntimeEffectPayload(spec.Definition))
            {
                _eventSink.Publish(new CombatEvent(CombatEventKind.EffectApplied, targetActor.ActorId, spec.Definition.Id.Value));
                if (record != null)
                {
                    record.AppliedEffects.Add(spec);
                }

                ProcessAfterEffectAppliedTriggers(targetActor.ActorId, spec.SourceActorId, spec.Definition, null);
                return;
            }

            if (spec.Definition.DurationPolicy == EffectDurationPolicy.Instant)
            {
                ApplyInstantEffect(targetActor, spec);

                if (record != null)
                {
                    record.AppliedEffects.Add(spec);
                }

                ProcessAfterEffectAppliedTriggers(targetActor.ActorId, spec.SourceActorId, spec.Definition, null);
                return;
            }

            var activeEffect = ApplyPersistentEffect(targetActor, spec);

            if (record != null)
            {
                record.AppliedEffects.Add(spec);
            }

            ProcessAfterEffectAppliedTriggers(targetActor.ActorId, spec.SourceActorId, spec.Definition, activeEffect);
        }

        /// <summary>
        /// 处理瞬时效果。
        /// </summary>
        private void ApplyInstantEffect(CombatActorState targetActor, EffectSpec spec)
        {
            CombatActorState sourceActor;
            WorldState.TryGetActor(spec.SourceActorId, out sourceActor);

            var deltas = spec.Definition.InstantResourceDeltas;
            for (var i = 0; i < deltas.Count; i++)
            {
                var delta = deltas[i];
                var adjustedAmount = ApplyRuleModulesToResourceDelta(
                    CombatResourceDeltaSourceKind.InstantEffect,
                    sourceActor,
                    targetActor,
                    spec.Definition,
                    null,
                    null,
                    null,
                    null,
                    delta.ResourceId,
                    delta.Amount * spec.Stacks,
                    out var consumeDefaultApply);
                if (consumeDefaultApply)
                {
                    continue;
                }

                var resourceValue = targetActor.Resources.GetOrCreate(delta.ResourceId);
                var previousValue = resourceValue.Current;
                resourceValue.Add(adjustedAmount);
                ProcessOwnedTriggerEvent(
                    CombatTriggerEventKind.OnResourceChanged,
                    CombatTriggerTiming.ImmediatePostResolve,
                    targetActor.ActorId,
                    spec.SourceActorId,
                    targetActor.ActorId,
                    null,
                    spec.Definition,
                    null,
                    null,
                    null,
                    null,
                    null,
                    delta.ResourceId,
                    previousValue,
                    resourceValue.Current,
                    null);

                ProcessOwnedTriggerEvent(
                    CombatTriggerEventKind.OnResourceChanged,
                    CombatTriggerTiming.ImmediatePostResolve,
                    spec.SourceActorId,
                    spec.SourceActorId,
                    targetActor.ActorId,
                    null,
                    spec.Definition,
                    null,
                    null,
                    null,
                    null,
                    null,
                    delta.ResourceId,
                    previousValue,
                    resourceValue.Current,
                    null);
            }

            _eventSink.Publish(new CombatEvent(CombatEventKind.EffectApplied, targetActor.ActorId, spec.Definition.Id.Value));
        }

        /// <summary>
        /// 处理持续效果，包括叠层、替换、属性修正与标签授予。
        /// </summary>
        private ActiveEffect ApplyPersistentEffect(CombatActorState targetActor, EffectSpec spec)
        {
            // Persistent effects are the GAS-style "active gameplay effect" layer: they own
            // duration, stacking, granted tags and attribute modifiers until removal / expiry.
            var existing = FindActiveEffect(targetActor, spec.Definition.Id);
            if (existing != null)
            {
                if (!HandleStacking(targetActor, existing, spec))
                {
                    return null;
                }
            }

            if (existing != null && spec.Definition.StackPolicy == EffectStackPolicy.ReplaceExisting)
            {
                RemoveEffect(targetActor, existing);
            }

            if (existing != null && spec.Definition.StackPolicy != EffectStackPolicy.ReplaceExisting)
            {
                _eventSink.Publish(new CombatEvent(CombatEventKind.EffectApplied, targetActor.ActorId, spec.Definition.Id.Value));
                return existing;
            }

            var activeEffect = RentActiveEffect(spec, WorldState.CurrentTick);
            ApplyPersistentModifiers(targetActor, activeEffect);
            ApplyGrantedTags(targetActor, activeEffect);
            targetActor.ActiveEffects.Add(activeEffect);
            NotifyEffectApplied(targetActor, activeEffect);
            RegisterEffectTriggerInstances(targetActor, activeEffect);
            _eventSink.Publish(new CombatEvent(CombatEventKind.EffectApplied, targetActor.ActorId, spec.Definition.Id.Value));
            return activeEffect;
        }

        /// <summary>
        /// 把持续效果的属性修正挂到目标属性集合上。
        /// </summary>
        private void ApplyPersistentModifiers(CombatActorState targetActor, ActiveEffect activeEffect)
        {
            var modifierDefinitions = activeEffect.Spec.Definition.AttributeModifiers;
            for (var i = 0; i < modifierDefinitions.Count; i++)
            {
                var definition = modifierDefinitions[i];
                var modifier = new AttributeModifier(
                    definition.AttributeId,
                    definition.ModifierType,
                    definition.Magnitude * activeEffect.Spec.Stacks,
                    activeEffect);

                targetActor.Attributes.AddModifier(modifier);
            }
        }

        /// <summary>
        /// 把持续效果授予的 Tag 挂到目标身上。
        /// </summary>
        private void ApplyGrantedTags(CombatActorState targetActor, ActiveEffect activeEffect)
        {
            foreach (var tag in activeEffect.Spec.Definition.GrantedTags)
            {
                targetActor.AddTagReference(tag);
            }
        }

        /// <summary>
        /// 推进单位身上的持续效果。
        /// </summary>
        /// <summary>
        /// 根据效果定义初始化护盾运行时状态。
        /// </summary>
        private void NotifyEffectApplied(CombatActorState targetActor, ActiveEffect activeEffect)
        {
            for (var moduleIndex = 0; moduleIndex < _ruleModules.Count; moduleIndex++)
            {
                _ruleModules[moduleIndex].OnEffectApplied(targetActor, activeEffect, this);
            }
        }

        /// <summary>
        /// 通知各规则模块某个效果刚被刷新。
        /// </summary>
        private void NotifyEffectRefreshed(CombatActorState targetActor, ActiveEffect activeEffect)
        {
            for (var moduleIndex = 0; moduleIndex < _ruleModules.Count; moduleIndex++)
            {
                _ruleModules[moduleIndex].OnEffectRefreshed(targetActor, activeEffect, this);
            }
        }

        /// <summary>
        /// 在效果真正移除前通知各规则模块做清理。
        /// </summary>
        private void NotifyEffectRemoving(CombatActorState targetActor, ActiveEffect activeEffect)
        {
            for (var moduleIndex = 0; moduleIndex < _ruleModules.Count; moduleIndex++)
            {
                _ruleModules[moduleIndex].OnEffectRemoving(targetActor, activeEffect, this);
            }
        }

        /// <summary>
        /// 让目标身上的标准护盾优先吸收本次伤害，返回剩余未吸收的伤害值。
        /// </summary>



        private void ProcessEffects(CombatActorState actor)
        {
            for (var i = actor.ActiveEffects.Count - 1; i >= 0; i--)
            {
                var activeEffect = actor.ActiveEffects[i];

                if (activeEffect.ShouldTriggerPeriodic(WorldState.CurrentTick))
                {
                    ApplyPeriodicEffect(actor, activeEffect);
                    activeEffect.MarkPeriodicTriggered(WorldState.CurrentTick);
                }

                if (!activeEffect.HasExpired(WorldState.CurrentTick))
                {
                    continue;
                }

                RemoveEffect(actor, activeEffect);
            }
        }

        /// <summary>
        /// 在周期 Tick 结算一个持续效果的周期逻辑。
        /// </summary>
        private void ApplyPeriodicEffect(CombatActorState actor, ActiveEffect activeEffect)
        {
            CombatActorState sourceActor;
            WorldState.TryGetActor(activeEffect.Spec.SourceActorId, out sourceActor);

            var deltas = activeEffect.Spec.Definition.PeriodicResourceDeltas;
            for (var i = 0; i < deltas.Count; i++)
            {
                var delta = deltas[i];
                var adjustedAmount = ApplyRuleModulesToResourceDelta(
                    CombatResourceDeltaSourceKind.PeriodicEffect,
                    sourceActor,
                    actor,
                    activeEffect.Spec.Definition,
                    activeEffect,
                    null,
                    null,
                    null,
                    delta.ResourceId,
                    delta.Amount * activeEffect.Spec.Stacks,
                    out var consumeDefaultApply);
                if (consumeDefaultApply)
                {
                    continue;
                }

                var resourceValue = actor.Resources.GetOrCreate(delta.ResourceId);
                var previousValue = resourceValue.Current;
                resourceValue.Add(adjustedAmount);
                ProcessOwnedTriggerEvent(
                    CombatTriggerEventKind.OnResourceChanged,
                    CombatTriggerTiming.ImmediatePostResolve,
                    actor.ActorId,
                    activeEffect.Spec.SourceActorId,
                    actor.ActorId,
                    null,
                    activeEffect.Spec.Definition,
                    null,
                    null,
                    null,
                    activeEffect,
                    null,
                    delta.ResourceId,
                    previousValue,
                    resourceValue.Current,
                    null);

                ProcessOwnedTriggerEvent(
                    CombatTriggerEventKind.OnResourceChanged,
                    CombatTriggerTiming.ImmediatePostResolve,
                    activeEffect.Spec.SourceActorId,
                    activeEffect.Spec.SourceActorId,
                    actor.ActorId,
                    null,
                    activeEffect.Spec.Definition,
                    null,
                    null,
                    null,
                    activeEffect,
                    null,
                    delta.ResourceId,
                    previousValue,
                    resourceValue.Current,
                    null);
            }
        }

        /// <summary>
        /// 按 GrantedTag 批量移除目标身上的效果。
        /// </summary>
        private void RemoveEffectsByGrantedTag(CombatActorState actor, GameplayTag tag)
        {
            for (var i = actor.ActiveEffects.Count - 1; i >= 0; i--)
            {
                var effect = actor.ActiveEffects[i];
                if (!effect.Spec.Definition.GrantedTags.Contains(tag))
                {
                    continue;
                }

                RemoveEffect(actor, effect);
            }
        }

        /// <summary>
        /// 按 EffectId 批量移除目标身上的效果。
        /// </summary>
        private void RemoveEffectsById(CombatActorState actor, EffectId effectId)
        {
            for (var i = actor.ActiveEffects.Count - 1; i >= 0; i--)
            {
                var effect = actor.ActiveEffects[i];
                if (effect.Spec.Definition.Id != effectId)
                {
                    continue;
                }

                RemoveEffect(actor, effect);
            }
        }

        /// <summary>
        /// 从单位身上彻底移除一个持续效果，并归还对象池。
        /// </summary>
        private void RemoveEffect(CombatActorState actor, ActiveEffect activeEffect)
        {
            if (actor == null || activeEffect == null || activeEffect.Spec == null)
            {
                return;
            }

            var effectIdValue = activeEffect.Spec.Definition.Id.Value;
            actor.Attributes.RemoveModifiersBySource(activeEffect);

            foreach (var tag in activeEffect.Spec.Definition.GrantedTags)
            {
                actor.RemoveTagReference(tag);
            }

            NotifyEffectRemoving(actor, activeEffect);
            actor.ActiveEffects.Remove(activeEffect);
            ProcessOwnedTriggerEvent(
                CombatTriggerEventKind.AfterEffectExpired,
                CombatTriggerTiming.ImmediatePostResolve,
                actor.ActorId,
                activeEffect.Spec.SourceActorId,
                actor.ActorId,
                null,
                activeEffect.Spec.Definition,
                null,
                null,
                null,
                activeEffect,
                null);
            _eventSink.Publish(new CombatEvent(CombatEventKind.EffectExpired, actor.ActorId, effectIdValue));
            ReleaseActiveEffect(activeEffect);
        }

        /// <summary>
        /// 查找单位身上某个 Id 的运行中效果。
        /// </summary>
        private ActiveEffect FindActiveEffect(CombatActorState actor, EffectId effectId)
        {
            for (var i = 0; i < actor.ActiveEffects.Count; i++)
            {
                var activeEffect = actor.ActiveEffects[i];
                if (activeEffect.Spec.Definition.Id == effectId)
                {
                    return activeEffect;
                }
            }

            return null;
        }

        /// <summary>
        /// 处理同类效果重复施加时的叠层行为。
        /// </summary>
        private bool HandleStacking(CombatActorState targetActor, ActiveEffect existing, EffectSpec incoming)
        {
            var definition = incoming.Definition;
            switch (definition.StackPolicy)
            {
                case EffectStackPolicy.RefreshDuration:
                    existing.Refresh(WorldState.CurrentTick);
                    NotifyEffectRefreshed(targetActor, existing);
                    return true;
                case EffectStackPolicy.AddStackAndRefresh:
                    var previousStacks = existing.Spec.Stacks;
                    if (existing.Spec.Stacks < definition.MaxStacks)
                    {
                        existing.Spec.Stacks += incoming.Stacks;
                        if (existing.Spec.Stacks > definition.MaxStacks)
                        {
                            existing.Spec.Stacks = definition.MaxStacks;
                        }
                    }

                    existing.Refresh(WorldState.CurrentTick);
                    NotifyEffectRefreshed(targetActor, existing);
                    if (existing.Spec.Stacks != previousStacks)
                    {
                        RebuildPersistentModifiers(targetActor, existing);
                    }

                    return true;
                case EffectStackPolicy.RejectNew:
                    return false;
                case EffectStackPolicy.ReplaceExisting:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 校验目标是否满足技能目标规则与射程要求。
        /// </summary>
        private bool ValidateTargets(
            CombatActorState sourceActor,
            AbilityDefinition ability,
            AbilityTargetData targetData,
            IList<CombatActorState> targets,
            out string reason)
        {
            reason = null;

            if (ability.Targeting.Kind == AbilityTargetKind.None)
            {
                return true;
            }

            if (ability.Targeting.Kind == AbilityTargetKind.Point || ability.Targeting.Kind == AbilityTargetKind.Area)
            {
                if (targetData != null && targetData.TargetPoint.HasValue)
                {
                    if (!IsPointInRange(sourceActor, targetData.TargetPoint.Value, ability))
                    {
                        reason = "Target point is out of range.";
                        return false;
                    }

                    return true;
                }

                reason = "Point-target ability requires a target point.";
                return false;
            }

            if (targets == null || targets.Count == 0)
            {
                reason = "No valid target resolved.";
                return false;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];

                if (!target.Tags.ContainsAll(ability.Targeting.RequiredTargetTags))
                {
                    reason = "Target missing required tags.";
                    return false;
                }

                if (target.Tags.ContainsAny(ability.Targeting.BlockedTargetTags))
                {
                    reason = "Target blocked by target tags.";
                    return false;
                }

                if (!MatchesTargetFlags(sourceActor, target, ability.Targeting.AllowedFlags))
                {
                    reason = "Target does not match targeting flags.";
                    return false;
                }

                if (!IsTargetInRange(sourceActor, target, ability))
                {
                    reason = "Target is out of range.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 判断目标单位是否满足技能目标标记约束。
        /// </summary>
        private bool MatchesTargetFlags(CombatActorState sourceActor, CombatActorState target, AbilityTargetFlags flags)
        {
            if (flags == AbilityTargetFlags.None)
            {
                return true;
            }

            var requiredRelations = GetRequiredTargetRelations(flags);
            var hasRelationConstraint = requiredRelations != CombatActorRelationFlags.None;
            var relationMatched = !hasRelationConstraint || MatchesActorRelation(sourceActor, target, requiredRelations);

            if (hasRelationConstraint && !relationMatched)
            {
                return false;
            }

            var hasLifeConstraint =
                (flags & AbilityTargetFlags.Dead) != 0 ||
                (flags & AbilityTargetFlags.Alive) != 0;

            if (hasLifeConstraint)
            {
                var lifeMatched = false;

                if ((flags & AbilityTargetFlags.Dead) != 0 && !target.IsAlive)
                {
                    lifeMatched = true;
                }

                if ((flags & AbilityTargetFlags.Alive) != 0 && target.IsAlive)
                {
                    lifeMatched = true;
                }

                if (!lifeMatched)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 将技能目标 Flags 中的关系部分映射为通用 Actor 关系标记。
        /// </summary>
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

        /// <summary>
        /// 解析两个 Actor 之间的抽象关系标记。
        /// </summary>
        internal CombatActorRelationFlags ResolveActorRelation(CombatActorState sourceActor, CombatActorState targetActor)
        {
            return _relationResolver.ResolveRelation(WorldState, sourceActor, targetActor);
        }

        /// <summary>
        /// 判断两个 Actor 是否满足指定关系 Flags。
        /// </summary>
        internal bool MatchesActorRelation(
            CombatActorState sourceActor,
            CombatActorState targetActor,
            CombatActorRelationFlags requiredRelations)
        {
            if (requiredRelations == CombatActorRelationFlags.Any)
            {
                return true;
            }

            if (requiredRelations == CombatActorRelationFlags.None ||
                sourceActor == null ||
                targetActor == null)
            {
                return false;
            }

            var resolvedRelations = ResolveActorRelation(sourceActor, targetActor);
            return (resolvedRelations & requiredRelations) != 0;
        }

        /// <summary>
        /// 判断某个 Actor 与指定 Id 对应单位之间是否满足指定关系 Flags。
        /// </summary>
        internal bool MatchesActorRelation(
            CombatActorState sourceActor,
            ActorId targetActorId,
            CombatActorRelationFlags requiredRelations)
        {
            if (requiredRelations == CombatActorRelationFlags.Any)
            {
                return true;
            }

            if (requiredRelations == CombatActorRelationFlags.None ||
                sourceActor == null ||
                targetActorId.IsEmpty)
            {
                return false;
            }

            CombatActorState targetActor;
            if (!WorldState.TryGetActor(targetActorId, out targetActor))
            {
                return false;
            }

            return MatchesActorRelation(sourceActor, targetActor, requiredRelations);
        }

        /// <summary>
        /// 让规则模块与目标 Tag 条件共同决定效果是否允许施加。
        /// </summary>
        private bool CanApplyEffectToTarget(CombatActorState targetActor, EffectDefinition effectDefinition)
        {
            if (!targetActor.Tags.ContainsAll(effectDefinition.RequiredTargetTags))
            {
                return false;
            }

            if (targetActor.Tags.ContainsAny(effectDefinition.BlockedTargetTags))
            {
                return false;
            }

            for (var moduleIndex = 0; moduleIndex < _ruleModules.Count; moduleIndex++)
            {
                string reason;
                if (_ruleModules[moduleIndex].TryBlockEffectApplication(targetActor, effectDefinition, WorldState, out reason))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 初始化全部规则模块。
        /// </summary>
        private void InitializeRuleModules()
        {
            for (var moduleIndex = 0; moduleIndex < _ruleModules.Count; moduleIndex++)
            {
                _ruleModules[moduleIndex].Initialize(this);
            }
        }

        /// <summary>
        /// 关闭全部规则模块。
        /// </summary>
        private void ShutdownRuleModules()
        {
            for (var moduleIndex = _ruleModules.Count - 1; moduleIndex >= 0; moduleIndex--)
            {
                _ruleModules[moduleIndex].Shutdown(this);
            }
        }

        /// <summary>
        /// 判断某个效果是否需要规则模块创建额外运行时状态。
        /// </summary>
        private bool HasRuntimeEffectPayload(EffectDefinition effectDefinition)
        {
            if (effectDefinition == null)
            {
                return false;
            }

            for (var moduleIndex = 0; moduleIndex < _ruleModules.Count; moduleIndex++)
            {
                if (_ruleModules[moduleIndex].HasRuntimeEffectPayload(effectDefinition))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 按顺序让全部规则模块参与一次资源变化改写。
        /// </summary>
        private FP ApplyRuleModulesToResourceDelta(
            CombatResourceDeltaSourceKind sourceKind,
            CombatActorState sourceActor,
            CombatActorState targetActor,
            EffectDefinition effectDefinition,
            ActiveEffect activeEffect,
            CombatActionAttempt attempt,
            CombatImpact impact,
            CombatImpactOperation operation,
            ResourceId resourceId,
            FP amount,
            out bool consumeDefaultApply)
        {
            if (_ruleModules.Count == 0)
            {
                consumeDefaultApply = false;
                return amount;
            }

            var context = new CombatResourceDeltaContext(
                this,
                sourceKind,
                sourceActor,
                targetActor,
                effectDefinition,
                activeEffect,
                attempt,
                impact,
                operation,
                resourceId,
                amount);

            for (var moduleIndex = 0; moduleIndex < _ruleModules.Count; moduleIndex++)
            {
                _ruleModules[moduleIndex].ProcessResourceDelta(context);
            }

            consumeDefaultApply = context.ConsumeDefaultApply;
            return context.Amount;
        }

        /// <summary>
        /// 判断某个点目标是否位于技能允许范围内。
        /// </summary>
        private bool IsPointInRange(CombatActorState sourceActor, WorldPosition point, AbilityDefinition ability)
        {
            if (ability.Targeting.MaxRange <= FP._0)
            {
                return true;
            }

            var maxDistanceSquared = ability.Targeting.MaxRange * ability.Targeting.MaxRange;
            return WorldPosition.DistanceSquared(sourceActor.Position, point) <= maxDistanceSquared;
        }

        /// <summary>
        /// 判断某个目标单位是否位于技能允许范围内。
        /// </summary>
        private bool IsTargetInRange(CombatActorState sourceActor, CombatActorState targetActor, AbilityDefinition ability)
        {
            if (ability.Targeting.MaxRange <= FP._0)
            {
                return true;
            }

            var maxDistanceSquared = ability.Targeting.MaxRange * ability.Targeting.MaxRange;
            return WorldPosition.DistanceSquared(sourceActor.Position, targetActor.Position) <= maxDistanceSquared;
        }

        /// <summary>
        /// 在叠层或刷新变化后重建某个效果带来的持续属性修正。
        /// </summary>
        private void RebuildPersistentModifiers(CombatActorState targetActor, ActiveEffect activeEffect)
        {
            targetActor.Attributes.RemoveModifiersBySource(activeEffect);
            ApplyPersistentModifiers(targetActor, activeEffect);
        }

        /// <summary>
        /// 从尝试对象和 Impact 中还原关联效果定义。
        /// </summary>
        private EffectDefinition ResolveRelatedEffectDefinition(CombatActionAttempt attempt, CombatImpact impact)
        {
            if (attempt != null && attempt.Ability != null)
            {
                for (var i = 0; i < attempt.Ability.Effects.Count; i++)
                {
                    var effect = attempt.Ability.Effects[i];
                    if (effect.Id == impact.EffectId)
                    {
                        return effect;
                    }
                }

                for (var i = 0; i < attempt.Ability.PeriodicEffects.Count; i++)
                {
                    var effect = attempt.Ability.PeriodicEffects[i];
                    if (effect.Id == impact.EffectId)
                    {
                        return effect;
                    }
                }

                for (var i = 0; i < attempt.Ability.EndEffects.Count; i++)
                {
                    var effect = attempt.Ability.EndEffects[i];
                    if (effect.Id == impact.EffectId)
                    {
                        return effect;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 在效果施加完成后补发相关 Trigger 事件。
        /// </summary>
        private void ProcessAfterEffectAppliedTriggers(
            ActorId targetActorId,
            ActorId sourceActorId,
            EffectDefinition effectDefinition,
            ActiveEffect activeEffect)
        {
            // 同一个 Effect 成功落地后，需要给目标和来源都补一个触发窗口。
            // 这样“被上了控制后净化自己”和“给敌人挂上标记后自己立刻进下一阶段”都能统一表达。
            ProcessOwnedTriggerEvent(
                CombatTriggerEventKind.AfterEffectApplied,
                CombatTriggerTiming.ImmediatePostResolve,
                targetActorId,
                sourceActorId,
                targetActorId,
                null,
                effectDefinition,
                null,
                null,
                null,
                activeEffect,
                null);

            ProcessOwnedTriggerEvent(
                CombatTriggerEventKind.AfterEffectApplied,
                CombatTriggerTiming.ImmediatePostResolve,
                sourceActorId,
                sourceActorId,
                targetActorId,
                null,
                effectDefinition,
                null,
                null,
                null,
                activeEffect,
                null);
        }

        /// <summary>
        /// 构造并投递一次“归属于某个 OwnerActor”的 Trigger 上下文。
        /// Runtime 中绝大多数触发入口最终都会收敛到这里。
        /// </summary>
        private void ProcessOwnedTriggerEvent(
            CombatTriggerEventKind eventKind,
            CombatTriggerTiming timing,
            ActorId ownerActorId,
            ActorId instigatorActorId,
            ActorId targetActorId,
            AbilityDefinition relatedAbility,
            EffectDefinition relatedEffect,
            CombatActionAttempt relatedAttempt,
            CombatImpact relatedImpact,
            CombatImpactOperation relatedOperation,
            ActiveEffect sourceActiveEffect,
            ActiveAbilityInstance sourceAbilityInstance,
            ResourceId resourceId = default(ResourceId),
            FP previousResourceValue = default(FP),
            FP currentResourceValue = default(FP),
            object customPayload = null)
        {
            RaiseTriggerEvent(new CombatTriggerContext
            {
                CurrentTick = WorldState.CurrentTick,
                EventKind = eventKind,
                Timing = timing,
                OwnerActorId = ownerActorId,
                InstigatorActorId = instigatorActorId,
                TargetActorId = targetActorId,
                RelatedAbility = relatedAbility,
                RelatedEffect = relatedEffect,
                RelatedAttempt = relatedAttempt,
                RelatedImpact = relatedImpact,
                RelatedOperation = relatedOperation,
                SourceActiveEffect = sourceActiveEffect,
                SourceAbilityInstance = sourceAbilityInstance,
                ResourceId = resourceId,
                PreviousResourceValue = previousResourceValue,
                CurrentResourceValue = currentResourceValue,
                CustomPayload = customPayload,
            });
        }

        /// <summary>
        /// 把一次失败的尝试包装成统一返回结果并派发事件。
        /// </summary>
        private AbilityActivationResult FailAttempt(CombatActionAttempt attempt)
        {
            var reason = attempt.Blocks.Count > 0
                ? attempt.Blocks[0].Message
                : "Ability activation failed.";

            var actorId = attempt.SourceActor == null ? ActorId.Empty : attempt.SourceActor.ActorId;
            _eventSink.Publish(new CombatEvent(CombatEventKind.AbilityBlocked, actorId, reason));
            ProcessOwnedTriggerEvent(
                CombatTriggerEventKind.AfterAttemptBlocked,
                CombatTriggerTiming.EndOfStage,
                actorId,
                actorId,
                attempt.Targets.Count > 0 ? attempt.Targets[0].ActorId : ActorId.Empty,
                attempt.Ability,
                null,
                attempt,
                null,
                null,
                null,
                attempt.AbilityInstance);
            return AbilityActivationResult.Fail(reason, attempt);
        }

        /// <summary>
        /// 从对象池租借并初始化一个技能实例。
        /// </summary>
        private ActiveAbilityInstance RentAbilityInstance(
            AbilityInstanceId instanceId,
            ActorId sourceActorId,
            AbilityDefinition ability,
            AbilityTargetData targetData,
            SimulationTick createdTick)
        {
            var instance = _pools.ActiveAbilityInstances.Rent();
            instance.Initialize(instanceId, sourceActorId, ability, targetData, createdTick);
            InitializeTriggerList(instance.ActiveTriggers, ability.Triggers);
            return instance;
        }

        /// <summary>
        /// 从对象池租借并初始化一个持续效果实例。
        /// </summary>
        private ActiveEffect RentActiveEffect(EffectSpec spec, SimulationTick currentTick)
        {
            var activeEffect = _pools.ActiveEffects.Rent();
            activeEffect.Initialize(spec, currentTick);
            InitializeTriggerList(activeEffect.ActiveTriggers, spec.Definition.Triggers);
            return activeEffect;
        }

        /// <summary>
        /// 从对象池租一个 Trigger 运行时实例，并写入静态定义。
        /// </summary>
        private ActiveTriggerInstance RentTriggerInstance(TriggerDefinition triggerDefinition)
        {
            var triggerInstance = _pools.ActiveTriggers.Rent();
            triggerInstance.Initialize(triggerDefinition);
            return triggerInstance;
        }

        /// <summary>
        /// 基于当前世界状态重建观察者 Trigger 注册表。
        /// </summary>
        private void RebuildTriggerRegistry()
        {
            _triggerRegistry.Reset();

            foreach (var actor in WorldState.Actors)
            {
                RegisterActorTriggerInstances(actor);

                for (var effectIndex = 0; effectIndex < actor.ActiveEffects.Count; effectIndex++)
                {
                    RegisterEffectTriggerInstances(actor, actor.ActiveEffects[effectIndex]);
                }

                for (var abilityIndex = 0; abilityIndex < actor.ActiveAbilityInstances.Count; abilityIndex++)
                {
                    RegisterAbilityTriggerInstances(actor, actor.ActiveAbilityInstances[abilityIndex]);
                }
            }
        }

        /// <summary>
        /// 把一个 Actor 身上的本地 Trigger 注册到观察者索引。
        /// </summary>
        private void RegisterActorTriggerInstances(CombatActorState ownerActor)
        {
            if (ownerActor == null)
            {
                return;
            }

            RegisterTriggerList(ownerActor.ActiveTriggers, ownerActor, null, null);
        }

        /// <summary>
        /// 注册一个单独的 Actor Trigger 实例。
        /// </summary>
        private void RegisterActorTriggerInstance(CombatActorState ownerActor, ActiveTriggerInstance triggerInstance)
        {
            if (ownerActor == null || triggerInstance == null)
            {
                return;
            }

            _triggerRegistry.RegisterActorTrigger(ownerActor, triggerInstance);
        }

        /// <summary>
        /// 把一个效果实例上的 Trigger 注册到观察者索引。
        /// </summary>
        private void RegisterEffectTriggerInstances(CombatActorState ownerActor, ActiveEffect activeEffect)
        {
            if (ownerActor == null || activeEffect == null)
            {
                return;
            }

            RegisterTriggerList(activeEffect.ActiveTriggers, ownerActor, activeEffect, null);
        }

        /// <summary>
        /// 把一个技能实例上的 Trigger 注册到观察者索引。
        /// </summary>
        private void RegisterAbilityTriggerInstances(CombatActorState ownerActor, ActiveAbilityInstance abilityInstance)
        {
            if (ownerActor == null || abilityInstance == null)
            {
                return;
            }

            RegisterTriggerList(abilityInstance.ActiveTriggers, ownerActor, null, abilityInstance);
        }

        /// <summary>
        /// 根据静态 TriggerDefinition 列表初始化运行时 Trigger 实例列表。
        /// 该方法会先清空旧列表，再为当前定义重新租借实例。
        /// </summary>
        private void InitializeTriggerList(IList<ActiveTriggerInstance> instances, IList<TriggerDefinition> definitions)
        {
            if (instances == null)
            {
                return;
            }

            instances.Clear();
            if (definitions == null)
            {
                return;
            }

            for (var i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] == null)
                {
                    continue;
                }

                instances.Add(RentTriggerInstance(definitions[i]));
            }
        }

        /// <summary>
        /// 按来源类型把一组 Trigger 实例注册到观察者索引。
        /// </summary>
        private void RegisterTriggerList(
            IList<ActiveTriggerInstance> triggers,
            CombatActorState ownerActor,
            ActiveEffect sourceEffect,
            ActiveAbilityInstance sourceAbilityInstance)
        {
            if (triggers == null || ownerActor == null)
            {
                return;
            }

            for (var i = 0; i < triggers.Count; i++)
            {
                var triggerInstance = triggers[i];
                if (triggerInstance == null)
                {
                    continue;
                }

                if (sourceEffect != null)
                {
                    _triggerRegistry.RegisterEffectTrigger(ownerActor, sourceEffect, triggerInstance);
                    continue;
                }

                if (sourceAbilityInstance != null)
                {
                    _triggerRegistry.RegisterAbilityTrigger(ownerActor, sourceAbilityInstance, triggerInstance);
                    continue;
                }

                _triggerRegistry.RegisterActorTrigger(ownerActor, triggerInstance);
            }
        }

        /// <summary>
        /// 释放一组 Trigger 运行时实例，并归还到对象池。
        /// </summary>
        private void ReleaseTriggerList(IList<ActiveTriggerInstance> triggers)
        {
            if (triggers == null)
            {
                return;
            }

            for (var i = triggers.Count - 1; i >= 0; i--)
            {
                _triggerRegistry.Unregister(triggers[i]);
                _pools.ActiveTriggers.Return(triggers[i]);
            }

            triggers.Clear();
        }

        /// <summary>
        /// 释放一个技能实例及其 Trigger 状态。
        /// </summary>
        private void ReleaseActiveAbilityInstance(ActiveAbilityInstance instance)
        {
            if (instance == null || instance.Ability == null)
            {
                return;
            }

            ReleaseTriggerList(instance.ActiveTriggers);
            _pools.ActiveAbilityInstances.Return(instance);
        }

        /// <summary>
        /// 释放一个持续效果实例及其 Trigger 状态。
        /// </summary>
        private void ReleaseActiveEffect(ActiveEffect activeEffect)
        {
            if (activeEffect == null || activeEffect.Spec == null)
            {
                return;
            }

            ReleaseTriggerList(activeEffect.ActiveTriggers);
            _pools.ActiveEffects.Return(activeEffect);
        }

        /// <summary>
        /// 释放全部世界级 Trigger 实例。
        /// </summary>
        private void ReleaseGlobalTriggers()
        {
            ReleaseTriggerList(WorldState.GlobalTriggers);
            WorldState.ClearGlobalTriggers();
        }

        /// <summary>
        /// 由 Trigger 系统驱动一次技能激活。
        /// </summary>
        internal AbilityActivationResult ExecuteTriggeredAbility(
            ActorId sourceActorId,
            AbilityId abilityId,
            ActorId targetActorId,
            WorldPosition? targetPoint,
            object customPayload)
        {
            var targetData = _pools.AbilityTargetDataItems.Rent();
            try
            {
                targetData.ResetForPool();
                if (!targetActorId.IsEmpty)
                {
                    targetData.TargetActorIds.Add(targetActorId);
                }

                if (targetPoint.HasValue)
                {
                    targetData.TargetPoint = targetPoint.Value;
                }

                var request = new AbilityActivationRequest
                {
                    SourceActorId = sourceActorId,
                    AbilityId = abilityId,
                    TargetData = targetData,
                    RequestTick = WorldState.CurrentTick,
                };

                if (customPayload is Serialization.IAbilityPayload abilityPayload)
                {
                    request.Payload = abilityPayload;
                }

                return TryActivate(request);
            }
            finally
            {
                _pools.AbilityTargetDataItems.Return(targetData);
            }
        }

        /// <summary>
        /// 由 Trigger 系统直接施加一个效果定义。
        /// </summary>
        internal void ApplyTriggerEffect(EffectDefinition effectDefinition, ActorId sourceActorId, ActorId targetActorId, int stacks)
        {
            CombatActorState targetActor;
            if (effectDefinition == null || !WorldState.TryGetActor(targetActorId, out targetActor))
            {
                return;
            }

            ApplyEffectSpec(targetActor, new EffectSpec(effectDefinition, sourceActorId, targetActorId, WorldState.CurrentTick, stacks), null);
        }

        /// <summary>
        /// 按比例把当前 Impact 的一部分伤害拆分给另一个单位。
        /// </summary>
        internal void SplitImpactToActorDirect(
            CombatTriggerContext context,
            ActorId targetActorId,
            FP ratio,
            GameplayTag sharedImpactTag)
        {
            if (context == null || context.RelatedAttempt == null || context.RelatedImpact == null || targetActorId.IsEmpty)
            {
                return;
            }

            if (ratio <= FP._0)
            {
                return;
            }

            if (ratio > FP._1)
            {
                ratio = FP._1;
            }

            var sourceImpact = context.RelatedImpact;
            var redirectedImpact = new CombatImpact(
                sourceImpact.SourceActorId,
                targetActorId,
                sourceImpact.AbilityId,
                WorldState.CurrentTick,
                sourceImpact.Stage)
            {
                EffectId = sourceImpact.EffectId,
            };

            foreach (var tag in sourceImpact.Tags)
            {
                redirectedImpact.Tags.Add(tag);
            }

            if (!string.IsNullOrWhiteSpace(sharedImpactTag.Value))
            {
                redirectedImpact.Tags.Add(sharedImpactTag);
            }

            for (var operationIndex = 0; operationIndex < sourceImpact.Operations.Count; operationIndex++)
            {
                var operation = sourceImpact.Operations[operationIndex];
                if (operation.Type != CombatImpactOperationType.ResourceDelta || operation.Amount >= FP._0)
                {
                    continue;
                }

                var redirectedAmount = operation.Amount * ratio;
                if (redirectedAmount == FP._0)
                {
                    continue;
                }

                operation.Amount -= redirectedAmount;

                redirectedImpact.Operations.Add(new CombatImpactOperation
                {
                    Type = CombatImpactOperationType.ResourceDelta,
                    ResourceId = operation.ResourceId,
                    Amount = redirectedAmount,
                });
            }

            if (redirectedImpact.Operations.Count > 0)
            {
                context.RelatedAttempt.Impacts.Add(redirectedImpact);
            }
        }

        /// <summary>
        /// 按当前 Impact 的规模折算一笔资源变化并直接施加。
        /// </summary>
        internal void AddResourceFromImpactDirect(
            CombatTriggerContext context,
            ActorId targetActorId,
            ResourceId resourceId,
            FP ratio,
            CombatActorState instigatorActor)
        {
            if (context == null || context.RelatedImpact == null || targetActorId.IsEmpty || resourceId.IsEmpty)
            {
                return;
            }

            if (ratio <= FP._0)
            {
                return;
            }

            FP totalMagnitude = FP._0;
            for (var operationIndex = 0; operationIndex < context.RelatedImpact.Operations.Count; operationIndex++)
            {
                var operation = context.RelatedImpact.Operations[operationIndex];
                if (operation.Type != CombatImpactOperationType.ResourceDelta ||
                    operation.ResourceId != resourceId ||
                    operation.Amount >= FP._0)
                {
                    continue;
                }

                totalMagnitude -= operation.Amount;
            }

            if (totalMagnitude <= FP._0)
            {
                return;
            }

            ModifyResourceDirect(targetActorId, resourceId, totalMagnitude * ratio, context, instigatorActor);
        }

        /// <summary>
        /// 由 Trigger 系统按 Tag 直接移除目标效果。
        /// </summary>
        internal void RemoveEffectsByTagDirect(ActorId actorId, GameplayTag tag)
        {
            CombatActorState actor;
            if (!WorldState.TryGetActor(actorId, out actor))
            {
                return;
            }

            RemoveEffectsByGrantedTag(actor, tag);
        }

        /// <summary>
        /// 由 Trigger 系统按效果 Id 直接移除目标效果。
        /// </summary>
        internal void RemoveEffectsByIdDirect(ActorId actorId, EffectId effectId)
        {
            CombatActorState actor;
            if (!WorldState.TryGetActor(actorId, out actor))
            {
                return;
            }

            RemoveEffectsById(actor, effectId);
        }

        /// <summary>
        /// 由 Trigger 系统直接修改一个单位的资源值，并补发资源变化触发窗口。
        /// </summary>
        internal void ModifyResourceDirect(
            ActorId actorId,
            ResourceId resourceId,
            FP delta,
            CombatTriggerContext sourceContext,
            CombatActorState instigatorActor)
        {
            CombatActorState actor;
            if (!WorldState.TryGetActor(actorId, out actor))
            {
                return;
            }

            var resourceValue = actor.Resources.GetOrCreate(resourceId);
            var previousValue = resourceValue.Current;
            resourceValue.Add(delta);

            ProcessOwnedTriggerEvent(
                CombatTriggerEventKind.OnResourceChanged,
                CombatTriggerTiming.ImmediatePostResolve,
                actor.ActorId,
                instigatorActor == null ? ActorId.Empty : instigatorActor.ActorId,
                actor.ActorId,
                sourceContext == null ? null : sourceContext.RelatedAbility,
                sourceContext == null ? null : sourceContext.RelatedEffect,
                sourceContext == null ? null : sourceContext.RelatedAttempt,
                sourceContext == null ? null : sourceContext.RelatedImpact,
                sourceContext == null ? null : sourceContext.RelatedOperation,
                sourceContext == null ? null : sourceContext.SourceActiveEffect,
                sourceContext == null ? null : sourceContext.SourceAbilityInstance,
                resourceId,
                previousValue,
                resourceValue.Current,
                sourceContext == null ? null : sourceContext.CustomPayload);

            if (instigatorActor != null)
            {
                ProcessOwnedTriggerEvent(
                    CombatTriggerEventKind.OnResourceChanged,
                    CombatTriggerTiming.ImmediatePostResolve,
                    instigatorActor.ActorId,
                    instigatorActor.ActorId,
                    actor.ActorId,
                    sourceContext == null ? null : sourceContext.RelatedAbility,
                    sourceContext == null ? null : sourceContext.RelatedEffect,
                    sourceContext == null ? null : sourceContext.RelatedAttempt,
                    sourceContext == null ? null : sourceContext.RelatedImpact,
                    sourceContext == null ? null : sourceContext.RelatedOperation,
                    sourceContext == null ? null : sourceContext.SourceActiveEffect,
                    sourceContext == null ? null : sourceContext.SourceAbilityInstance,
                    resourceId,
                    previousValue,
                    resourceValue.Current,
                    sourceContext == null ? null : sourceContext.CustomPayload);
            }
        }

        /// <summary>
        /// 由 Trigger 系统直接给单位添加一个运行时 Tag。
        /// </summary>
        internal void AddRuntimeTagDirect(ActorId actorId, GameplayTag tag)
        {
            CombatActorState actor;
            if (!WorldState.TryGetActor(actorId, out actor))
            {
                return;
            }

            actor.AddTagReference(tag);
        }

        /// <summary>
        /// 由 Trigger 系统直接移除单位上的一个运行时 Tag。
        /// </summary>
        internal void RemoveRuntimeTagDirect(ActorId actorId, GameplayTag tag)
        {
            CombatActorState actor;
            if (!WorldState.TryGetActor(actorId, out actor))
            {
                return;
            }

            actor.RemoveTagReference(tag);
        }

        /// <summary>
        /// 由 Trigger 系统直接取消目标单位的某个技能实例。
        /// </summary>
        internal bool CancelAbilityByIdDirect(ActorId actorId, AbilityId abilityId, string reason)
        {
            CombatActorState actor;
            if (!WorldState.TryGetActor(actorId, out actor))
            {
                return false;
            }

            for (var i = actor.ActiveAbilityInstances.Count - 1; i >= 0; i--)
            {
                var instance = actor.ActiveAbilityInstances[i];
                if (instance.Ability == null || instance.Ability.Id != abilityId)
                {
                    continue;
                }

                CancelAbilityInstance(actor, instance, reason);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 收集 Runtime 启动时可见的自定义 TriggerAction 注册表。
        /// 这里会把全局自动注册表和本次 Runtime 额外注入的注册表合并去重。
        /// </summary>
        private static IReadOnlyList<ICombatCustomTriggerActionRegistry> CollectCustomTriggerActionRegistries(
            IList<ICombatCustomTriggerActionRegistry> runtimeRegistries)
        {
            var results = new List<ICombatCustomTriggerActionRegistry>();
            AppendUniqueRegistries(results, CombatCustomTriggerActionRegistryHub.Snapshot());
            AppendUniqueRegistries(results, runtimeRegistries);
            return results;
        }

        /// <summary>
        /// 把一组注册表按引用去重后追加到目标列表。
        /// </summary>
        private static void AppendUniqueRegistries(
            IList<ICombatCustomTriggerActionRegistry> results,
            IEnumerable<ICombatCustomTriggerActionRegistry> registries)
        {
            if (results == null || registries == null)
            {
                return;
            }

            foreach (var registry in registries)
            {
                if (registry == null || ContainsRegistry(results, registry))
                {
                    continue;
                }

                results.Add(registry);
            }
        }

        /// <summary>
        /// 判断目标列表里是否已经存在同一个注册表引用。
        /// </summary>
        private static bool ContainsRegistry(
            IList<ICombatCustomTriggerActionRegistry> results,
            ICombatCustomTriggerActionRegistry registry)
        {
            for (var i = 0; i < results.Count; i++)
            {
                if (ReferenceEquals(results[i], registry))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 由 Trigger 系统直接发出一个表现 Cue 事件。
        /// </summary>
        internal void EmitCueDirect(ActorId actorId, string cueName)
        {
            _eventSink.Publish(new CombatEvent(CombatEventKind.EffectApplied, actorId, cueName));
        }

        /// <summary>
        /// 由 Trigger 系统直接移除一个运行中的效果实例。
        /// </summary>
        internal void RemoveEffectDirect(CombatActorState actor, ActiveEffect activeEffect)
        {
            RemoveEffect(actor, activeEffect);
        }

        /// <summary>
        /// 由 Trigger 系统直接消耗一个效果层数，必要时移除效果。
        /// </summary>
        internal void ConsumeEffectStackDirect(CombatActorState actor, ActiveEffect activeEffect)
        {
            if (actor == null || activeEffect == null || activeEffect.Spec == null)
            {
                return;
            }

            if (activeEffect.Spec.Stacks > 1)
            {
                activeEffect.Spec.Stacks -= 1;
                RebuildPersistentModifiers(actor, activeEffect);
                return;
            }

            RemoveEffect(actor, activeEffect);
        }

        /// <summary>
        /// 将 actor 内部嵌套的能力实例、效果和自身状态全部归还对象池。
        /// </summary>
        private void ReleaseActor(CombatActorState actor)
        {
            // Actor teardown must release nested runtime state before the actor itself goes back
            // to the pool, otherwise the pooled actor would keep references to expired effects /
            // ability instances and retain large container graphs.
            for (var i = actor.ActiveAbilityInstances.Count - 1; i >= 0; i--)
            {
                ReleaseActiveAbilityInstance(actor.ActiveAbilityInstances[i]);
            }

            for (var i = actor.ActiveEffects.Count - 1; i >= 0; i--)
            {
                NotifyEffectRemoving(actor, actor.ActiveEffects[i]);
                ReleaseActiveEffect(actor.ActiveEffects[i]);
            }

            ReleaseTriggerList(actor.ActiveTriggers);
            _pools.ActorStates.Return(actor);
        }
    }
}
