# Saber.GAS 框架说明与实战手册

本文是 `Saber.GAS` 当前版本的一体化说明，覆盖架构、运行链路、Unity 表现层接入、投射物调参、Inspector 观测与排障。

## 1. 目标与定位

`Saber.GAS` 是一个可组合、可扩展、可回归验证的战斗框架，核心目标：

- 扩展优先通过注册表接入，不修改 Runtime 核心分发代码。
- 组合优于继承：能力、效果、触发、规则模块按契约拼装。
- 规则计算保持 deterministic，表现层通过事件和状态投影解耦。

## 2. 核心分层

1. Authoring 层（ScriptableObject）
   - Ability / Effect / Trigger / ActorTemplate 等资产定义。
2. Build 层
   - Authoring 数据编译为 Runtime definition。
3. Runtime 层
   - `CombatRuntime` 负责生命周期与编排。
   - Activation / Impact / EffectLifecycle / TriggerBridge 分服务执行。
4. Extension 层
   - TriggerAction Descriptor Registry
   - ImpactOperation Handler Registry
   - CustomTriggerAction Registry
5. Presentation 层（Unity Mono）
   - 负责模型、动画、UI、Inspector 调试展示，不承载核心规则。

## 3. 关键对象

- `CombatWorldState`：世界状态容器（Actor、Ability、Projectile、Tick）。
- `CombatActorState`：运行时 Actor，包含 `Attributes`、`Resources`、标签、技能实例。
- `AbilityDefinition`：技能定义，包含 Targeting、Effects、Triggers。
- `EffectDefinition`：效果定义，可包含即时和周期影响、ImpactOperations。
- `TriggerDefinition`：触发定义，决定触发时机和动作。

## 4. 运行时链路（从一次施法到伤害落地）

1. 外部调用 `runtime.TryActivate(request)`。
2. ActivationService 校验施法者、目标、标签、资源、冷却。
3. 构建 `CombatActionAttempt`，生成初始 `Impact` 列表。
4. ImpactService 依次执行：
   - Mutator 改写
   - Operation Handler Registry 分发
   - 默认处理器执行（ResourceDelta / ApplyEffect / Remove... / Cue）
5. TriggerBridge 在关键时机组装 `CombatTriggerContext`。
6. TriggerProcessor 通过 TriggerAction Descriptor 执行动作。
7. 事件输出走 `CombatDomainEventBus`：
   - Deterministic 通道：规则链
   - Observer 通道：UI/日志/表现订阅

## 5. Unity 表现层配置（Mono 侧）

推荐把表现逻辑挂在场景对象上，核心规则仍由 Runtime 驱动。

### 5.1 即时伤害示例（双 Actor 互殴）

示例场景：`Assets/GAS/SaberGAS/Examples/Scenes/GAS_QuickStart_Duel.unity`

关键组件：

- `GasQuickStartDuelRunner`
  - 自动创建 `ActorA` / `ActorB`
  - 构建即时伤害 Ability
  - 按 Tick 让双方互相施法
- 可选：`GasActorRuntimeDisplay`
  - 在 Inspector 查看 Actor 运行时状态

核心参数：

- `_secondsPerTick`：逻辑 Tick 时间步长。
- `_ticksBetweenAttacks`：每次攻击间隔 Tick。
- `_startingHealth`：初始生命。
- `_instantDamage`：每次即时伤害数值。
- `_attackRange`：技能生效距离。

### 5.2 投射物示例（互射）

示例场景：`Assets/GAS/SaberGAS/Examples/Scenes/GAS_QuickStart_ProjectileDuel.unity`

关键组件：

- `GasQuickStartProjectileDuelRunner`
  - 创建发射技能（`SpawnProjectile` operation）
  - 驱动投射物飞行、命中与表现事件

投射物关键参数：

- `_trackingMode`
  - `TrackActor`：追踪目标 Actor。
  - `FixedPoint`：按目标点直线飞行。
- `_speedPerTick`：每 Tick 移动速度。
- `_hitRadius`：命中半径。
- `_lifetimeTicks`：投射物最大存活 Tick。
- `_aimAngleOffsetDegrees`：瞄准偏移角（用于散射/偏转）。

调参建议：

1. 先固定 `_secondsPerTick`，再调 `_speedPerTick`，保证手感稳定。
2. 远程技能先用较大 `_hitRadius` 验证链路，之后再收紧命中窗口。
3. `_lifetimeTicks` 过低会导致“看似穿过目标但提前过期”。

## 6. 投射物模型与动画接入

`GasQuickStartProjectileDuelRunner` 会在视图对象上读取 `Animator` 并发送参数：

- Trigger：`Spawn`
- Trigger：`Hit`
- Trigger：`Expire`
- Float：`Speed`

### 接入步骤

1. 给 `_projectilePrefab` 指定你的投射物模型预制体。
2. 在预制体上挂 `Animator`，并创建上述参数。
3. 在 Animator Controller 中配置三个触发过渡：
   - Idle -> Spawn
   - Fly -> Hit
   - Fly -> Expire
4. 可在飞行动画中读取 `Speed` 做速率驱动。
5. 若未配置预制体，可勾选 `_usePrimitiveWhenPrefabMissing` 用球体占位。

## 7. Inspector 运行时观测

组件：`GasActorRuntimeDisplay`（挂在 Actor Anchor 上）

支持分区：

- Overview
- AttributeSet
- Ability
- Effect
- Trigger
- ResourceSet

使用要点：

1. 在 Inspector 的 `Section Switch` 切换分区。
2. `Jump To Definition` 可从运行时 ID 跳到 Authoring 资产或示例代码定义。
3. `Refresh Now` 手动刷新；Play Mode 可开启自动刷新。

## 8. 事件广播与观察者

- Runtime 内部使用 `CombatDomainEventBus`。
- UI/日志层建议订阅 Observer 通道，不直接侵入核心逻辑。
- 示例：`CombatDomainEventObserverExamples` 和 `CombatDomainEventRecorder`。

## 9. 扩展开发原则（商业化）

1. 新 TriggerAction：走 Descriptor Registry。
2. 新 ImpactOperation：走 Handler Registry。
3. 项目专属动作：走 CustomTriggerAction Registry。
4. Editor 扩展：走模块绘制注册表，不写集中式 switch。
5. 扩展应可独立程序集落地，避免反向依赖 Core。

详细模板和步骤见：`SaberGAS_Extension_SDK.md`。

## 10. 验收与排障清单

### 10.1 最小验收

1. `dotnet build UnityProject/UnityProject.sln` 通过。
2. EditMode 测试通过（含 RTS 扩展示例测试）。
3. 两个 QuickStart 场景可进入 Play 并持续推进 Tick。
4. Inspector 可切换分区并显示 Attribute/Ability/Effect/Trigger/ResourceSet。

### 10.2 常见问题

- 现象：`Ability Id xxx 没有匹配到 Authoring 资产`
  - 原因：运行时定义来自代码构造，未生成同名资产。
  - 处理：Inspector 会回退到代码定位；如需资产跳转，请补对应 Authoring 资产。
- 现象：场景保存报临时文件移动拒绝访问
  - 处理：关闭占用文件的外部程序，确认 Unity 目录写权限，必要时另存为新场景再替换。
- 现象：投射物不显示动画
  - 处理：确认预制体存在 `Animator`，且参数名与 Runner 触发一致。

## 11. 推荐工作流

1. 先在 QuickStart 场景验证核心链路。
2. 再加表现层（模型/动画/UI）。
3. 最后做扩展（新 Action/Operation）并补对应测试。
4. 保持“扩展不改核心”，把复杂业务放到扩展程序集。
