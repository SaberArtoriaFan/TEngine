# Saber.GAS

`Saber.GAS` 是本项目的战斗能力系统（Gameplay Ability System）实现，重点提供：

- 可组合的 Ability / Effect / Trigger 数据模型
- 运行时可插拔扩展（Action / Operation / Rule / Event）
- 与 Unity 表现层解耦的战斗执行链路

## 文档入口

- [Docs/README.md](./Docs/README.md)
- [Docs/SaberGAS_Framework_Manual.md](./Docs/SaberGAS_Framework_Manual.md)
- [Docs/SaberGAS_QuickStart_Examples.md](./Docs/SaberGAS_QuickStart_Examples.md)
- [Docs/SaberGAS_Extension_SDK.md](./Docs/SaberGAS_Extension_SDK.md)

## 示例场景

- `Assets/GAS/SaberGAS/Examples/Scenes/GAS_QuickStart_Duel.unity`
- `Assets/GAS/SaberGAS/Examples/Scenes/GAS_QuickStart_ProjectileDuel.unity`

## 设计目标

1. 扩展优先，不改核心。
2. 组合优于继承。
3. 规则 deterministic，表现观察者化。
4. 可测试、可回归、可商业化演进。
