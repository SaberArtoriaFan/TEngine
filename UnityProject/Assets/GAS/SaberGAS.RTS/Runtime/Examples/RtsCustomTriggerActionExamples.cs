using Herta;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;
using Saber.GAS.RTS.CustomActions;
using Saber.GAS.Triggers;

namespace Saber.GAS.RTS.Examples
{
    /// <summary>
    /// RTS 侧的自定义 TriggerAction 示例构造器。
    /// 用于演示业务层如何引用源码生成自动注册的自定义动作。
    /// </summary>
    public static class RtsCustomTriggerActionExamples
    {
        /// <summary>
        /// 创建一个“观察友军承伤并代为分担一半伤害”的 Trigger 定义。
        /// </summary>
        public static TriggerDefinition CreateGuardianShareDamageTrigger(TriggerId triggerId)
        {
            var trigger = new TriggerDefinition(triggerId)
            {
                Name = "RTS Guardian Share Damage",
                SourceKind = TriggerSourceKind.Actor,
                EventKind = CombatTriggerEventKind.BeforeImpactResolve,
                Timing = CombatTriggerTiming.ImmediatePreResolve,
                CollectionMode = TriggerCollectionMode.ObserveTarget,
                TargetRelationFilter = CombatActorRelationFlags.Ally,
                InstigatorRelationFilter = CombatActorRelationFlags.Enemy,
                ObserverMaxDistance = FP._5,
            };

            trigger.Action.Kind = TriggerActionKind.Custom;
            trigger.Action.TargetActor = TriggerActorReference.Owner;
            trigger.Action.CustomActionId = RtsGuardianShareDamageTriggerAction.ActionId;
            return trigger;
        }
    }
}
