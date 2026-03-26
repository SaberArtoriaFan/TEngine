using System.Collections.Generic;
using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Resources;
using Saber.GAS.Runtime;
using Saber.GAS.Tags;
using Saber.GAS.Triggers;

namespace Saber.GAS.Examples
{
    /// <summary>
    /// 观察者 Trigger 示例定义包。
    /// </summary>
    public sealed class TriggerExampleDefinitionSet
    {
        /// <summary>
        /// 创建一个观察者 Trigger 示例包。
        /// </summary>
        public TriggerExampleDefinitionSet(string name)
        {
            Name = name;
            Effects = new List<EffectDefinition>();
            Abilities = new List<AbilityDefinition>();
        }

        /// <summary>
        /// 获取示例名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 获取示例中的效果定义列表。
        /// </summary>
        public IList<EffectDefinition> Effects { get; }

        /// <summary>
        /// 获取示例中的技能定义列表。
        /// </summary>
        public IList<AbilityDefinition> Abilities { get; }
    }

    /// <summary>
    /// 观察者 Trigger 示例工厂。
    /// </summary>
    public static class ObserverTriggerExamples
    {
        /// <summary>
        /// 构造“队友受伤时分担部分伤害”的观察者 Trigger 示例。
        /// </summary>
        public static TriggerExampleDefinitionSet CreateGuardianShareDamageExample(ResourceId healthResourceId)
        {
            var bundle = new TriggerExampleDefinitionSet("GuardianShareDamage");
            var sharedDamageTag = new GameplayTag("Impact.Internal.Example.SharedDamage");

            var effect = new EffectDefinition(new EffectId("Effect.Example.GuardianShare"))
            {
                DurationPolicy = EffectDurationPolicy.Infinite,
            };
            effect.EffectTags.Add(new GameplayTag("Effect.Example.GuardianShare"));
            effect.GrantedTags.Add(new GameplayTag("State.Example.GuardianShare.Active"));

            var trigger = new TriggerDefinition(new TriggerId("Trigger.Example.GuardianShare.BeforeImpact"))
            {
                Name = "Share ally incoming damage",
                SourceKind = TriggerSourceKind.Effect,
                EventKind = CombatTriggerEventKind.BeforeImpactResolve,
                Timing = CombatTriggerTiming.ImmediatePreResolve,
                CollectionMode = TriggerCollectionMode.ObserveTarget,
                TargetRelationFilter = CombatActorRelationFlags.Ally,
                InstigatorRelationFilter = CombatActorRelationFlags.Enemy,
                ObserverMaxDistance = 6,
                Priority = 200,
                MaxTriggerCountPerTick = 1,
                Action = new TriggerActionDefinition
                {
                    Kind = TriggerActionKind.SplitImpactToActor,
                    TargetActor = TriggerActorReference.Owner,
                    MagnitudeMultiplier = 35 * FP._1 / 100,
                    Tag = sharedDamageTag,
                    ResourceId = healthResourceId,
                },
            };
            trigger.RequiredOwnerTags.Add(new GameplayTag("State.Example.GuardianShare.Active"));
            trigger.RequiredImpactTags.Add(new GameplayTag("Impact.Damage"));
            trigger.BlockedImpactTags.Add(sharedDamageTag);

            effect.Triggers.Add(trigger);
            bundle.Effects.Add(effect);
            return bundle;
        }

        /// <summary>
        /// 构造“附近敌人受伤时自己回血”的观察者 Trigger 示例。
        /// </summary>
        public static TriggerExampleDefinitionSet CreateBloodPactRecoveryExample(ResourceId healthResourceId)
        {
            var bundle = new TriggerExampleDefinitionSet("BloodPactRecovery");

            var effect = new EffectDefinition(new EffectId("Effect.Example.BloodPactRecovery"))
            {
                DurationPolicy = EffectDurationPolicy.Infinite,
            };
            effect.EffectTags.Add(new GameplayTag("Effect.Example.BloodPactRecovery"));
            effect.GrantedTags.Add(new GameplayTag("State.Example.BloodPactRecovery.Active"));

            var trigger = new TriggerDefinition(new TriggerId("Trigger.Example.BloodPact.AfterEnemyDamaged"))
            {
                Name = "Recover when nearby enemy takes damage",
                SourceKind = TriggerSourceKind.Effect,
                EventKind = CombatTriggerEventKind.AfterImpactResolved,
                Timing = CombatTriggerTiming.ImmediatePostResolve,
                CollectionMode = TriggerCollectionMode.ObserveTarget,
                TargetRelationFilter = CombatActorRelationFlags.Enemy,
                ObserverMaxDistance = 8,
                Priority = 100,
                Action = new TriggerActionDefinition
                {
                    Kind = TriggerActionKind.AddResourceFromImpact,
                    TargetActor = TriggerActorReference.Owner,
                    ResourceId = healthResourceId,
                    MagnitudeMultiplier = 20 * FP._1 / 100,
                },
            };
            trigger.RequiredOwnerTags.Add(new GameplayTag("State.Example.BloodPactRecovery.Active"));
            trigger.RequiredImpactTags.Add(new GameplayTag("Impact.Damage"));

            effect.Triggers.Add(trigger);
            bundle.Effects.Add(effect);
            return bundle;
        }
    }
}
