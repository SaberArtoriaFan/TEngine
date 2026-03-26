# 从 NewBattle 迁移到 Saber.GAS

## 1. 应该保留什么

旧 `NewBattle` 里最值得保留的不是回合主循环，而是这些设计思想：

- 动作定义与运行时上下文分离
- 阻断链
- 影响包
- Buff 模板 / 实例分离
- 目标侧 impact handler 思想

这些现在已经在 `Saber.GAS` 里对应成：

- `AbilityDefinition`
- `CombatActionAttempt`
- `ActionBlock`
- `CombatImpact`
- `EffectDefinition`
- `ActiveEffect`

## 2. 不应该继续带过来的东西

下面这些不应再进入新内核：

- `BattleMainSystem`
- `TurnBehavior`
- `BattleTeamQueue`
- `RoundCount` 驱动 CD
- `ActionResourceModule` 里的固定 AP/MP 模型
- `DependencyInjectionMgr`
- `ICharacter` 上混杂的 View / Animation / UI 接口
- 协程驱动的核心执行模型

这些不是“代码写得不好”，而是职责属于旧项目的具体玩法层。

## 3. 迁移映射表

### 3.1 动作与能力

- `ActionObject`
  -> `AbilityDefinition`
- `ActionRuntimeSituation`
  -> `CombatActionAttempt`
- `BlockAttempt`
  -> `ActionBlock`
- `AttemptImpact`
  -> `CombatImpact`

### 3.2 Buff 与效果

- `BuffProperties`
  -> `EffectDefinition`
- `BuffInstance`
  -> `ActiveEffect`
- `BuffContainer`
  -> `CombatActorState.ActiveEffects`

### 3.3 角色与数值

- `CharacterSheetData`
  -> `AttributeSet + ResourceSet`
- `BattleInfoModule`
  -> `CombatActorState`

## 4. 迁移建议顺序

推荐顺序是：

1. 先迁数据概念
2. 再迁逻辑解释层
3. 最后迁 authoring 和表现接入

### 4.1 第一阶段

先把旧配置映射成新定义：

- 技能 -> `AbilityDefinition`
- Buff -> `EffectDefinition`
- Sheet -> `AttributeSet / ResourceSet`

### 4.2 第二阶段

再把旧逻辑入口映射到新运行时：

- 旧技能点击 / AI 触发
  -> `AbilityActivationRequest`
- 旧 buff 应用
  -> `CombatImpactOperationType.ApplyEffect`

### 4.3 第三阶段

最后再补：

- ScriptableObject authoring
- Unity 展示桥
- 旧资产自动迁移器

## 5. 迁移时的红线

- 不要把 `RoundCount` 再写回核心
- 不要把 `float` 数值带回来
- 不要把协程塞回核心执行链
- 不要让核心层直接调镜头、UI、音效
- 不要让旧 `Manager.Instance` 重新进入这套逻辑层
