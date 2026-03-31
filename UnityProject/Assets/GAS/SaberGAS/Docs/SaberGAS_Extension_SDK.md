# Saber.GAS Extension SDK

本文定义扩展开发契约，目标是：**新增玩法能力时不修改 `Saber.GAS` Core**。

## 1. 扩展原则

1. 组合优于继承。
2. 扩展通过注册表接入，不改核心 switch 分发。
3. 规则和表现分离：核心算规则，Observer 通道做 UI/日志/特效。
4. 新扩展必须有最小回归测试。

## 2. 扩展入口一览

### 2.1 TriggerAction Descriptor Registry

用途：新增 Trigger 动作并统一 Runtime 执行、Editor 绘制、Validation 校验。

核心类型：

- `TriggerActionDescriptor`
- `ICombatTriggerActionDescriptorRegistry`
- `CombatTriggerActionDescriptorRegistryHub`

### 2.2 ImpactOperation Handler Registry

用途：新增 `CombatImpactOperation` 处理逻辑，不改 Runtime 核心分发。

核心类型：

- `IImpactOperationHandler`
- `IImpactOperationHandlerRegistry`
- `CombatImpactOperationHandlerRegistryHub`

### 2.3 CustomTriggerAction Registry

用途：项目专属复杂动作（通常带自定义 payload）。

核心类型：

- `ICombatCustomTriggerAction`
- `ICombatCustomTriggerActionRegistry`
- `CombatCustomTriggerActionRegistryHub`

### 2.4 Runtime 服务替换

用途：按需替换 Activation / Impact / EffectLifecycle / TriggerBridge。

配置入口：`CombatRuntimeOptions`

- `ActivationService`
- `ImpactService`
- `EffectLifecycleService`
- `TriggerBridgeService`

### 2.5 观察者扩展

用途：UI、日志、埋点、录像观察。

配置入口：`ICombatDomainEventBus`

- Deterministic 通道：规则侧
- Observer 通道：表现侧

## 3. 推荐目录组织

建议在独立程序集实现扩展，例如：

- `Assets/GAS/SaberGAS.RTS/Runtime/Operations/`
- `Assets/GAS/SaberGAS.RTS/Runtime/CustomActions/`
- `Assets/GAS/SaberGAS.RTS/Tests/Editor/`

## 4. 最小接入流程

1. 新建扩展实现（Handler/Descriptor/CustomAction）。
2. 提供 Registry 包装。
3. 在 Runtime 装配层注入（如 `RtsCombatRuntimeModulePack.ApplyTo`）。
4. 增加 1~2 个回归测试：
   - 注入扩展时生效。
   - 未注入扩展时保持默认行为。

## 5. 本仓库已落地示例

### 5.1 ImpactOperation 插件化示例（吸血）

- `RtsLifeStealImpactOperationHandler`
- `RtsLifeStealImpactOperationHandlerRegistry`
- `RtsCustomTriggerActionExamples.CreateLifeStealStrikeAbility(...)`

验证用例：

- `RtsLifeStealImpactOperationHandlerTests.LifeStealOperation_ShouldHealSource_WhenHandlerInjected`
- `RtsLifeStealImpactOperationHandlerTests.LifeStealOperation_ShouldNotHealSource_WhenHandlerNotInjected`

### 5.2 CustomTriggerAction 示例（受伤反给最远敌人）

- `RtsReflectDamageToFarthestEnemyTriggerAction`
- `RtsReflectDamageCustomTriggerActionRegistry`
- 由 `RtsCombatRuntimeModulePack` 自动兜底注入

## 6. 模板文件

模板目录：`Assets/GAS/SaberGAS/Docs/Templates/`

- `TriggerActionDescriptorRegistryTemplate.cs.txt`
- `ImpactOperationHandlerTemplate.cs.txt`
- `CustomTriggerActionRegistryTemplate.cs.txt`

复制模板后只需替换：

- 命名空间
- Id / Cue / Payload
- 核心业务逻辑
- 运行时注入点

## 7. 开发检查清单

1. 是否避免修改 `CombatRuntime` 分发核心？
2. 是否通过 Registry 接入？
3. 是否补了 Editor/Validation 映射（若是 TriggerAction）？
4. 是否新增回归测试并通过？
5. 是否补充文档和示例？

## 8. 兼容性建议

- 为扩展保留稳定 Id，避免热更后资产失配。
- 避免在扩展中保存不可序列化运行时状态。
- 复杂 payload 建议显式类型，避免弱类型 object 强转失误。
