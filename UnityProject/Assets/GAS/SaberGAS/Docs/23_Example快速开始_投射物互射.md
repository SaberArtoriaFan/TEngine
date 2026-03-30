# Example 快速开始：投射物互射（可调角度/速度/表现层）

这份文档对应一个“可直接运行”的投射物示例场景，目标是帮助你快速确认：

- 投射物逻辑链路是否生效
- 参数调节（角度/速度/寿命）是否生效
- Unity 表现层（模型/动画）是否正确衔接

## 1. 资源位置

- 场景：`Assets/GAS/SaberGAS/Examples/Scenes/GAS_QuickStart_ProjectileDuel.unity`
- Runner：`Assets/GAS/SaberGAS/Examples/QuickStart/GasQuickStartProjectileDuelRunner.cs`

## 2. 一分钟跑起来

1. 打开场景 `GAS_QuickStart_ProjectileDuel.unity`
2. 选中 `GAS_QuickStart_ProjectileDuelRunner`
3. 点击 `Play`
4. 观察 Runner 调试字段（Inspector）：
   - `_debugTick`：持续增长
   - `_debugSpawned`：持续增长（产生投射物）
   - `_debugHit`：持续增长（命中次数）
   - `_debugHpA/_debugHpB`：持续下降
5. 当任一方生命值归零后，互射结束

## 3. 如何调投射物角度与速度

在 `GasQuickStartProjectileDuelRunner` Inspector 中调这些字段：

- `_trackingMode`
  - `TrackActor`：持续追踪目标 Actor
  - `FixedPoint`：朝固定点飞行
- `_speedPerTick`：每 Tick 飞行速度
- `_hitRadius`：命中半径
- `_lifetimeTicks`：最大存活 Tick
- `_aimAngleOffsetDegrees`：仅在 `FixedPoint` 模式下生效，给目标方向增加偏转角

调参建议：

1. 先固定 `_ticksBetweenShots`，只调 `_speedPerTick` 感受飞行节奏
2. 再调 `_lifetimeTicks`，避免“飞不到就过期”
3. 最后在 `FixedPoint` 模式下调 `_aimAngleOffsetDegrees` 做抛射偏角

## 4. Unity 表现层如何配置

示例支持两种显示路径：

- 指定 `_projectilePrefab`：使用你的模型/VFX 预制体
- 不指定 Prefab 且 `_usePrimitiveWhenPrefabMissing=true`：自动使用 `Sphere` 占位体

常用表现参数：

- `_projectileScale`：投射物缩放
- `_terminalVisualDuration`：命中/过期后保留时长
- `_colorA/_colorB`：A/B 阵营颜色
- `_hitColor`：命中颜色
- `_expireColor`：过期颜色

## 5. 如何设置投射物模型动画

建议 Prefab 结构：

- `ProjectileRoot`
- `Mesh`（模型）
- `Animator`（关闭 Root Motion）
- `TrailRenderer`/`ParticleSystem`（可选）

Runner 会自动尝试驱动以下 Animator 参数：

- Trigger `Spawn`
- Trigger `Hit`
- Trigger `Expire`
- Float `Speed`

建议动画状态机：

- `Spawn -> FlyLoop -> (Hit 或 Expire)`

如果 Animator 上没有这些参数，示例仍可运行，只是不会触发对应动画。

## 6. 在 Actor 身上挂载 Inspector 运行时显示脚本

本次已提供 Actor 侧显示脚本：

- `Assets/GAS/SaberGAS/Examples/QuickStart/GasActorRuntimeDisplay.cs`

配套 Provider 接口：

- `Assets/GAS/SaberGAS/Examples/QuickStart/IGasActorRuntimeStateProvider.cs`

两个 QuickStart Runner 已实现该接口并默认自动挂载显示组件：

- `GasQuickStartDuelRunner`
- `GasQuickStartProjectileDuelRunner`

显示内容包括：

- AttributeSet（Base/Current/Modifier 数）
- ResourceSet（Current/Max/Regen）
- Ability（Granted + ActiveInstance）
- Effect（ActiveEffects）
- Trigger（Actor/AbilityInstance/EffectInstance）

你也可以手动挂：

1. 把 `GasActorRuntimeDisplay` 挂到任意 Actor 表现体
2. `Runtime Provider` 选择一个实现 `IGasActorRuntimeStateProvider` 的 Runner
3. `Actor Id` 填运行时 ActorId（例如 `Actor.Example.Projectile.A`）
4. 在组件 Inspector 中用按钮切换查看：
   - `Overview`
   - `AttributeSet`
   - `Ability`
   - `Effect`
   - `Trigger`
5. 在 `Ability / Effect / Trigger` 区域点击条目按钮，可直接定位并打开对应定义资产（按 Id 匹配）。

## 7. 链路说明（Authoring -> Runtime -> Event -> View）

1. Ability 触发 `SpawnProjectile`（ImpactOperation）
2. Runtime 创建并推进 `CombatProjectileState`
3. 事件层抛出 `ProjectileSpawned/ProjectileHit/ProjectileExpired`
4. Runner 通过 `ProjectileInstanceId -> ViewBinding` 维护视图生命周期
5. 命中/过期后播放收尾效果并回收对象

## 8. 常见问题排查

### 7.1 Tick 不增长

- 确认对象上已挂 `GasQuickStartProjectileDuelRunner`
- 确认 `_autoStartOnPlay=true`
- 示例已在 `Start()` 中启用 `Application.runInBackground` 兜底，后台运行也会推进 Tick

### 7.2 看不到投射物

- 确认 `_projectilePrefab` 不为空或 `_usePrimitiveWhenPrefabMissing=true`
- 确认相机能看到双方锚点位置（默认约 `x=-3` 与 `x=3`）

### 7.3 只生成不命中

- 提高 `_hitRadius`
- 提高 `_lifetimeTicks`
- 检查 `_speedPerTick` 是否过低导致寿命内飞不到目标

## 9. 推荐验证顺序

1. 先跑 `GAS_QuickStart_Duel.unity`（即时伤害最短链路）
2. 再跑 `GAS_QuickStart_ProjectileDuel.unity`（投射物与表现层链路）
3. 最后替换成你的 Projectile Prefab 和 Animator，按上面的参数逐项收敛
