using Herta;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;
using Saber.GAS.Tags;
using Saber.GAS.Triggers;

namespace Saber.GAS.RTS.CustomActions
{
    /// <summary>
    /// RTS 示例自定义 TriggerAction。
    /// 当拥有者观察到友军承伤时，会替目标分担一半负向资源变化。
    /// </summary>
    public sealed class RtsGuardianShareDamageTriggerAction : ICombatCustomTriggerAction
    {
        /// <summary>
        /// 该自定义动作暴露给 TriggerActionDefinition 使用的稳定 Id。
        /// </summary>
        public const int ActionId = 1001;
        /// <summary>
        /// 标记分摊伤害而生成的重定向 Impact。
        /// </summary>
        private static readonly GameplayTag RedirectedImpactTag = new GameplayTag("rts.custom.damage_share");
        /// <summary>
        /// 默认分担比例。
        /// </summary>
        private static readonly FP ShareRatio = FP._0_50;

        /// <summary>
        /// 获取当前自定义动作的稳定 Id。
        /// </summary>
        public int CustomId => ActionId;

        /// <summary>
        /// 执行一次代伤逻辑，把当前 Impact 中的一半负向资源变化转移给 Trigger 拥有者。
        /// </summary>
        public void ExecuteAction(CombatCustomTriggerActionExecutionContext context)
        {
            var triggerContext = context.TriggerContext;
            if (triggerContext == null ||
                triggerContext.RelatedAttempt == null ||
                triggerContext.RelatedImpact == null)
            {
                return;
            }

            var ownerActorId = context.OwnerActorId;
            if (ownerActorId.IsEmpty || ownerActorId == triggerContext.TargetActorId)
            {
                return;
            }

            var sourceImpact = triggerContext.RelatedImpact;
            var redirectedImpact = new CombatImpact(
                sourceImpact.SourceActorId,
                ownerActorId,
                sourceImpact.AbilityId,
                context.WorldState == null ? SimulationTick.Zero : context.WorldState.CurrentTick,
                sourceImpact.Stage)
            {
                EffectId = sourceImpact.EffectId,
            };

            foreach (var tag in sourceImpact.Tags)
            {
                redirectedImpact.Tags.Add(tag);
            }

            redirectedImpact.Tags.Add(RedirectedImpactTag);

            var hasRedirectedOperation = false;
            for (var operationIndex = 0; operationIndex < sourceImpact.Operations.Count; operationIndex++)
            {
                var operation = sourceImpact.Operations[operationIndex];
                if (operation.Type != CombatImpactOperationType.ResourceDelta || operation.Amount >= FP._0)
                {
                    continue;
                }

                var redirectedAmount = operation.Amount * ShareRatio;
                if (redirectedAmount == FP._0)
                {
                    continue;
                }

                operation.Amount -= redirectedAmount;
                redirectedImpact.Operations.Add(new CombatImpactOperation
                {
                    Type = CombatImpactOperationType.ResourceDelta,
                    ResourceId = operation.ResourceId,
                    Amount = redirectedAmount,
                });
                hasRedirectedOperation = true;
            }

            if (hasRedirectedOperation)
            {
                triggerContext.RelatedAttempt.Impacts.Add(redirectedImpact);
            }
        }
    }
}
