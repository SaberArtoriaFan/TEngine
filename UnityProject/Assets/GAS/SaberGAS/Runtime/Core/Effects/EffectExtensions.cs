using Saber.GAS.Pooling;

namespace Saber.GAS.Effects
{
    /// <summary>
    /// 效果定义上的扩展数据接口。
    /// Core 只负责存放这些扩展，不解释具体语义。
    /// </summary>
    public interface ICombatEffectExtensionDefinition
    {
    }

    /// <summary>
    /// 持续效果实例上的扩展运行时状态接口。
    /// 具体状态由上层模块创建、维护和回收。
    /// </summary>
    public interface ICombatActiveEffectExtensionState : ICombatPoolable
    {
    }
}
