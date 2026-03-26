# 自定义 TriggerAction 与源码生成

## 目标

`TriggerActionKind.Custom` 现在已经不是一个空占位分支，而是一条正式的扩展入口。  
它适合处理这类需求：

- 某个 Trigger 需要执行项目专属逻辑
- 逻辑不适合上升为通用语义层
- 又不希望回头去改 `CombatTriggerProcessor` 或 `CombatRuntime`

这条链路的关键点是：

- Core 只定义协议和派发
- 业务层程序集实现自己的动作类
- 源码生成器自动收集实现类并生成程序集注册表
- Runtime 通过注册表按 `CustomActionId` 查找并执行

## 核心类型

相关文件：

- `Assets/ScriptsAssembly/SaberGAS/Runtime/Core/Triggers/CustomTriggerActions.cs`
- `Assets/ScriptsAssembly/SaberGAS/Runtime/Core/Triggers/TriggerDefinitions.cs`
- `Assets/ScriptsAssembly/SaberGAS/Runtime/Core/Triggers/CombatTriggerProcessor.cs`
- `Assets/ScriptsAssembly/SaberGAS/Runtime/Core/Runtime/CombatRuntime.cs`

主要类型分工：

- `ICombatCustomTriggerAction`
  业务层实现的自定义 TriggerAction 协议，必须提供稳定的 `CustomId` 和 `ExecuteAction(...)`
- `CombatCustomTriggerActionExecutionContext`
  自定义动作的执行上下文，统一提供 Runtime、TriggerContext、Owner、SourceEffect、SourceAbilityInstance 等信息
- `ICombatCustomTriggerActionRegistry`
  程序集级自定义动作注册表接口
- `CombatCustomTriggerActionRegistryHub`
  全局注册中心，由源码生成器产出的程序集注册表在加载时自动注册到这里
- `CompositeCombatCustomTriggerActionRegistry`
  Runtime 内部真正使用的派发表

## 业务层如何扩展

只需要做这几步：

1. 新建一个类，实现 `ICombatCustomTriggerAction`
2. 提供稳定不变的 `int CustomId`
3. 在 `ExecuteAction(...)` 中编写业务逻辑
4. 把 `TriggerActionDefinition.Kind` 设为 `Custom`
5. 把 `TriggerActionDefinition.CustomActionId` 设为对应的 `CustomId`

示例：

- `Assets/ScriptsAssembly/SaberGAS.RTS/Runtime/CustomActions/RtsGuardianShareDamageTriggerAction.cs`
- `Assets/ScriptsAssembly/SaberGAS.RTS/Runtime/Examples/RtsCustomTriggerActionExamples.cs`

## 运行时派发链路

完整流程如下：

1. 业务层程序集实现 `ICombatCustomTriggerAction`
2. Saber.GAS 的源码生成器扫描当前程序集里的实现类
3. 生成程序集级自定义动作注册表
4. 生成的模块初始化代码在程序集加载时把注册表注册到 `CombatCustomTriggerActionRegistryHub`
5. `CombatRuntime` 构造时收集：
   - 全局自动注册表
   - `CombatRuntimeOptions.CustomTriggerActionRegistries` 中手工补充的额外注册表
6. `CombatTriggerProcessor` 遇到 `TriggerActionKind.Custom` 时，构造 `CombatCustomTriggerActionExecutionContext`
7. `CombatRuntime.ExecuteCustomTriggerAction(...)` 通过组合注册表查找并执行真正的动作实现

## 什么时候该用它

推荐使用 `CustomTriggerAction` 的场景：

- 某个 Trigger 的行为非常项目化
- 逻辑天然属于“Trigger 触发后做什么”
- 不值得抽成通用语义层

不推荐长期滥用的场景：

- 常规伤害、治疗、护盾、控制、驱散、免疫
- 可以抽成标准规则模块或语义层的机制
- 需要全局统一解释的通用战斗语义

换句话说：

- 通用机制优先放到 `Semantics` 或其他通用扩展接口
- `CustomTriggerAction` 负责少量项目专属 Trigger 行为

## 和统一扩展注册的关系

现在的源码生成器不只处理 `CustomTriggerAction`，也会处理：

- `ICombatActionGate`
- `ICombatImpactMutator`
- `ICombatImpactResolver`
- `ICombatRuleModule`
- `ICombatExtensionCloneHandler`

但 `CustomTriggerAction` 仍然是特殊的一类，因为它需要通过 `CustomActionId` 做精确分发。  
其他运行时扩展则走程序集级扩展注册表自动收集。

更完整的统一扩展说明，见：

- `Assets/ScriptsAssembly/SaberGAS/Docs/16_运行时扩展自动注册与拓展指南.md`
