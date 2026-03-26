using System.Collections.Generic;
using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;
using Saber.GAS.Semantics;
using Saber.GAS.Serialization;

namespace Saber.GAS.Examples
{
    /// <summary>
    /// 标准战斗语义层示例包。
    /// 方便直接查看 Catalog、Builder、RuntimeOptions 和 CloneProvider 应该如何配套使用。
    /// </summary>
    public sealed class CombatSemanticExampleBundle
    {
        /// <summary>
        /// 创建一份完整的语义层示例包。
        /// 默认走自动注册的规则模块和克隆处理器，因此不需要再手工把语义模块塞进 RuntimeOptions。
        /// </summary>
        public CombatSemanticExampleBundle(string name)
        {
            Name = name;
            Catalog = new CombatSemanticCatalog();
            Builder = new CombatSemanticBuilder(Catalog);
            RuntimeOptions = new CombatRuntimeOptions();
            CloneProvider = new FastClonerDeepCloneProvider();
            Effects = new List<EffectDefinition>();
            Abilities = new List<AbilityDefinition>();
        }

        /// <summary>
        /// 获取示例包名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 获取示例使用的语义标签目录。
        /// </summary>
        public CombatSemanticCatalog Catalog { get; }

        /// <summary>
        /// 获取示例使用的语义构造器。
        /// </summary>
        public CombatSemanticBuilder Builder { get; }

        /// <summary>
        /// 获取示例对应的运行时配置。
        /// 自动注册的规则模块会在 Runtime 创建时合并进来，因此这里保持空配置即可。
        /// </summary>
        public CombatRuntimeOptions RuntimeOptions { get; }

        /// <summary>
        /// 获取示例使用的深拷贝提供器。
        /// 默认会收集自动注册的扩展克隆处理器，因此无需手工传入模块列表。
        /// </summary>
        public IDeepCloneProvider CloneProvider { get; }

        /// <summary>
        /// 获取示例中构造出的效果定义列表。
        /// </summary>
        public IList<EffectDefinition> Effects { get; }

        /// <summary>
        /// 获取示例中构造出的技能定义列表。
        /// </summary>
        public IList<AbilityDefinition> Abilities { get; }
    }

    /// <summary>
    /// 标准战斗语义示例工厂。
    /// </summary>
    public static class CombatSemanticExamples
    {
        /// <summary>
        /// 构造“爆发伤害 + 护盾 + 净化”组合示例。
        /// </summary>
        public static CombatSemanticExampleBundle CreateBurstBarrierAndCleanseExample(ResourceId healthResourceId)
        {
            var bundle = new CombatSemanticExampleBundle("BurstBarrierAndCleanse");

            var strikeAbility = new AbilityDefinition(new AbilityId("Ability.Example.Semantic.BurstStrike"));
            bundle.Builder.MarkAsAttackAbility(strikeAbility);
            strikeAbility.Effects.Add(bundle.Builder.CreateDamageEffect(
                new EffectId("Effect.Example.Semantic.BurstStrike.Damage"),
                healthResourceId,
                120));

            var barrierAbility = new AbilityDefinition(new AbilityId("Ability.Example.Semantic.Barrier"));
            bundle.Builder.MarkAsCastAbility(barrierAbility);
            barrierAbility.Effects.Add(bundle.Builder.CreateShieldEffect(
                new EffectId("Effect.Example.Semantic.Barrier.Shield"),
                healthResourceId,
                180,
                durationTicks: 120));

            var stunEffect = bundle.Builder.CreateControlEffect(
                new EffectId("Effect.Example.Semantic.Stun"),
                ControlSemanticKind.Stun,
                durationTicks: 45);
            var dispelEffect = bundle.Builder.CreateDispelEffect(
                new EffectId("Effect.Example.Semantic.Cleanse"),
                DispelSemanticKind.Cleanse,
                removeNegative: true);

            var cleanseAbility = new AbilityDefinition(new AbilityId("Ability.Example.Semantic.Cleanse"));
            bundle.Builder.MarkAsCastAbility(cleanseAbility);
            cleanseAbility.Effects.Add(dispelEffect);

            bundle.Effects.Add(stunEffect);
            bundle.Effects.Add(dispelEffect);
            bundle.Abilities.Add(strikeAbility);
            bundle.Abilities.Add(barrierAbility);
            bundle.Abilities.Add(cleanseAbility);
            return bundle;
        }

        /// <summary>
        /// 构造“控制免疫对抗控制效果”示例。
        /// </summary>
        public static CombatSemanticExampleBundle CreateControlImmunityExample()
        {
            var bundle = new CombatSemanticExampleBundle("ControlImmunity");

            var immunityAbility = new AbilityDefinition(new AbilityId("Ability.Example.Semantic.ControlWard"));
            bundle.Builder.MarkAsCastAbility(immunityAbility);
            immunityAbility.Effects.Add(bundle.Builder.CreateImmunityEffect(
                new EffectId("Effect.Example.Semantic.ControlWard"),
                ImmunitySemanticKind.Control,
                durationTicks: 90));

            var silenceAbility = new AbilityDefinition(new AbilityId("Ability.Example.Semantic.SilenceField"));
            bundle.Builder.MarkAsCastAbility(silenceAbility);
            silenceAbility.Effects.Add(bundle.Builder.CreateControlEffect(
                new EffectId("Effect.Example.Semantic.Silence"),
                ControlSemanticKind.Silence,
                durationTicks: 60));

            bundle.Abilities.Add(immunityAbility);
            bundle.Abilities.Add(silenceAbility);
            return bundle;
        }

        /// <summary>
        /// 构造“持续伤害 + 吸血治疗”示例。
        /// </summary>
        public static CombatSemanticExampleBundle CreateDrainAndPeriodicDamageExample(ResourceId healthResourceId)
        {
            var bundle = new CombatSemanticExampleBundle("DrainAndPeriodicDamage");

            var dotAbility = new AbilityDefinition(new AbilityId("Ability.Example.Semantic.BurningCurse"));
            bundle.Builder.MarkAsCastAbility(dotAbility);
            dotAbility.Effects.Add(bundle.Builder.CreatePeriodicDamageEffect(
                new EffectId("Effect.Example.Semantic.BurningCurse.Dot"),
                healthResourceId,
                magnitudePerTick: 18,
                periodTicks: 15,
                durationTicks: 90));

            var drainAbility = new AbilityDefinition(new AbilityId("Ability.Example.Semantic.DrainTouch"));
            bundle.Builder.MarkAsCastAbility(drainAbility);
            drainAbility.Effects.Add(bundle.Builder.CreateHealEffect(
                new EffectId("Effect.Example.Semantic.DrainTouch.Heal"),
                healthResourceId,
                magnitude: 40,
                healKind: HealSemanticKind.Drain));

            bundle.Abilities.Add(dotAbility);
            bundle.Abilities.Add(drainAbility);
            return bundle;
        }
    }
}
