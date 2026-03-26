using System.Collections.Generic;
using Saber.GAS.Foundation;
using Saber.GAS.Pooling;
using Saber.GAS.Triggers;

namespace Saber.GAS.Abilities
{
    /// <summary>
    /// 长生命周期技能实例的运行时状态。
    /// </summary>
    public enum ActiveAbilityState
    {
        /// <summary>
        /// 正在施法，尚未进入正式激活阶段。
        /// </summary>
        Casting = 0,
        /// <summary>
        /// 已激活，可能持续生效或周期触发。
        /// </summary>
        Active = 1,
        /// <summary>
        /// 已按正常流程完成。
        /// </summary>
        Completed = 2,
        /// <summary>
        /// 已被外部打断或取消。
        /// </summary>
        Cancelled = 3,
    }

    /// <summary>
    /// 运行时能力实例，负责保存施法、激活、周期触发和结束所需状态。
    /// </summary>
    public sealed class ActiveAbilityInstance : ICombatPoolable
    {
        /// <summary>
        /// 创建一个可池化的能力实例，并初始化内部容器。
        /// </summary>
        internal ActiveAbilityInstance()
        {
            LockedTargetActorIds = new List<ActorId>();
            ActiveTriggers = new List<ActiveTriggerInstance>();
            ResetForPool();
        }

        /// <summary>
        /// 使用完整参数创建一份能力实例。
        /// </summary>
        public ActiveAbilityInstance(
            AbilityInstanceId instanceId,
            ActorId sourceActorId,
            AbilityDefinition ability,
            AbilityTargetData targetData,
            SimulationTick createdTick)
            : this()
        {
            Initialize(instanceId, sourceActorId, ability, targetData, createdTick);
        }

        /// <summary>
        /// 获取当前技能实例 Id。
        /// </summary>
        public AbilityInstanceId InstanceId { get; internal set; }

        /// <summary>
        /// 获取当前技能实例的施法者 Id。
        /// </summary>
        public ActorId SourceActorId { get; internal set; }

        /// <summary>
        /// 获取当前技能实例对应的技能定义。
        /// </summary>
        public AbilityDefinition Ability { get; internal set; }

        /// <summary>
        /// 获取技能实例创建时的 Tick。
        /// </summary>
        public SimulationTick CreatedTick { get; internal set; }

        /// <summary>
        /// 获取施法结束并进入下一阶段的 Tick。
        /// </summary>
        public SimulationTick ResolveTick { get; internal set; }

        /// <summary>
        /// 获取进入 Active 状态时的 Tick。
        /// </summary>
        public SimulationTick ActivatedTick { get; internal set; }

        /// <summary>
        /// 获取下一次周期结算 Tick。
        /// </summary>
        public SimulationTick NextPeriodicTick { get; internal set; }

        /// <summary>
        /// 获取技能过期 Tick。
        /// </summary>
        public SimulationTick ExpireTick { get; internal set; }

        /// <summary>
        /// 获取当前技能实例状态。
        /// </summary>
        public ActiveAbilityState State { get; internal set; }

        /// <summary>
        /// 获取或设置首次执行是否已完成。
        /// </summary>
        public bool InitialExecutionCompleted { get; set; }

        /// <summary>
        /// 获取或设置结束执行是否已完成。
        /// </summary>
        public bool EndExecutionCompleted { get; set; }

        /// <summary>
        /// 获取锁定的目标单位列表。
        /// </summary>
        public IList<ActorId> LockedTargetActorIds { get; internal set; }

        /// <summary>
        /// 获取当前技能实例持有的运行时 Trigger 列表。
        /// </summary>
        public IList<ActiveTriggerInstance> ActiveTriggers { get; internal set; }

        /// <summary>
        /// 获取锁定的点目标坐标。
        /// </summary>
        public WorldPosition? LockedTargetPoint { get; internal set; }

        /// <summary>
        /// 用新的能力参数重新初始化实例。
        /// </summary>
        internal void Initialize(
            AbilityInstanceId instanceId,
            ActorId sourceActorId,
            AbilityDefinition ability,
            AbilityTargetData targetData,
            SimulationTick createdTick)
        {
            InstanceId = instanceId;
            SourceActorId = sourceActorId;
            Ability = ability;
            CreatedTick = createdTick;
            LockedTargetActorIds.Clear();
            LockedTargetPoint = null;
            ResolveTick = SimulationTick.Zero;
            ActivatedTick = SimulationTick.Zero;
            NextPeriodicTick = SimulationTick.Zero;
            ExpireTick = SimulationTick.Zero;
            State = ActiveAbilityState.Casting;
            InitialExecutionCompleted = false;
            EndExecutionCompleted = false;

            if (targetData != null)
            {
                LockedTargetPoint = targetData.TargetPoint;

                for (var i = 0; i < targetData.TargetActorIds.Count; i++)
                {
                    LockedTargetActorIds.Add(targetData.TargetActorIds[i]);
                }
            }

            ResolveTick = createdTick + ability.CastDurationTicks;
            State = ability.CastDurationTicks > 0 ? ActiveAbilityState.Casting : ActiveAbilityState.Active;

            if (State == ActiveAbilityState.Active)
            {
                ActivatedTick = createdTick;
                NextPeriodicTick = ability.IntervalTicks > 0 ? createdTick + ability.IntervalTicks : SimulationTick.Zero;
                ExpireTick = ability.ActiveDurationTicks > 0 ? createdTick + ability.ActiveDurationTicks : SimulationTick.Zero;
            }
        }

        /// <summary>
        /// 构造一份新的目标数据副本。
        /// </summary>
        public AbilityTargetData BuildTargetData()
        {
            var result = new AbilityTargetData();
            PopulateTargetData(result);
            return result;
        }

        /// <summary>
        /// 将实例锁定的目标写入外部 targetData 缓冲。
        /// </summary>
        internal void PopulateTargetData(AbilityTargetData targetData)
        {
            if (targetData == null)
            {
                return;
            }

            targetData.ResetForPool();
            if (LockedTargetPoint.HasValue)
            {
                targetData.TargetPoint = LockedTargetPoint.Value;
            }

            for (var i = 0; i < LockedTargetActorIds.Count; i++)
            {
                targetData.TargetActorIds.Add(LockedTargetActorIds[i]);
            }
        }

        /// <summary>
        /// 判断施法阶段是否已经走完。
        /// </summary>
        public bool ShouldResolveCast(SimulationTick currentTick)
        {
            return State == ActiveAbilityState.Casting && currentTick >= ResolveTick;
        }

        /// <summary>
        /// 判断当前 Tick 是否已到达下一次周期触发时间。
        /// </summary>
        public bool ShouldTriggerPeriodic(SimulationTick currentTick)
        {
            return State == ActiveAbilityState.Active &&
                   Ability.IntervalTicks > 0 &&
                   currentTick >= NextPeriodicTick;
        }

        /// <summary>
        /// 判断能力是否达到过期时间。
        /// </summary>
        public bool ShouldExpire(SimulationTick currentTick)
        {
            if (State != ActiveAbilityState.Active)
            {
                return false;
            }

            if (Ability.ActiveDurationTicks <= 0)
            {
                return false;
            }

            return currentTick >= ExpireTick;
        }

        /// <summary>
        /// 将实例推进到 Active 阶段。
        /// </summary>
        public void EnterActive(SimulationTick currentTick)
        {
            State = ActiveAbilityState.Active;
            ActivatedTick = currentTick;
            NextPeriodicTick = Ability.IntervalTicks > 0 ? currentTick + Ability.IntervalTicks : SimulationTick.Zero;
            ExpireTick = Ability.ActiveDurationTicks > 0 ? currentTick + Ability.ActiveDurationTicks : SimulationTick.Zero;
        }

        /// <summary>
        /// 推进下一次周期触发时间。
        /// </summary>
        public void AdvancePeriodic()
        {
            if (Ability.IntervalTicks > 0)
            {
                NextPeriodicTick = NextPeriodicTick + Ability.IntervalTicks;
            }
        }

        /// <summary>
        /// 标记能力实例正常完成。
        /// </summary>
        public void MarkCompleted()
        {
            State = ActiveAbilityState.Completed;
        }

        /// <summary>
        /// 标记能力实例被取消。
        /// </summary>
        public void MarkCancelled()
        {
            State = ActiveAbilityState.Cancelled;
        }

        /// <summary>
        /// 回收到对象池前清空内部状态。
        /// </summary>
        public void ResetForPool()
        {
            InstanceId = AbilityInstanceId.Empty;
            SourceActorId = ActorId.Empty;
            Ability = null;
            CreatedTick = SimulationTick.Zero;
            ResolveTick = SimulationTick.Zero;
            ActivatedTick = SimulationTick.Zero;
            NextPeriodicTick = SimulationTick.Zero;
            ExpireTick = SimulationTick.Zero;
            State = ActiveAbilityState.Casting;
            InitialExecutionCompleted = false;
            EndExecutionCompleted = false;
            LockedTargetActorIds.Clear();
            LockedTargetPoint = null;
            ActiveTriggers.Clear();
        }
    }
}
