# Unity 配置资产层

## 1. 目标形态

当前推荐的 GAS 使用方式已经收敛成一套“单根配置 + 可视化编辑 + 一行初始化”的工作流：

- 只保留一个根资产：`CombatDefinitionCatalogAsset`
- `Ability / Effect / Trigger / ActorTemplate` 全都作为这个根资产的子资产
- 根资产自动收集同一个 `.asset` 文件中的 GAS 子配置
- 项目初始化时可以直接一行启动整套 GAS
- 编辑阶段优先在 GAS Workbench 中完成导航、连线、预览和校验

也就是说，现在一份根配置资产就可以同时承担：

- 运行时初始化入口
- 编辑器工作台入口
- 配置关系图的宿主
- 预览和校验的载体

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
- 写入随机种子与世界标签
- 注册 Ability 定义
- 按配置自动生成初始 Actor
- 创建 `CombatRuntime`

## 4. 创建与组织资产

### 4.1 创建根资产

先创建：

- `Saber.GAS/Combat System Config`

对应类型仍然是 `CombatDefinitionCatalogAsset`。

### 4.2 在根资产中创建子配置

现在有两种方式可以新增子配置：

1. 选中根资产后使用 ContextMenu：
   - `Saber.GAS/新增 Ability 子配置`
   - `Saber.GAS/新增 Effect 子配置`
   - `Saber.GAS/新增 Trigger 子配置`
   - `Saber.GAS/新增 ActorTemplate 子配置`
2. 直接在根资产 Inspector 或 Workbench 中使用模板按钮和快速创建按钮

这些子资产会被直接写进同一个 `.asset` 文件中。

### 4.3 配置启动时自动生成的 Actor

根资产上有：

- `Random Seed`
- `World Tags`
- `Initial Actors`

其中 `Initial Actors` 用来声明启动时自动生成的单位：

- 选择一个 `CombatActorTemplateAsset`
- 可选覆盖 `ActorId`
- 可选覆盖 `TeamId`
- 可选覆盖 `Position`

这样项目初始化时就不需要你手动 `AddActor` 再套模板。

### 4.4 老资产升级与重序列化

如果你是从旧版、非模块化的 Authoring 资产升级过来，建议在 Unity 菜单里依次执行：

- `Saber.GAS/Normalize Module Assets`
- `Saber.GAS/Force Reserialize GAS Assets`

第一步会补齐缺失的 `_modules` 结构；第二步会强制 Unity 用当前序列化结构重写 `.asset`，把旧字段残留尽量清掉。

## 5. GAS Workbench

### 5.1 打开方式

你可以从两个入口进入：

- 菜单：`Saber.GAS/Workbench`
- `CombatDefinitionCatalogAsset` Inspector 里的 `打开 GAS Workbench`

### 5.2 当前布局

Workbench 目前是三栏结构：

- 左侧：根配置与全部子资产导航
- 中间：Graph 关系图
- 右侧：当前选中资产的 Inspector

工具栏里还提供：

- `刷新`
- `定位选中资源`
- `运行预览`

### 5.3 当前支持的图编辑范围

Graph 现在只覆盖高频、低歧义的关系，并且直接回写到底层 ScriptableObject，不引入第二份 graph asset。

当前支持：

- `Ability -> Effects`
- `Ability -> PeriodicEffects`
- `Ability -> EndEffects`
- `Ability -> Triggers`
- `Effect -> RemovedTargetEffects`
- `Effect -> Triggers`
- `ActorTemplate -> GrantedAbilities`
- `ActorTemplate -> ActorTriggers`
- `Trigger -> ApplyEffect`
- `Trigger -> ActivateAbility`
- `Trigger -> CancelAbility`

不适合图形化的复杂标量参数仍然放在右侧 Inspector 编辑，例如：

- 各类 Tag 过滤
- Threshold 细节
- 数值 Delta
- 资源与属性参数

## 6. 引导式 Inspector

现在根资产和四类子资产都已经有了分组式 Inspector。

### 6.1 根资产 Inspector

根资产 Inspector 里会直接展示：

- 配置问题概览
- 子资产数量摘要
- Workbench 入口
- 同步内嵌配置按钮
- 起手模板按钮
- 初始化字段
- 各类子资产只读列表

### 6.2 子资产 Inspector

`Ability / Effect / Trigger / ActorTemplate` Inspector 都已经按工作流拆分为多个 section，重点是：

- 先看到摘要和校验
- 再看到分组字段
- 最后看到快速补链按钮

例如：

- Ability Inspector 可以直接新建并挂接 Effect / Trigger
- Effect Inspector 可以直接新建并挂接 Trigger
- ActorTemplate Inspector 可以直接新建并授予 Ability / Trigger
- Trigger Inspector 会根据当前动作类型提示缺少的 Ability / Effect 引用，并提供一键创建绑定

## 7. 模板创建

根资产 Inspector 当前至少支持三种起手模板：

- `瞬发伤害 Ability`
- `Buff Effect`
- `被动 Trigger`

其中：

- `瞬发伤害 Ability` 会同时创建一个 Ability 和一个被其引用的 Effect
- `Buff Effect` 会创建一个带持续时间和示例 GrantedTag 的 Effect
- `被动 Trigger` 会创建一个按 `OnTick` 触发、执行 `AddTag` 的示例 Trigger

此外也保留了空白模板入口：

- `空 Ability`
- `空 Effect`
- `空 Trigger`

## 8. 预览与校验

### 8.1 内联校验

当前内联校验重点覆盖：

- 缺失 id
- 数组中的空引用
- TriggerAction 的无效高频目标

其中 TriggerAction 当前会检查：

- `ActivateAbility` 缺少 Ability
- `ApplyEffect` 缺少 Effect
- `RemoveEffectById` 缺少 Effect
- `CancelAbility` 缺少 Ability
- 若干基于 Tag / Resource / Cue 的必填字段为空

### 8.2 运行预览

Workbench 的 `运行预览` 会直接复用 `catalog.Initialize()` 的构建链路，生成当前配置快照，展示：

- Built Abilities
- Built Effects
- Built Triggers
- Initial Actors

如果预览失败：

- 会显示异常信息
- 会尝试根据异常文本定位出错资产
- 可以一键在 Workbench 中聚焦该资产

重新编辑后再次点击 `运行预览`，会读取当前最新资产状态，而不是旧缓存。

## 9. 示例资产

现在提供了一个编辑器菜单来生成示例根资产：

- `Saber.GAS/Create Workbench Sample Catalog`

它会在：

- `Assets/GAS/SaberGAS/Samples/`

下生成一份可直接打开 Workbench 的样例 Catalog，并自动带上：

- 一个瞬发伤害 Ability
- 一个 Buff Effect
- 一个被动 Trigger
- 一个授予 Ability/Trigger 的 ActorTemplate
- 一个 `Initial Actor`

如果你手上已经有较早生成的示例资产，也可以在升级后执行一次：

- `Saber.GAS/Normalize Module Assets`

这样能把旧样例同步到当前模块化结构。

## 10. 保留的进阶入口

如果你不想整套一键启动，仍然可以继续用细粒度接口：

```csharp
var worldState = combatCatalog.CreateWorldState();
var runtime = new CombatRuntime(worldState, runtimeOptions);
```

或者：

```csharp
CombatAuthoringInstaller.ApplyTemplate(runtime, actor, actorTemplateAsset);
```

所以当前是两层模式并存：

- 默认用根资产一键初始化
- 复杂场景再退回细粒度装配

## 11. 当前边界

当前这层已经打通了：

- 单根配置
- 自动收集
- 一行初始化
- 自定义 Inspector
- 图关系编辑
- 构建预览

但还没有覆盖所有 UE 风格高级体验，当前边界主要在：

- `TriggerAction.CustomPayload` 还没有 Unity 侧序列化方案
- `AddImpactOperation` 暂不支持直接 authoring `ApplyEffect`
- 图编辑目前只覆盖高频关系，不覆盖所有嵌套标量参数
- 还没有更高级的节点搜索、批量重排、分组框和小地图

## 12. 下一步建议

如果继续往 UE 风格靠拢，优先级最高的是：

1. Graph 的节点视觉打磨、搜索、分组和小地图
2. 更完整的 TriggerAction 图节点表达
3. 更细的预览能力，例如模拟一次 Ability 激活链路
4. 示例资产与教程面板联动
