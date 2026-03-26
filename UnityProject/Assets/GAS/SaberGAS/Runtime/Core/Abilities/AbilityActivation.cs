using System.Collections.Generic;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Networking;
using Saber.GAS.Pooling;
using Saber.GAS.Runtime;
using Saber.GAS.Serialization;

namespace Saber.GAS.Abilities
{
    /// <summary>
    /// 一次技能激活请求携带的目标数据。
    /// </summary>
    public sealed class AbilityTargetData : ICombatPoolable
    {
        /// <summary>
        /// 创建一份空的目标数据容器。
        /// </summary>
        public AbilityTargetData()
        {
            TargetActorIds = new List<ActorId>();
        }

        /// <summary>
        /// 获取目标单位 Id 列表。
        /// </summary>
        public IList<ActorId> TargetActorIds { get; }

        /// <summary>
        /// 获取或设置点目标坐标。
        /// </summary>
        public WorldPosition? TargetPoint { get; set; }

        /// <summary>
        /// 回收到对象池前清空目标列表与点目标。
        /// </summary>
        public void ResetForPool()
        {
            TargetActorIds.Clear();
            TargetPoint = null;
        }
    }

    /// <summary>
    /// 外部系统投递给 CombatRuntime 的能力激活请求。
    /// </summary>
    public sealed class AbilityActivationRequest
    {
        /// <summary>
        /// 获取或设置请求的施法者。
        /// </summary>
        public ActorId SourceActorId { get; set; }

        /// <summary>
        /// 获取或设置要激活的技能 Id。
        /// </summary>
        public AbilityId AbilityId { get; set; }

        /// <summary>
        /// 获取或设置本次激活的目标输入数据。
        /// </summary>
        public AbilityTargetData TargetData { get; set; }

        /// <summary>
        /// 获取或设置请求发起时的逻辑 Tick。
        /// </summary>
        public SimulationTick RequestTick { get; set; }

        /// <summary>
        /// 获取或设置网络预测键。
        /// </summary>
        public PredictionKey PredictionKey { get; set; }

        /// <summary>
        /// 获取或设置附带的自定义能力载荷。
        /// </summary>
        public IAbilityPayload Payload { get; set; }
    }

    /// <summary>
    /// 一次能力执行过程中共享的只读上下文。
    /// </summary>
    public sealed class AbilityExecutionContext
    {
        /// <summary>
        /// 用请求、技能定义和执行 Tick 组装一份执行上下文。
        /// </summary>
        public AbilityExecutionContext(AbilityActivationRequest request, AbilityDefinition ability, SimulationTick executionTick)
        {
            Request = request;
            Ability = ability;
            ExecutionTick = executionTick;
        }

        /// <summary>
        /// 获取原始激活请求。
        /// </summary>
        public AbilityActivationRequest Request { get; }

        /// <summary>
        /// 获取本次执行对应的技能定义。
        /// </summary>
        public AbilityDefinition Ability { get; }

        /// <summary>
        /// 获取本次执行对应的逻辑 Tick。
        /// </summary>
        public SimulationTick ExecutionTick { get; }
    }

    /// <summary>
    /// 记录一次能力执行最终产生的效果和 impact。
    /// </summary>
    public sealed class AbilityExecutionRecord
    {
        /// <summary>
        /// 创建一份空的执行记录，并准备效果与 Impact 缓冲。
        /// </summary>
        public AbilityExecutionRecord()
        {
            AppliedEffects = new List<EffectSpec>();
            AppliedImpacts = new List<CombatImpact>();
        }

        /// <summary>
        /// 获取或设置执行记录中的施法者 Id。
        /// </summary>
        public ActorId SourceActorId { get; set; }

        /// <summary>
        /// 获取或设置执行记录中的技能 Id。
        /// </summary>
        public AbilityId AbilityId { get; set; }

        /// <summary>
        /// 获取或设置执行发生的逻辑 Tick。
        /// </summary>
        public SimulationTick ExecutionTick { get; set; }

        /// <summary>
        /// 获取本次执行施加的效果快照列表。
        /// </summary>
        public IList<EffectSpec> AppliedEffects { get; }

        /// <summary>
        /// 获取本次执行生成的 Impact 列表。
        /// </summary>
        public IList<CombatImpact> AppliedImpacts { get; }

        /// <summary>
        /// 获取或设置本次启动的技能实例 Id。
        /// </summary>
        public AbilityInstanceId StartedAbilityInstanceId { get; set; }
    }

    /// <summary>
    /// 运行时返回给外部的能力激活结果。
    /// </summary>
    public sealed class AbilityActivationResult
    {
        /// <summary>
        /// 生成一个失败结果。
        /// </summary>
        public static AbilityActivationResult Fail(string reason)
        {
            return Fail(reason, null);
        }

        /// <summary>
        /// 生成一个附带 attempt 详情的失败结果。
        /// </summary>
        public static AbilityActivationResult Fail(string reason, CombatActionAttempt attempt)
        {
            return new AbilityActivationResult
            {
                Succeeded = false,
                FailureReason = reason,
                Attempt = attempt,
                Blocks = attempt == null ? new List<ActionBlock>() : new List<ActionBlock>(attempt.Blocks),
            };
        }

        /// <summary>
        /// 生成一个成功结果。
        /// </summary>
        public static AbilityActivationResult Success(AbilityExecutionRecord record, CombatActionAttempt attempt)
        {
            return new AbilityActivationResult
            {
                Succeeded = true,
                Record = record,
                Attempt = attempt,
                Blocks = attempt == null ? new List<ActionBlock>() : new List<ActionBlock>(attempt.Blocks),
            };
        }

        /// <summary>
        /// 获取本次激活是否成功。
        /// </summary>
        public bool Succeeded { get; private set; }

        /// <summary>
        /// 获取失败原因描述。
        /// </summary>
        public string FailureReason { get; private set; }

        /// <summary>
        /// 获取成功时生成的执行记录。
        /// </summary>
        public AbilityExecutionRecord Record { get; private set; }

        /// <summary>
        /// 获取本次尝试中记录的阻断列表。
        /// </summary>
        public IReadOnlyList<ActionBlock> Blocks { get; private set; }

        /// <summary>
        /// 获取底层尝试对象。
        /// </summary>
        public CombatActionAttempt Attempt { get; private set; }
    }
}
