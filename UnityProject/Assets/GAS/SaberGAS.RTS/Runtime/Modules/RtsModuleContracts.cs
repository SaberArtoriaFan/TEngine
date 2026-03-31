using System.Collections.Generic;
using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;

namespace Saber.GAS.RTS.Modules
{
    /// <summary>
    /// RTS 空间查询服务接口。
    /// 用于承接半径检索、范围单位收集等更偏 RTS 的查询能力。
    /// </summary>
    public interface IRtsSpatialQueryService
    {
        /// <summary>
        /// 查询指定范围内的单位，并写入结果缓冲。
        /// </summary>
        void QueryActorsInRange(CombatWorldState worldState, WorldPosition center, FP radius, IList<CombatActorState> results);

        /// <summary>
        /// 查询离指定点最近的单位。
        /// 当 maxDistance 小于等于 0 时表示不限制最大距离。
        /// </summary>
        bool TryFindNearestActor(CombatWorldState worldState, WorldPosition center, FP maxDistance, out CombatActorState nearestActor);

        /// <summary>
        /// 查询离指定施法者最近的其他单位（不返回自己）。
        /// 当 maxDistance 小于等于 0 时表示不限制最大距离。
        /// </summary>
        bool TryFindNearestActor(CombatWorldState worldState, CombatActorState sourceActor, FP maxDistance, out CombatActorState nearestActor);
    }

    /// <summary>
    /// RTS 可见性服务接口。
    /// 预留给视野、战争迷雾、隐身侦测等系统接入。
    /// </summary>
    public interface IRtsVisibilityService
    {
        /// <summary>
        /// 判断观察者当前是否能看到目标。
        /// </summary>
        bool CanObserve(CombatWorldState worldState, CombatActorState observer, CombatActorState target);
    }

    /// <summary>
    /// RTS 指令校验接口。
    /// 用于把选中单位、命令面板和 Ability 请求之间的额外规则拦截放在 RTS 层。
    /// </summary>
    public interface IRtsOrderRuleService
    {
        /// <summary>
        /// 判断当前 Ability 请求是否允许作为 RTS 指令下发。
        /// </summary>
        bool CanIssueOrder(CombatWorldState worldState, CombatActorState sourceActor, AbilityActivationRequest request, out string reason);
    }
}
