# RTS 八叉树空间检索体系（FixedPoint 版）

## 目标

本模块基于 `FixedPoint` 程序集实现了一个轻量 RTS 空间检索体系，聚焦以下能力：

1. 范围 AOE 单位选取（`Area` 目标类型）
2. 某个 Actor 最近单位检索（排除自身）
3. GAS 目标解析器自动接入（`RtsCombatRuntimeModulePack` 默认启用）

当前阶段不包含刚体积分、碰撞响应、连续碰撞检测等复杂物理逻辑。

## 代码结构

- `RtsSpatialQueryOptions.cs`
  - 八叉树构建参数（节点容量、最大深度、最小节点尺寸、边界冗余）
- `RtsOctreeSpatialIndex.cs`
  - 纯 FixedPoint 八叉树索引
  - 支持球形范围查询、最近点查询
- `RtsOctreeSpatialQueryService.cs`
  - `IRtsSpatialQueryService` 实现
  - 按 `CombatWorldState + CurrentTick` 自动重建缓存索引
- `RtsSpatialTargetingResolver.cs`
  - GAS 目标解析接入层
  - `Area`：按范围取候选并按关系/生死/标签过滤
  - `Actor` 且无显式目标时：自动选最近匹配单位

## 装配方式

`RtsCombatRuntimeModulePack` 默认会注入：

1. `SpatialQueryService = new RtsOctreeSpatialQueryService()`
2. 当 `CombatRuntimeOptions.TargetingResolver == null` 时，创建 `RtsSpatialTargetingResolver`

如果你在外部手动设置了 `TargetingResolver`，则会保留你的配置。

## 行为说明

### Area（范围 AOE）

1. 读取 `AbilityTargetData.TargetPoint` 作为圆心
2. 通过 `AreaRadiusResolver` 获取半径（默认取 `ability.Targeting.MaxRange`）
3. 用八叉树查候选
4. 逐个按 `AllowedFlags + RequiredTargetTags + BlockedTargetTags` 过滤

### Actor（最近单位自动选取）

当 `AbilityTargetKind.Actor` 且 `TargetActorIds` 为空：

1. 在 `TargetPoint`（若有）或施法者位置附近搜索
2. 若 `MaxRange > 0`，先做范围候选，再选最近
3. 若 `MaxRange <= 0`，在全世界遍历（仍使用统一过滤规则）

## 可扩展点

1. 自定义 AOE 半径规则：
   - 给 `RtsSpatialTargetingResolver.AreaRadiusResolver` 赋值
2. 自定义关系解析：
   - 传入自定义 `ICombatActorRelationResolver`
3. 手动控制重建时机：
   - 在同 Tick 大量改位移后调用 `RtsOctreeSpatialQueryService.Rebuild(worldState)`

## Ability 示例

`RtsCustomTriggerActionExamples` 新增了“受伤反给最远敌人”的示例构造器：

1. `CreateReflectDamageToFarthestEnemyTrigger(...)`
2. `CreateReflectDamageToFarthestEnemyPassiveAbility(...)`

实现动作是 `RtsReflectDamageToFarthestEnemyTriggerAction`（CustomActionId=`1002`）：

1. 触发窗口：`BeforeImpactResolve`
2. 仅在拥有者是受击目标且存在负向资源变化时生效
3. 按 `ReflectionRatio` 把伤害反给最远敌方单位
4. 可通过 `MaxDistance` 限制搜索半径

`RtsCombatRuntimeModulePack` 会在检测到全局注册表里缺少 `1002` 时，自动注入
`RtsReflectDamageCustomTriggerActionRegistry` 作为回退注册，保证该动作可执行。

## 当前约束

1. 这是“查询物理”层，不是“动力学物理”层
2. `AbilityTargetingDefinition` 当前只有 `MaxRange`，未拆分“施法距离”和“AOE 半径”
3. 最近目标在极端大规模场景下可继续优化（例如子节点按边界距离排序早停）

## 自测覆盖

Editor 测试程序集：`Saber.GAS.RTS.Tests.Editor`

当前包含 6 个用例：

1. `QueryActorsInRange_ShouldReturnActorsInsideRadius`
2. `TryFindNearestActor_ShouldExcludeSourceActor`
3. `AreaTargeting_ShouldFilterByTargetFlagsAndTags`
4. `ActorTargetingWithoutInput_ShouldResolveNearestMatchingActor`
5. `ReflectDamage_ShouldHitFarthestEnemyWhenDamaged`
6. `ReflectDamage_WithMaxDistance_ShouldIgnoreOutOfRangeEnemies`

最近一次结果：6/6 通过（EditMode）。
