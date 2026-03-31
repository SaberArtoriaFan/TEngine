using Saber.GAS.Actors;
using Saber.GAS.Runtime;

namespace Saber.GAS.RTS.Relations
{
    /// <summary>
    /// RTS 默认的 Team 阵营关系解析器。
    /// 它把 TeamId 相同解释为 Ally，不同解释为 Enemy，空 TeamId 解释为 Neutral。
    /// 返回值只使用 Self/Ally/Enemy/Neutral 四种基础关系位，避免别名位带来的误匹配。
    /// </summary>
    public sealed class RtsTeamRelationResolver : ICombatActorRelationResolver
    {
        /// <summary>
        /// 获取或设置是否将空 TeamId 视为 Neutral。
        /// </summary>
        public bool TreatEmptyTeamAsNeutral { get; set; } = true;

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

            if (TreatEmptyTeamAsNeutral &&
                (sourceActor.TeamId.IsEmpty || targetActor.TeamId.IsEmpty))
            {
                return CombatActorRelationFlags.Neutral;
            }

            if (sourceActor.TeamId == targetActor.TeamId)
            {
                return CombatActorRelationFlags.Ally;
            }

            return CombatActorRelationFlags.Enemy;
        }
    }
}
