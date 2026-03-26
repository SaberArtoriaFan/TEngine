using System.Collections.Generic;
using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Pooling;
using Saber.GAS.Runtime;
using Saber.GAS.Tags;

namespace Saber.GAS.Triggers
{
    /// <summary>
    /// Trigger 在某个结算窗口拿到的标准上下文。
    /// 它把 Owner、Instigator、Target、Ability、Effect、Impact 等信息收敛到一个对象里，
    /// 让 Trigger 逻辑始终基于确定性的战斗上下文工作。
    /// </summary>
    public sealed class CombatTriggerContext
    {
        /// <summary>
        /// 创建一份空 Trigger 上下文。
        /// </summary>
        public CombatTriggerContext()
        {
            Flags = new GameplayTagContainer();
        }

        /// <summary>
        /// 触发上下文所属的当前模拟 Tick。
        /// </summary>
        public SimulationTick CurrentTick { get; set; }

        /// <summary>
        /// 这次上下文对应的触发事件窗口类型。
        /// </summary>
        public CombatTriggerEventKind EventKind { get; set; }

        /// <summary>
        /// Trigger 动作应在该事件的哪个时机执行。
        /// </summary>
        public CombatTriggerTiming Timing { get; set; }

        /// <summary>
        /// 本轮要评估谁的 Trigger，因此谁被视为 Trigger 拥有者。
        /// </summary>
        public ActorId OwnerActorId { get; set; }

        /// <summary>
        /// 触发事件中的施加者，例如攻击者或技能释放者。
        /// </summary>
        public ActorId InstigatorActorId { get; set; }

        /// <summary>
        /// 触发事件中的受影响目标。
        /// </summary>
        public ActorId TargetActorId { get; set; }

        /// <summary>
        /// 与本次事件直接关联的技能定义。
        /// </summary>
        public AbilityDefinition RelatedAbility { get; set; }

        /// <summary>
        /// 与本次事件直接关联的效果定义。
        /// </summary>
        public EffectDefinition RelatedEffect { get; set; }

        /// <summary>
        /// 与本次事件关联的能力尝试记录。
        /// </summary>
        public CombatActionAttempt RelatedAttempt { get; set; }

        /// <summary>
        /// 与本次事件关联的 Impact 数据。
        /// </summary>
        public CombatImpact RelatedImpact { get; set; }

        /// <summary>
        /// 与本次事件关联的单条 Impact 操作。
        /// </summary>
        public CombatImpactOperation RelatedOperation { get; set; }

        /// <summary>
        /// 若事件来自某个 ActiveEffect，则记录其来源实例。
        /// </summary>
        public ActiveEffect SourceActiveEffect { get; set; }

        /// <summary>
        /// 若事件来自某个技能生命周期实例，则记录其来源实例。
        /// </summary>
        public ActiveAbilityInstance SourceAbilityInstance { get; set; }

        /// <summary>
        /// 与资源变化类事件相关的资源标识。
        /// </summary>
        public ResourceId ResourceId { get; set; }

        /// <summary>
        /// 资源变化前的旧值。
        /// </summary>
        public FP PreviousResourceValue { get; set; }

        /// <summary>
        /// 资源变化后的新值。
        /// </summary>
        public FP CurrentResourceValue { get; set; }

        /// <summary>
        /// 描述这次上下文附加语义的运行时标记。
        /// </summary>
        public GameplayTagContainer Flags { get; }

        /// <summary>
        /// 预留给外部扩展携带的自定义负载。
        /// </summary>
        public object CustomPayload { get; set; }
    }

    /// <summary>
    /// TriggerDefinition 的运行时实例。
    /// 用来记录触发冷却、每 Tick 次数、总次数和剩余充能等动态状态。
    /// </summary>
    public sealed class ActiveTriggerInstance : ICombatPoolable
    {
        /// <summary>
        /// 创建一个可池化的 Trigger 运行时实例。
        /// </summary>
        internal ActiveTriggerInstance()
        {
            ResetForPool();
        }

        /// <summary>
        /// 该运行时实例所引用的静态 Trigger 定义。
        /// </summary>
        public TriggerDefinition Definition { get; internal set; }

        /// <summary>
        /// 上一次成功触发发生的 Tick。
        /// </summary>
        public SimulationTick LastTriggeredTick { get; internal set; }

        /// <summary>
        /// 当前冷却结束的 Tick。
        /// </summary>
        public SimulationTick CooldownEndTick { get; internal set; }

        /// <summary>
        /// 用于重置“本 Tick 次数”统计的基准 Tick。
        /// </summary>
        public SimulationTick CountResetTick { get; internal set; }

        /// <summary>
        /// 当前 Tick 内已经触发的次数。
        /// </summary>
        public int TriggerCountThisTick { get; internal set; }

        /// <summary>
        /// 生命周期内累计成功触发的总次数。
        /// </summary>
        public int TriggerCountTotal { get; internal set; }

        /// <summary>
        /// 剩余可消耗的充能次数。
        /// </summary>
        public int RemainingCharges { get; internal set; }

        /// <summary>
        /// 该 Trigger 实例当前是否仍允许参与评估。
        /// </summary>
        public bool Enabled { get; internal set; }

        /// <summary>
        /// 用新的静态定义初始化运行时状态。
        /// </summary>
        internal void Initialize(TriggerDefinition definition)
        {
            Definition = definition;
            LastTriggeredTick = SimulationTick.Zero;
            CooldownEndTick = SimulationTick.Zero;
            CountResetTick = SimulationTick.Zero;
            TriggerCountThisTick = 0;
            TriggerCountTotal = 0;
            RemainingCharges = definition == null ? 0 : definition.InitialCharges;
            Enabled = definition != null && definition.EnabledOnCreate;
        }

        /// <summary>
        /// 判断当前 Tick 下该 Trigger 是否仍然允许触发。
        /// </summary>
        public bool CanTrigger(SimulationTick currentTick)
        {
            if (Definition == null || !Enabled)
            {
                return false;
            }

            ResetPerTickCounters(currentTick);

            if (Definition.CooldownTicks > 0 && currentTick < CooldownEndTick)
            {
                return false;
            }

            if (Definition.MaxTriggerCountPerTick > 0 && TriggerCountThisTick >= Definition.MaxTriggerCountPerTick)
            {
                return false;
            }

            if (Definition.MaxTriggerCountTotal > 0 && TriggerCountTotal >= Definition.MaxTriggerCountTotal)
            {
                return false;
            }

            if (Definition.InitialCharges > 0 && RemainingCharges <= 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 在触发成功后写回运行时计数与冷却状态。
        /// </summary>
        public void RegisterTriggered(SimulationTick currentTick)
        {
            if (Definition == null)
            {
                return;
            }

            ResetPerTickCounters(currentTick);
            LastTriggeredTick = currentTick;
            TriggerCountThisTick += 1;
            TriggerCountTotal += 1;

            if (Definition.CooldownTicks > 0)
            {
                CooldownEndTick = currentTick + Definition.CooldownTicks;
            }

            if (Definition.InitialCharges > 0)
            {
                RemainingCharges -= 1;
                if (RemainingCharges <= 0)
                {
                    Enabled = false;
                }
            }
        }

        /// <summary>
        /// 清空 Trigger 运行时状态，供对象池回收时复位。
        /// </summary>
        public void ResetForPool()
        {
            Definition = null;
            LastTriggeredTick = SimulationTick.Zero;
            CooldownEndTick = SimulationTick.Zero;
            CountResetTick = SimulationTick.Zero;
            TriggerCountThisTick = 0;
            TriggerCountTotal = 0;
            RemainingCharges = 0;
            Enabled = false;
        }

        /// <summary>
        /// 在 Tick 切换时重置“本 Tick 触发次数”计数。
        /// </summary>
        private void ResetPerTickCounters(SimulationTick currentTick)
        {
            if (CountResetTick.Equals(currentTick))
            {
                return;
            }

            CountResetTick = currentTick;
            TriggerCountThisTick = 0;
        }
    }

    /// <summary>
    /// TriggerProcessor 在收集候选触发器时使用的内部结构。
    /// 它把“触发器实例属于谁、来自哪里”打包起来，方便后续排序与执行。
    /// </summary>
    internal struct TriggerCandidate
    {
        /// <summary>
        /// 这条候选 Trigger 最终归属的拥有者 Actor。
        /// </summary>
        public CombatActorState OwnerActor;
        /// <summary>
        /// 这条候选 Trigger 来自 Actor、Effect、Ability 还是全局规则。
        /// </summary>
        public TriggerSourceKind SourceKind;
        /// <summary>
        /// 若来源是 ActiveEffect，则记录对应来源实例。
        /// </summary>
        public ActiveEffect SourceEffect;
        /// <summary>
        /// 若来源是 ActiveAbility，则记录对应来源实例。
        /// </summary>
        public ActiveAbilityInstance SourceAbilityInstance;
        /// <summary>
        /// 候选中真正要执行的 Trigger 运行时实例。
        /// </summary>
        public ActiveTriggerInstance TriggerInstance;
    }
}
