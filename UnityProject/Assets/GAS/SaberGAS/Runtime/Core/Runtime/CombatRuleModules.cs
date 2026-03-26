using System.Collections.Generic;
using Herta;
using Saber.GAS.Actors;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Tags;

namespace Saber.GAS.Runtime
{
    /// <summary>
    /// 资源变化来源分类，方便上层模块区分这次数值修改是来自结算包、瞬时效果还是周期效果。
    /// </summary>
    public enum CombatResourceDeltaSourceKind
    {
        /// <summary>
        /// 来自 ImpactOperation 的资源修改。
        /// </summary>
        ImpactOperation = 0,
        /// <summary>
        /// 来自瞬时效果的资源修改。
        /// </summary>
        InstantEffect = 1,
        /// <summary>
        /// 来自周期效果的资源修改。
        /// </summary>
        PeriodicEffect = 2,
    }

    /// <summary>
    /// 一次资源变化进入 Core 默认写入前，交给上层模块处理时使用的上下文。
    /// 模块可以改写 Amount、附加 ImpactTag，或者直接将本次变化消费掉。
    /// </summary>
    public sealed class CombatResourceDeltaContext
    {
        /// <summary>
        /// 使用完整运行时上下文创建一次资源变化处理请求。
        /// </summary>
        public CombatResourceDeltaContext(
            CombatRuntime runtime,
            CombatResourceDeltaSourceKind sourceKind,
            CombatActorState sourceActor,
            CombatActorState targetActor,
            EffectDefinition effectDefinition,
            ActiveEffect activeEffect,
            CombatActionAttempt attempt,
            CombatImpact impact,
            CombatImpactOperation operation,
            ResourceId resourceId,
            FP amount)
        {
            Runtime = runtime;
            SourceKind = sourceKind;
            SourceActor = sourceActor;
            TargetActor = targetActor;
            EffectDefinition = effectDefinition;
            ActiveEffect = activeEffect;
            Attempt = attempt;
            Impact = impact;
            Operation = operation;
            ResourceId = resourceId;
            Amount = amount;
        }

        /// <summary>
        /// 获取当前资源变化所属的 Runtime。
        /// </summary>
        public CombatRuntime Runtime { get; }

        /// <summary>
        /// 获取当前资源变化对应的世界状态。
        /// </summary>
        public CombatWorldState WorldState => Runtime == null ? null : Runtime.WorldState;

        /// <summary>
        /// 获取资源变化来源类型。
        /// </summary>
        public CombatResourceDeltaSourceKind SourceKind { get; }

        /// <summary>
        /// 获取资源变化的来源单位。
        /// </summary>
        public CombatActorState SourceActor { get; }

        /// <summary>
        /// 获取资源变化的目标单位。
        /// </summary>
        public CombatActorState TargetActor { get; }

        /// <summary>
        /// 获取关联效果定义。
        /// </summary>
        public EffectDefinition EffectDefinition { get; }

        /// <summary>
        /// 获取关联的运行时持续效果。
        /// </summary>
        public ActiveEffect ActiveEffect { get; }

        /// <summary>
        /// 获取关联的行动尝试对象。
        /// </summary>
        public CombatActionAttempt Attempt { get; }

        /// <summary>
        /// 获取关联的 Impact。
        /// </summary>
        public CombatImpact Impact { get; }

        /// <summary>
        /// 获取关联的 Impact 操作。
        /// </summary>
        public CombatImpactOperation Operation { get; }

        /// <summary>
        /// 获取当前处理的资源 Id。
        /// </summary>
        public ResourceId ResourceId { get; }

        /// <summary>
        /// 获取或设置当前资源变化量。
        /// </summary>
        public FP Amount { get; set; }

        /// <summary>
        /// 获取或设置是否吞掉 Core 默认写入逻辑。
        /// </summary>
        public bool ConsumeDefaultApply { get; set; }

        /// <summary>
        /// 向当前 Impact 增加一个语义 Tag。
        /// </summary>
        public void AddImpactTag(GameplayTag tag)
        {
            if (Impact == null || string.IsNullOrWhiteSpace(tag.Value))
            {
                return;
            }

            Impact.Tags.Add(tag);
        }
    }

    /// <summary>
    /// Core 的规则模块扩展接口。
    /// Runtime 只依赖这一层抽象，把战斗语义、模式机制等可演进逻辑交给模块自己解释。
    /// </summary>
    public interface ICombatRuleModule
    {
        void Initialize(CombatRuntime runtime);

        void Shutdown(CombatRuntime runtime);

        void EvaluateAbilityAttempt(CombatActionAttempt attempt, CombatWorldState worldState);

        bool TryBlockEffectApplication(
            CombatActorState targetActor,
            EffectDefinition effectDefinition,
            CombatWorldState worldState,
            out string reason);

        bool HasRuntimeEffectPayload(EffectDefinition effectDefinition);

        void OnEffectApplied(CombatActorState targetActor, ActiveEffect activeEffect, CombatRuntime runtime);

        void OnEffectRefreshed(CombatActorState targetActor, ActiveEffect activeEffect, CombatRuntime runtime);

        void OnEffectRemoving(CombatActorState targetActor, ActiveEffect activeEffect, CombatRuntime runtime);

        void ProcessResourceDelta(CombatResourceDeltaContext context);
    }

    /// <summary>
    /// 供深拷贝层克隆效果扩展定义和运行时状态的扩展接口。
    /// </summary>
    public interface ICombatExtensionCloneHandler
    {
        bool CanCloneEffectExtension(ICombatEffectExtensionDefinition definition);

        ICombatEffectExtensionDefinition CloneEffectExtension(ICombatEffectExtensionDefinition definition);

        bool CanCloneActiveEffectState(ICombatActiveEffectExtensionState state);

        ICombatActiveEffectExtensionState CloneActiveEffectState(ICombatActiveEffectExtensionState state);
    }
}
