using System.Collections.Generic;
using FastCloner.SourceGenerator.Shared;
using Herta;
using Saber.GAS.Attributes;
using Saber.GAS.Foundation;
using Saber.GAS.Tags;
using Saber.GAS.Triggers;

namespace Saber.GAS.Effects
{
    /// <summary>
    /// 效果的持续时间策略。
    /// </summary>
    public enum EffectDurationPolicy
    {
        /// <summary>
        /// 立即结算，结算后不保留实例。
        /// </summary>
        Instant = 0,
        /// <summary>
        /// 有固定持续时间，到期自动移除。
        /// </summary>
        Timed = 1,
        /// <summary>
        /// 无限持续，需要外部显式移除。
        /// </summary>
        Infinite = 2,
    }

    /// <summary>
    /// 效果重复施加时的叠层策略。
    /// </summary>
    public enum EffectStackPolicy
    {
        /// <summary>
        /// 仅刷新持续时间，不增加层数。
        /// </summary>
        RefreshDuration = 0,
        /// <summary>
        /// 增加层数并刷新持续时间。
        /// </summary>
        AddStackAndRefresh = 1,
        /// <summary>
        /// 拒绝新的效果施加。
        /// </summary>
        RejectNew = 2,
        /// <summary>
        /// 用新效果替换旧效果。
        /// </summary>
        ReplaceExisting = 3,
    }

    [FastClonerClonable]
    /// <summary>
    /// 属性修正定义。
    /// </summary>
    public struct AttributeModifierDefinition
    {
        /// <summary>
        /// 使用属性标识、修正类型与数值创建属性修正定义。
        /// </summary>
        public AttributeModifierDefinition(AttributeId attributeId, AttributeModifierType modifierType, FP magnitude)
        {
            AttributeId = attributeId;
            ModifierType = modifierType;
            Magnitude = magnitude;
        }

        /// <summary>
        /// 获取被修改的属性 Id。
        /// </summary>
        public AttributeId AttributeId { get; }

        /// <summary>
        /// 获取属性修正计算方式。
        /// </summary>
        public AttributeModifierType ModifierType { get; }

        /// <summary>
        /// 获取属性修正数值。
        /// </summary>
        public FP Magnitude { get; }
    }

    [FastClonerClonable]
    /// <summary>
    /// 资源增减定义。
    /// </summary>
    public struct ResourceDeltaDefinition
    {
        /// <summary>
        /// 使用资源标识与增减值创建资源变化定义。
        /// </summary>
        public ResourceDeltaDefinition(ResourceId resourceId, FP amount)
        {
            ResourceId = resourceId;
            Amount = amount;
        }

        /// <summary>
        /// 获取被修改的资源 Id。
        /// </summary>
        public ResourceId ResourceId { get; }

        /// <summary>
        /// 获取资源变化量。
        /// </summary>
        public FP Amount { get; }
    }

    /// <summary>
    /// 效果定义本体，描述一次效果施加后可能产生的属性、资源、标签与移除行为。
    /// </summary>
    public sealed class EffectDefinition
    {
        /// <summary>
        /// 创建一份空效果定义并初始化默认集合字段。
        /// </summary>
        internal EffectDefinition()
        {
            Id = EffectId.Empty;
            EffectTags = new GameplayTagContainer();
            GrantedTags = new GameplayTagContainer();
            RequiredTargetTags = new GameplayTagContainer();
            BlockedTargetTags = new GameplayTagContainer();
            RemovedTargetEffectTags = new GameplayTagContainer();
            RemovedTargetEffectIds = new List<EffectId>();
            AttributeModifiers = new List<AttributeModifierDefinition>();
            InstantResourceDeltas = new List<ResourceDeltaDefinition>();
            PeriodicResourceDeltas = new List<ResourceDeltaDefinition>();
            Extensions = new List<ICombatEffectExtensionDefinition>();
            Triggers = new List<TriggerDefinition>();
            MaxStacks = 1;
            DurationPolicy = EffectDurationPolicy.Instant;
            StackPolicy = EffectStackPolicy.RefreshDuration;
        }

        /// <summary>
        /// 使用效果标识创建一份效果定义。
        /// </summary>
        public EffectDefinition(EffectId id)
            : this()
        {
            Id = id;
        }

        /// <summary>
        /// 获取效果定义 Id。
        /// </summary>
        public EffectId Id { get; internal set; }

        /// <summary>
        /// 获取效果自身语义标签。
        /// </summary>
        public GameplayTagContainer EffectTags { get; internal set; }

        /// <summary>
        /// 获取效果授予目标的运行时标签。
        /// </summary>
        public GameplayTagContainer GrantedTags { get; internal set; }

        /// <summary>
        /// 获取目标必须具备的标签集合。
        /// </summary>
        public GameplayTagContainer RequiredTargetTags { get; internal set; }

        /// <summary>
        /// 获取会阻止目标通过施加校验的标签集合。
        /// </summary>
        public GameplayTagContainer BlockedTargetTags { get; internal set; }

        /// <summary>
        /// 获取施加时要按 Tag 清理的目标效果标签集合。
        /// </summary>
        public GameplayTagContainer RemovedTargetEffectTags { get; internal set; }

        /// <summary>
        /// 获取施加时要按 Id 清理的目标效果列表。
        /// </summary>
        public IList<EffectId> RemovedTargetEffectIds { get; internal set; }

        /// <summary>
        /// 获取持续属性修正列表。
        /// </summary>
        public IList<AttributeModifierDefinition> AttributeModifiers { get; internal set; }

        /// <summary>
        /// 获取瞬时资源变化列表。
        /// </summary>
        public IList<ResourceDeltaDefinition> InstantResourceDeltas { get; internal set; }

        /// <summary>
        /// 获取周期资源变化列表。
        /// </summary>
        public IList<ResourceDeltaDefinition> PeriodicResourceDeltas { get; internal set; }

        /// <summary>
        /// 获取效果扩展定义列表。
        /// </summary>
        public IList<ICombatEffectExtensionDefinition> Extensions { get; internal set; }

        /// <summary>
        /// 获取效果附带的 Trigger 定义列表。
        /// </summary>
        public IList<TriggerDefinition> Triggers { get; internal set; }

        /// <summary>
        /// 获取或设置持续 Tick。
        /// </summary>
        public long DurationTicks { get; set; }

        /// <summary>
        /// 获取或设置周期触发间隔 Tick。
        /// </summary>
        public long PeriodTicks { get; set; }

        /// <summary>
        /// 获取或设置最大叠层数。
        /// </summary>
        public int MaxStacks { get; set; }

        /// <summary>
        /// 获取或设置持续策略。
        /// </summary>
        public EffectDurationPolicy DurationPolicy { get; set; }

        /// <summary>
        /// 获取或设置叠层策略。
        /// </summary>
        public EffectStackPolicy StackPolicy { get; set; }
    }
}
