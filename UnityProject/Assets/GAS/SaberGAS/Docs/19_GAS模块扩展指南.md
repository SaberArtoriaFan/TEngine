# GAS 模块扩展指南

这份文档回答两个问题：

1. 现在这套模块化 Authoring，到底支不支持你自己新增 `Ability / Effect / Trigger` 模块？
2. 如果你想做一个当前内置模块里还没有的新功能，应该优先改 Authoring，还是要继续改 Runtime？

结论先说在前面：

- 当前框架已经支持新增自定义 `AbilityAuthoringModule`、`EffectAuthoringModule`、`TriggerAuthoringModule`，不需要再额外改一轮底层才能开始用。
- 只要你的新功能本质上是在“给现有 Definition 填值、组合现有规则”，通常只加一个新模块就够了。
- 如果你要发明全新的运行时语义，比如新的 Effect 扩展状态、全新的 Trigger 动作、全新的 Impact 解释器，那就要继续补 Runtime 扩展点。

## 1. 当前模块框架已经支持什么

当前入口代码在这些文件里：

- `Assets/GAS/SaberGAS/Authoring/GasAuthoringModules.cs`
- `Assets/GAS/SaberGAS/Authoring/AbilityDefinitionAsset.cs`
- `Assets/GAS/SaberGAS/Authoring/EffectDefinitionAsset.cs`
- `Assets/GAS/SaberGAS/Authoring/TriggerDefinitionAsset.cs`
- `Assets/GAS/SaberGAS/Editor/GasModuleEditorUtility.cs`

现在的模块系统已经具备下面这些能力：

- 任意 `[Serializable]` 的 `AbilityAuthoringModule` / `EffectAuthoringModule` / `TriggerAuthoringModule` 子类，都会自动出现在 Inspector 的“添加模块”菜单里。
- `BuildDefinition()` 会自动遍历模块列表，并逐个执行模块的 `ApplyTo(...)`。
- 没有专门写自定义绘制器的模块，仍然可以通过通用 `SerializeReference` 面板编辑。
- 模块类型可以通过 `[GasAuthoringModule("中文名", 排序值)]` 决定显示名和排序。
- 如果模块实现了 `ICombatLocalTagModule`，它提供的局部 Tag 会自动进入当前链路的 Tag 候选池。

当前也有几个需要明确告诉使用者的边界：

- 同一个资产里，同一个“模块类型”默认只能添加一次。
- 图编辑器目前只认识内置关系，不会自动把你的自定义模块关系画成边。
- 自定义模块里的 `Tag` / `ResourceId` 字符串字段，默认会走普通文本框；如果你也想要下拉选择器，需要额外补一段 Editor 绘制逻辑。
- 根配置校验目前只覆盖内置高频问题；自定义模块的业务校验，需要你自己补到校验工具里。

## 2. 先判断：你要做的是“组合新配置”还是“发明新语义”

推荐先用这个判断方式：

- 如果你只是想把现有字段打包成一个项目预设，优先新增 Authoring 模块。
- 如果你只是觉得当前面板太分散，想把一组固定搭配封装成一个模块，优先新增 Authoring 模块。
- 如果你希望 `Ability / Effect / Trigger` 在构建后写入一些当前 Runtime 已经存在的字段，优先新增 Authoring 模块。
- 如果你要让 Runtime 学会一套新的结算语义，才需要继续改 Runtime。

可以这样理解：

- Authoring 模块负责“怎么把策划配置翻译成 Definition”。
- Runtime 扩展负责“Definition 到了运行时以后，怎么真正执行新的语义”。

## 3. 不写代码时，怎样先用现有模块拼出新功能

在写新模块之前，建议先确认是不是已有模块组合就能达成目标。

### Ability 常见组合

- 基础信息 + 激活规则 + 标签与过滤 + 效果载荷
- 基础信息 + 目标规则 + 消耗与冷却 + 效果载荷
- 基础信息 + 激活规则 + 触发器载荷

### Effect 常见组合

- 基础信息 + 标签 + 时序规则 + 资源变化
- 基础信息 + 标签 + 属性修正 + 时序规则
- 基础信息 + 标签 + 护盾语义 + 触发器载荷

### Trigger 常见组合

- 基础信息 + 路由 + 标签过滤 + 动作
- 基础信息 + 路由 + 关系限制 + 动作
- 基础信息 + 路由 + 阈值 + 动作

如果你只是想把这几类常见组合变成“项目模板”或者“预设块”，没必要改 Runtime，直接新建 Authoring 模块即可。

## 4. 如何新增一个 Ability 模块

### 第一步：新建模块类

最小要求只有 4 个：

- 继承 `AbilityAuthoringModule`
- 标记 `[Serializable]`
- 用 `[GasAuthoringModule]` 提供 Inspector 标题
- 实现 `ApplyTo(...)`

示例：

```csharp
using System;
using Saber.GAS.Abilities;
using Saber.GAS.Authoring;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    [Serializable, GasAuthoringModule("项目预设/冲刺标签包", 130)]
    public sealed class AbilityDashPresetModule : AbilityAuthoringModule
    {
        [SerializeField, InspectorName("能力标签")]
        private string _abilityTag = "ability.movement.dash";

        [SerializeField, InspectorName("冷却标签")]
        private string _cooldownTag = "cooldown.dash";

        [SerializeField, InspectorName("冷却 Tick")]
        private long _cooldownTicks = 30;

        public override void ApplyTo(
            AbilityDefinition definition,
            AbilityDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            CombatAuthoringUtility.AddTags(definition.AbilityTags, _abilityTag);
            definition.Cooldown = new AbilityCooldownDefinition(
                _cooldownTicks < 0 ? 0 : _cooldownTicks,
                CombatAuthoringUtility.OptionalTag(_cooldownTag));
        }
    }
}
```

保存后，这个模块会自动出现在 `Ability` Inspector 的“添加模块”菜单里，不需要注册表，不需要手工把类型塞进某个列表。

### 第二步：什么时候该写 Ability 模块

适合写成 Ability 模块的情况：

- 你想封装一组固定的激活规则
- 你想封装一组项目约定的 Ability 标签
- 你想把 Cost、Cooldown、Targeting 这些现有能力组合成一个可复用预设

不适合写成 Ability 模块的情况：

- 你只是想引用不同的 Effect 资产，这通常直接配现有“效果载荷”模块就够了
- 你想发明新的 Ability 运行时生命周期，这通常已经超出 Authoring 模块职责

## 5. 如何新增一个 Effect 模块

如果你的需求仍然只是往 `EffectDefinition` 里写现有字段，那么新增 `EffectAuthoringModule` 就够了。

示例：

```csharp
using System;
using Saber.GAS.Authoring;
using Saber.GAS.Effects;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    [Serializable, GasAuthoringModule("项目预设/持续 Buff 签名", 130)]
    public sealed class EffectTimedBuffSignatureModule : EffectAuthoringModule
    {
        [SerializeField, InspectorName("授予标签")]
        private string[] _grantedTags = { "state.buff.project" };

        [SerializeField, InspectorName("持续 Tick")]
        private long _durationTicks = 300;

        [SerializeField, InspectorName("最大层数")]
        private int _maxStacks = 1;

        public override void ApplyTo(
            EffectDefinition definition,
            EffectDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            CombatAuthoringUtility.AddTags(definition.GrantedTags, _grantedTags);
            definition.DurationPolicy = EffectDurationPolicy.Timed;
            definition.DurationTicks = _durationTicks < 0 ? 0 : _durationTicks;
            definition.MaxStacks = _maxStacks < 1 ? 1 : _maxStacks;
        }
    }
}
```

### 什么时候不该只写 Effect 模块

下面这些场景，通常还要继续补 Runtime：

- 你要给 `EffectDefinition.Extensions` 塞入一种全新的扩展定义
- 你要让 `ActiveEffect` 挂上新的扩展状态
- 你要自定义深拷贝行为，让快照和回放也认识你的扩展状态

这类情况请继续看：

- `16_运行时扩展自动注册与拓展指南.md`

## 6. 如何新增一个 Trigger 模块

如果你只是要补一组固定的路由、关系过滤、冷却、Tag 过滤，新增 `TriggerAuthoringModule` 就够了。

示例：

```csharp
using System;
using Saber.GAS.Authoring;
using Saber.GAS.Runtime;
using Saber.GAS.Triggers;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    [Serializable, GasAuthoringModule("项目预设/敌方伤害监听", 130)]
    public sealed class TriggerEnemyDamagePresetModule : TriggerAuthoringModule
    {
        [SerializeField, InspectorName("冷却 Tick")]
        private long _cooldownTicks = 5;

        [SerializeField, InspectorName("Impact 标签")]
        private string[] _requiredImpactTags = { "Impact.Damage" };

        public override void ApplyTo(
            TriggerDefinition definition,
            TriggerDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            definition.RelationFilter = CombatActorRelationFlags.Enemy;
            definition.CooldownTicks = _cooldownTicks < 0 ? 0 : _cooldownTicks;
            CombatAuthoringUtility.AddTags(definition.RequiredImpactTags, _requiredImpactTags);
        }
    }
}
```

### 如果你想要“新的 Trigger 动作”

不要先急着写一个新 Trigger 模块去硬塞业务逻辑。

如果需求是“Trigger 命中后执行一个当前标准动作里没有的新行为”，优先走：

- `TriggerActionKind.Custom`
- `ICombatCustomTriggerAction`

对应说明文档见：

- `15_自定义TriggerAction与源码生成.md`

也就是说：

- 新模块负责“配置长什么样”
- 自定义 Trigger Action 负责“运行时真正怎么执行”

## 7. 如何让你的自定义模块也拥有更好的编辑体验

默认情况下，自定义模块就算不写额外编辑器，也可以正常工作；但如果你想让它和内置模块一样好用，还可以继续扩展下面这些入口。

### 7.1 自定义模块标题和顺序

用：

```csharp
[GasAuthoringModule("中文标题", 130)]
```

其中：

- 第一个参数决定“添加模块”菜单和模块卡片标题
- 第二个参数决定排序

### 7.2 想要 Tag 下拉和检索器

当前 Tag 选择器入口在：

- `Assets/GAS/SaberGAS/Editor/GasEditorUtility.cs`

常用方法：

- `GasEditorUtility.DrawTagEditor(...)`
- `GasEditorUtility.DrawSingleTagEditor(...)`
- `GasEditorUtility.DrawTagDefinitionLibrary(...)`

如果你的自定义模块里只是普通 `string` 字段，默认会显示成文本框。  
如果你也想要和内置模块一样的 Tag 选择器，需要在：

- `Assets/GAS/SaberGAS/Editor/GasModuleEditorUtility.cs`

里给你的模块类型补一个自定义绘制分支。

### 7.3 想要 ResourceId 下拉选择器

当前 ResourceId 选择器入口在：

- `Assets/GAS/SaberGAS/Editor/GasResourceEditorUtility.cs`

常用方法：

- `GasResourceEditorUtility.DrawSingleResourceEditor(...)`
- `GasResourceEditorUtility.DrawResourceIdPopup(...)`

同样地，如果你的自定义模块自己声明了 `string resourceId`，默认仍然只是文本框；想接入全局 ResourceId 词库，需要自己补 Editor 绘制逻辑。

### 7.4 想让模块贡献局部 Tag 词库

如果你的模块要自己维护一组局部 Tag 定义，请实现：

```csharp
ICombatLocalTagModule
```

示例结构：

```csharp
using System;
using System.Collections.Generic;
using Saber.GAS.Triggers;
using UnityEngine;

[Serializable]
public sealed class TriggerLocalRulesModule : TriggerAuthoringModule, ICombatLocalTagModule
{
    [SerializeField]
    private CombatTagDefinitionAuthoringData[] _localTagDefinitions = Array.Empty<CombatTagDefinitionAuthoringData>();

    public IReadOnlyList<CombatTagDefinitionAuthoringData> LocalTagDefinitions => _localTagDefinitions;

    public override void ApplyTo(
        TriggerDefinition definition,
        TriggerDefinitionAsset owner,
        CombatAuthoringBuildContext context)
    {
    }
}
```

这样这些局部 Tag 会自动进入当前链路的候选池。

### 7.5 想让 Workbench 图里也显示你的关系

当前图关系适配器在：

- `Assets/GAS/SaberGAS/Editor/GasGraphAdapter.cs`

如果你的自定义模块引入了新的“资产到资产”关系，比如：

- 自定义 Ability 模块引用了额外的 Effect
- 自定义 Trigger 模块引用了新的 Ability 或 Effect 资产

那你需要手动补：

- 新的 `GasGraphRelationKind`
- 关系读取
- 连线写回
- 连线删除写回

否则 Inspector 可以正常配，但图上不会自动出现那条边。

### 7.6 想把你的规则纳入统一校验

当前校验入口在：

- `Assets/GAS/SaberGAS/Editor/GasAuthoringValidationUtility.cs`

如果你的自定义模块也有“必填字段”“引用不能为空”这类要求，建议把校验补进去。这样：

- 根配置 Inspector 会看到总览级警告
- Workbench 预览也更容易提前发现问题

## 8. 什么时候必须继续改 Runtime

下面这些场景，Authoring 模块本身不够：

- 你要新增一种全新的 TriggerAction 执行逻辑
- 你要新增一种全新的 Effect 扩展语义
- 你要新增一种全新的 Impact 操作解释方式
- 你要让快照、回放、克隆都认识新的扩展状态

推荐的 Runtime 入口如下：

- 新 Trigger 动作：`ICombatCustomTriggerAction`
- 新运行时规则模块：`ICombatRuleModule`
- 新 Impact 改写：`ICombatImpactMutator`
- 新 Impact 解释：`ICombatImpactResolver`
- 新 Effect 扩展克隆：`ICombatExtensionCloneHandler`

详细见：

- `15_自定义TriggerAction与源码生成.md`
- `16_运行时扩展自动注册与拓展指南.md`

## 9. 一次完整扩展通常要动哪些文件

如果你只是加一个新的 Authoring 模块，通常会动这些地方：

- 新增模块类：`Assets/GAS/SaberGAS/Authoring/...`
- 如果要更好 Inspector：`Assets/GAS/SaberGAS/Editor/GasModuleEditorUtility.cs`
- 如果要图连线支持：`Assets/GAS/SaberGAS/Editor/GasGraphAdapter.cs`
- 如果要统一校验：`Assets/GAS/SaberGAS/Editor/GasAuthoringValidationUtility.cs`
- 如果要模板起手：`Assets/GAS/SaberGAS/Editor/GasAuthoringTemplateUtility.cs`
- 如果要样例演示：`Assets/GAS/SaberGAS/Editor/GasSampleCatalogUtility.cs`

如果还要补 Runtime 语义，再继续看：

- `Assets/GAS/SaberGAS/Runtime/...`

## 10. 扩展完成后的验证顺序

建议每次都按这个顺序验证：

1. 保存脚本，确认 Unity 编译通过。
2. 如果这次修改涉及老资产升级，先执行 `Saber.GAS/Normalize Module Assets`，必要时再执行 `Saber.GAS/Force Reserialize GAS Assets`。
3. 在根 `Catalog` 里打开对应 `Ability / Effect / Trigger`，确认新模块能出现在“添加模块”菜单里。
4. 实际把模块挂上去，确认 Inspector 能正常编辑。
5. 运行 Workbench 预览，确认 `BuildDefinition()` 不报错。
6. 如果模块引用了其他资产，确认图关系是否符合预期。
7. 如果你新增了全局 Tag 或全局 ResourceId，同步运行一次全局 Id 源码生成。

## 11. 当前这套方案最适合怎样的团队分工

推荐这样协作：

- 策划或系统同学：优先用现有模块拼装功能
- 框架同学：把高频组合沉淀成新的 Authoring 模块
- 底层同学：只在确实需要新运行时语义时，继续补 Runtime 扩展点

这样可以避免两种常见问题：

- 明明只是配置组合，却动了太多 Runtime
- 明明已经是新语义，却硬塞进旧模块里，最后把 Authoring 和 Runtime 都搞乱

如果你只是想“更快、更稳地把一组项目约定配置打包出来”，优先新增模块。  
如果你发现自己已经在 `ApplyTo(...)` 里试图发明一套新的运行时解释规则，就该停一下，转去扩展 Runtime 了。
