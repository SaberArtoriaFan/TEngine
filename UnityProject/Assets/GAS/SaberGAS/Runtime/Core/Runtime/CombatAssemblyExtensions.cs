using System;
using System.Collections.Generic;

namespace Saber.GAS.Runtime
{
    /// <summary>
    /// 程序集级战斗扩展注册表接口。
    /// 源码生成器会为每个程序集生成一个实现，把该程序集内可自动发现的扩展实例统一暴露出来。
    /// </summary>
    public interface ICombatAssemblyExtensionRegistry
    {
        /// <summary>
        /// 获取该程序集提供的行动阻断扩展列表。
        /// </summary>
        IReadOnlyList<ICombatActionGate> ActionGates { get; }

        /// <summary>
        /// 获取该程序集提供的 Impact 改写扩展列表。
        /// </summary>
        IReadOnlyList<ICombatImpactMutator> ImpactMutators { get; }

        /// <summary>
        /// 获取该程序集提供的 Impact 解析扩展列表。
        /// </summary>
        IReadOnlyList<ICombatImpactResolver> ImpactResolvers { get; }

        /// <summary>
        /// 获取该程序集提供的规则模块列表。
        /// </summary>
        IReadOnlyList<ICombatRuleModule> RuleModules { get; }

        /// <summary>
        /// 获取该程序集提供的扩展克隆处理器列表。
        /// </summary>
        IReadOnlyList<ICombatExtensionCloneHandler> ExtensionCloneHandlers { get; }
    }

    /// <summary>
    /// 全局程序集扩展注册中心。
    /// 由源码生成出的模块初始化代码在程序集加载时自动向这里注册程序集级扩展注册表。
    /// </summary>
    public static class CombatAssemblyExtensionRegistryHub
    {
        /// <summary>
        /// 保护注册表列表并发访问的同步锁。
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// 当前进程内已注册的程序集级扩展注册表列表。
        /// </summary>
        private static readonly List<ICombatAssemblyExtensionRegistry> Registries = new List<ICombatAssemblyExtensionRegistry>();

        /// <summary>
        /// 注册一个程序集级扩展注册表。
        /// 相同引用的注册表不会被重复加入。
        /// </summary>
        public static void Register(ICombatAssemblyExtensionRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            lock (SyncRoot)
            {
                for (var i = 0; i < Registries.Count; i++)
                {
                    if (ReferenceEquals(Registries[i], registry))
                    {
                        return;
                    }
                }

                Registries.Add(registry);
            }
        }

        /// <summary>
        /// 获取当前已注册程序集扩展注册表的快照。
        /// Runtime 和深拷贝提供器会基于这个快照构建自己的组合扩展视图。
        /// </summary>
        public static IReadOnlyList<ICombatAssemblyExtensionRegistry> Snapshot()
        {
            lock (SyncRoot)
            {
                return Registries.ToArray();
            }
        }
    }

    /// <summary>
    /// 组合多个程序集扩展注册表与手工注入扩展后的统一扩展视图。
    /// Runtime 和深拷贝提供器都通过它获取最终可用的扩展实例列表。
    /// </summary>
    internal sealed class CompositeCombatAssemblyExtensions
    {
        /// <summary>
        /// 使用程序集扩展注册表快照和手工注入扩展创建组合扩展视图。
        /// </summary>
        public CompositeCombatAssemblyExtensions(
            IReadOnlyList<ICombatAssemblyExtensionRegistry> registries,
            IEnumerable<ICombatActionGate> actionGates,
            IEnumerable<ICombatImpactMutator> impactMutators,
            IEnumerable<ICombatImpactResolver> impactResolvers,
            IEnumerable<ICombatRuleModule> ruleModules,
            IEnumerable<ICombatExtensionCloneHandler> extensionCloneHandlers)
        {
            ActionGates = BuildInterfaceList(registries, static registry => registry.ActionGates, actionGates);
            ImpactMutators = BuildInterfaceList(registries, static registry => registry.ImpactMutators, impactMutators);
            ImpactResolvers = BuildInterfaceList(registries, static registry => registry.ImpactResolvers, impactResolvers);
            RuleModules = BuildInterfaceList(registries, static registry => registry.RuleModules, ruleModules);
            ExtensionCloneHandlers = BuildInterfaceList(registries, static registry => registry.ExtensionCloneHandlers, extensionCloneHandlers);
        }

        /// <summary>
        /// 获取组合后的行动阻断扩展列表。
        /// </summary>
        public IReadOnlyList<ICombatActionGate> ActionGates { get; }

        /// <summary>
        /// 获取组合后的 Impact 改写扩展列表。
        /// </summary>
        public IReadOnlyList<ICombatImpactMutator> ImpactMutators { get; }

        /// <summary>
        /// 获取组合后的 Impact 解析扩展列表。
        /// </summary>
        public IReadOnlyList<ICombatImpactResolver> ImpactResolvers { get; }

        /// <summary>
        /// 获取组合后的规则模块列表。
        /// </summary>
        public IReadOnlyList<ICombatRuleModule> RuleModules { get; }

        /// <summary>
        /// 获取组合后的扩展克隆处理器列表。
        /// </summary>
        public IReadOnlyList<ICombatExtensionCloneHandler> ExtensionCloneHandlers { get; }

        /// <summary>
        /// 构建某一类接口扩展的最终列表。
        /// 先合并源码生成注册表中的实例，再合并手工注入实例，并按引用去重。
        /// </summary>
        private static IReadOnlyList<T> BuildInterfaceList<T>(
            IReadOnlyList<ICombatAssemblyExtensionRegistry> registries,
            Func<ICombatAssemblyExtensionRegistry, IReadOnlyList<T>> selector,
            IEnumerable<T> manualEntries)
            where T : class
        {
            var results = new List<T>();

            if (registries != null)
            {
                for (var registryIndex = 0; registryIndex < registries.Count; registryIndex++)
                {
                    var registry = registries[registryIndex];
                    if (registry == null)
                    {
                        continue;
                    }

                    AppendUnique(results, selector(registry));
                }
            }

            AppendUnique(results, manualEntries);
            return results.ToArray();
        }

        /// <summary>
        /// 把一组扩展实例按引用去重后追加到结果列表。
        /// </summary>
        private static void AppendUnique<T>(IList<T> results, IEnumerable<T> entries)
            where T : class
        {
            if (results == null || entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (entry == null || ContainsReference(results, entry))
                {
                    continue;
                }

                results.Add(entry);
            }
        }

        /// <summary>
        /// 判断目标列表中是否已经存在同一对象引用。
        /// </summary>
        private static bool ContainsReference<T>(IList<T> results, T entry)
            where T : class
        {
            if (results == null || entry == null)
            {
                return false;
            }

            for (var i = 0; i < results.Count; i++)
            {
                if (ReferenceEquals(results[i], entry))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
