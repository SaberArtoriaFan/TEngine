# Unity 配置资产层

## 1. 目标形态

现在推荐的使用方式已经收敛成：

- 只保留一个根资产：`CombatDefinitionCatalogAsset`
- 把 `Ability / Effect / Trigger / ActorTemplate` 都作为这个根资产的子资源
- 根资产自动收集同一个 `.asset` 文件里的所有 GAS 子配置
- 项目初始化时直接一行代码启动整套系统

也就是说，现在更接近“一个配置资产就是一套 GAS 系统”。

## 2. 一行初始化

最简单的入口有两种，任选其一：

```csharp
var gas = combatCatalog.Initialize();
```

或者：

```csharp
var gas = CombatAuthoringInstaller.Initialize(combatCatalog);
```

返回值 `CombatSystemInstance` 里会带上：

- `WorldState`
- `Runtime`
- `BuildContext`

销毁时可以直接：

```csharp
gas.Dispose();
```

## 3. 根资产负责什么

`CombatDefinitionCatalogAsset` 现在会负责：

- 自动收集同一 `.asset` 下的 `AbilityDefinitionAsset`
- 自动收集同一 `.asset` 下的 `EffectDefinitionAsset`
- 自动收集同一 `.asset` 下的 `TriggerDefinitionAsset`
- 自动收集同一 `.asset` 下的 `CombatActorTemplateAsset`
- 初始化 `CombatWorldState`
- 写入世界随机种子和世界标签
- 注册全部 Ability 定义
- 按配置自动生成初始 Actor
- 创建 `CombatRuntime`

## 4. 单资产工作流

### 4.1 创建根资产

先创建一个：

- `Saber.GAS/Combat System Config`

对应类型仍然是 `CombatDefinitionCatalogAsset`。

### 4.2 在根资产上新增子配置

选中根资产后，用它的 ContextMenu：

- `Saber.GAS/新增 Ability 子配置`
- `Saber.GAS/新增 Effect 子配置`
- `Saber.GAS/新增 Trigger 子配置`
- `Saber.GAS/新增 ActorTemplate 子配置`

这些子资源会直接写进同一个 `.asset` 文件里。  
根资产会在校验时自动同步收集结果；如果你手动改过子资源，也可以执行：

- `Saber.GAS/同步内嵌配置`

### 4.3 配置初始化生成的 Actor

根资产上有：

- `Random Seed`
- `World Tags`
- `Initial Actors`

其中 `Initial Actors` 用来声明启动时自动生成的单位：

- 选择一个 `CombatActorTemplateAsset`
- 可选覆盖 `ActorId`
- 可选覆盖 `TeamId`
- 可选覆盖 `Position`

这样项目初始化时就不需要你自己再手动 `AddActor` 再套模板了。

## 5. 保留的进阶入口

如果你不想整套一键启动，仍然可以继续用细粒度接口：

```csharp
var worldState = combatCatalog.CreateWorldState();
var runtime = new CombatRuntime(worldState, runtimeOptions);
```

或者：

```csharp
CombatAuthoringInstaller.ApplyTemplate(runtime, actor, actorTemplateAsset);
```

所以现在是两层模式并存：

- 默认用根资产一键初始化
- 复杂场景再退回细粒度装配

## 6. 当前支持的范围

### 6.1 Effect 侧

`EffectDefinitionAsset` 当前支持：

- `EffectTags`
- `GrantedTags`
- `RequiredTargetTags`
- `BlockedTargetTags`
- `RemovedTargetEffectTags`
- `RemovedTargetEffects`
- `AttributeModifiers`
- `InstantResourceDeltas`
- `PeriodicResourceDeltas`
- `Triggers`
- 标准语义层里的护盾扩展 `ShieldSemanticDefinition`

### 6.2 Trigger 侧

`TriggerDefinitionAsset` 当前支持：

- 事件窗口、时机、收集模式
- Owner / Instigator / Target / Ability / Effect / Impact 的 Tag 条件
- 关系过滤、距离过滤
- 阈值触发
- 标准动作：
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
  - `EmitCue`
  - `SplitImpactToActor`
  - `AddResourceFromImpact`
  - `Custom`

## 7. 当前边界

这层现在已经把“单资产 + 自动收集 + 一行初始化”接通了，但还有几块没有做：

- `TriggerAction.CustomPayload` 还没有 Unity 侧序列化方案
- `AddImpactOperation` 暂不支持直接 authoring `ApplyEffect`
- 还没有自定义 Inspector / ReorderableList / 校验面板
- 还没有旧资产自动迁移器

## 8. 建议的下一步

如果继续优化易用性，最值得补的是：

1. 根资产的自定义 Inspector
2. 子配置的可视化创建按钮和校验提示
3. `CustomPayload` 的可序列化包装
4. 旧战斗资产到单根 GAS 配置的迁移工具
