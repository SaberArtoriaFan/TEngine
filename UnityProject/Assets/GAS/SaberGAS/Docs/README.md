# Saber.GAS 文档入口

本文档集覆盖 `Saber.GAS` 当前主线架构（截至 2026-03-31）。

## 先看哪份文档

1. [SaberGAS_Framework_Manual.md](./SaberGAS_Framework_Manual.md)
   - 框架总览、运行时链路、Unity 表现层接入、Inspector 观测。
2. [SaberGAS_QuickStart_Examples.md](./SaberGAS_QuickStart_Examples.md)
   - 直接运行示例场景，验证“两个 Actor 互相造成伤害”和“投射物互射”。
3. [SaberGAS_Extension_SDK.md](./SaberGAS_Extension_SDK.md)
   - 面向商业项目的扩展契约与模板，目标是“扩展不改核心”。
4. [Templates/README.md](./Templates/README.md)
   - 可直接复制的 TriggerAction / ImpactOperation / CustomAction 模板。

## 文档治理说明

- 旧版编号文档（`00_*` 到 `31_*`）已下线，避免与当前可插拔架构重复或冲突。
- 新文档采用“入口 + 专题”结构，按运行链路组织，便于团队协作维护。
- 若新增功能，请优先更新本目录文档，再补充代码注释与示例。
