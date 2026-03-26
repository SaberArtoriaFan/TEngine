# 10_Trigger系统与方案C实现

## 1. 这份文档的目的

这份文档专门说明 `Saber.GAS` 当前已经落地的方案 C：

- `EffectDefinition` 负责状态与效果定义
- `TriggerDefinition` 负责触发条件与触发动作定义
- `AbilityDefinition` 负责主动或被动派生出的标准行为

它不是旧 `NewBattle` 里那种“BuffLogic 监听事件然后直接改状态”的写法，而是把反应式逻辑统一收敛进 `CombatRuntime -> CombatTriggerProcessor` 这条确定性结算链。

这意味着现在的 Trigger 系统已经可以作为你后续自定义 GAS 的正式一层，而不只是临时扩展点。

## 2. 当前代码结构

当前方案 C 相关代码主要分成下面几层：

- `Runtime/Triggers/TriggerDefinitions.cs`
  负责 Trigger 静态定义，包括事件类型、时机、关系过滤、阈值条件、动作定义。
- `Runtime/Triggers/CombatTriggers.cs`
  负责 Trigger 的运行时上下文 `CombatTriggerContext` 和运行时实例 `ActiveTriggerInstance`。
- `Runtime/Triggers/CombatTriggerProcessor.cs`
  负责候选 Trigger 收集、排序、过滤、执行。
- `Runtime/Runtime/CombatRuntime.cs`
  负责在合适的结算窗口抛出 Trigger 上下文，并提供 Trigger 动作最终落地的统一入口。

从职责上可以把它理解为：

- `Definition`：描述“什么条件下触发”
- `Instance`：记录“现在还能不能触发”
- `Context`：描述“这次到底发生了什么”
- `Processor`：决定“哪些 Trigger 命中、按什么顺序执行”
- `Runtime`：真正把动作转成 Ability / Effect / Resource / Tag / Impact 的变化

## 3. Trigger 到底解决了什么问题

旧框架里的常见写法是：

1. 给单位挂一个 Buff
2. BuffLogic 监听某个事件
3. 事件回调里直接派生逻辑

这种方式写单个玩法很快，但会有几个问题：

- 结算顺序分散，难以推理
- 多个 Buff 同时监听同类事件时，优先级不稳定
- 网络同步、快照、回滚时很难保证一致性
- “状态”和“行为”容易写进同一个 BuffLogic，后续越来越难维护

Trigger 系统的目的，就是把这些“反应式行为”收束为统一规则：

- 状态仍然由 `Effect` 和 `Tag` 表达
- 行为改由 `Trigger` 表达
- 真正动作继续回流到 `Ability / Effect / Impact / Resource` 这些标准战斗通道

## 4. 当前已经支持的触发来源

当前版本支持 4 类 Trigger 来源：

- `Effect`
  最适合表达 Buff / Debuff 带来的反应式行为。
- `Ability`
  最适合表达技能施法期间、技能命中后、技能结束后附带的后续逻辑。
- `Actor`
  最适合表达角色固有被动、装备被动、天赋、职业特性。
- `Global`
  最适合表达地图规则、模式规则、环境规则。

你可以把它们理解成：

- `Effect Trigger`：这层状态存在时才触发
- `Ability Trigger`：这个技能实例存在时才触发
- `Actor Trigger`：这个单位天生或动态拥有的被动
- `Global Trigger`：世界规则

## 5. 当前已经支持的触发事件

当前 `CombatTriggerEventKind` 已经覆盖了一批通用窗口：

- `BeforeAttempt`
- `AfterAttemptSucceeded`
- `AfterAttemptBlocked`
- `BeforeImpactResolve`
- `AfterImpactResolved`
- `BeforeEffectApplied`
- `AfterEffectApplied`
- `AfterEffectExpired`
- `OnAbilityStarted`
- `OnAbilityCompleted`
- `OnAbilityCancelled`
- `OnResourceChanged`
- `OnResourceThresholdCrossed`
- `OnTick`
- `OnDeath`
- `OnKill`
- `OnAllyDeath`
- `OnEnterArea`
- `OnLeaveArea`

当前真正已经接入 `CombatRuntime` 主流程的，主要是：

- 行为尝试成功 / 失败
- Impact 结算前 / 后
- Effect 施加前 / 后 / 移除后
- Ability 开始 / 完成 / 取消
- 资源变化
- Tick
- 死亡 / 击杀 / 队友死亡

其中 `OnEnterArea`、`OnLeaveArea` 这类空间事件目前还是预留事件位，后续要等 Area / Query 层接进来之后，才会真正发挥作用。

## 6. 当前已经支持的过滤条件

一个 TriggerDefinition 当前可以按下面这些维度过滤：

- 事件类型
- 时机
- Owner 自身 Tag
- Instigator Tag
- Target Tag
- Incoming Ability Tag
- Incoming Effect Tag
- Incoming Impact Tag
- Actor 关系
  - Self
  - Ally
  - Enemy
  - Other
- 资源阈值跨越
- 来源是否存活
- 目标是否存活
- 冷却 Tick
- 每 Tick 最大触发次数
- 全局最大触发次数
- 初始充能数

这套过滤结构已经足够表达绝大多数“反应式被动”。

## 7. 当前已经支持的触发动作

当前 `TriggerActionKind` 已经支持这些标准动作：

- `ActivateAbility`
- `ApplyEffect`
- `RemoveEffectById`
- `RemoveEffectsByTag`
- `AddImpactOperation`
- `ModifyImpactMagnitude`
- `AddResource`
- `RemoveResource`
- `AddTag`
- `RemoveTag`
- `CancelAbility`
- `CleanseByTag`
- `EmitCue`

其中最重要的几类是：

- `ActivateAbility`
  适合“受击反击”“死亡后放技能”“暴击后追加攻击”。
- `ApplyEffect`
  适合“低血自动开盾”“队友死亡后狂暴”“进入草丛后隐身”。
- `ModifyImpactMagnitude`
  适合“护盾吸收”“受击减伤”“暴击增伤”“脆弱易伤”。
- `RemoveEffectsByTag`
  适合“净化”“驱散”“解除某类控制”。

## 8. CombatRuntime 里是怎么接入的

当前 `CombatRuntime` 会在多个结算窗口主动构造 `CombatTriggerContext`，再统一调用：

```csharp
RaiseTriggerEvent(context);
```

Trigger 系统不会自己订阅 Unity 事件，也不会分散到外层去监听，而是完全依赖 Runtime 在这些稳定节点投递上下文。

当前主要接入点包括：

- `TryActivate`
  在尝试成功后触发 `AfterAttemptSucceeded`
- `FailAttempt`
  在尝试失败后触发 `AfterAttemptBlocked`
- `ResolveAttemptImpacts`
  在每个 Impact 结算前后触发 `BeforeImpactResolve` / `AfterImpactResolved`
- `ApplyEffectSpec`
  在效果施加前后触发 `BeforeEffectApplied` / `AfterEffectApplied`
- `RemoveEffect`
  在效果移除后触发 `AfterEffectExpired`
- `StartAbilityLifecycle`
  在技能实例开始时触发 `OnAbilityStarted`
- `CompleteAbilityInstance`
  在技能实例完成时触发 `OnAbilityCompleted`
- `CancelAbilityInstance`
  在技能实例取消时触发 `OnAbilityCancelled`
- `ModifyResourceDirect`
  在资源变化时触发 `OnResourceChanged`
- `SetActorAliveState`
  在死亡相关状态切换时触发 `OnDeath` / `OnKill` / `OnAllyDeath`
- `Tick`
  每个 Tick 触发 `OnTick`

这条设计很关键，因为它保证了：

- Trigger 一定在确定的战斗窗口执行
- 触发链可以参与快照、回滚、重放
- 后续网络同步时更容易做服务端权威和客户端预测

## 9. CombatTriggerProcessor 是怎么工作的

`CombatTriggerProcessor` 当前的执行流程可以概括为：

1. 收集候选 Trigger
2. 排序
3. 过滤
4. 执行动作
5. 必要时消费来源 Effect 或移除来源 Effect

### 9.1 收集候选

当前会收集：

- OwnerActor 自身 `ActiveTriggers`
- OwnerActor 身上所有 `ActiveEffect.ActiveTriggers`
- OwnerActor 身上所有 `ActiveAbilityInstance.ActiveTriggers`
- `WorldState.GlobalTriggers`

并且还补了一个细节：

- 如果这次事件的来源 Effect 或 AbilityInstance 已经从 Owner 列表里移除了，但上下文仍然持有来源引用，Processor 仍然会把它作为候选加入

这保证了“某个 Effect 正在过期时自己最后触发一次”这类情况不会丢。

### 9.2 排序

当前排序规则是：

1. `Priority` 降序
2. `OwnerActorId`
3. `SourceKind`
4. `TriggerId`

这样做的目的是让 Trigger 触发顺序稳定，避免同一局数据在不同机器上因为遍历顺序不同而分叉。

### 9.3 过滤

Processor 当前会依次判断：

- 事件是否匹配
- 时机是否匹配
- Owner 状态是否匹配
- Instigator / Target Tag 是否匹配
- Incoming Ability / Effect / Impact Tag 是否匹配
- Actor 关系是否匹配
- 生存状态是否匹配
- 冷却 / 次数 / 充能是否允许

### 9.4 执行动作

动作执行不会直接散落在 Trigger 层里，而是尽量回调到 `CombatRuntime`：

- 触发 Ability 走 `ExecuteTriggeredAbility`
- 施加 Effect 走 `ApplyTriggerEffect`
- 改资源走 `ModifyResourceDirect`
- 改 Tag 走 `AddRuntimeTagDirect / RemoveRuntimeTagDirect`
- 取消技能走 `CancelAbilityByIdDirect`

这意味着 Trigger 不是一套平行系统，而是 Runtime 主结算链的上层调度器。

## 10. 现在怎么表达旧框架里的“受击反击”

最推荐的表达方式已经不是：

- BuffLogic 监听“被攻击”事件

而是下面这一组数据：

### 10.1 Effect

定义一个 Buff：

- `Effect.CounterAttackBuff`
- 给持有者加 `State.CounterAttack.Ready`

### 10.2 Trigger

定义一个 Trigger：

- `EventKind = AfterImpactResolved`
- `Timing = ImmediatePostResolve`
- `SourceKind = Effect`
- `RequiredOwnerTags` 包含 `State.CounterAttack.Ready`
- `RelationFilter = Enemy`
- `Action.Kind = ActivateAbility`
- `Action.TriggeredAbilityId = Ability.CounterSlash`
- `Action.SourceActor = Owner`
- `Action.TargetActor = Instigator`

### 10.3 Ability

定义一个标准能力：

- `Ability.CounterSlash`
- 正常走目标校验、冷却、结算、效果链

这样一来：

- Buff 只负责“你现在有反击姿态”
- Trigger 负责“什么时候把反击姿态转成一次行为”
- Ability 负责“反击时到底做什么”

## 11. 现在怎么表达文档 09 里列的那些典型机制

### 11.1 受击反弹

- Effect 给持有者挂 `State.Thorns`
- Trigger 监听 `AfterImpactResolved`
- TriggerAction 用 `AddImpactOperation` 或 `ActivateAbility` 把伤害返给 Instigator

### 11.2 暴击后追加斩杀

- Ability 或 Actor 挂一个 Trigger
- Trigger 监听 `AfterImpactResolved`
- 通过 Impact Tag 或自定义上下文标记确认“这次是暴击”
- Action 触发 `Ability.ExecuteFollowUp`

### 11.3 低血量自动开盾

- Trigger 监听 `OnResourceThresholdCrossed`
- 阈值设为生命跌破某一数值
- Action 施加 `Effect.AutoShield`

### 11.4 被控制后净化

- Trigger 监听 `AfterEffectApplied`
- 判断 Incoming Effect Tag 是否带控制标签
- Action 执行 `RemoveEffectsByTag`

### 11.5 队友死亡后狂暴

- Actor 或被动 Ability 挂 Trigger
- Trigger 监听 `OnAllyDeath`
- Action 施加 `Effect.Berserk`

### 11.6 进入草丛后隐身

这个能力现在还差空间查询层，但 Trigger 形态已经预留好了：

- Trigger 监听 `OnEnterArea`
- Area 或 Terrain 层把“进入草丛”转成标准 TriggerContext
- Action 施加 `Effect.Stealth`

## 12. 这一层现在已经做到什么程度

从“框架能力”来看，当前 Trigger 系统已经不是概念验证，而是能正式承载大部分被动和反应式机制的一层。

现在已经具备：

- 静态定义层
- 运行时实例层
- 冷却 / 次数 / 充能
- 多来源 Trigger
- 多窗口触发
- 统一排序
- 统一动作落地
- 对象池支持
- 深拷贝支持
- 与 Runtime 结算链整合

也就是说，方案 C 现在已经真正进入“可用”阶段，而不是只停留在文档设计。

## 13. 目前还没有补完的地方

虽然 Trigger 主体已经落地，但想完全支撑 DOTA / MOBA 那种复杂机制，还需要继续补下面这些底座：

- 更正式的 Damage / Heal / Shield 语义层
- Area / Aura / Projectile / Thinker 实体层
- 世界查询与地形查询层
- 更细粒度的状态免疫 / 驱散 / 抗性体系
- 更丰富的 TriggerContext 扩展字段
  - 例如暴击标记
  - 命中结果类型
  - 攻击类型
  - 伤害类型

所以现在的结论可以理解为：

- Trigger 这层已经补上了
- 但完整 MOBA 战斗语义还需要继续往上叠

## 14. 验收建议

如果你回来要快速验收 Trigger 系统，建议按下面顺序看：

1. 先看 `Runtime/Triggers/TriggerDefinitions.cs`
   确认数据结构和枚举是否覆盖你想要的表达能力。
2. 再看 `Runtime/Triggers/CombatTriggerProcessor.cs`
   确认收集、排序、过滤、动作执行逻辑是否集中且稳定。
3. 再看 `Runtime/Runtime/CombatRuntime.cs`
   确认关键结算窗口是否都补了 TriggerContext。
4. 最后再看 `Runtime/Serialization/FastClonerDeepCloneProvider.cs`
   确认快照和深拷贝是否覆盖了 Trigger 的运行时状态。

如果这四处都能成立，那么方案 C 这一层就已经是正式战斗能力，而不是文档上的方向描述。
