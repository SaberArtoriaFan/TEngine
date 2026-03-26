using System;
using System.Collections.Generic;
using Saber.GAS.Attributes;
using Saber.GAS.Foundation;
using Saber.GAS.Pooling;
using Saber.GAS.Triggers;

namespace Saber.GAS.Effects
{
    /// <summary>
    /// 一次效果施加时携带的静态快照数据。
    /// </summary>
    public sealed class EffectSpec
    {
        /// <summary>
        /// 创建供池化和深拷贝使用的空效果快照。
        /// </summary>
        internal EffectSpec()
        {
            Definition = new EffectDefinition(EffectId.Empty);
            SourceActorId = ActorId.Empty;
            TargetActorId = ActorId.Empty;
            AppliedTick = SimulationTick.Zero;
            Stacks = 1;
        }

        /// <summary>
        /// 使用完整参数创建一份效果快照。
        /// </summary>
        public EffectSpec(EffectDefinition definition, ActorId sourceActorId, ActorId targetActorId, SimulationTick appliedTick, int stacks = 1)
        {
            Definition = definition;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            AppliedTick = appliedTick;
            Stacks = stacks < 1 ? 1 : stacks;
        }

        /// <summary>
        /// 获取效果定义。
        /// </summary>
        public EffectDefinition Definition { get; internal set; }

        /// <summary>
        /// 获取效果来源单位 Id。
        /// </summary>
        public ActorId SourceActorId { get; internal set; }

        /// <summary>
        /// 获取效果目标单位 Id。
        /// </summary>
        public ActorId TargetActorId { get; internal set; }

        /// <summary>
        /// 获取效果施加时的 Tick。
        /// </summary>
        public SimulationTick AppliedTick { get; internal set; }

        /// <summary>
        /// 获取或设置效果层数。
        /// </summary>
        public int Stacks { get; set; }
    }

    /// <summary>
    /// 持续效果的运行时实例。
    /// </summary>
    public sealed class ActiveEffect : ICombatPoolable
    {
        /// <summary>
        /// 创建一个可池化的持续效果实例，并初始化内部容器。
        /// </summary>
        internal ActiveEffect()
        {
            ActiveTriggers = new List<ActiveTriggerInstance>();
            ExtensionStates = new List<ICombatActiveEffectExtensionState>();
            ResetForPool();
        }

        /// <summary>
        /// 使用效果快照与当前 Tick 创建运行时效果实例。
        /// </summary>
        public ActiveEffect(EffectSpec spec, SimulationTick currentTick)
            : this()
        {
            Initialize(spec, currentTick);
        }

        /// <summary>
        /// 获取当前持续效果对应的效果快照。
        /// </summary>
        public EffectSpec Spec { get; internal set; }

        /// <summary>
        /// 获取效果开始生效的 Tick。
        /// </summary>
        public SimulationTick StartTick { get; internal set; }

        /// <summary>
        /// 获取最近一次周期结算 Tick。
        /// </summary>
        public SimulationTick LastPeriodicTick { get; internal set; }

        /// <summary>
        /// 获取效果到期 Tick。
        /// </summary>
        public SimulationTick ExpireTick { get; internal set; }

        /// <summary>
        /// 获取效果附带的运行时 Trigger 列表。
        /// </summary>
        public IList<ActiveTriggerInstance> ActiveTriggers { get; internal set; }

        /// <summary>
        /// 获取效果扩展的运行时状态列表。
        /// </summary>
        public IList<ICombatActiveEffectExtensionState> ExtensionStates { get; internal set; }

        /// <summary>
        /// 获取当前效果已经应用的属性修正只读视图。
        /// </summary>
        public IReadOnlyList<AttributeModifier> AppliedModifiers => Array.Empty<AttributeModifier>();

        /// <summary>
        /// 用新的效果快照初始化运行时状态。
        /// </summary>
        internal void Initialize(EffectSpec spec, SimulationTick currentTick)
        {
            Spec = spec;
            StartTick = currentTick;
            LastPeriodicTick = currentTick;
            ExpireTick = spec.Definition.DurationPolicy == EffectDurationPolicy.Timed
                ? currentTick + spec.Definition.DurationTicks
                : SimulationTick.Zero;
        }

        /// <summary>
        /// 刷新效果持续时间与周期触发基准。
        /// </summary>
        public void Refresh(SimulationTick currentTick)
        {
            StartTick = currentTick;
            LastPeriodicTick = currentTick;

            if (Spec.Definition.DurationPolicy == EffectDurationPolicy.Timed)
            {
                ExpireTick = currentTick + Spec.Definition.DurationTicks;
            }
        }

        /// <summary>
        /// 判断效果在当前 Tick 是否已经过期。
        /// </summary>
        public bool HasExpired(SimulationTick currentTick)
        {
            if (Spec.Definition.DurationPolicy == EffectDurationPolicy.Instant)
            {
                return true;
            }

            if (Spec.Definition.DurationPolicy == EffectDurationPolicy.Infinite)
            {
                return false;
            }

            return currentTick >= ExpireTick;
        }

        /// <summary>
        /// 判断效果是否应在当前 Tick 触发一次周期逻辑。
        /// </summary>
        public bool ShouldTriggerPeriodic(SimulationTick currentTick)
        {
            if (Spec.Definition.PeriodTicks <= 0)
            {
                return false;
            }

            return currentTick - LastPeriodicTick >= Spec.Definition.PeriodTicks;
        }

        /// <summary>
        /// 记录最近一次周期触发的 Tick。
        /// </summary>
        public void MarkPeriodicTriggered(SimulationTick currentTick)
        {
            LastPeriodicTick = currentTick;
        }

        /// <summary>
        /// 清空效果实例状态，供对象池回收时复位。
        /// </summary>
        public void ResetForPool()
        {
            Spec = null;
            StartTick = SimulationTick.Zero;
            LastPeriodicTick = SimulationTick.Zero;
            ExpireTick = SimulationTick.Zero;
            ActiveTriggers.Clear();
            ExtensionStates.Clear();
        }
    }
}
