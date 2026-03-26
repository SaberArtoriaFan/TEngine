using Saber.GAS.Abilities;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;

namespace Saber.GAS.Networking
{
    /// <summary>
    /// 一次本地预测请求的关联键。
    /// </summary>
    public struct PredictionKey
    {
        /// <summary>
        /// 使用长整型值创建预测键。
        /// </summary>
        public PredictionKey(long value)
        {
            Value = value;
        }

        /// <summary>
        /// 获取预测键原始值。
        /// </summary>
        public long Value { get; }

        /// <summary>
        /// 获取预测键是否有效。
        /// </summary>
        public bool IsValid => Value != 0;
    }

    /// <summary>
    /// 网络权威模式。
    /// </summary>
    public enum NetworkAuthorityMode
    {
        /// <summary>
        /// 单机模式，不区分客户端和服务端。
        /// </summary>
        Standalone = 0,
        /// <summary>
        /// 客户端先预测执行，再等待服务端确认。
        /// </summary>
        ClientPredicted = 1,
        /// <summary>
        /// 服务端拥有最终权威，客户端只提交命令。
        /// </summary>
        ServerAuthoritative = 2,
    }

    public sealed class CombatInputCommand
    {
        /// <summary>
        /// 获取或设置该命令的权威模式。
        /// </summary>
        public NetworkAuthorityMode AuthorityMode { get; set; }

        /// <summary>
        /// 获取或设置命令计划执行 Tick。
        /// </summary>
        public SimulationTick ScheduledTick { get; set; }

        /// <summary>
        /// 获取或设置命令携带的技能激活请求。
        /// </summary>
        public AbilityActivationRequest ActivationRequest { get; set; }
    }

    public sealed class CombatCommandResult
    {
        /// <summary>
        /// 获取或设置命令实际执行 Tick。
        /// </summary>
        public SimulationTick Tick { get; set; }

        /// <summary>
        /// 获取或设置对应的输入命令。
        /// </summary>
        public CombatInputCommand Command { get; set; }

        /// <summary>
        /// 获取或设置命令执行结果。
        /// </summary>
        public AbilityActivationResult Result { get; set; }
    }

    public sealed class CombatSnapshot
    {
        /// <summary>
        /// 获取或设置快照对应的逻辑 Tick。
        /// </summary>
        public SimulationTick Tick { get; set; }

        /// <summary>
        /// 获取或设置快照中的战斗世界状态。
        /// </summary>
        public CombatWorldState WorldState { get; set; }

        /// <summary>
        /// 获取或设置快照附带的扩展载荷。
        /// </summary>
        public object Payload { get; set; }
    }

    public interface ICombatSnapshotStore
    {
        /// <summary>
        /// 保存一份快照。
        /// </summary>
        void Save(CombatSnapshot snapshot);

        /// <summary>
        /// 按 Tick 读取一份快照。
        /// </summary>
        bool TryLoad(SimulationTick tick, out CombatSnapshot snapshot);

        /// <summary>
        /// 读取不晚于指定 Tick 的最近一份快照。
        /// </summary>
        bool TryGetLatestBeforeOrAt(SimulationTick tick, out CombatSnapshot snapshot);
    }

    public interface IResettableCombatSnapshotStore
    {
        /// <summary>
        /// 清空全部快照缓存。
        /// </summary>
        void Clear();
    }

    public interface ICombatReplicator
    {
        /// <summary>
        /// 向网络层推送一份最新快照。
        /// </summary>
        void PushSnapshot(CombatSnapshot snapshot);

        /// <summary>
        /// 向网络层推送一次命令预测结果。
        /// </summary>
        void PushCommandResult(PredictionKey predictionKey, AbilityActivationResult result);
    }
}
