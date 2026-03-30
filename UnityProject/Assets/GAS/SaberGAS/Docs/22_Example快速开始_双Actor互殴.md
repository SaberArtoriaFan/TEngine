# Example 快速开始：双 Actor 即时伤害互殴

这份文档用于验证 Saber.GAS 的最小闭环是否生效：

- 两个 Actor
- 一个即时伤害能力
- 周期互相造成伤害
- 一方死亡后对局结束

如果这条链路能跑通，说明 `WorldState -> Runtime -> Ability -> Effect -> Resource -> Tick` 主干是正常的。

## 1. 资源位置

- 示例脚本：`Assets/GAS/SaberGAS/Examples/QuickStart/GasQuickStartDuelRunner.cs`
- 示例程序集：`Assets/GAS/SaberGAS/Examples/QuickStart/Saber.GAS.Examples.QuickStart.asmdef`
- 示例场景：`Assets/GAS/SaberGAS/Examples/Scenes/GAS_QuickStart_Duel.unity`

## 2. 30 秒启动

1. 打开场景 `GAS_QuickStart_Duel.unity`
2. 选中对象 `GAS_QuickStart_DuelRunner`
3. 点击 `Play`
4. 在 Console 观察：
   - 对局启动日志
   - 双方血量变化日志
   - 胜负结果日志

## 3. Inspector 常用参数

- `Auto Start On Play`：进入 Play 自动启动
- `Seconds Per Tick`：逻辑 Tick 间隔
- `Ticks Between Attacks`：每隔多少 Tick 发起一次攻击
- `Max Simulation Ticks`：最大仿真 Tick（安全上限）
- `Starting Health`：初始生命值
- `Instant Damage`：每次命中伤害
- `Attack Range`：攻击距离
- `Restart After Finished`：结束后是否自动重启

## 4. 适用场景

这个示例适合作为“框架体检入口”，重点验证：

- 基础运行时能否初始化
- Ability/Effect 调用是否通
- 资源扣减和死亡判定是否通

如果你还要验证投射物飞行、命中表现、动画触发，请继续看文档：

- `20_投射物飞行系统说明.md`
- `21_投射物表现层接入指南.md`
- `23_Example快速开始_投射物互射.md`

## 5. Actor Inspector 实时状态显示

即时伤害示例同样支持在 Actor 身上挂显示脚本（由 Runner 自动挂载）：

- `GasActorRuntimeDisplay`

该显示会实时输出：

- AttributeSet
- Ability
- Effect
- Trigger

以及资源与基础状态，便于你直接在 Inspector 观察框架链路是否正确刷新。
在 `Ability / Effect / Trigger` 标签下点击对应条目，可直接跳转到定义资产查看。
