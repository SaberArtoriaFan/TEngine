# Example 快速开始：Actor Inspector 分区与定义跳转

本文补充说明 `GasActorRuntimeDisplay` 在 QuickStart 示例中的 Inspector 使用方式，重点覆盖：

- 分区查看（AttributeSet / Ability / Effect / Trigger）
- 定义跳转来源（Asset / Code / Missing）
- 投射物示例 `Ability.Example.Projectile.Strike` 的定位链路

## 1. 适用组件

- 运行时显示脚本：`Assets/GAS/SaberGAS/Examples/QuickStart/GasActorRuntimeDisplay.cs`
- 自定义 Inspector：`Assets/GAS/SaberGAS/Examples/QuickStart/Editor/GasActorRuntimeDisplayEditor.cs`

## 2. 分区显示规则

`Section Switch` 切换后，每个分区仅显示自己的运行时信息：

- `Overview`：摘要（数量与基础状态）
- `AttributeSet`：仅 AttributeSet 条目（不混入 ResourceSet）
- `Ability`：Granted + Active Ability 实例
- `Effect`：Active Effect 列表
- `Trigger`：Actor / AbilityInstance / EffectInstance Trigger

## 3. Jump To Definition 使用规则

`Jump To Definition` 区域会根据当前分区展示对应按钮组：

- `Ability` 分区：只显示 Ability 相关按钮
- `Effect` 分区：只显示 Effect 相关按钮
- `Trigger` 分区：只显示 Trigger 相关按钮
- 其它分区：显示提示文本，不混排跳转按钮

每个条目右侧会显示来源状态：

- `Asset`：命中 Authoring ScriptableObject，点击后打开对应资产
- `Code`：未命中 Authoring，已回退命中 QuickStart 代码定义，点击后打开脚本并定位行号
- `Missing`：既未命中 Authoring，也未命中 QuickStart 代码定义

## 4. 投射物示例定位链路

在 `GAS_QuickStart_ProjectileDuel.unity` 中，以下 Id 由代码构建而非 Authoring 资产：

- `Ability.Example.Projectile.Strike`
- `Effect.Example.Projectile.Strike`

点击对应按钮时，Inspector 会显示 `Code`，并跳转到：

- `Assets/GAS/SaberGAS/Examples/QuickStart/GasQuickStartProjectileDuelRunner.cs`

## 5. 常见排查

1. 按钮显示 `Missing`
- 检查该 Id 是否确实存在于当前运行时 Actor 状态。
- 检查是否在 `Assets/GAS/SaberGAS/Examples/QuickStart` 下有对应代码定义。

2. 点击后没有定位到脚本行
- 确认脚本已导入且无编译错误。
- Unity 重新编译后再点击一次（缓存会自动刷新）。
