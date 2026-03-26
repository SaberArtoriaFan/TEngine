using System.Collections.Generic;
using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;

namespace Saber.GAS.RTS.Examples
{
    /// <summary>
    /// RTS 自动扩展示例包。
    /// 用来展示跨程序集自动注册的扩展如何在不手工修改 RuntimeOptions 的情况下生效。
    /// </summary>
    public sealed class RtsAutoExtensionExampleBundle
    {
        /// <summary>
        /// 创建一份 RTS 自动扩展示例包。
        /// </summary>
        public RtsAutoExtensionExampleBundle(string name)
        {
            Name = name;
            RuntimeOptions = new CombatRuntimeOptions();
            Abilities = new List<AbilityDefinition>();
            Notes = new List<string>();
        }

        /// <summary>
        /// 获取示例包名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 获取示例运行时配置。
        /// 示例依赖的 Gate 和 Mutator 由源码生成自动注册，因此这里不需要手工追加扩展实例。
        /// </summary>
        public CombatRuntimeOptions RuntimeOptions { get; }

        /// <summary>
        /// 获取示例中构造出的技能定义列表。
        /// </summary>
        public IList<AbilityDefinition> Abilities { get; }

        /// <summary>
        /// 获取示例使用说明列表。
        /// </summary>
        public IList<string> Notes { get; }
    }

    /// <summary>
    /// RTS 自动扩展示例工厂。
    /// </summary>
    public static class RtsAutoExtensionExamples
    {
        /// <summary>
        /// 构造“指令锁定 + 攻城翻倍伤害”示例。
        /// </summary>
        public static RtsAutoExtensionExampleBundle CreateOrderLockAndSiegeExample(ResourceId healthResourceId)
        {
            var bundle = new RtsAutoExtensionExampleBundle("OrderLockAndSiege");

            var orderAbility = new AbilityDefinition(new AbilityId("Ability.Example.RTS.OrderMove"))
            {
                Name = "RTS Order Move",
            };
            orderAbility.AbilityTags.Add(RtsAutoExtensionExampleTags.OrderAbility);
            orderAbility.Effects.Add(CreateDamageEffect(
                new EffectId("Effect.Example.RTS.OrderMove.PingDamage"),
                healthResourceId,
                FP._10));

            var siegeAbility = new AbilityDefinition(new AbilityId("Ability.Example.RTS.SiegeShot"))
            {
                Name = "RTS Siege Shot",
            };
            siegeAbility.Effects.Add(CreateDamageEffect(
                new EffectId("Effect.Example.RTS.SiegeShot.Damage"),
                healthResourceId,
                40));

            bundle.Abilities.Add(orderAbility);
            bundle.Abilities.Add(siegeAbility);
            bundle.Notes.Add("给施法者添加 State.RTS.Example.CommandLocked 后，带有 Ability.RTS.Example.Order 标签的技能会被自动阻断。");
            bundle.Notes.Add("给施法者添加 Unit.RTS.Example.Siege，给目标添加 Target.RTS.Example.Structure 后，负向生命变化会被自动翻倍。");
            bundle.Notes.Add("以上扩展来自 Saber.GAS.RTS 程序集自动注册，不需要手工把 Gate 或 Mutator 塞进 RuntimeOptions。");
            return bundle;
        }

        /// <summary>
        /// 创建一个用于示例的即时伤害效果。
        /// </summary>
        private static EffectDefinition CreateDamageEffect(EffectId effectId, ResourceId resourceId, FP amount)
        {
            var effect = new EffectDefinition(effectId);
            effect.InstantResourceDeltas.Add(new ResourceDeltaDefinition(resourceId, -amount));
            return effect;
        }
    }
}
