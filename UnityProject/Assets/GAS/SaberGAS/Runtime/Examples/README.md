# Saber.GAS Examples

这个目录现在主要放三类示例：

- 观察者式 Trigger 示例
- 标准战斗语义层示例
- 自动扩展注册示例

## 1. 观察者式 Trigger

代码位置：

- `ObserverTriggerExamples.CreateGuardianShareDamageExample(...)`
- `ObserverTriggerExamples.CreateBloodPactRecoveryExample(...)`

这两组示例主要展示第三方观察者 Trigger 的写法，例如：

- 队友受击时，自己分担一部分伤害
- 范围内敌人受伤时，自己恢复生命

重点在 Trigger 的观察与筛选配置：

- `ObserveTarget`
- `TargetRelationFilter`
- `InstigatorRelationFilter`
- `ObserverMaxDistance`

关系筛选现在已经是可组合的 `CombatActorRelationFlags`，而不是单值枚举。  
真正的敌我友中立定义由 `CombatRuntimeOptions.RelationResolver` 决定，不再由 Core 直接比较 `TeamId`。

## 2. 标准战斗语义层

代码位置：

- `CombatSemanticExamples.CreateBurstBarrierAndCleanseExample(...)`
- `CombatSemanticExamples.CreateControlImmunityExample(...)`
- `CombatSemanticExamples.CreateDrainAndPeriodicDamageExample(...)`

这些示例展示的是：

- `CombatSemanticCatalog`
  统一管理标准语义标签
- `CombatSemanticBuilder`
  构造伤害、治疗、护盾、控制、驱散、免疫等常规机制
- `CombatSemanticModule`
  在运行时解释这些标准语义

现在 `CombatSemanticModule` 已经走程序集自动注册。

因此示例中的推荐接线已经变成：

```csharp
var bundle = CombatSemanticExamples.CreateBurstBarrierAndCleanseExample(healthResourceId);
var runtime = new CombatRuntime(worldState, bundle.RuntimeOptions);
var cloneProvider = bundle.CloneProvider;
```

也就是说：

- `CombatRuntimeOptions` 不需要再手工 `RuleModules.Add(module)`
- `FastClonerDeepCloneProvider` 不需要再手工传 `ICombatExtensionCloneHandler[]`

只要对应程序集已经被加载，自动注册的规则模块和克隆处理器就会自动参与。

## 3. 自动扩展注册

代码位置：

- `Saber.GAS.RTS/Runtime/Examples/RtsAutoRegisteredExtensions.cs`
- `Saber.GAS.RTS/Runtime/Examples/RtsAutoExtensionExamples.cs`

这组示例演示跨程序集自动注册的两种运行时扩展：

- `RtsCommandLockedActionGate`
  当技能带有 `Ability.RTS.Example.Order`，且施法者带有 `State.RTS.Example.CommandLocked` 时，阻断该技能
- `RtsSiegeStructureImpactMutator`
  当攻城单位攻击建筑单位时，把负向生命资源变化翻倍

对应的示例工厂是：

- `RtsAutoExtensionExamples.CreateOrderLockAndSiegeExample(...)`

这个示例的重点是：

- 扩展实现写在 `Saber.GAS.RTS`
- 不需要改 `Saber.GAS` Core
- 不需要手工往 `CombatRuntimeOptions.ActionGates` 或 `ImpactMutators` 里塞实例
- 由源码生成器自动收集实现类，并在程序集加载时注册

## 4. 如何看这些示例

如果你想看：

- Trigger 的第三方观察与收集：先看 `ObserverTriggerExamples`
- 常规 MOBA / RPG 语义效果：先看 `CombatSemanticExamples`
- 开发者自定义扩展如何不改 Core 接进去：先看 `RtsAutoRegisteredExtensions` 和 `RtsAutoExtensionExamples`

如果后面还要继续扩：

- 能抽象成标准机制的，优先继续扩 `Semantics`
- 项目专属逻辑，优先走自动注册扩展
- 极少数特殊 Trigger 行为，再走 `CustomTriggerAction`
