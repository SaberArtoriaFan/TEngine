# Extension Templates

本目录提供可直接复制的扩展模板（`.cs.txt`），用于快速创建：

- TriggerAction 描述符注册表
- ImpactOperation 处理器与注册表
- CustomTriggerAction 与注册表

## 使用方式

1. 复制目标模板到你的扩展程序集目录。
2. 改名为 `.cs`。
3. 替换命名空间、Id、Cue、Payload 与业务逻辑。
4. 在 Runtime 装配层（如 ModulePack）注册。
5. 为扩展补最小回归测试。
