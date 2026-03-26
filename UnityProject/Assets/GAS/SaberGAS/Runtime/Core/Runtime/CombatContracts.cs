using System;
using System.Collections.Generic;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Foundation;

namespace Saber.GAS.Runtime
{
    /// <summary>
    /// 战斗运行时向外发出的事件类型。
    /// </summary>
    public enum CombatEventKind
    {
        /// <summary>
        /// 技能成功激活。
        /// </summary>
        AbilityActivated = 0,
        /// <summary>
        /// 技能请求被阻断。
        /// </summary>
        AbilityBlocked = 1,
        /// <summary>
        /// 长生命周期技能开始运行。
        /// </summary>
        AbilityStarted = 2,
        /// <summary>
        /// 技能按正常流程完成。
        /// </summary>
        AbilityCompleted = 3,
        /// <summary>
        /// 技能被取消。
        /// </summary>
        AbilityCancelled = 4,
        /// <summary>
        /// 效果被成功施加。
        /// </summary>
        EffectApplied = 5,
        /// <summary>
        /// 效果到期或被移除。
        /// </summary>
        EffectExpired = 6,
        /// <summary>
        /// 某个资源值发生改变。
        /// </summary>
        ResourceChanged = 7,
        /// <summary>
        /// 逻辑 Tick 向前推进。
        /// </summary>
        TickAdvanced = 8,
        /// <summary>
        /// 生成了一份战斗快照。
        /// </summary>
        SnapshotCaptured = 9,
        /// <summary>
        /// 已恢复到一份历史快照。
        /// </summary>
        SnapshotRestored = 10,
        /// <summary>
        /// 开始重放历史命令。
        /// </summary>
        ReplayStarted = 11,
        /// <summary>
        /// 历史命令重放完成。
        /// </summary>
        ReplayCompleted = 12,
    }

    /// <summary>
    /// 战斗事件消息体。
    /// </summary>
    public sealed class CombatEvent
    {
        /// <summary>
        /// 使用事件类型、主体 Actor 和说明消息创建战斗事件。
        /// </summary>
        public CombatEvent(CombatEventKind kind, ActorId actorId, string message)
        {
            Kind = kind;
            ActorId = actorId;
            Message = message;
        }

        /// <summary>
        /// 获取事件类型。
        /// </summary>
        public CombatEventKind Kind { get; }

        /// <summary>
        /// 获取事件关联的主体单位 Id。
        /// </summary>
        public ActorId ActorId { get; }

        /// <summary>
        /// 获取事件附带的说明消息。
        /// </summary>
        public string Message { get; }
    }

    [Flags]
    /// <summary>
    /// 描述两个 Actor 之间关系的可组合标记。
    /// Core 只认识这些抽象关系，不直接假设具体阵营系统如何实现。
    /// </summary>
    public enum CombatActorRelationFlags
    {
        /// <summary>
        /// 不匹配任何关系。
        /// </summary>
        None = 0,
        /// <summary>
        /// 目标就是自己。
        /// </summary>
        Self = 1 << 0,
        /// <summary>
        /// 目标属于友方关系。
        /// </summary>
        Ally = 1 << 1,
        /// <summary>
        /// 目标属于敌对关系。
        /// </summary>
        Enemy = 1 << 2,
        /// <summary>
        /// 目标属于中立关系。
        /// </summary>
        Neutral = 1 << 3,
        /// <summary>
        /// 目标是除自己外的任意其他关系。
        /// </summary>
        Other = Ally | Enemy | Neutral,
        /// <summary>
        /// 匹配自己、友军、敌军和中立的全部关系。
        /// </summary>
        Any = Self | Ally | Enemy | Neutral,
    }

    /// <summary>
    /// 运行时事件输出接口。
    /// </summary>
    public interface ICombatEventSink
    {
        void Publish(CombatEvent combatEvent);
    }

    /// <summary>
    /// 战斗模式规则接口，用于注入回合制、RTS、MOBA 等不同模式约束。
    /// </summary>
    public interface ICombatModeRules
    {
        bool CanActorAct(CombatWorldState worldState, CombatActorState actor, AbilityDefinition ability, out string reason);

        void OnPostTick(CombatWorldState worldState);
    }

    /// <summary>
    /// 负责定义两个 Actor 之间关系的查询接口。
    /// 业务层可以通过它接入复杂阵营、临时盟约或中立逻辑。
    /// </summary>
    public interface ICombatActorRelationResolver
    {
        CombatActorRelationFlags ResolveRelation(CombatWorldState worldState, CombatActorState sourceActor, CombatActorState targetActor);
    }

    /// <summary>
    /// 目标解析接口，负责把输入 targetData 转成运行时目标集合。
    /// </summary>
    public interface ITargetingResolver
    {
        void ResolveTargets(
            CombatWorldState worldState,
            CombatActorState source,
            AbilityDefinition ability,
            AbilityTargetData targetData,
            IList<CombatActorState> results);
    }

    /// <summary>
    /// 默认空事件接收器。
    /// </summary>
    public sealed class NullCombatEventSink : ICombatEventSink
    {
        /// <summary>
        /// 丢弃所有事件输出。
        /// </summary>
        public void Publish(CombatEvent combatEvent)
        {
        }
    }

    /// <summary>
    /// 默认模式规则，仅限制死亡单位不能行动。
    /// </summary>
    public sealed class DefaultCombatModeRules : ICombatModeRules
    {
        /// <summary>
        /// 判断单位当前是否允许行动。
        /// </summary>
        public bool CanActorAct(CombatWorldState worldState, CombatActorState actor, AbilityDefinition ability, out string reason)
        {
            reason = actor.IsAlive ? null : "Source actor is not alive.";
            return actor.IsAlive;
        }

        /// <summary>
        /// 默认模式在 Tick 结束后不做额外处理。
        /// </summary>
        public void OnPostTick(CombatWorldState worldState)
        {
        }
    }

    /// <summary>
    /// Core 侧的关系回退解析器。
    /// 当业务层未注入具体阵营实现时，除自己外的其他单位统一按 Neutral 处理。
    /// </summary>
    public sealed class FallbackCombatActorRelationResolver : ICombatActorRelationResolver
    {
        /// <summary>
        /// 解析两个 Actor 之间的关系标记。
        /// </summary>
        public CombatActorRelationFlags ResolveRelation(CombatWorldState worldState, CombatActorState sourceActor, CombatActorState targetActor)
        {
            if (sourceActor == null || targetActor == null)
            {
                return CombatActorRelationFlags.None;
            }

            if (sourceActor.ActorId == targetActor.ActorId)
            {
                return CombatActorRelationFlags.Self;
            }

            return CombatActorRelationFlags.Neutral | CombatActorRelationFlags.Other;
        }
    }

    /// <summary>
    /// 默认目标解析器，只处理 None、Self 和显式 actorId 列表。
    /// </summary>
    public sealed class DefaultTargetingResolver : ITargetingResolver
    {
        /// <summary>
        /// 将外部输入的目标数据写入结果缓冲。
        /// </summary>
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

            if (ability.Targeting.Kind == AbilityTargetKind.None)
            {
                return;
            }

            if (ability.Targeting.Kind == AbilityTargetKind.Self)
            {
                results.Add(source);
                return;
            }

            if (targetData == null)
            {
                return;
            }

            var actorIds = targetData.TargetActorIds;
            for (var i = 0; i < actorIds.Count; i++)
            {
                CombatActorState actor;
                if (worldState.TryGetActor(actorIds[i], out actor))
                {
                    results.Add(actor);
                }
            }
        }
    }
}
