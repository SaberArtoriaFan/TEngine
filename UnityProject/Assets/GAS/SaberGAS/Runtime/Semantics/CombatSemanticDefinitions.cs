using System.Collections.Generic;
using Herta;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Pooling;
using Saber.GAS.Tags;

namespace Saber.GAS.Semantics
{
    /// <summary>
    /// 标准伤害语义分类。
    /// </summary>
    public enum DamageSemanticKind
    {
        /// <summary>
        /// 常规伤害。
        /// </summary>
        Standard = 0,
        /// <summary>
        /// 周期性伤害。
        /// </summary>
        Periodic = 1,
        /// <summary>
        /// 纯粹伤害，不受普通伤害免疫影响。
        /// </summary>
        Pure = 2,
        /// <summary>
        /// 反弹伤害。
        /// </summary>
        Reflect = 3,
    }

    /// <summary>
    /// 标准治疗语义分类。
    /// </summary>
    public enum HealSemanticKind
    {
        /// <summary>
        /// 常规治疗。
        /// </summary>
        Standard = 0,
        /// <summary>
        /// 周期性治疗。
        /// </summary>
        Periodic = 1,
        /// <summary>
        /// 吸血或汲取型治疗。
        /// </summary>
        Drain = 2,
    }

    /// <summary>
    /// 标准控制语义分类。
    /// </summary>
    public enum ControlSemanticKind
    {
        /// <summary>
        /// 眩晕。
        /// </summary>
        Stun = 0,
        /// <summary>
        /// 沉默。
        /// </summary>
        Silence = 1,
        /// <summary>
        /// 定身。
        /// </summary>
        Root = 2,
        /// <summary>
        /// 缴械。
        /// </summary>
        Disarm = 3,
        /// <summary>
        /// 嘲讽。
        /// </summary>
        Taunt = 4,
        /// <summary>
        /// 恐惧。
        /// </summary>
        Fear = 5,
        /// <summary>
        /// 魅惑。
        /// </summary>
        Charm = 6,
        /// <summary>
        /// 压制。
        /// </summary>
        Suppression = 7,
    }

    /// <summary>
    /// 标准驱散语义分类。
    /// </summary>
    public enum DispelSemanticKind
    {
        /// <summary>
        /// 仅清除负面控制。
        /// </summary>
        Cleanse = 0,
        /// <summary>
        /// 驱散常规增益或减益。
        /// </summary>
        Purge = 1,
        /// <summary>
        /// 完全驱散，覆盖更广范围。
        /// </summary>
        FullPurge = 2,
    }

    /// <summary>
    /// 标准免疫语义分类。
    /// </summary>
    public enum ImmunitySemanticKind
    {
        /// <summary>
        /// 免疫常规伤害。
        /// </summary>
        Damage = 0,
        /// <summary>
        /// 免疫控制。
        /// </summary>
        Control = 1,
        /// <summary>
        /// 免疫驱散。
        /// </summary>
        Dispel = 2,
        /// <summary>
        /// 免疫全部标准语义。
        /// </summary>
        All = 3,
    }

    /// <summary>
    /// 标准护盾定义。
    /// 这部分不放在 Core，而是作为语义层附加到效果定义上的扩展数据。
    /// </summary>
    public sealed class ShieldSemanticDefinition
    {
        /// <summary>
        /// 创建一份空护盾定义，并初始化 Tag 条件容器。
        /// </summary>
        public ShieldSemanticDefinition()
        {
            ProtectedResourceId = ResourceId.Empty;
            RequiredImpactTags = new GameplayTagContainer();
            BlockedImpactTags = new GameplayTagContainer();
            RefreshCapacityOnReapply = true;
            RemoveSourceEffectWhenDepleted = true;
        }

        /// <summary>
        /// 使用受保护资源和容量创建一份护盾定义。
        /// </summary>
        public ShieldSemanticDefinition(ResourceId protectedResourceId, FP capacity)
            : this()
        {
            ProtectedResourceId = protectedResourceId;
            Capacity = capacity;
        }

        /// <summary>
        /// 获取或设置护盾名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置该护盾保护的资源类型。
        /// </summary>
        public ResourceId ProtectedResourceId { get; set; }

        /// <summary>
        /// 获取或设置护盾容量。
        /// </summary>
        public FP Capacity { get; set; }

        /// <summary>
        /// 获取命中该护盾所需的 Impact 标签。
        /// </summary>
        public GameplayTagContainer RequiredImpactTags { get; internal set; }

        /// <summary>
        /// 获取会阻止护盾生效的 Impact 标签。
        /// </summary>
        public GameplayTagContainer BlockedImpactTags { get; internal set; }

        /// <summary>
        /// 获取或设置重复施加时是否刷新护盾容量。
        /// </summary>
        public bool RefreshCapacityOnReapply { get; set; }

        /// <summary>
        /// 获取或设置护盾耗尽后是否移除源效果。
        /// </summary>
        public bool RemoveSourceEffectWhenDepleted { get; set; }
    }

    /// <summary>
    /// 挂在效果定义上的标准战斗语义扩展。
    /// 当前主要承载护盾等需要运行时状态的机制；伤害、治疗、控制等仍通过统一语义标签表达。
    /// </summary>
    public sealed class CombatSemanticEffectExtension : ICombatEffectExtensionDefinition
    {
        /// <summary>
        /// 创建一份空语义扩展，并初始化护盾列表。
        /// </summary>
        public CombatSemanticEffectExtension()
        {
            Shields = new List<ShieldSemanticDefinition>();
        }

        /// <summary>
        /// 获取该扩展携带的护盾定义列表。
        /// </summary>
        public IList<ShieldSemanticDefinition> Shields { get; internal set; }
    }

    /// <summary>
    /// 单个护盾层的运行时状态。
    /// </summary>
    public sealed class ActiveShieldState : ICombatPoolable
    {
        /// <summary>
        /// 获取当前护盾实例对应的静态定义。
        /// </summary>
        public ShieldSemanticDefinition Definition { get; internal set; }

        /// <summary>
        /// 获取当前护盾剩余容量。
        /// </summary>
        public FP Remaining { get; internal set; }

        /// <summary>
        /// 按护盾定义和当前层数初始化运行时剩余容量。
        /// </summary>
        internal void Initialize(ShieldSemanticDefinition definition, int stacks)
        {
            Definition = definition;
            var effectiveStacks = stacks < 1 ? 1 : stacks;
            Remaining = definition == null ? FP._0 : definition.Capacity * effectiveStacks;
        }

        /// <summary>
        /// 清空护盾运行时状态，供对象池回收时复位。
        /// </summary>
        public void ResetForPool()
        {
            Definition = null;
            Remaining = FP._0;
        }
    }

    /// <summary>
    /// 标准战斗语义附加在持续效果实例上的运行时状态。
    /// </summary>
    public sealed class CombatSemanticEffectState : ICombatActiveEffectExtensionState
    {
        /// <summary>
        /// 创建语义状态容器，并初始化护盾实例列表。
        /// </summary>
        public CombatSemanticEffectState()
        {
            ActiveShields = new List<ActiveShieldState>();
        }

        /// <summary>
        /// 获取当前效果关联的语义扩展定义。
        /// </summary>
        public CombatSemanticEffectExtension Extension { get; internal set; }

        /// <summary>
        /// 获取当前效果持有的护盾运行时实例列表。
        /// </summary>
        public IList<ActiveShieldState> ActiveShields { get; internal set; }

        /// <summary>
        /// 使用指定扩展定义初始化语义运行时状态。
        /// </summary>
        internal void Initialize(CombatSemanticEffectExtension extension)
        {
            Extension = extension;
        }

        /// <summary>
        /// 清空语义状态，供对象池回收时复位。
        /// </summary>
        public void ResetForPool()
        {
            Extension = null;
            ActiveShields.Clear();
        }
    }
}
