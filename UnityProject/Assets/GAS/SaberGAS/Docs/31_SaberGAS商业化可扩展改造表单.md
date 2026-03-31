# Saber.GAS 商业化可扩展改造表单（组合优先 / 事件驱动 / 可插拔）

> 目标：后续新增玩法能力时，仅新增模块或配置，不修改 Runtime 核心代码。

## 总体架构原则

- 组合优于继承：行为由可组合 Handler/Policy/Module 构成，不把逻辑塞进超大基类。
- 事件广播 + 观察者：核心只发布领域事件，不直接耦合 UI、日志、埋点、表现层。
- 配置驱动 + 注册中心：以 Descriptor/Registry 替代多处 switch/case 硬编码。
- 核心稳定、边缘扩展：新增 TriggerAction/Impact/语义扩展通过插件注册接入。
- 无侵入扩展：新增功能默认不改 `CombatRuntime` / `CombatTriggerProcessor` 主干。

## 分阶段执行（表单）

| 编号 | 当前问题（臃肿/重复） | 改造方案（解耦与可扩展） | 设计模式 | 扩展方式（不改底层） | 验收标准 | 优先级 | 状态 |
|---|---|---|---|---|---|---|---|
| A1 | `TriggerActionKind` 在 Runtime/Editor/Graph/Validation 多处 `switch` 硬编码 | 建立 `TriggerActionDescriptorRegistry`（执行器/校验器/Inspector绘制/图关系）统一元数据驱动 | 策略 + 注册表 + 工厂 | 新 Action 仅新增 `Descriptor + Executor + Drawer` 并注册 | 新增 `TriggerActionKind` 时核心文件 0 改动 | P0 | 待开始 |
| A2 | Impact 处理逻辑集中在 `CombatRuntime.ResolveBuiltInImpact`，扩展会持续膨胀 | 建立 `IImpactOperationHandler` 链，内置处理器模块化，运行时按 `OperationType` 分派 | 责任链 + 策略 | 新增 ImpactOperation 仅新增 Handler 插件 | 新增 Operation 不改 `ResolveBuiltInImpact` | P0 | 待开始 |
| A3 | `CombatRuntime` 单类职责过载（激活/结算/生命周期/触发桥接） | 拆分 `ActivationService`、`ImpactService`、`EffectLifecycleService`、`TriggerBridge`，Runtime 只做编排 | 门面 + 组合 | 新规则以 Service 插件注册接入 | Runtime 核心代码行数显著下降，职责边界清晰 | P0 | 待开始 |
| A4 | Trigger 相关 Direct API 与 Actor 查找重复，参数拼装长且重复 | 抽 `ActorCommandFacade` 与 `TriggerEventFactory`，统一资源变更/标签变更/效果移除流程 | 门面 + 模板方法 | 新 Trigger 动作复用 Facade，无需复制样板 | Direct API 重复代码下降 50%+ | P1 | 待开始 |
| A5 | 候选收集与执行散落在 `CombatTriggerProcessor`，可读性差 | 拆 `TriggerCandidateCollector`、`TriggerMatcher`、`TriggerActionDispatcher` | 管道 + 策略 | 可替换 Matcher/Collector 实现 | 处理流程可单测分层，Processor 仅串联 | P1 | 待开始 |
| A6 | Authoring 中数组去重/删除逻辑重复（AppendUnique/RemoveReference） | 提供 `AuthoringCollectionUtility` 泛型工具并统一替换 | 模板函数 | 新模块复用通用集合工具 | 同类重复方法清零 | P1 | 待开始 |
| A7 | Editor 工具类过大（Tag UI/模块绘制/资产操作耦合） | 拆 `TagEditorUI`、`ModuleDrawerRegistry`、`CatalogAssetOps`、`SerializedPropertyOps` | 注册表 + 组合 | 新模块绘制器通过 Registry 扩展 | 新模块 UI 接入不改 `GasModuleEditorUtility` 主体 | P1 | 待开始 |
| A8 | `*Id` 结构体模板代码大量重复 | 引入 SourceGenerator 或统一模板（保留强类型） | 代码生成 | 新增 Id 类型自动生成 | 新增一个 Id 类型只写声明不写样板 | P2 | 待开始 |
| A9 | 克隆逻辑分散，`CloneTagContainer` 等重复 | 抽 `CombatCloneUtility` 与可扩展 `ICloneNodeHandler` | 访问者 + 策略 | 新扩展状态提供 CloneHandler 即可 | 克隆重复逻辑统一，扩展状态零侵入 | P2 | 待开始 |
| A10 | 示例 Runner 重复（双Actor/投射物） | 抽 `QuickStartScenarioHost` 基座，场景差异策略化 | 组合 + 策略 | 新示例只实现 Scenario 配置类 | 新增示例脚本体量下降 40%+ | P3 | 待开始 |
| A11 | 事件出口单一且语义偏弱（仅 `EventSink`） | 增加 `CombatDomainEventBus`（同步 deterministic + 异步观察） | 观察者 + 发布订阅 | UI/埋点/录像订阅事件，无需入侵核心 | 新增观察模块不改 Runtime | P0 | 待开始 |
| A12 | 扩展接入规范分散，团队难以一致执行 | 补 `Extension SDK` 文档与模板（Action/Impact/Semantic/Editor） | 模板化工程实践 | 新人按模板 30 分钟可接入 | 出现新增扩展需求时无需修改底层 | P0 | 待开始 |

## 里程碑（建议）

| 里程碑 | 包含项 | 目标 |
|---|---|---|
| M1（核心可插拔） | A1 + A2 + A11 | Trigger/Impact/事件三条主干去硬编码，建立插件式扩展入口 |
| M2（Runtime 解耦） | A3 + A4 + A5 | CombatRuntime 瘦身，业务路径服务化、可测试化 |
| M3（Authoring/Editor 解耦） | A6 + A7 + A12 | 配置与编辑器扩展规范化，新功能接入成本下降 |
| M4（工程化收尾） | A8 + A9 + A10 | 减少样板与重复，实现长期维护友好 |

## 每项统一完成定义（DoD）

- 有独立接口契约与默认实现，且支持注册多个实现。
- 有示例扩展（至少 1 个）证明“新增功能不改底层”。
- 有回归测试（Runtime + Editor 关键路径）。
- 有文档：接入步骤、约束、常见错误。
- 关键核心文件复杂度下降（行数/圈复杂度/重复率可度量）。
