using System.Collections.Generic;
using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Projectiles;
using Saber.GAS.Tags;

namespace Saber.GAS.Runtime
{
    /// <summary>
    /// 表示一次能力执行在结算链中的阶段。
    /// </summary>
    public enum ActionExecutionStage
    {
        /// <summary>
        /// 激活阶段，进行请求校验、扣费和冷却处理。
        /// </summary>
        Activation = 0,
        /// <summary>
        /// 主执行阶段，处理技能的主体效果。
        /// </summary>
        Execute = 1,
        /// <summary>
        /// 周期阶段，处理引导或持续效果的周期结算。
        /// </summary>
        Periodic = 2,
        /// <summary>
        /// 结束阶段，处理收尾或结束效果。
        /// </summary>
        End = 3,
    }

    /// <summary>
    /// 表示一次行动尝试被阻断的原因分类。
    /// </summary>
    public enum ActionBlockKind
    {
        /// <summary>
        /// 请求数据本身无效。
        /// </summary>
        InvalidRequest = 0,
        /// <summary>
        /// 施法者状态不满足要求。
        /// </summary>
        SourceState = 1,
        /// <summary>
        /// 技能定义或拥有状态异常。
        /// </summary>
        AbilityState = 2,
        /// <summary>
        /// 资源消耗不足。
        /// </summary>
        Cost = 3,
        /// <summary>
        /// 处于冷却中。
        /// </summary>
        Cooldown = 4,
        /// <summary>
        /// 目标输入或目标校验失败。
        /// </summary>
        Targeting = 5,
        /// <summary>
        /// 生命周期限制导致无法激活。
        /// </summary>
        Lifecycle = 6,
        /// <summary>
        /// 战斗模式规则阻断。
        /// </summary>
        ModeRule = 7,
        /// <summary>
        /// 自定义扩展逻辑阻断。
        /// </summary>
        Custom = 8,
    }

    /// <summary>
    /// 记录一次阻断结果。
    /// </summary>
    public sealed class ActionBlock
    {
        /// <summary>
        /// 使用阻断类型和消息创建一条阻断记录。
        /// </summary>
        public ActionBlock(ActionBlockKind kind, string message)
        {
            Kind = kind;
            Message = message;
        }

        /// <summary>
        /// 获取阻断类型。
        /// </summary>
        public ActionBlockKind Kind { get; }

        /// <summary>
        /// 获取阻断消息。
        /// </summary>
        public string Message { get; }
    }

    /// <summary>
    /// 表示一个 impact 中的单个操作类型。
    /// </summary>
    public enum CombatImpactOperationType
    {
        /// <summary>
        /// 修改目标资源值。
        /// </summary>
        ResourceDelta = 0,
        /// <summary>
        /// 施加一个效果。
        /// </summary>
        ApplyEffect = 1,
        /// <summary>
        /// 按 Tag 移除一组效果。
        /// </summary>
        RemoveEffectsByTag = 2,
        /// <summary>
        /// 按 Id 移除一个具体效果。
        /// </summary>
        RemoveEffectById = 3,
        /// <summary>
        /// 发出一个表现 Cue。
        /// </summary>
        Cue = 4,
        SpawnProjectile = 5,
    }

    /// <summary>
    /// 描述一次 impact 里的具体操作负载。
    /// </summary>
    public sealed class CombatImpactOperation
    {
        /// <summary>
        /// 获取或设置操作类型。
        /// </summary>
        public CombatImpactOperationType Type { get; set; }

        /// <summary>
        /// 获取或设置关联资源 Id。
        /// </summary>
        public ResourceId ResourceId { get; set; }

        /// <summary>
        /// 获取或设置关联效果 Id。
        /// </summary>
        public EffectId EffectId { get; set; }

        /// <summary>
        /// 获取或设置关联标签。
        /// </summary>
        public GameplayTag Tag { get; set; }

        /// <summary>
        /// 获取或设置数值变化量。
        /// </summary>
        public FP Amount { get; set; }

        /// <summary>
        /// 获取或设置要施加的效果快照。
        /// </summary>
        public EffectSpec EffectSpec { get; set; }

        /// <summary>
        /// 获取或设置要发出的表现 Cue 名称。
        /// </summary>
        public string CueName { get; set; }

        /// <summary>
        /// 获取或设置投射物生成定义。
        /// </summary>
        public CombatProjectileSpawnDefinition Projectile { get; set; }

        /// <summary>
        /// 获取或设置自定义载荷。
        /// </summary>
        public object Payload { get; set; }
    }

    /// <summary>
    /// 表示一次尝试针对某个目标生成的结构化影响包。
    /// </summary>
    public sealed class CombatImpact
    {
        /// <summary>
        /// 用来源、目标、技能和执行阶段创建一份 Impact。
        /// </summary>
        public CombatImpact(ActorId sourceActorId, ActorId targetActorId, AbilityId abilityId, SimulationTick createdTick, ActionExecutionStage stage)
        {
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            AbilityId = abilityId;
            CreatedTick = createdTick;
            Stage = stage;
            EffectId = EffectId.Empty;
            Tags = new GameplayTagContainer();
            Operations = new List<CombatImpactOperation>();
        }

        /// <summary>
        /// 获取来源单位 Id。
        /// </summary>
        public ActorId SourceActorId { get; }

        /// <summary>
        /// 获取目标单位 Id。
        /// </summary>
        public ActorId TargetActorId { get; }

        /// <summary>
        /// 获取关联技能 Id。
        /// </summary>
        public AbilityId AbilityId { get; }

        /// <summary>
        /// 获取该 Impact 创建时的 Tick。
        /// </summary>
        public SimulationTick CreatedTick { get; }

        /// <summary>
        /// 获取该 Impact 所属执行阶段。
        /// </summary>
        public ActionExecutionStage Stage { get; }

        /// <summary>
        /// 获取或设置关联效果 Id。
        /// </summary>
        public EffectId EffectId { get; set; }

        /// <summary>
        /// 获取该 Impact 携带的标签集合。
        /// </summary>
        public GameplayTagContainer Tags { get; }

        /// <summary>
        /// 获取该 Impact 包含的操作列表。
        /// </summary>
        public IList<CombatImpactOperation> Operations { get; }
    }

    /// <summary>
    /// 表示一次技能从请求到阻断、选目标、生成 impact 的完整尝试对象。
    /// </summary>
    public sealed class CombatActionAttempt
    {
        /// <summary>
        /// 使用请求、来源、技能和上下文创建一次完整尝试。
        /// </summary>
        public CombatActionAttempt(
            AbilityActivationRequest request,
            CombatActorState sourceActor,
            AbilityDefinition ability,
            AbilityExecutionContext executionContext,
            ActionExecutionStage stage)
        {
            Request = request;
            SourceActor = sourceActor;
            Ability = ability;
            ExecutionContext = executionContext;
            Stage = stage;
            Targets = new List<CombatActorState>();
            Blocks = new List<ActionBlock>();
            Impacts = new List<CombatImpact>();
        }

        /// <summary>
        /// 获取原始激活请求。
        /// </summary>
        public AbilityActivationRequest Request { get; }

        /// <summary>
        /// 获取本次尝试的施法者状态。
        /// </summary>
        public CombatActorState SourceActor { get; }

        /// <summary>
        /// 获取本次尝试对应的技能定义。
        /// </summary>
        public AbilityDefinition Ability { get; }

        /// <summary>
        /// 获取本次尝试共享的执行上下文。
        /// </summary>
        public AbilityExecutionContext ExecutionContext { get; }

        /// <summary>
        /// 获取本次尝试所属的执行阶段。
        /// </summary>
        public ActionExecutionStage Stage { get; }

        /// <summary>
        /// 获取或设置关联的运行中技能实例。
        /// </summary>
        public ActiveAbilityInstance AbilityInstance { get; set; }

        /// <summary>
        /// 获取本次尝试解析出的目标列表。
        /// </summary>
        public IList<CombatActorState> Targets { get; }

        /// <summary>
        /// 获取阻断记录列表。
        /// </summary>
        public IList<ActionBlock> Blocks { get; }

        /// <summary>
        /// 获取本次尝试生成的 Impact 列表。
        /// </summary>
        public IList<CombatImpact> Impacts { get; }

        /// <summary>
        /// 获取本次尝试当前是否已经被阻断。
        /// </summary>
        public bool IsBlocked => Blocks.Count > 0;

        /// <summary>
        /// 向本次尝试附加一个阻断原因。
        /// </summary>
        public void AddBlock(ActionBlockKind kind, string message)
        {
            Blocks.Add(new ActionBlock(kind, message));
        }

        /// <summary>
        /// 覆盖本次尝试当前解析到的目标集合。
        /// </summary>
        public void SetTargets(IEnumerable<CombatActorState> targets)
        {
            Targets.Clear();

            if (targets == null)
            {
                return;
            }

            foreach (var target in targets)
            {
                Targets.Add(target);
            }
        }
    }

    /// <summary>
    /// 在内建规则之外补充行动阻断逻辑的扩展点。
    /// </summary>
    public interface ICombatActionGate
    {
        void Evaluate(CombatActionAttempt attempt, CombatWorldState worldState);
    }

    /// <summary>
    /// 在 impact 真正 resolve 前改写其内容的扩展点。
    /// </summary>
    public interface ICombatImpactMutator
    {
        void Mutate(CombatActionAttempt attempt, CombatImpact impact, CombatWorldState worldState);
    }

    /// <summary>
    /// 处理自定义 impact operation 的扩展点。
    /// </summary>
    public interface ICombatImpactResolver
    {
        bool CanResolve(CombatImpactOperation operation);

        void Resolve(CombatActionAttempt attempt, CombatImpact impact, CombatImpactOperation operation, CombatRuntime runtime);
    }

    /// <summary>
    /// CombatRuntime 的外部可配置项集合。
    /// </summary>
    public sealed class CombatRuntimeOptions
    {
        /// <summary>
        /// 创建默认运行时配置，并初始化各类扩展模块列表。
        /// </summary>
        public CombatRuntimeOptions()
        {
            ActionGates = new List<ICombatActionGate>();
            ImpactMutators = new List<ICombatImpactMutator>();
            ImpactResolvers = new List<ICombatImpactResolver>();
            CustomTriggerActionRegistries = new List<Triggers.ICombatCustomTriggerActionRegistry>();
            RuleModules = new List<ICombatRuleModule>();
        }

        /// <summary>
        /// 获取或设置战斗模式规则实现。
        /// </summary>
        public ICombatModeRules ModeRules { get; set; }

        /// <summary>
        /// 获取或设置 Actor 关系解析器。
        /// </summary>
        public ICombatActorRelationResolver RelationResolver { get; set; }

        /// <summary>
        /// 获取或设置目标解析器。
        /// </summary>
        public ITargetingResolver TargetingResolver { get; set; }

        /// <summary>
        /// 获取或设置事件输出器。
        /// </summary>
        public ICombatEventSink EventSink { get; set; }

        /// <summary>
        /// 获取行动阻断扩展列表。
        /// </summary>
        public IList<ICombatActionGate> ActionGates { get; }

        /// <summary>
        /// 获取 Impact 改写扩展列表。
        /// </summary>
        public IList<ICombatImpactMutator> ImpactMutators { get; }

        /// <summary>
        /// 获取自定义 Impact 解析扩展列表。
        /// </summary>
        public IList<ICombatImpactResolver> ImpactResolvers { get; }

        /// <summary>
        /// 获取自定义 TriggerAction 注册表列表。
        /// 业务层可以在 Runtime 构造前补充额外注册表，和全局自动注册表一起参与派发。
        /// </summary>
        public IList<Triggers.ICombatCustomTriggerActionRegistry> CustomTriggerActionRegistries { get; }

        /// <summary>
        /// 获取规则模块列表。
        /// </summary>
        public IList<ICombatRuleModule> RuleModules { get; }
    }
}
