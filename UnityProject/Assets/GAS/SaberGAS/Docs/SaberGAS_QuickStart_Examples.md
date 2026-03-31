# Saber.GAS 快速开始（示例场景）

本文用于“最快看到框架跑起来”，并且能定位链路是否生效。

## 1. 前置条件

- Unity 版本：`2022.3.x`（与当前工程一致）。
- 场景位于：`Assets/GAS/SaberGAS/Examples/Scenes/`
- 关键示例脚本位于：`Assets/GAS/SaberGAS/Examples/QuickStart/`

## 2. 一分钟冒烟测试

1. 打开场景：`GAS_QuickStart_Duel.unity`
2. 选中 `GasQuickStartDuelRunner` 所在对象。
3. 点击 Play。
4. 观察 Inspector：
   - Tick 持续增长。
   - Actor A / B 生命值持续下降。
5. 若挂了 `GasActorRuntimeDisplay`：
   - 切换到 `Ability` / `Effect` / `Trigger` / `ResourceSet` 区域检查状态。

通过标准：双方会互相施放即时伤害技能，至少一方生命降到 0。

## 3. 示例 A：即时伤害 + 双 Actor 对殴

场景：`GAS_QuickStart_Duel.unity`

该场景会自动完成：

- 创建两个 Actor（不同队伍）。
- 创建并授予即时伤害 Ability。
- 定时互相施法并结算伤害。

### 可调参数（Runner Inspector）

- `_secondsPerTick`：Tick 速度。
- `_ticksBetweenAttacks`：互殴频率。
- `_startingHealth`：初始血量。
- `_instantDamage`：单次伤害。
- `_attackRange`：最大施法距离。

## 4. 示例 B：投射物互射

场景：`GAS_QuickStart_ProjectileDuel.unity`

该场景会自动完成：

- 双方循环发射投射物。
- 投射物命中后造成伤害。
- 触发 Spawn / Hit / Expire 事件用于表现层。

### 可调参数（Runner Inspector）

- `_trackingMode`：追踪 Actor 或按固定点飞行。
- `_speedPerTick`：飞行速度。
- `_hitRadius`：命中半径。
- `_lifetimeTicks`：最大存活 Tick。
- `_aimAngleOffsetDegrees`：偏转角。

## 5. 如何快速判断“框架是否正常生效”

满足以下 6 条即可判定主链路正常：

1. 能进入 Play 且不报运行时异常。
2. Tick 递增。
3. `TryActivate` 返回成功（可从日志观察）。
4. Resource（生命）发生变化。
5. 至少一个 Trigger 分支被执行。
6. 事件（AbilityActivated / ProjectileHit 等）可被观察到。

## 6. 表现层接入建议（先跑通再精修）

1. 先使用默认 Primitive 占位，验证逻辑和事件。
2. 再替换成正式模型 Prefab。
3. 最后接 Animator 参数（Spawn/Hit/Expire/Speed）做动画细节。

## 7. 常见排障

- 场景能跑但看不到投射物：
  - 检查 `_projectilePrefab` 或 `_usePrimitiveWhenPrefabMissing`。
- 投射物飞行方向异常：
  - 检查 `_trackingMode` 与 `_aimAngleOffsetDegrees`。
- Inspector 没有数据：
  - 检查 `GasActorRuntimeDisplay` 的 `_runtimeProvider` 和 `_actorId` 绑定。

## 8. 下一步

- 复制 QuickStart 逻辑到项目 `Example` 层。
- 替换 Ability/Effect/Trigger 资产为项目配置。
- 按 `SaberGAS_Extension_SDK.md` 增加自定义 Action/Operation。
