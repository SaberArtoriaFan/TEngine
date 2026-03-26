using System;
using System.Collections.Generic;
using Saber.GAS.Abilities;
using Saber.GAS.Foundation;
using Saber.GAS.Pooling;
using Saber.GAS.Runtime;
using Saber.GAS.Serialization;

namespace Saber.GAS.Networking
{
    /// <summary>
    /// 单步仿真执行后的返回结果。
    /// </summary>
    public sealed class CombatSimulationStepResult
    {
        /// <summary>
        /// 创建一份步进结果容器。
        /// </summary>
        public CombatSimulationStepResult()
        {
            ExecutedCommands = new List<CombatCommandResult>();
        }

        /// <summary>
        /// 获取或设置步进开始 Tick。
        /// </summary>
        public SimulationTick BeginTick { get; set; }

        /// <summary>
        /// 获取或设置步进结束 Tick。
        /// </summary>
        public SimulationTick EndTick { get; set; }

        /// <summary>
        /// 获取本次步进执行过的命令结果列表。
        /// </summary>
        public IList<CombatCommandResult> ExecutedCommands { get; }

        /// <summary>
        /// 获取或设置本次步进捕获的快照。
        /// </summary>
        public CombatSnapshot Snapshot { get; set; }
    }

    /// <summary>
    /// 一次回滚重放完成后的统计结果。
    /// </summary>
    public sealed class CombatReplayReport
    {
        /// <summary>
        /// 创建一份回滚重放报告容器。
        /// </summary>
        public CombatReplayReport()
        {
            ReplayedCommands = new List<CombatCommandResult>();
        }

        /// <summary>
        /// 获取或设置回放起始恢复 Tick。
        /// </summary>
        public SimulationTick RestoredTick { get; set; }

        /// <summary>
        /// 获取或设置回放结束 Tick。
        /// </summary>
        public SimulationTick FinalTick { get; set; }

        /// <summary>
        /// 获取本次回放重新执行的命令结果列表。
        /// </summary>
        public IList<CombatCommandResult> ReplayedCommands { get; }
    }

    /// <summary>
    /// 默认的内存快照仓库实现。
    /// </summary>
    public sealed class InMemoryCombatSnapshotStore : ICombatSnapshotStore, IResettableCombatSnapshotStore
    {
        /// <summary>
        /// 缓存按 Tick 排序的快照字典。
        /// </summary>
        private readonly SortedDictionary<long, CombatSnapshot> _snapshots = new SortedDictionary<long, CombatSnapshot>();

        /// <summary>
        /// 按 Tick 覆盖保存一份快照。
        /// </summary>
        public void Save(CombatSnapshot snapshot)
        {
            _snapshots[snapshot.Tick.Value] = snapshot;
        }

        /// <summary>
        /// 按精确 Tick 读取一份快照。
        /// </summary>
        public bool TryLoad(SimulationTick tick, out CombatSnapshot snapshot)
        {
            return _snapshots.TryGetValue(tick.Value, out snapshot);
        }

        /// <summary>
        /// 读取不晚于目标 Tick 的最近一份快照。
        /// </summary>
        public bool TryGetLatestBeforeOrAt(SimulationTick tick, out CombatSnapshot snapshot)
        {
            snapshot = null;

            foreach (var pair in _snapshots)
            {
                if (pair.Key > tick.Value)
                {
                    break;
                }

                snapshot = pair.Value;
            }

            return snapshot != null;
        }

        /// <summary>
        /// 清空所有缓存快照。
        /// </summary>
        public void Clear()
        {
            _snapshots.Clear();
        }
    }

    /// <summary>
    /// 战斗仿真宿主，负责命令调度、快照、恢复、回滚和整场战斗生命周期管理。
    /// </summary>
    public sealed class CombatSimulationHost : IDisposable
    {
        /// <summary>
        /// 缓存深拷贝提供者。
        /// </summary>
        private readonly IDeepCloneProvider _cloneProvider;
        /// <summary>
        /// 缓存命令列表对象池。
        /// </summary>
        private readonly CombatListPool<CombatInputCommand> _commandListPool = new CombatListPool<CombatInputCommand>();
        /// <summary>
        /// 缓存已经执行过的命令历史。
        /// </summary>
        private readonly List<CombatCommandResult> _commandHistory = new List<CombatCommandResult>();
        /// <summary>
        /// 缓存网络复制器。
        /// </summary>
        private readonly ICombatReplicator _replicator;
        /// <summary>
        /// 缓存运行时配置。
        /// </summary>
        private readonly CombatRuntimeOptions _runtimeOptions;
        /// <summary>
        /// 缓存按 Tick 排队的待执行命令。
        /// </summary>
        private readonly SortedDictionary<long, List<CombatInputCommand>> _scheduledCommands = new SortedDictionary<long, List<CombatInputCommand>>();
        /// <summary>
        /// 缓存快照仓库。
        /// </summary>
        private readonly ICombatSnapshotStore _snapshotStore;
        /// <summary>
        /// 缓存当前仿真运行时。
        /// </summary>
        private CombatRuntime _runtime;

        /// <summary>
        /// 使用世界状态、深拷贝器和网络宿主依赖创建战斗仿真宿主。
        /// </summary>
        public CombatSimulationHost(
            CombatWorldState worldState,
            IDeepCloneProvider cloneProvider,
            ICombatSnapshotStore snapshotStore = null,
            ICombatReplicator replicator = null,
            CombatRuntimeOptions runtimeOptions = null)
        {
            _cloneProvider = cloneProvider;
            _snapshotStore = snapshotStore ?? new InMemoryCombatSnapshotStore();
            _replicator = replicator;
            _runtimeOptions = runtimeOptions ?? new CombatRuntimeOptions();
            _runtime = new CombatRuntime(worldState, _runtimeOptions);
        }

        /// <summary>
        /// 获取当前仿真使用的 Runtime。
        /// </summary>
        public CombatRuntime Runtime => _runtime;

        /// <summary>
        /// 获取当前仿真的世界状态。
        /// </summary>
        public CombatWorldState WorldState => _runtime.WorldState;

        /// <summary>
        /// 获取历史命令执行记录。
        /// </summary>
        public IReadOnlyList<CombatCommandResult> CommandHistory => _commandHistory;

        /// <summary>
        /// 将一条外部输入命令排入未来 Tick。
        /// </summary>
        public void EnqueueCommand(CombatInputCommand command)
        {
            if (command == null)
            {
                return;
            }

            var tick = command.ScheduledTick.Value;
            List<CombatInputCommand> commandsAtTick;
            if (!_scheduledCommands.TryGetValue(tick, out commandsAtTick))
            {
                commandsAtTick = _commandListPool.Rent();
                _scheduledCommands[tick] = commandsAtTick;
            }

            commandsAtTick.Add(command);
        }

        /// <summary>
        /// 执行一个仿真步进。
        /// </summary>
        public CombatSimulationStepResult Step(bool captureSnapshot)
        {
            var result = new CombatSimulationStepResult
            {
                BeginTick = WorldState.CurrentTick,
            };

            // Commands execute against the current tick first, then the simulation advances.
            // This keeps command scheduling deterministic for lockstep / replay scenarios.
            ExecuteCommandsForTick(WorldState.CurrentTick, result.ExecutedCommands);
            _runtime.Tick();
            result.EndTick = WorldState.CurrentTick;

            if (captureSnapshot)
            {
                result.Snapshot = CaptureSnapshot();
            }

            return result;
        }

        /// <summary>
        /// 捕获当前世界快照。
        /// </summary>
        public CombatSnapshot CaptureSnapshot()
        {
            if (_cloneProvider == null)
            {
                return null;
            }

            // Snapshot capture is intentionally delegated to a deep-clone boundary so the core
            // runtime can stay serializer-agnostic and swap in FastCloner later.
            var clonedWorld = _cloneProvider.Clone(WorldState);
            var snapshot = new CombatSnapshot
            {
                Tick = WorldState.CurrentTick,
                WorldState = clonedWorld,
                Payload = clonedWorld,
            };

            _snapshotStore.Save(snapshot);

            if (_runtimeOptions.EventSink != null)
            {
                _runtimeOptions.EventSink.Publish(new CombatEvent(CombatEventKind.SnapshotCaptured, ActorId.Empty, snapshot.Tick.ToString()));
            }

            if (_replicator != null)
            {
                _replicator.PushSnapshot(snapshot);
            }

            return snapshot;
        }

        /// <summary>
        /// 尝试从指定 Tick 的快照恢复世界。
        /// </summary>
        public bool TryRestoreSnapshot(SimulationTick tick)
        {
            CombatSnapshot snapshot;
            if (!_snapshotStore.TryLoad(tick, out snapshot))
            {
                return false;
            }

            return RestoreSnapshot(snapshot);
        }

        /// <summary>
        /// 使用给定快照直接恢复当前运行时。
        /// </summary>
        public bool RestoreSnapshot(CombatSnapshot snapshot)
        {
            if (snapshot == null || snapshot.WorldState == null)
            {
                return false;
            }

            var restoredWorld = _cloneProvider == null
                ? snapshot.WorldState
                : _cloneProvider.Clone(snapshot.WorldState);

            if (_runtime != null)
            {
                // Rollback swaps the authoritative world instance. The previous runtime has to
                // release pooled actor/effect/ability-instance state before we replace it.
                _runtime.Shutdown();
            }

            _runtime = new CombatRuntime(restoredWorld, _runtimeOptions);

            if (_runtimeOptions.EventSink != null)
            {
                _runtimeOptions.EventSink.Publish(new CombatEvent(CombatEventKind.SnapshotRestored, ActorId.Empty, snapshot.Tick.ToString()));
            }

            return true;
        }

        /// <summary>
        /// 从某个 Tick 开始做一次回滚重放。
        /// </summary>
        public CombatReplayReport ReplayFrom(SimulationTick fromTick, bool captureSnapshotAfterReplay)
        {
            var report = new CombatReplayReport
            {
                RestoredTick = fromTick,
            };

            CombatSnapshot baseSnapshot;
            if (!_snapshotStore.TryGetLatestBeforeOrAt(fromTick, out baseSnapshot))
            {
                return report;
            }

            if (!RestoreSnapshot(baseSnapshot))
            {
                return report;
            }

            var pendingCommands = _commandListPool.Rent();
            var replayHistory = _commandListPool.Rent();
            CollectScheduledCommandsAfter(baseSnapshot.Tick, pendingCommands);
            CollectHistoryAfter(baseSnapshot.Tick, replayHistory);

            if (_runtimeOptions.EventSink != null)
            {
                _runtimeOptions.EventSink.Publish(new CombatEvent(CombatEventKind.ReplayStarted, ActorId.Empty, fromTick.ToString()));
            }

            try
            {
                // Replay rebuilds the pending command queue from both executed history and future
                // scheduled commands so rollback can deterministically reconstruct the same timeline.
                TrimHistoryAfter(baseSnapshot.Tick);
                ClearScheduledCommands();

                for (var i = 0; i < replayHistory.Count; i++)
                {
                    EnqueueCommand(replayHistory[i]);
                }

                for (var i = 0; i < pendingCommands.Count; i++)
                {
                    EnqueueCommand(pendingCommands[i]);
                }

                while (HasScheduledCommands())
                {
                    var stepResult = Step(false);
                    for (var commandIndex = 0; commandIndex < stepResult.ExecutedCommands.Count; commandIndex++)
                    {
                        report.ReplayedCommands.Add(stepResult.ExecutedCommands[commandIndex]);
                    }
                }
            }
            finally
            {
                _commandListPool.Return(replayHistory);
                _commandListPool.Return(pendingCommands);
            }

            report.FinalTick = WorldState.CurrentTick;

            if (captureSnapshotAfterReplay)
            {
                CaptureSnapshot();
            }

            if (_runtimeOptions.EventSink != null)
            {
                _runtimeOptions.EventSink.Publish(new CombatEvent(CombatEventKind.ReplayCompleted, ActorId.Empty, report.FinalTick.ToString()));
            }

            return report;
        }

        /// <summary>
        /// 执行当前 Tick 排队的命令，并把结果写入历史与输出缓冲。
        /// </summary>
        private void ExecuteCommandsForTick(SimulationTick tick, IList<CombatCommandResult> resultBuffer)
        {
            List<CombatInputCommand> commands;
            if (!_scheduledCommands.TryGetValue(tick.Value, out commands))
            {
                return;
            }

            _scheduledCommands.Remove(tick.Value);

            // Results are persisted in command history so later rollback can restore an older
            // snapshot and rebuild the authoritative timeline from this log.
            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                if (command.ActivationRequest != null)
                {
                    command.ActivationRequest.RequestTick = tick;
                }

                var result = _runtime.TryActivate(command.ActivationRequest);
                var commandResult = new CombatCommandResult
                {
                    Tick = tick,
                    Command = command,
                    Result = result,
                };

                _commandHistory.Add(commandResult);
                resultBuffer.Add(commandResult);

                if (_replicator != null &&
                    command.ActivationRequest != null &&
                    command.ActivationRequest.PredictionKey.IsValid)
                {
                    _replicator.PushCommandResult(command.ActivationRequest.PredictionKey, result);
                }
            }

            _commandListPool.Return(commands);
        }

        /// <summary>
        /// 判断是否还存在待执行的计划命令。
        /// </summary>
        private bool HasScheduledCommands()
        {
            return _scheduledCommands.Count > 0;
        }

        /// <summary>
        /// 结束一场战斗仿真，并释放宿主与 runtime 持有的缓存状态。
        /// </summary>
        public void Shutdown()
        {
            // Host shutdown is the outer-most lifecycle boundary for one battle simulation:
            // clear scheduled/history buffers first, then let runtime release world-owned state.
            ClearScheduledCommands();
            _commandHistory.Clear();

            var resettableSnapshotStore = _snapshotStore as IResettableCombatSnapshotStore;
            if (resettableSnapshotStore != null)
            {
                resettableSnapshotStore.Clear();
            }

            if (_runtime != null)
            {
                _runtime.Shutdown();
            }

            _commandListPool.Reset();
        }

        /// <summary>
        /// 让宿主可以通过 IDisposable 在外层作用域结束时被安全释放。
        /// </summary>
        public void Dispose()
        {
            Shutdown();
        }

        /// <summary>
        /// 收集某个 Tick 之后已经执行过的命令历史。
        /// </summary>
        private void CollectHistoryAfter(SimulationTick tick, IList<CombatInputCommand> commands)
        {
            if (commands == null)
            {
                return;
            }

            commands.Clear();

            for (var i = 0; i < _commandHistory.Count; i++)
            {
                var historyEntry = _commandHistory[i];
                if (historyEntry.Tick <= tick)
                {
                    continue;
                }

                commands.Add(historyEntry.Command);
            }
        }

        /// <summary>
        /// 收集某个 Tick 之后尚未执行的计划命令。
        /// </summary>
        private void CollectScheduledCommandsAfter(SimulationTick tick, IList<CombatInputCommand> commands)
        {
            if (commands == null)
            {
                return;
            }

            commands.Clear();

            foreach (var pair in _scheduledCommands)
            {
                if (pair.Key <= tick.Value)
                {
                    continue;
                }

                for (var i = 0; i < pair.Value.Count; i++)
                {
                    commands.Add(pair.Value[i]);
                }
            }
        }

        /// <summary>
        /// 删除某个 Tick 之后的执行历史。
        /// </summary>
        private void TrimHistoryAfter(SimulationTick tick)
        {
            for (var i = _commandHistory.Count - 1; i >= 0; i--)
            {
                if (_commandHistory[i].Tick > tick)
                {
                    _commandHistory.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 清空全部计划命令，并归还对应的列表缓冲。
        /// </summary>
        private void ClearScheduledCommands()
        {
            // Each tick bucket list is pooled so heavy rollback/resim flows do not allocate a new
            // List<CombatInputCommand> every time the schedule is rebuilt.
            foreach (var pair in _scheduledCommands)
            {
                _commandListPool.Return(pair.Value);
            }

            _scheduledCommands.Clear();
        }
    }
}
