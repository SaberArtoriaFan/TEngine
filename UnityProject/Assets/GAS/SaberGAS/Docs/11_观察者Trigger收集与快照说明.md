# 11_观察者Trigger收集与快照说明

## 1. 这份补充文档解决什么问题
这份文档只回答两个非常具体的问题：

1. `TriggerCollectionMode.ObserveTarget` 和 `TriggerCollectionMode.ObserveInstigator` 到底在哪里被收集到。
2. `CombatTriggerRegistry` 现在为什么没有被放进 `CombatSnapshot`，这样做是否合理。

如果你刚看完 `TriggerDefinition`，会很容易产生一个疑问：

- 我看到了 `ObserveTarget = 1`
- 我也看到了 `ObserveInstigator = 2`
- 但我没看到哪里直接“遍历全场单位然后把这些 Trigger 收进来”

答案是：当前实现不是“事件发生时全场扫描”，而是“平时先注册到观察者索引，事件发生时按上下文从索引里取”。

## 2. 先说结论
`ObserveTarget` 和 `ObserveInstigator` 的完整链路是：

1. `TriggerDefinition.CollectionMode` 决定这个 Trigger 属于哪一种收集模式。
2. `CombatRuntime` 在 Actor / Effect / Ability 的 Trigger 实例创建后，把非 `OwnerOnly` 的 Trigger 注册进 `CombatTriggerRegistry`。
3. `CombatTriggerRegistry` 按 `CollectionMode` 分别放进：
   - `_targetObservers`
   - `_instigatorObservers`
4. 某次战斗事件发生时，`CombatTriggerProcessor.CollectCandidates(...)` 会调用 `CombatTriggerRegistry.Collect(...)`。
5. `Collect(...)` 根据当前 `CombatTriggerContext` 里是否存在 `TargetActorId` 或 `InstigatorActorId`，把对应观察者候选取出来。
6. 这些候选进入 `CombatTriggerProcessor` 之后，还会再经过一次 `MatchesCollectionMode(...)` 和关系/Tag/距离过滤，最终才真正执行。

也就是说：

- `CombatTriggerRegistry` 负责“索引和初筛”
- `CombatTriggerProcessor` 负责“最终判定和执行”

## 3. 观察者 Trigger 的注册链
观察者 Trigger 不是在事件发生那一刻临时生成的，而是在运行时对象初始化时就注册好了。

### 3.1 Runtime 创建时会重建注册表
`CombatRuntime` 构造函数里会创建一个新的 `CombatTriggerRegistry`，随后调用 `RebuildTriggerRegistry()`：

```csharp
_triggerRegistry = new CombatTriggerRegistry();
_triggerProcessor = new CombatTriggerProcessor(this);
RebuildTriggerRegistry();
```

这一步的作用是：

- 扫描当前 `WorldState`
- 把 Actor 自身的 Trigger 注册进去
- 把 ActiveEffect 上的 Trigger 注册进去
- 把 ActiveAbilityInstance 上的 Trigger 注册进去

### 3.2 注册入口在 CombatRuntime
注册最终会落到 `RegisterTriggerList(...)`。

它会根据来源类型，把 Trigger 实例送到不同的注册函数：

- `RegisterActorTrigger(...)`
- `RegisterEffectTrigger(...)`
- `RegisterAbilityTrigger(...)`

这几个入口最后都会汇总到 `CombatTriggerRegistry.Register(...)`。

### 3.3 真正按模式分桶在 CombatTriggerRegistry
`CombatTriggerRegistry.Register(...)` 里有一段很关键：

```csharp
if (definition.CollectionMode == TriggerCollectionMode.OwnerOnly)
{
    return;
}

var lookup = GetLookup(definition.CollectionMode);
```

接着 `GetLookup(...)` 会把模式映射到不同字典：

```csharp
case TriggerCollectionMode.ObserveTarget:
    return _targetObservers;
case TriggerCollectionMode.ObserveInstigator:
    return _instigatorObservers;
```

这就是为什么你在 `TriggerDefinition` 那里看不到“收集逻辑”。
因为枚举本身只定义模式，真正收纳到观察者索引是在 `CombatTriggerRegistry` 这一层完成的。

## 4. 观察者 Trigger 的收集链
注册只是把 Trigger 放进索引里，真正“这次事件要不要拿它出来”发生在 `CombatTriggerProcessor`。

### 4.1 CollectCandidates 会调 TriggerRegistry
在 `CombatTriggerProcessor.CollectCandidates(...)` 里，顺序大致是：

1. 先收 `OwnerActorId` 对应的本地 Trigger
2. 再调 `_runtime.TriggerRegistry.Collect(context, results)`
3. 再补 source effect / source ability 的特殊候选
4. 最后再收全局 Trigger

也就是说，观察者 Trigger 是在第二步进来的，不是混在本地 Trigger 里。

### 4.2 CombatTriggerRegistry.Collect 的行为
`CombatTriggerRegistry.Collect(...)` 的核心逻辑很简单：

```csharp
if (!context.TargetActorId.IsEmpty)
{
    Append(_targetObservers, context.EventKind, results);
}

if (!context.InstigatorActorId.IsEmpty)
{
    Append(_instigatorObservers, context.EventKind, results);
}
```

它不做重逻辑判断，只做两件事：

- 看这次上下文里有没有 `TargetActorId`
- 看这次上下文里有没有 `InstigatorActorId`

如果有，就把这个事件类型下注册过的观察者候选追加进候选列表。

所以你可以把它理解成：

- `ObserveTarget` 意味着“我关心这次事件里的 Target”
- `ObserveInstigator` 意味着“我关心这次事件里的 Instigator”

## 5. 为什么还要二次校验
如果只是把 `_targetObservers` 或 `_instigatorObservers` 整批拿出来，还不够安全。

因为同一个事件类型下，可能会有很多观察者 Trigger：

- 有的要求观察者和 Target 是友军
- 有的要求观察者和 Instigator 是敌人
- 有的要求观察者距离被观察对象在 6 格内
- 有的要求目标身上带某个 Tag

所以候选拿出来之后，`CombatTriggerProcessor` 还会做一次真正的命中判定。

### 5.1 CollectionMode 的二次匹配
`MatchesCollectionMode(...)` 负责确认“这个候选现在的观察对象到底对不对”：

```csharp
case TriggerCollectionMode.ObserveTarget:
    return MatchesObservedActor(candidate.OwnerActor, context.TargetActorId, definition.ObserverMaxDistance);
case TriggerCollectionMode.ObserveInstigator:
    return MatchesObservedActor(candidate.OwnerActor, context.InstigatorActorId, definition.ObserverMaxDistance);
```

这里同时做了两件事：

- 检查被观察 Actor 是否有效
- 如果配置了 `ObserverMaxDistance`，则做距离限制

### 5.2 关系过滤不是收集，而是过滤
还要特别强调一点：

- `ObserveTarget / ObserveInstigator` 决定的是“观察谁”
- `TargetRelationFilter / InstigatorRelationFilter` 决定的是“Owner 和这个被观察对象之间必须是什么关系”

也就是说：

- `CollectionMode` 决定候选从哪一类索引拿
- `RelationFilter` 决定拿出来以后是不是合法

这两件事不能混为一谈。

## 6. 用两个例子理解

### 6.1 队友受击时自己分担伤害
守护者 C 身上有一个 Trigger：

- `CollectionMode = ObserveTarget`
- `TargetRelationFilter = Ally`
- `InstigatorRelationFilter = Enemy`
- `EventKind = BeforeImpactResolve`

当 `A -> B` 的伤害要结算时：

1. 这次 `CombatTriggerContext.TargetActorId = B`
2. `CombatTriggerRegistry.Collect(...)` 会把所有监听 Target 的候选拿出来
3. `CombatTriggerProcessor` 再检查：
   - C 是否是 B 的友军
   - C 是否是 A 的敌人
   - C 和 B 是否在观察半径内
4. 条件成立，C 这个 Trigger 才会生效

### 6.2 范围内敌人受伤时自己回血
吸血观察者 C 身上有一个 Trigger：

- `CollectionMode = ObserveTarget`
- `TargetRelationFilter = Enemy`
- `EventKind = AfterImpactResolved`

当某个敌人 B 受伤时：

1. `TargetActorId = B`
2. `CombatTriggerRegistry` 从 `_targetObservers` 里取出候选
3. `CombatTriggerProcessor` 检查 C 与 B 的敌对关系和距离
4. 命中后执行 `AddResourceFromImpact`

这类功能如果没有观察者索引，就只能靠全场扫描或者写死规则层，都会很别扭。

## 7. CombatTriggerRegistry 需要进快照吗
当前版本下，结论是：不需要单独进入快照，而且不应该单独进入快照。

原因不是“它不重要”，而是“它是可重建索引，不是独立真状态”。

### 7.1 现在的 CombatSnapshot 存的是什么
当前 `CombatSnapshot` 只存：

- `Tick`
- `WorldState`
- `Payload`

其中真正的战斗状态主体是 `WorldState`。

### 7.2 Registry 当前保存的是什么
`CombatTriggerRegistry` 当前保存的是两类索引：

- `_targetObservers`
- `_instigatorObservers`

而这两个字典里的内容，本质上只是对这些真实状态的运行时索引：

- `CombatActorState.ActiveTriggers`
- `ActiveEffect.ActiveTriggers`
- `ActiveAbilityInstance.ActiveTriggers`

也就是说，它没有拥有额外的独立业务状态，只是把已经存在于 `WorldState` 里的 Trigger 实例按观察方式重新组织了一遍。

### 7.3 恢复快照时会自动重建
`CombatSimulationHost.RestoreSnapshot(...)` 当前流程是：

1. 克隆 `snapshot.WorldState`
2. 关闭旧 runtime
3. `new CombatRuntime(restoredWorld, _runtimeOptions)`

而 `CombatRuntime` 构造函数里会立刻：

1. 创建新的 `CombatTriggerRegistry`
2. 调 `RebuildTriggerRegistry()`

这意味着：

- 快照恢复后不会沿用旧 registry
- 会基于恢复后的 `WorldState` 重新生成一份新的 registry

所以当前设计下，`CombatTriggerRegistry` 是典型的“派生缓存”或“运行时索引”。

### 7.4 什么情况下它就必须进快照
如果未来你把下面这些内容也塞进 `CombatTriggerRegistry`，那它就不能再只靠重建了：

- 不存在于 `ActiveTriggerInstance` 里的额外计数器
- 非可重建的触发队列
- 延迟触发计划表
- 已排序但未消费的候选缓存
- 与世界状态分离的订阅版本号

只要 registry 内部出现“无法完全从 `WorldState` 重建”的数据，它就必须满足二选一：

1. 一并进入快照
2. 或者把那部分真状态下沉回 `WorldState / ActiveTriggerInstance`

### 7.5 当前推荐原则
我更推荐继续保持现在这个原则：

- `CombatWorldState`、`ActiveTriggerInstance` 持有真正状态
- `CombatTriggerRegistry` 只做索引、查找、分桶、缓存
- 快照只保存真状态
- 恢复时重建所有派生索引

这样设计的好处是：

- 快照结构更干净
- 深拷贝边界更清楚
- 回滚恢复更稳
- registry 内部可以持续优化而不破坏存档结构

## 8. 一句话结论
`ObserveTarget / ObserveInstigator` 不是在定义处直接“扫描收集”的，而是：

1. 先在 `CombatRuntime` 初始化或新增实例时注册进 `CombatTriggerRegistry`
2. 事件发生时由 `CombatTriggerRegistry.Collect(...)` 按 `TargetActorId / InstigatorActorId` 取回候选
3. 最后再由 `CombatTriggerProcessor` 做距离、关系、Tag、次数、冷却等正式过滤

而 `CombatTriggerRegistry` 当前不进快照是合理的，因为它只是从 `WorldState` 派生出来的运行时索引，恢复快照时会自动重建。
