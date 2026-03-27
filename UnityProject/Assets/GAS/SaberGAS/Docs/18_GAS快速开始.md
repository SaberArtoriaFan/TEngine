# GAS 快速开始

这份文档只回答一个问题：第一次接触这套 GAS Authoring / Workbench，怎么在 10 分钟内跑起来，并把全局标签、局部标签、全局 ResourceId 和基础连线都看明白。

## 1. 先记住一个核心入口

当前这套编辑工作流围绕一个根资产展开：

- `CombatDefinitionCatalogAsset`

它同时承担几件事：

- 作为整个 GAS 的初始化入口
- 作为 `Ability / Effect / Trigger / ActorTemplate` 的宿主容器
- 作为全局标签词库的宿主
- 作为全局 `ResourceId` 词库的宿主
- 作为 GAS Workbench 的根配置

也就是说，平时你主要只需要盯住一个 `.asset` 文件。

## 2. 两种开始方式

### 方式 A：先看 Example

如果你想先看一套成品，再反推怎么搭，建议直接这样做：

1. 选中任意一个 `CombatDefinitionCatalogAsset`
2. 在 Inspector 里点击 `生成 Example Catalog`
3. 系统会自动生成一份示例资产，并直接打开 GAS Workbench

这份 Example 会包含：

- 一个 `ActorTemplate`
- 一个 `Ability`
- 一个 `Effect`
- 一个 `Trigger`
- 一组全局标签词库
- 一组全局 `ResourceId` 词库
- 每种资产各自的一些局部标签定义

打开后你可以直接看到：

- `ActorTemplate -> Ability`
- `ActorTemplate -> Trigger`
- `Trigger -> Effect`

然后在右侧 Inspector 里依次点开标签编辑器和资源字段，就能直接看到“全局词库 + 局部词库 + 选择器”的完整工作流。

### 方式 B：从零创建

如果你想自己从空白开始搭：

1. 在 `Project` 里创建 `Saber.GAS/Combat System Config`
2. 选中这个根资产
3. 先补 `全局标签词库` 和 `全局 ResourceId 词库`
4. 点击 `打开 GAS Workbench`
5. 用根 Inspector 里的模板按钮，先生成一批起手资源

推荐第一次的最小组合是：

- `瞬发伤害 Ability`
- `Buff Effect`
- `被动 Trigger`

这样你马上就有图可以看，也能开始理解引用关系。

## 3. Workbench 怎么看

Workbench 现在是三栏结构：

- 左侧：导航区。显示根配置和全部子资源
- 中间：关系图。看引用、拖线、改关系
- 右侧：Inspector。看具体字段并编辑细节

常用按钮在顶部工具栏：

- `刷新`：重新同步当前视图
- `运行预览`：按当前 Catalog 真正构建一次，检查是否能初始化成功
- `返回全图`：退出当前局部视图
- `定位选中资源`：把当前选中的资源在 Project 里定位出来

图面板交互现在是：

- 单击节点：只选中
- 双击节点：进入该节点的局部视图

右侧 Inspector 支持滚动，字段很多时可以直接往下拉到底。

## 4. 标签怎么管理

### 全局标签词库

位置：`CombatDefinitionCatalogAsset` Inspector 里的 `全局标签词库`

适合放：

- 跨多个 Ability / Effect / Trigger 反复使用的标签
- 团队约定好的公共标签
- Actor 身份、职业、公共状态、通用冷却标签

### 局部标签库

位置：

- `AbilityDefinitionAsset` 里的 `局部标签库`
- `EffectDefinitionAsset` 里的 `局部标签库`
- `TriggerDefinitionAsset` 里的 `局部标签库`

适合放：

- 只在当前链路里临时使用的标签
- 不值得放进全局词库的内部状态标签
- 某个 Ability / Effect / Trigger 自己的局部约定

### 选择标签时

现在标签字段不再默认展开一堆空数组，而是：

1. 先显示当前摘要
2. 点击 `编辑标签`
3. 输入检索词
4. 从候选里点选
5. 必要时再手动补充

候选来源会自动合并：

- 根配置的全局标签词库
- 当前 Catalog 内 Ability / Effect / Trigger 的局部标签库

## 5. ResourceId 怎么管理

### 全局 ResourceId 词库

位置：`CombatDefinitionCatalogAsset` Inspector 里的 `全局 ResourceId 词库`

适合放：

- `health`
- `mana`
- `stamina`
- 任何需要被 Cost、Delta、Threshold、TriggerAction 反复引用的资源 Id

每个资源定义可以写：

- `ResourceId`
- 分组
- 说明

### 配置资源时

现在这些地方会优先走全局 `ResourceId` 选择器：

- `ActorTemplate` 里的初始资源
- `Ability` 里的资源消耗
- `Effect` 里的即时 / 周期资源变化
- `Effect` 里的护盾保护资源
- `Trigger` 里的阈值资源
- `Trigger` 动作里的资源目标
- `ImpactOperation` 里的资源目标

如果根配置里已经定义好资源词库，这些位置就不需要再手填字符串了。

## 6. 第一次建议你这样走一遍

如果你想最快建立直觉，可以照这个顺序操作：

1. 生成 Example Catalog
2. 在根 Inspector 里先看 `全局标签词库`
3. 再看 `全局 ResourceId 词库`
4. 点开 `Ability_FireBolt`，看它的资源消耗和局部标签库
5. 点开 `Effect_BattleFocus`，看它的周期资源变化
6. 点开 `Trigger_BattleFocusPulse`，看它的动作和资源 / 标签选择器
7. 回到图里看 `ActorTemplate -> Ability -> Effect / Trigger` 的关系

这一遍走完，你基本就能理解现在这套 Authoring 的主路径了。

## 7. 一行初始化怎么写

运行时最简单的入口：

```csharp
var gas = combatCatalog.Initialize();
```

或者：

```csharp
var gas = CombatAuthoringInstaller.Initialize(combatCatalog);
```

返回值是 `CombatSystemInstance`，里面会带上：

- `WorldState`
- `Runtime`
- `BuildContext`

释放时：

```csharp
gas.Dispose();
```

## 8. 最小心智模型

你可以先把这几个配置这样理解：

- `Ability`：角色“做什么”
- `Effect`：做完后“产生什么结果”
- `Trigger`：在什么时候“自动做什么”
- `ActorTemplate`：角色出生时自带什么能力和触发器
- `Initial Actor`：开局要生成哪些角色
- `全局标签词库`：跨系统复用的公共标签
- `局部标签库`：只在当前链路内部使用的标签
- `全局 ResourceId 词库`：所有资源字段共享的统一资源定义

## 9. 常见卡点

### 预览失败

先看 Workbench 下方预览面板，它会尽量定位出错资源。

### Trigger 报缺引用

说明当前 `Action` 需要 `Ability` 或 `Effect`，但你还没绑定。Trigger Inspector 里会给出快速补链按钮。

### 图上关系不完整

图编辑器主要覆盖高频引用关系。复杂数值、Tag 过滤、Threshold 这类细节，仍然在右侧 Inspector 里改。

### 资源字段还是手填

先确认你有没有在根配置里补 `全局 ResourceId 词库`。如果词库为空，资源字段会退回普通字符串输入。

### 老资产还带着旧字段

如果你是从旧版 Authoring 资产升级过来，或者发现 `.asset` 里仍然残留旧根字段，请执行：

1. `Saber.GAS/Normalize Module Assets`
2. `Saber.GAS/Force Reserialize GAS Assets`

第一步补齐模块，第二步强制 Unity 按当前结构重写资产文件。

## 10. 下一步建议学什么

如果你已经跑通第一遍，下一步最值得继续的是：

1. 给自己的项目先补一份全局标签词库
2. 给自己的项目补一份全局 `ResourceId` 词库
3. 给每条复杂链路补局部标签库
4. 把“手输字符串”逐步收敛到“候选选择”
5. 每次改完都跑一遍 `运行预览`

等你把这几步做顺，再去打磨更复杂的图形化体验就会很顺手。
