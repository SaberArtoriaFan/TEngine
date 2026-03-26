# 扩展点与 FastCloner 接入

## 1. 当前开放的扩展点

`Saber.GAS` 现在不是一个封死的战斗框架，而是带了一组明确的扩展口。

### 1.1 运行时规则

- `ICombatModeRules`
- `ITargetingResolver`

这两个接口决定：

- 谁能行动
- 如何解析目标
- Tick 后规则如何收尾

### 1.2 行动流水线

- `ICombatActionGate`
- `ICombatImpactMutator`
- `ICombatImpactResolver`

这三个接口决定：

- 是否阻断尝试
- 是否改写 impact
- 是否解释新的 impact operation

### 1.3 网络 / 复制

- `ICombatSnapshotStore`
- `ICombatReplicator`

它们负责：

- 快照存储策略
- 向外广播快照和预测结果

### 1.4 深拷贝 / 序列化

- `IDeepCloneProvider`
- `IStateSerializer`
- `IJsonAdapter`

这些接口现在都没有被写死到任何具体库。

## 2. 当前项目里的 FastCloner 接法

当前 `Saber.GAS` 已经落了一版可工作的 FastCloner 接入:

- `Runtime/Serialization/FastClonerDeepCloneProvider.cs`
- `Runtime/Foundation/DeterministicRandom.cs`
- `Runtime/Tags/GameplayTags.cs`
- `Runtime/Resources/ResourceSet.cs`

这次接入的实际策略不是“把整张战斗状态图全部交给当前包版本的 SourceGenerator 自动生成”, 而是分成两层:

- 底层稳定叶子状态:
  `DeterministicRandom`、`GameplayTagContainer`、`ResourceValue / ResourceSet` 这些结构继续保留 FastCloner 特性
- 高层战斗状态图:
  `CombatWorldState -> Actor -> Ability / Effect / Attribute` 由 `FastClonerDeepCloneProvider` 显式编排克隆

这样做的原因是当前项目内实际接入的 `FastCloner.SourceGenerator 1.1.5` 在 Unity 编译链下, 对跨命名空间扩展方法解析和复杂对象图自动生成存在明显限制。直接把整张战斗图都交给它, 会出现:

- 生成方法在 `dotnet build` 下可见, 但普通 Unity 业务代码里不可稳定直接引用
- 带循环或复杂引用关系的对象图容易触发生成失败
- 某些只读构造状态即使生成成功, 也不一定适合作为最终快照恢复入口

因此当前版本的落点是:

- 让 FastCloner 负责它目前真正稳定的那一层
- 让 `FastClonerDeepCloneProvider` 保证战斗快照恢复所需的完整性与引用关系

## 3. FastCloner 应该接在哪里

最直接的接法是：

1. 提供一个 `IDeepCloneProvider` 实现
2. 在创建 `CombatSimulationHost` 时注入

也就是说，后续你只需要做类似这样的桥接：

```csharp
public sealed class FastClonerCloneProvider : IDeepCloneProvider
{
    public T Clone<T>(T source)
    {
        return FastClonerAPI.DeepClone(source);
    }
}
```

然后在宿主初始化时传入它。

## 4. 目前适合继续扩展的方向

### 4.1 逻辑层

- Projectile resolver
- Area / Aura resolver
- Summon / Trap resolver
- 更丰富的 resource policy
- 更丰富的 tag relation

### 4.2 网络层

- Delta snapshot
- 更细粒度回放
- 快照裁剪策略
- 预测回滚优化

### 4.3 数据层

- ScriptableObject authoring
- 旧资产导入器
- 配置校验器

## 5. 当前已知边界

为了保持核心干净，下面这些仍在外层处理：

- UI
- 特效
- 音频
- 镜头
- 具体投射物实体
- 具体地图空间查询

这不是缺失，而是刻意的分层边界。
