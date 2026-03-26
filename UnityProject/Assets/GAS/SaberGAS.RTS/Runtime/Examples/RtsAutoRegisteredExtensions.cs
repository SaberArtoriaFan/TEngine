using Herta;
using Saber.GAS.Runtime;
using Saber.GAS.Tags;

namespace Saber.GAS.RTS.Examples
{
    /// <summary>
    /// RTS 自动扩展示例使用的标签目录。
    /// 这些标签只用于演示自动注册扩展的触发条件，避免影响正式业务标签命名。
    /// </summary>
    public static class RtsAutoExtensionExampleTags
    {
        /// <summary>
        /// 获取“该技能属于 RTS 指令技能”示例标签。
        /// </summary>
        public static GameplayTag OrderAbility { get; } = new GameplayTag("Ability.RTS.Example.Order");

        /// <summary>
        /// 获取“该单位当前无法下达 RTS 指令”示例标签。
        /// </summary>
        public static GameplayTag CommandLocked { get; } = new GameplayTag("State.RTS.Example.CommandLocked");

        /// <summary>
        /// 获取“该单位属于攻城单位”示例标签。
        /// </summary>
        public static GameplayTag SiegeUnit { get; } = new GameplayTag("Unit.RTS.Example.Siege");

        /// <summary>
        /// 获取“该目标属于建筑单位”示例标签。
        /// </summary>
        public static GameplayTag StructureTarget { get; } = new GameplayTag("Target.RTS.Example.Structure");
    }

    /// <summary>
    /// RTS 自动注册行动阻断示例。
    /// 当技能被标记为 RTS 指令技能且施法者带有“指令锁定”标签时，会直接阻断本次尝试。
    /// </summary>
    public sealed class RtsCommandLockedActionGate : ICombatActionGate
    {
        /// <summary>
        /// 评估一次行动尝试是否应被 RTS 指令锁定示例规则阻断。
        /// </summary>
        public void Evaluate(CombatActionAttempt attempt, CombatWorldState worldState)
        {
            if (attempt == null || attempt.SourceActor == null || attempt.Ability == null)
            {
                return;
            }

            if (!attempt.Ability.AbilityTags.Contains(RtsAutoExtensionExampleTags.OrderAbility))
            {
                return;
            }

            if (!attempt.SourceActor.Tags.Contains(RtsAutoExtensionExampleTags.CommandLocked))
            {
                return;
            }

            attempt.AddBlock(ActionBlockKind.Custom, "Source actor is blocked by the RTS command lock example gate.");
        }
    }

    /// <summary>
    /// RTS 自动注册 Impact 改写示例。
    /// 当攻城单位攻击建筑单位时，会把负向生命资源变化翻倍。
    /// </summary>
    public sealed class RtsSiegeStructureImpactMutator : ICombatImpactMutator
    {
        /// <summary>
        /// 按示例规则改写一次 Impact 的资源变化。
        /// </summary>
        public void Mutate(CombatActionAttempt attempt, CombatImpact impact, CombatWorldState worldState)
        {
            if (impact == null || worldState == null)
            {
                return;
            }

            if (!worldState.TryGetActor(impact.SourceActorId, out var sourceActor) ||
                !worldState.TryGetActor(impact.TargetActorId, out var targetActor))
            {
                return;
            }

            if (!sourceActor.Tags.Contains(RtsAutoExtensionExampleTags.SiegeUnit) ||
                !targetActor.Tags.Contains(RtsAutoExtensionExampleTags.StructureTarget))
            {
                return;
            }

            for (var operationIndex = 0; operationIndex < impact.Operations.Count; operationIndex++)
            {
                var operation = impact.Operations[operationIndex];
                if (operation.Type != CombatImpactOperationType.ResourceDelta || operation.Amount >= FP._0)
                {
                    continue;
                }

                operation.Amount *= FP._2;
            }
        }
    }
}
