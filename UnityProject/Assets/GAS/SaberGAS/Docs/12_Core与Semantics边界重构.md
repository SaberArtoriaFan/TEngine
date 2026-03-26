# Core 与 Semantics 边界重构

## 1. 这次重构解决了什么问题

这次调整之前，`CombatRuntime` 里直接引用了 `CombatSemanticRules`、`ShieldDefinition`、`ActiveShieldState` 这类语义层对象。  
这会带来两个问题：

- `Core` 不是纯底层，新增或修改语义机制时，容易反向改到 Runtime 核心代码。
- 语义层既不是独立模块，也不是清晰的中间层，开发者很难知道该在哪里扩功能。

这次重构后，职责被重新划开：

- `Runtime/Core`
  只保留通用对象模型、战斗主流程、Trigger、快照、对象池、深拷贝骨架。
- `Runtime/Semantics`
  作为核心层和业务层之间的标准战斗语义中间层，负责：
  - 标签目录
  - 标准机制定义构造
  - 运行时解释执行
  - 扩展状态克隆

## 2. Core 现在只依赖什么

`Core` 现在只认识这些抽象：

- `ICombatRuleModule`
  运行时规则模块接口。
- `ICombatEffectExtensionDefinition`
  效果定义上的扩展数据接口。
- `ICombatActiveEffectExtensionState`
  持续效果实例上的扩展运行时状态接口。
- `ICombatExtensionCloneHandler`
  深拷贝时克隆扩展定义和扩展状态的接口。

也就是说，`Core` 不再知道“伤害语义”“护盾语义”“控制免疫”这些具体概念，只负责在合适的生命周期向模块发问：

- 这次能力尝试要不要阻断
- 这个效果能不能落到目标身上
- 这个效果有没有模块自己的运行时负载
- 这次资源变化要不要被语义层改写
- 这个持续效果开始、刷新、移除时，要不要做额外状态管理

## 3. Semantics 现在提供了什么

标准语义层被拆成三个对象：

### `CombatSemanticCatalog`

负责统一管理标准语义标签。

主要分组：

- `Ability`
  - `Cast`
  - `Attack`
- `Effect`
  - `Damage`
  - `Heal`
  - `Shield`
  - `Control`
  - `Dispel`
  - `Immunity`
  - `Periodic`
  - `PureDamage`
  - `ReflectDamage`
- `Impact`
  - `Damage`
  - `Heal`
  - `ShieldAbsorbed`
  - `PreventedByImmunity`
- `State`
  - `Control.*`
  - `Immunity.*`
  - `Dispellable.*`
  - `Shield.Active`

### `CombatSemanticBuilder`

负责构造标准机制定义。

当前已覆盖：

- 伤害
- 周期伤害
- 治疗
- 护盾
- 控制
- 免疫
- 驱散

Builder 会把这些常见机制翻译成：

- 标准效果标签
- 标准状态标签
- `EffectDefinition` 上的扩展数据

### `CombatSemanticModule`

负责把语义定义接入运行时。

当前承担的职责包括：

- 能力尝试阻断
  - 眩晕、压制、恐惧、魅惑、嘲讽
  - 沉默阻断施法型能力
  - 缴械阻断普攻型能力
- 效果落地阻断
  - 控制免疫
  - 驱散免疫
- 资源变化改写
  - 伤害免疫
  - 护盾吸收
  - 护盾耗尽后移除源效果
- 持续效果扩展状态管理
  - 初始化护盾层
  - 刷新护盾层
  - 回收护盾层
- 快照深拷贝扩展克隆

## 4. Runtime 是如何接模块的

现在的接线方式统一走 `CombatRuntimeOptions.RuleModules`：

```csharp
var catalog = new CombatSemanticCatalog();
var semanticModule = new CombatSemanticModule(catalog);

var runtimeOptions = new CombatRuntimeOptions();
runtimeOptions.RuleModules.Add(semanticModule);
```

`CombatRuntime` 在这些阶段会主动查询模块：

1. `EvaluateBuiltInBlocks(...)` 之后  
   调 `EvaluateAbilityAttempt(...)`
2. `CanApplyEffectToTarget(...)`  
   调 `TryBlockEffectApplication(...)`
3. 生成和处理效果时  
   调 `HasRuntimeEffectPayload(...)`
4. 资源变化落地前  
   调 `ProcessResourceDelta(...)`
5. 持续效果开始 / 刷新 / 移除时  
   分别调：
   - `OnEffectApplied(...)`
   - `OnEffectRefreshed(...)`
   - `OnEffectRemoving(...)`

## 5. 快照为什么还能工作

因为 `Core` 的深拷贝层现在只依赖 `ICombatExtensionCloneHandler`。

标准语义层通过 `CombatSemanticModule` 自己实现这套接口，把：

- `CombatSemanticEffectExtension`
- `CombatSemanticEffectState`
- `ActiveShieldState`

这些扩展数据和扩展状态都交给模块自己克隆。

接线方式如下：

```csharp
var cloneProvider = new FastClonerDeepCloneProvider(
    new ICombatExtensionCloneHandler[] { semanticModule });
```

这样 `FastClonerDeepCloneProvider` 不需要知道任何具体语义类型，仍然保持在 Core 边界内。

## 6. 以后怎么继续扩

以后如果要继续往标准语义层加东西，推荐按这个顺序扩：

1. 先扩 `CombatSemanticCatalog`
   定义统一标签命名。
2. 再扩 `CombatSemanticBuilder`
   定义业务层最常用的构造入口。
3. 最后扩 `CombatSemanticModule`
   补运行时解释。

如果新增机制需要持续状态，例如：

- 可叠层护盾
- 吸收特定伤害类型的护盾
- 特殊充能层

就再新增：

- 一个 `ICombatEffectExtensionDefinition`
- 一个 `ICombatActiveEffectExtensionState`
- 以及模块里的克隆逻辑

而不是回头改 `CombatRuntime`。
