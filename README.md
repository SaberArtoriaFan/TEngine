# TEngineFantasy

基于 `TEngine + HybridCLR + YooAsset + UniTask + Luban` 的 Unity 项目仓库。

## 项目结构

- `UnityProject/`：主 Unity 工程
- `UnityProject/Assets/GameScripts/`：业务逻辑（Main/HotFix）
- `UnityProject/Assets/GAS/SaberGAS/`：GAS 战斗框架与示例
- `openspec/`：变更规范与归档

## 快速开始

1. 用 Unity 2022.3.x 打开 `UnityProject`。
2. 等待编译完成。
3. 打开以下任一示例场景并运行：
   - `Assets/GAS/SaberGAS/Examples/Scenes/GAS_QuickStart_Duel.unity`
   - `Assets/GAS/SaberGAS/Examples/Scenes/GAS_QuickStart_ProjectileDuel.unity`

## Saber.GAS 文档

- [Saber.GAS 主 README](UnityProject/Assets/GAS/SaberGAS/README.md)
- [Saber.GAS 文档入口](UnityProject/Assets/GAS/SaberGAS/Docs/README.md)
- [Saber.GAS 框架手册](UnityProject/Assets/GAS/SaberGAS/Docs/SaberGAS_Framework_Manual.md)
- [Saber.GAS 快速开始](UnityProject/Assets/GAS/SaberGAS/Docs/SaberGAS_QuickStart_Examples.md)
- [Saber.GAS Extension SDK](UnityProject/Assets/GAS/SaberGAS/Docs/SaberGAS_Extension_SDK.md)

## 开发约束（摘录）

- IO 异步优先：使用 `UniTask`。
- 模块访问通过 `GameModule.XXX`。
- 资源加载后必须成对释放。
- 热更边界：`GameScripts/Main` 不热更，`GameScripts/HotFix` 热更。
- 不在 `Assets/TEngine/Runtime` 写项目业务代码。
