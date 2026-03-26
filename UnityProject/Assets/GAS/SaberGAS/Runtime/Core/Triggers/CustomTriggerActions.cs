using System;
using System.Collections.Generic;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;

namespace Saber.GAS.Triggers
{
    /// <summary>
    /// 自定义 TriggerAction 的标准扩展接口。
    /// 业务层只需要实现固定的动作标识和执行入口，即可通过源码生成自动接入 Runtime。
    /// </summary>
    public interface ICombatCustomTriggerAction
    {
        /// <summary>
        /// 获取当前自定义动作的稳定整型标识。
        /// 该值会被 TriggerActionDefinition.CustomActionId 引用。
        /// </summary>
        int CustomId { get; }

        /// <summary>
        /// 执行一次自定义 TriggerAction。
        /// </summary>
        void ExecuteAction(CombatCustomTriggerActionExecutionContext context);
    }

    /// <summary>
    /// 自定义 TriggerAction 注册表接口。
    /// 一般由源码生成器在每个程序集内自动生成具体实现。
    /// </summary>
    public interface ICombatCustomTriggerActionRegistry
    {
        /// <summary>
        /// 获取当前注册表暴露的全部自定义动作 Id 列表。
        /// </summary>
        IReadOnlyList<int> RegisteredCustomActionIds { get; }

        /// <summary>
        /// 尝试解析指定 Id 对应的自定义动作处理器。
        /// </summary>
        bool TryResolve(int customActionId, out ICombatCustomTriggerAction action);
    }

    /// <summary>
    /// 自定义 TriggerAction 的执行上下文。
    /// 它把 Runtime、Trigger 上下文、动作定义和来源信息聚合到一处，供业务层读取。
    /// </summary>
    public readonly struct CombatCustomTriggerActionExecutionContext
    {
        /// <summary>
        /// 使用 Runtime、Trigger 上下文和动作定义创建自定义动作执行上下文。
        /// </summary>
        public CombatCustomTriggerActionExecutionContext(
            CombatRuntime runtime,
            CombatTriggerContext triggerContext,
            TriggerActionDefinition actionDefinition,
            CombatActorState ownerActor,
            TriggerSourceKind sourceKind,
            ActiveEffect sourceEffect,
            ActiveAbilityInstance sourceAbilityInstance)
        {
            Runtime = runtime;
            TriggerContext = triggerContext;
            ActionDefinition = actionDefinition;
            OwnerActor = ownerActor;
            SourceKind = sourceKind;
            SourceEffect = sourceEffect;
            SourceAbilityInstance = sourceAbilityInstance;
        }

        /// <summary>
        /// 获取当前执行所依附的 CombatRuntime。
        /// </summary>
        public CombatRuntime Runtime { get; }

        /// <summary>
        /// 获取当前执行共享的战斗世界状态。
        /// </summary>
        public CombatWorldState WorldState => Runtime == null ? null : Runtime.WorldState;

        /// <summary>
        /// 获取本次自定义动作对应的 Trigger 上下文。
        /// </summary>
        public CombatTriggerContext TriggerContext { get; }

        /// <summary>
        /// 获取当前正在执行的 Trigger 动作定义。
        /// </summary>
        public TriggerActionDefinition ActionDefinition { get; }

        /// <summary>
        /// 获取当前 Trigger 的拥有者 Actor。
        /// </summary>
        public CombatActorState OwnerActor { get; }

        /// <summary>
        /// 获取当前 Trigger 候选的来源类型。
        /// </summary>
        public TriggerSourceKind SourceKind { get; }

        /// <summary>
        /// 获取当前 Trigger 候选关联的来源效果实例。
        /// </summary>
        public ActiveEffect SourceEffect { get; }

        /// <summary>
        /// 获取当前 Trigger 候选关联的来源技能实例。
        /// </summary>
        public ActiveAbilityInstance SourceAbilityInstance { get; }

        /// <summary>
        /// 获取 Trigger 拥有者的 ActorId。
        /// </summary>
        public ActorId OwnerActorId => OwnerActor == null
            ? (TriggerContext == null ? ActorId.Empty : TriggerContext.OwnerActorId)
            : OwnerActor.ActorId;

        /// <summary>
        /// 获取事件上下文中的施加者 ActorId。
        /// </summary>
        public ActorId InstigatorActorId => TriggerContext == null ? ActorId.Empty : TriggerContext.InstigatorActorId;

        /// <summary>
        /// 获取事件上下文中的目标 ActorId。
        /// </summary>
        public ActorId TargetActorId => TriggerContext == null ? ActorId.Empty : TriggerContext.TargetActorId;

        /// <summary>
        /// 获取动作定义中的自定义动作标识。
        /// </summary>
        public int CustomActionId => ActionDefinition == null ? 0 : ActionDefinition.CustomActionId;

        /// <summary>
        /// 获取动作定义挂带的自定义负载。
        /// </summary>
        public object CustomPayload => ActionDefinition == null ? null : ActionDefinition.CustomPayload;

        /// <summary>
        /// 按 TriggerActorReference 解析到实际的 ActorId。
        /// </summary>
        public ActorId ResolveActor(TriggerActorReference reference)
        {
            switch (reference)
            {
                case TriggerActorReference.Owner:
                    return OwnerActorId;
                case TriggerActorReference.Instigator:
                    return InstigatorActorId;
                case TriggerActorReference.Target:
                    return TargetActorId;
                default:
                    return ActorId.Empty;
            }
        }
    }

    /// <summary>
    /// 全局自定义 TriggerAction 注册中心。
    /// 由源码生成的模块初始化代码自动向这里注册程序集内的生成注册表。
    /// </summary>
    public static class CombatCustomTriggerActionRegistryHub
    {
        /// <summary>
        /// 保护注册表列表并发访问的同步锁。
        /// </summary>
        private static readonly object SyncRoot = new object();
        /// <summary>
        /// 当前进程内已注册的自定义 TriggerAction 注册表列表。
        /// </summary>
        private static readonly List<ICombatCustomTriggerActionRegistry> Registries = new List<ICombatCustomTriggerActionRegistry>();

        /// <summary>
        /// 注册一个新的自定义 TriggerAction 注册表。
        /// 相同引用的注册表不会被重复加入。
        /// </summary>
        public static void Register(ICombatCustomTriggerActionRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            lock (SyncRoot)
            {
                for (var i = 0; i < Registries.Count; i++)
                {
                    if (ReferenceEquals(Registries[i], registry))
                    {
                        return;
                    }
                }

                Registries.Add(registry);
            }
        }

        /// <summary>
        /// 获取当前已注册注册表的快照。
        /// Runtime 构造时会基于该快照建立自己的派发表。
        /// </summary>
        public static IReadOnlyList<ICombatCustomTriggerActionRegistry> Snapshot()
        {
            lock (SyncRoot)
            {
                return Registries.ToArray();
            }
        }
    }

    /// <summary>
    /// 把多个注册表折叠成一个高效的自定义动作派发表。
    /// 它会在构造时校验重复 Id，避免运行时静默覆盖。
    /// </summary>
    internal sealed class CompositeCombatCustomTriggerActionRegistry
    {
        /// <summary>
        /// 当前 Runtime 使用的自定义动作查找表。
        /// </summary>
        private readonly Dictionary<int, ICombatCustomTriggerAction> _actionLookup;

        /// <summary>
        /// 使用一组注册表创建组合派发表。
        /// </summary>
        public CompositeCombatCustomTriggerActionRegistry(IReadOnlyList<ICombatCustomTriggerActionRegistry> registries)
        {
            _actionLookup = new Dictionary<int, ICombatCustomTriggerAction>();
            if (registries == null)
            {
                return;
            }

            for (var registryIndex = 0; registryIndex < registries.Count; registryIndex++)
            {
                var registry = registries[registryIndex];
                if (registry == null || registry.RegisteredCustomActionIds == null)
                {
                    continue;
                }

                for (var actionIndex = 0; actionIndex < registry.RegisteredCustomActionIds.Count; actionIndex++)
                {
                    var customActionId = registry.RegisteredCustomActionIds[actionIndex];
                    if (customActionId <= 0)
                    {
                        throw new InvalidOperationException("Custom trigger action ids must be positive integers.");
                    }

                    if (!registry.TryResolve(customActionId, out var action) || action == null)
                    {
                        throw new InvalidOperationException(
                            $"Custom trigger action registry '{registry.GetType().FullName}' failed to resolve id '{customActionId}'.");
                    }

                    if (_actionLookup.ContainsKey(customActionId))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate custom trigger action id detected: '{customActionId}'.");
                    }

                    _actionLookup.Add(customActionId, action);
                }
            }
        }

        /// <summary>
        /// 尝试执行一次自定义 TriggerAction。
        /// </summary>
        public bool TryExecute(CombatCustomTriggerActionExecutionContext context)
        {
            if (context.ActionDefinition == null || context.CustomActionId <= 0)
            {
                return false;
            }

            if (!_actionLookup.TryGetValue(context.CustomActionId, out var action) || action == null)
            {
                return false;
            }

            action.ExecuteAction(context);
            return true;
        }
    }
}
