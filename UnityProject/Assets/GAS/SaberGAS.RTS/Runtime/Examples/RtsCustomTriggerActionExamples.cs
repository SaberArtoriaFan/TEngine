using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;
using Saber.GAS.RTS.CustomActions;
using Saber.GAS.RTS.Operations;
using Saber.GAS.Resources;
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

        /// <summary>
        /// 创建一个“受伤后反给最远敌人”的 Trigger 定义。
        /// </summary>
        public static TriggerDefinition CreateReflectDamageToFarthestEnemyTrigger(
            TriggerId triggerId,
            FP reflectionRatio,
            FP maxDistance = default(FP))
        {
            if (reflectionRatio <= FP._0)
            {
                reflectionRatio = FP._1;
            }

            if (maxDistance < FP._0)
            {
                maxDistance = FP._0;
            }

            var trigger = new TriggerDefinition(triggerId)
            {
                Name = "RTS Reflect Damage To Farthest Enemy",
                SourceKind = TriggerSourceKind.Ability,
                EventKind = CombatTriggerEventKind.BeforeImpactResolve,
                Timing = CombatTriggerTiming.ImmediatePreResolve,
                CollectionMode = TriggerCollectionMode.OwnerOnly,
                TargetRelationFilter = CombatActorRelationFlags.Self,
                InstigatorRelationFilter = CombatActorRelationFlags.Enemy,
            };

            trigger.Action.Kind = TriggerActionKind.Custom;
            trigger.Action.TargetActor = TriggerActorReference.Owner;
            trigger.Action.CustomActionId = RtsReflectDamageToFarthestEnemyTriggerAction.ActionId;
            trigger.Action.CustomPayload = new RtsReflectDamageToFarthestEnemyPayload
            {
                ReflectionRatio = reflectionRatio,
                MaxDistance = maxDistance,
            };
            return trigger;
        }

        /// <summary>
        /// 创建一个演示用被动技能：
        /// 拥有者受到伤害时，按配置把伤害反给最远敌方单位。
        /// </summary>
        public static AbilityDefinition CreateReflectDamageToFarthestEnemyPassiveAbility(
            AbilityId abilityId,
            TriggerId triggerId,
            FP reflectionRatio,
            FP maxDistance = default(FP))
        {
            var ability = new AbilityDefinition(abilityId)
            {
                Name = "RTS Reflect Damage To Farthest Enemy",
                ActivationMode = AbilityActivationMode.Passive,
                AutoActivatePassive = true,
            };
            ability.Triggers.Add(CreateReflectDamageToFarthestEnemyTrigger(triggerId, reflectionRatio, maxDistance));
            return ability;
        }

        /// <summary>
        /// 创建一个用于测试反伤触发器的简单即时伤害技能。
        /// </summary>
        public static AbilityDefinition CreateSimpleDamageAbility(AbilityId abilityId, ResourceId healthResourceId, FP damage)
        {
            var ability = new AbilityDefinition(abilityId)
            {
                Name = "RTS Simple Damage Ability",
            };
            ability.Targeting.Kind = AbilityTargetKind.Actor;

            var effect = new EffectDefinition(new EffectId($"{abilityId.Value}.Damage"));
            effect.InstantResourceDeltas.Add(new ResourceDeltaDefinition(healthResourceId, -damage));
            ability.Effects.Add(effect);
            return ability;
        }

        /// <summary>
        /// 创建一个附带吸血扩展示例操作的即时伤害技能。
        /// 吸血逻辑通过外部 ImpactOperationHandler 处理，不需要修改 CombatRuntime 核心。
        /// </summary>
        public static AbilityDefinition CreateLifeStealStrikeAbility(
            AbilityId abilityId,
            ResourceId healthResourceId,
            FP damage,
            FP lifeStealRatio)
        {
            if (lifeStealRatio < FP._0)
            {
                lifeStealRatio = FP._0;
            }

            var ability = CreateSimpleDamageAbility(abilityId, healthResourceId, damage);
            ability.Name = "RTS LifeSteal Strike Ability";
            if (ability.Effects.Count == 0)
            {
                return ability;
            }

            var effect = ability.Effects[0];
            effect.ImpactOperations.Add(new CombatImpactOperation
            {
                Type = CombatImpactOperationType.Cue,
                CueName = RtsLifeStealImpactOperationHandler.CueName,
                Payload = new RtsLifeStealImpactOperationPayload
                {
                    ResourceId = healthResourceId,
                    Ratio = lifeStealRatio,
                },
            });
            return ability;
        }
    }
}
