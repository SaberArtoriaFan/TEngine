using System.Collections.Generic;
using Herta;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Resources;
using Saber.GAS.Runtime;
using Saber.GAS.Tags;

namespace Saber.GAS.Triggers
{
    /// <summary>
    /// Trigger 的来源类型。
    /// 用于区分该触发器是挂在 Effect、Ability、Actor 还是全局世界规则上。
    /// </summary>
    public enum TriggerSourceKind
    {
        /// <summary>
        /// 来源于持续效果。
        /// </summary>
        Effect = 0,
        /// <summary>
        /// 来源于技能定义或技能实例。
        /// </summary>
        Ability = 1,
        /// <summary>
        /// 来源于角色自身的被动或常驻触发器。
        /// </summary>
        Actor = 2,
        /// <summary>
        /// 来源于世界级规则。
        /// </summary>
        Global = 3,
    }

    /// <summary>
    /// 运行时会抛给 Trigger 系统的标准事件窗口。
    /// Trigger 不直接订阅 Unity 事件，而是统一响应这些确定性的结算节点。
    /// </summary>
    public enum CombatTriggerEventKind
    {
        /// <summary>
        /// 手动触发，不依赖标准事件窗口。
        /// </summary>
        Manual = 0,
        /// <summary>
        /// 在能力尝试正式校验前触发。
        /// </summary>
        BeforeAttempt = 1,
        /// <summary>
        /// 在能力尝试成功后触发。
        /// </summary>
        AfterAttemptSucceeded = 2,
        /// <summary>
        /// 在能力尝试被阻断后触发。
        /// </summary>
        AfterAttemptBlocked = 3,
        /// <summary>
        /// 在 Impact 解析前触发。
        /// </summary>
        BeforeImpactResolve = 4,
        /// <summary>
        /// 在 Impact 解析后触发。
        /// </summary>
        AfterImpactResolved = 5,
        /// <summary>
        /// 在效果施加前触发。
        /// </summary>
        BeforeEffectApplied = 6,
        /// <summary>
        /// 在效果施加后触发。
        /// </summary>
        AfterEffectApplied = 7,
        /// <summary>
        /// 在效果移除或到期后触发。
        /// </summary>
        AfterEffectExpired = 8,
        /// <summary>
        /// 在长生命周期技能进入 Active 时触发。
        /// </summary>
        OnAbilityStarted = 9,
        /// <summary>
        /// 在长生命周期技能正常结束时触发。
        /// </summary>
        OnAbilityCompleted = 10,
        /// <summary>
        /// 在长生命周期技能被取消时触发。
        /// </summary>
        OnAbilityCancelled = 11,
        /// <summary>
        /// 在资源值发生变化时触发。
        /// </summary>
        OnResourceChanged = 12,
        /// <summary>
        /// 在资源跨越阈值时触发。
        /// </summary>
        OnResourceThresholdCrossed = 13,
        /// <summary>
        /// 每个逻辑 Tick 结束时触发。
        /// </summary>
        OnTick = 14,
        /// <summary>
        /// 当拥有者死亡时触发。
        /// </summary>
        OnDeath = 15,
        /// <summary>
        /// 当拥有者击杀其他单位时触发。
        /// </summary>
        OnKill = 16,
        /// <summary>
        /// 当拥有者的队友死亡时触发。
        /// </summary>
        OnAllyDeath = 17,
        /// <summary>
        /// 当单位进入区域时触发。
        /// </summary>
        OnEnterArea = 18,
        /// <summary>
        /// 当单位离开区域时触发。
        /// </summary>
        OnLeaveArea = 19,
    }

    /// <summary>
    /// Trigger 动作真正落地的时机。
    /// 当前版本主要使用立即前、立即后和阶段结束三个窗口。
    /// </summary>
    public enum CombatTriggerTiming
    {
        /// <summary>
        /// 手动时机，由外部自行控制。
        /// </summary>
        Manual = 0,
        /// <summary>
        /// 在对应结算节点的前半段执行。
        /// </summary>
        ImmediatePreResolve = 1,
        /// <summary>
        /// 在对应结算节点的后半段执行。
        /// </summary>
        ImmediatePostResolve = 2,
        /// <summary>
        /// 在当前结算阶段末尾执行。
        /// </summary>
        EndOfStage = 3,
        /// <summary>
        /// 延后到下一 Tick 执行。
        /// </summary>
        NextTick = 4,
    }

    /// <summary>
    /// Trigger 候选的收集方式。
    /// </summary>
    public enum TriggerCollectionMode
    {
        /// <summary>
        /// 只收集拥有者自己的本地 Trigger。
        /// </summary>
        OwnerOnly = 0,
        /// <summary>
        /// 作为目标观察者收集。
        /// </summary>
        ObserveTarget = 1,
        /// <summary>
        /// 作为施加者观察者收集。
        /// </summary>
        ObserveInstigator = 2,
    }

    /// <summary>
    /// 资源阈值事件的判定方向。
    /// </summary>
    public enum TriggerThresholdDirection
    {
        /// <summary>
        /// 不启用阈值判定。
        /// </summary>
        None = 0,
        /// <summary>
        /// 从阈值上方跌破到小于等于阈值时触发。
        /// </summary>
        CrossBelowOrEqual = 1,
        /// <summary>
        /// 从阈值下方回升到大于等于阈值时触发。
        /// </summary>
        CrossAboveOrEqual = 2,
    }

    /// <summary>
    /// Trigger 命中后可执行的标准动作类型。
    /// 这些动作最终仍然回流到 CombatRuntime 的统一结算入口。
    /// </summary>
    public enum TriggerActionKind
    {
        /// <summary>
        /// 不执行任何动作。
        /// </summary>
        None = 0,
        /// <summary>
        /// 触发一个技能激活请求。
        /// </summary>
        ActivateAbility = 1,
        /// <summary>
        /// 施加一个效果。
        /// </summary>
        ApplyEffect = 2,
        /// <summary>
        /// 按效果 Id 移除效果。
        /// </summary>
        RemoveEffectById = 3,
        /// <summary>
        /// 按效果 Tag 批量移除效果。
        /// </summary>
        RemoveEffectsByTag = 4,
        /// <summary>
        /// 向现有 Impact 追加一个操作。
        /// </summary>
        AddImpactOperation = 5,
        /// <summary>
        /// 调整现有 Impact 的数值幅度。
        /// </summary>
        ModifyImpactMagnitude = 6,
        /// <summary>
        /// 增加资源。
        /// </summary>
        AddResource = 7,
        /// <summary>
        /// 扣减资源。
        /// </summary>
        RemoveResource = 8,
        /// <summary>
        /// 添加运行时 Tag。
        /// </summary>
        AddTag = 9,
        /// <summary>
        /// 移除运行时 Tag。
        /// </summary>
        RemoveTag = 10,
        /// <summary>
        /// 取消一个技能。
        /// </summary>
        CancelAbility = 11,
        /// <summary>
        /// 按 Tag 清理效果，常用于净化。
        /// </summary>
        CleanseByTag = 12,
        /// <summary>
        /// 发出表现 Cue。
        /// </summary>
        EmitCue = 13,
        /// <summary>
        /// 把部分 Impact 重定向到另一个单位。
        /// </summary>
        SplitImpactToActor = 14,
        /// <summary>
        /// 按既有 Impact 规模折算资源回复或扣减。
        /// </summary>
        AddResourceFromImpact = 15,
        /// <summary>
        /// 预留给自定义动作解释器。
        /// </summary>
        Custom = 16,
    }

    /// <summary>
    /// TriggerAction 中引用 Actor 的方式。
    /// 用来声明“由谁施放”“对谁生效”。
    /// </summary>
    public enum TriggerActorReference
    {
        /// <summary>
        /// 不引用任何 Actor。
        /// </summary>
        None = 0,
        /// <summary>
        /// 引用 Trigger 拥有者。
        /// </summary>
        Owner = 1,
        /// <summary>
        /// 引用事件中的施加者。
        /// </summary>
        Instigator = 2,
        /// <summary>
        /// 引用事件中的目标。
        /// </summary>
        Target = 3,
    }

    /// <summary>
    /// 资源阈值触发的附加条件。
    /// 例如生命值跌破 30% 时自动开盾。
    /// </summary>
    public sealed class TriggerThresholdDefinition
    {
        /// <summary>
        /// 创建一份空阈值定义。
        /// </summary>
        public TriggerThresholdDefinition()
        {
            ResourceId = ResourceId.Empty;
            Value = FP._0;
            Direction = TriggerThresholdDirection.None;
        }

        /// <summary>
        /// 使用资源、阈值和方向创建阈值定义。
        /// </summary>
        public TriggerThresholdDefinition(ResourceId resourceId, FP value, TriggerThresholdDirection direction)
        {
            ResourceId = resourceId;
            Value = value;
            Direction = direction;
        }

        /// <summary>
        /// 要观察的资源类型。
        /// </summary>
        public ResourceId ResourceId { get; set; }

        /// <summary>
        /// 触发阈值数值。
        /// </summary>
        public FP Value { get; set; }

        /// <summary>
        /// 阈值跨越时的触发方向。
        /// </summary>
        public TriggerThresholdDirection Direction { get; set; }
    }

    /// <summary>
    /// Trigger 命中后要执行的动作定义。
    /// 它只描述“要做什么”，真正执行由 CombatTriggerProcessor 转回 CombatRuntime。
    /// </summary>
    public sealed class TriggerActionDefinition
    {
        /// <summary>
        /// 创建一份默认 Trigger 动作定义。
        /// </summary>
        public TriggerActionDefinition()
        {
            Kind = TriggerActionKind.None;
            SourceActor = TriggerActorReference.Owner;
            TargetActor = TriggerActorReference.None;
            TriggeredAbilityId = AbilityId.Empty;
            EffectId = EffectId.Empty;
            ResourceId = ResourceId.Empty;
            Tag = default(GameplayTag);
            MagnitudeMultiplier = FP._1;
            CustomActionId = 0;
        }

        /// <summary>
        /// 要执行的动作类型。
        /// </summary>
        public TriggerActionKind Kind { get; set; }

        /// <summary>
        /// 动作的来源 Actor 从上下文中如何解析。
        /// </summary>
        public TriggerActorReference SourceActor { get; set; }

        /// <summary>
        /// 动作的目标 Actor 从上下文中如何解析。
        /// </summary>
        public TriggerActorReference TargetActor { get; set; }

        /// <summary>
        /// 当动作是触发技能时要激活的技能标识。
        /// </summary>
        public AbilityId TriggeredAbilityId { get; set; }

        /// <summary>
        /// 当动作是施加效果时直接使用的效果定义模板。
        /// </summary>
        public EffectDefinition EffectDefinition { get; set; }

        /// <summary>
        /// 当动作按效果标识操作时使用的效果 Id。
        /// </summary>
        public EffectId EffectId { get; set; }

        /// <summary>
        /// 当动作按 Tag 处理效果时使用的效果 Tag。
        /// </summary>
        public GameplayTag EffectTag { get; set; }

        /// <summary>
        /// 当动作涉及资源改动时使用的资源类型。
        /// </summary>
        public ResourceId ResourceId { get; set; }

        /// <summary>
        /// 当动作涉及资源改动时使用的固定点数值。
        /// </summary>
        public FP ResourceAmount { get; set; }

        /// <summary>
        /// 当动作涉及 Tag 增删时使用的目标 Tag。
        /// </summary>
        public GameplayTag Tag { get; set; }

        /// <summary>
        /// 追加或改写 Impact 时使用的操作模板。
        /// </summary>
        public CombatImpactOperation OperationTemplate { get; set; }

        /// <summary>
        /// 对目标数值进行缩放时使用的倍率。
        /// </summary>
        public FP MagnitudeMultiplier { get; set; }

        /// <summary>
        /// 发出表现 Cue 时使用的名称。
        /// </summary>
        public string CueName { get; set; }

        /// <summary>
        /// 当动作是取消技能时要匹配的技能标识。
        /// </summary>
        public AbilityId AbilityToCancelId { get; set; }

        /// <summary>
        /// 当动作类型为 Custom 时要分发到的自定义动作 Id。
        /// </summary>
        public int CustomActionId { get; set; }

        /// <summary>
        /// 预留给业务层扩展解释器使用的自定义负载。
        /// </summary>
        public object CustomPayload { get; set; }
    }

    /// <summary>
    /// Trigger 的静态定义数据。
    /// 它负责描述触发时机、过滤条件、冷却/次数限制以及命中后的动作。
    /// </summary>
    public sealed class TriggerDefinition
    {
        /// <summary>
        /// 创建一份空 Trigger 定义并初始化默认集合字段。
        /// </summary>
        internal TriggerDefinition()
        {
            Id = TriggerId.Empty;
            EventKind = CombatTriggerEventKind.Manual;
            Timing = CombatTriggerTiming.Manual;
            CollectionMode = TriggerCollectionMode.OwnerOnly;
            SourceKind = TriggerSourceKind.Actor;
            RelationFilter = CombatActorRelationFlags.Any;
            TargetRelationFilter = CombatActorRelationFlags.Any;
            InstigatorRelationFilter = CombatActorRelationFlags.Any;
            RequiredOwnerTags = new GameplayTagContainer();
            BlockedOwnerTags = new GameplayTagContainer();
            RequiredInstigatorTags = new GameplayTagContainer();
            BlockedInstigatorTags = new GameplayTagContainer();
            RequiredTargetTags = new GameplayTagContainer();
            BlockedTargetTags = new GameplayTagContainer();
            RequiredIncomingAbilityTags = new GameplayTagContainer();
            BlockedIncomingAbilityTags = new GameplayTagContainer();
            RequiredIncomingEffectTags = new GameplayTagContainer();
            BlockedIncomingEffectTags = new GameplayTagContainer();
            RequiredImpactTags = new GameplayTagContainer();
            BlockedImpactTags = new GameplayTagContainer();
            Threshold = new TriggerThresholdDefinition();
            Action = new TriggerActionDefinition();
            EnabledOnCreate = true;
        }

        /// <summary>
        /// 使用 Trigger 标识创建一份 Trigger 定义。
        /// </summary>
        public TriggerDefinition(TriggerId id)
            : this()
        {
            Id = id;
        }

        /// <summary>
        /// Trigger 的稳定标识。
        /// </summary>
        public TriggerId Id { get; internal set; }

        /// <summary>
        /// 便于调试和日志识别的人类可读名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 声明该 Trigger 逻辑来源于 Actor、Effect、Ability 或全局规则。
        /// </summary>
        public TriggerSourceKind SourceKind { get; set; }

        /// <summary>
        /// 该 Trigger 监听的事件窗口。
        /// </summary>
        public CombatTriggerEventKind EventKind { get; set; }

        /// <summary>
        /// 该 Trigger 在对应事件中的执行时机。
        /// </summary>
        public CombatTriggerTiming Timing { get; set; }

        /// <summary>
        /// 该 Trigger 应以本地、自身观察还是观察者方式被收集。
        /// </summary>
        public TriggerCollectionMode CollectionMode { get; set; }

        /// <summary>
        /// 候选 Trigger 排序时使用的优先级。
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Owner 必须具备的 Tag 条件。
        /// </summary>
        public GameplayTagContainer RequiredOwnerTags { get; internal set; }

        /// <summary>
        /// Owner 只要拥有任一 Tag 就会阻断触发。
        /// </summary>
        public GameplayTagContainer BlockedOwnerTags { get; internal set; }

        /// <summary>
        /// Instigator 必须具备的 Tag 条件。
        /// </summary>
        public GameplayTagContainer RequiredInstigatorTags { get; internal set; }

        /// <summary>
        /// Instigator 具备这些 Tag 时会阻断触发。
        /// </summary>
        public GameplayTagContainer BlockedInstigatorTags { get; internal set; }

        /// <summary>
        /// Target 必须具备的 Tag 条件。
        /// </summary>
        public GameplayTagContainer RequiredTargetTags { get; internal set; }

        /// <summary>
        /// Target 具备这些 Tag 时会阻断触发。
        /// </summary>
        public GameplayTagContainer BlockedTargetTags { get; internal set; }

        /// <summary>
        /// 关联技能必须具备的 Tag 条件。
        /// </summary>
        public GameplayTagContainer RequiredIncomingAbilityTags { get; internal set; }

        /// <summary>
        /// 关联技能具备这些 Tag 时会阻断触发。
        /// </summary>
        public GameplayTagContainer BlockedIncomingAbilityTags { get; internal set; }

        /// <summary>
        /// 关联效果必须具备的 Tag 条件。
        /// </summary>
        public GameplayTagContainer RequiredIncomingEffectTags { get; internal set; }

        /// <summary>
        /// 关联效果具备这些 Tag 时会阻断触发。
        /// </summary>
        public GameplayTagContainer BlockedIncomingEffectTags { get; internal set; }

        /// <summary>
        /// 关联 Impact 必须具备的 Tag 条件。
        /// </summary>
        public GameplayTagContainer RequiredImpactTags { get; internal set; }

        /// <summary>
        /// 关联 Impact 具备这些 Tag 时会阻断触发。
        /// </summary>
        public GameplayTagContainer BlockedImpactTags { get; internal set; }

        /// <summary>
        /// Owner 与主要相关对象之间的通用关系过滤。
        /// </summary>
        public CombatActorRelationFlags RelationFilter { get; set; }

        /// <summary>
        /// Owner 与 Target 之间的关系过滤。
        /// </summary>
        public CombatActorRelationFlags TargetRelationFilter { get; set; }

        /// <summary>
        /// Owner 与 Instigator 之间的关系过滤。
        /// </summary>
        public CombatActorRelationFlags InstigatorRelationFilter { get; set; }

        /// <summary>
        /// 观察者 Trigger 允许生效的最大距离。
        /// </summary>
        public FP ObserverMaxDistance { get; set; }

        /// <summary>
        /// 两次成功触发之间需要等待的冷却 Tick 数。
        /// </summary>
        public long CooldownTicks { get; set; }

        /// <summary>
        /// 单个 Tick 内最多允许触发的次数。
        /// </summary>
        public int MaxTriggerCountPerTick { get; set; }

        /// <summary>
        /// 生命周期内最多允许触发的总次数。
        /// </summary>
        public int MaxTriggerCountTotal { get; set; }

        /// <summary>
        /// 创建时拥有的初始充能数量。
        /// </summary>
        public int InitialCharges { get; set; }

        /// <summary>
        /// 触发成功后是否消耗来源效果的叠层或次数。
        /// </summary>
        public bool ConsumeSourceEffectOnTrigger { get; set; }

        /// <summary>
        /// 触发成功后是否直接移除来源效果。
        /// </summary>
        public bool RemoveSourceEffectOnTrigger { get; set; }

        /// <summary>
        /// 当来源单位死亡后是否禁用该 Trigger。
        /// </summary>
        public bool DisableWhenSourceDead { get; set; }

        /// <summary>
        /// 当上下文目标死亡后是否禁用该 Trigger。
        /// </summary>
        public bool DisableWhenTargetDead { get; set; }

        /// <summary>
        /// 创建运行时实例时是否默认启用。
        /// </summary>
        public bool EnabledOnCreate { get; set; }

        /// <summary>
        /// 资源阈值类触发使用的附加阈值条件。
        /// </summary>
        public TriggerThresholdDefinition Threshold { get; internal set; }

        /// <summary>
        /// 命中后最终要执行的动作定义。
        /// </summary>
        public TriggerActionDefinition Action { get; internal set; }
    }
}
