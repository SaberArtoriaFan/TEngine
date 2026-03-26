using System;
using System.Collections.Generic;
using FastCloner.SourceGenerator.Shared;
using Herta;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Resources;
using Saber.GAS.Tags;
using Saber.GAS.Triggers;

namespace Saber.GAS.Abilities
{
    /// <summary>
    /// 技能激活的生命周期模式。
    /// </summary>
    public enum AbilityActivationMode
    {
        /// <summary>
        /// 瞬发技能，激活后立即结算。
        /// </summary>
        Instant = 0,
        /// <summary>
        /// 需要先完成施法时间，再进入结算或持续阶段。
        /// </summary>
        Cast = 1,
        /// <summary>
        /// 引导型技能，会在激活期间持续触发周期逻辑。
        /// </summary>
        Channeled = 2,
        /// <summary>
        /// 被动技能，不允许主动请求释放。
        /// </summary>
        Passive = 3,
    }

    /// <summary>
    /// 技能期望的目标输入形态。
    /// </summary>
    public enum AbilityTargetKind
    {
        /// <summary>
        /// 不需要目标。
        /// </summary>
        None = 0,
        /// <summary>
        /// 目标固定为自己。
        /// </summary>
        Self = 1,
        /// <summary>
        /// 目标是一个或多个 Actor。
        /// </summary>
        Actor = 2,
        /// <summary>
        /// 目标是一个世界坐标点。
        /// </summary>
        Point = 3,
        /// <summary>
        /// 目标是一个区域输入。
        /// </summary>
        Area = 4,
    }

    [Flags]
    /// <summary>
    /// 技能允许命中的目标关系与生存状态标记。
    /// </summary>
    public enum AbilityTargetFlags
    {
        /// <summary>
        /// 不附加任何目标限制。
        /// </summary>
        None = 0,
        /// <summary>
        /// 允许以自己为目标。
        /// </summary>
        Self = 1 << 0,
        /// <summary>
        /// 允许命中友军。
        /// </summary>
        Ally = 1 << 1,
        /// <summary>
        /// 允许命中敌军。
        /// </summary>
        Enemy = 1 << 2,
        /// <summary>
        /// 允许命中中立单位。
        /// </summary>
        Neutral = 1 << 3,
        /// <summary>
        /// 允许命中死亡单位。
        /// </summary>
        Dead = 1 << 4,
        /// <summary>
        /// 允许命中存活单位。
        /// </summary>
        Alive = 1 << 5,
        /// <summary>
        /// 允许使用点目标输入。
        /// </summary>
        Point = 1 << 6,
    }

    [FastClonerClonable]
    /// <summary>
    /// 技能冷却配置。
    /// </summary>
    public struct AbilityCooldownDefinition
    {
        /// <summary>
        /// 使用持续 Tick 与冷却 Tag 创建冷却配置。
        /// </summary>
        public AbilityCooldownDefinition(long durationTicks, GameplayTag cooldownTag)
        {
            DurationTicks = durationTicks;
            CooldownTag = cooldownTag;
        }

        /// <summary>
        /// 获取冷却持续的 Tick 数。
        /// </summary>
        public long DurationTicks { get; }

        /// <summary>
        /// 获取用于标记冷却状态的 Tag。
        /// </summary>
        public GameplayTag CooldownTag { get; }
    }

    /// <summary>
    /// 技能目标解析与目标校验配置。
    /// </summary>
    public sealed class AbilityTargetingDefinition
    {
        /// <summary>
        /// 创建一份默认目标配置，并初始化所有 Tag 容器。
        /// </summary>
        public AbilityTargetingDefinition()
        {
            Kind = AbilityTargetKind.None;
            AllowedFlags = AbilityTargetFlags.None;
            MaxRange = FP._0;
            RequiredSourceTags = new GameplayTagContainer();
            RequiredTargetTags = new GameplayTagContainer();
            BlockedSourceTags = new GameplayTagContainer();
            BlockedTargetTags = new GameplayTagContainer();
        }

        /// <summary>
        /// 获取或设置目标输入类型。
        /// </summary>
        public AbilityTargetKind Kind { get; set; }

        /// <summary>
        /// 获取或设置允许的目标标记组合。
        /// </summary>
        public AbilityTargetFlags AllowedFlags { get; set; }

        /// <summary>
        /// 获取或设置最大作用距离。
        /// </summary>
        public FP MaxRange { get; set; }

        /// <summary>
        /// 获取施法者必须具备的标签集合。
        /// </summary>
        public GameplayTagContainer RequiredSourceTags { get; internal set; }

        /// <summary>
        /// 获取目标必须具备的标签集合。
        /// </summary>
        public GameplayTagContainer RequiredTargetTags { get; internal set; }

        /// <summary>
        /// 获取会阻止施法者通过校验的标签集合。
        /// </summary>
        public GameplayTagContainer BlockedSourceTags { get; internal set; }

        /// <summary>
        /// 获取会阻止目标通过校验的标签集合。
        /// </summary>
        public GameplayTagContainer BlockedTargetTags { get; internal set; }
    }

    /// <summary>
    /// 技能定义本体，描述一个能力在运行时的静态配置。
    /// </summary>
    public sealed class AbilityDefinition
    {
        /// <summary>
        /// 创建一份空技能定义并初始化默认集合字段。
        /// </summary>
        internal AbilityDefinition()
        {
            Id = AbilityId.Empty;
            AbilityTags = new GameplayTagContainer();
            GrantedTagsWhileActive = new GameplayTagContainer();
            ActivationRequiredTags = new GameplayTagContainer();
            ActivationBlockedTags = new GameplayTagContainer();
            Costs = new List<ResourceCost>();
            Effects = new List<EffectDefinition>();
            PeriodicEffects = new List<EffectDefinition>();
            EndEffects = new List<EffectDefinition>();
            Triggers = new List<TriggerDefinition>();
            Cooldown = new AbilityCooldownDefinition(0, default(GameplayTag));
            Targeting = new AbilityTargetingDefinition();
            ActivationMode = AbilityActivationMode.Instant;
            ExecuteEffectsOnActivate = true;
            CancelOnSourceDeath = true;
            AutoActivatePassive = true;
        }

        /// <summary>
        /// 使用技能标识创建一份技能定义。
        /// </summary>
        public AbilityDefinition(AbilityId id)
            : this()
        {
            Id = id;
        }

        /// <summary>
        /// 获取技能定义 Id。
        /// </summary>
        public AbilityId Id { get; internal set; }

        /// <summary>
        /// 获取或设置技能名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置技能激活模式。
        /// </summary>
        public AbilityActivationMode ActivationMode { get; set; }

        /// <summary>
        /// 获取技能自身的语义标签。
        /// </summary>
        public GameplayTagContainer AbilityTags { get; internal set; }

        /// <summary>
        /// 获取技能激活期间授予施法者的标签。
        /// </summary>
        public GameplayTagContainer GrantedTagsWhileActive { get; internal set; }

        /// <summary>
        /// 获取激活所需的施法者标签。
        /// </summary>
        public GameplayTagContainer ActivationRequiredTags { get; internal set; }

        /// <summary>
        /// 获取会阻止激活的施法者标签。
        /// </summary>
        public GameplayTagContainer ActivationBlockedTags { get; internal set; }

        /// <summary>
        /// 获取技能消耗列表。
        /// </summary>
        public IList<ResourceCost> Costs { get; internal set; }

        /// <summary>
        /// 获取主执行阶段施加的效果列表。
        /// </summary>
        public IList<EffectDefinition> Effects { get; internal set; }

        /// <summary>
        /// 获取周期阶段施加的效果列表。
        /// </summary>
        public IList<EffectDefinition> PeriodicEffects { get; internal set; }

        /// <summary>
        /// 获取结束阶段施加的效果列表。
        /// </summary>
        public IList<EffectDefinition> EndEffects { get; internal set; }

        /// <summary>
        /// 获取技能自带的 Trigger 定义列表。
        /// </summary>
        public IList<TriggerDefinition> Triggers { get; internal set; }

        /// <summary>
        /// 获取或设置冷却配置。
        /// </summary>
        public AbilityCooldownDefinition Cooldown { get; set; }

        /// <summary>
        /// 获取目标解析配置。
        /// </summary>
        public AbilityTargetingDefinition Targeting { get; internal set; }

        /// <summary>
        /// 获取或设置施法持续 Tick。
        /// </summary>
        public long CastDurationTicks { get; set; }

        /// <summary>
        /// 获取或设置技能激活持续 Tick。
        /// </summary>
        public long ActiveDurationTicks { get; set; }

        /// <summary>
        /// 获取或设置周期触发间隔 Tick。
        /// </summary>
        public long IntervalTicks { get; set; }

        /// <summary>
        /// 获取或设置是否在激活时立即执行主效果。
        /// </summary>
        public bool ExecuteEffectsOnActivate { get; set; }

        /// <summary>
        /// 获取或设置施法者死亡时是否取消技能。
        /// </summary>
        public bool CancelOnSourceDeath { get; set; }

        /// <summary>
        /// 获取或设置被动技能是否自动激活。
        /// </summary>
        public bool AutoActivatePassive { get; set; }
    }
}
