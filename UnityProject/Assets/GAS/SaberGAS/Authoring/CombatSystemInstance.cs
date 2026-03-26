using System;
using Saber.GAS.Runtime;

namespace Saber.GAS.Authoring
{
    /// <summary>
    /// 一次 GAS 系统初始化后的运行时句柄。
    /// </summary>
    public sealed class CombatSystemInstance : IDisposable
    {
        public CombatSystemInstance(
            CombatDefinitionCatalogAsset config,
            CombatAuthoringBuildContext buildContext,
            CombatWorldState worldState,
            CombatRuntime runtime)
        {
            Config = config;
            BuildContext = buildContext;
            WorldState = worldState;
            Runtime = runtime;
        }

        /// <summary>
        /// 初始化所使用的根配置资产。
        /// </summary>
        public CombatDefinitionCatalogAsset Config { get; }

        /// <summary>
        /// 本轮初始化构建出的运行时定义缓存。
        /// </summary>
        public CombatAuthoringBuildContext BuildContext { get; }

        /// <summary>
        /// 当前系统持有的战斗世界状态。
        /// </summary>
        public CombatWorldState WorldState { get; }

        /// <summary>
        /// 当前系统持有的战斗运行时。
        /// </summary>
        public CombatRuntime Runtime { get; }

        /// <summary>
        /// 关闭运行时并回收内部池。
        /// </summary>
        public void Dispose()
        {
            Runtime?.Shutdown();
        }
    }
}
