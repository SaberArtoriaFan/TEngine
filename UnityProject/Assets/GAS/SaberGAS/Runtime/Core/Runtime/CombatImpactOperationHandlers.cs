using System;
using System.Collections.Generic;
using Saber.GAS.Abilities;

namespace Saber.GAS.Runtime
{
    public readonly struct CombatImpactOperationExecutionContext
    {
        public CombatImpactOperationExecutionContext(
            CombatRuntime runtime,
            CombatActionAttempt attempt,
            CombatImpact impact,
            CombatImpactOperation operation,
            AbilityExecutionRecord record)
        {
            Runtime = runtime;
            Attempt = attempt;
            Impact = impact;
            Operation = operation;
            Record = record;
        }

        public CombatRuntime Runtime { get; }

        public CombatActionAttempt Attempt { get; }

        public CombatImpact Impact { get; }

        public CombatImpactOperation Operation { get; }

        public AbilityExecutionRecord Record { get; }
    }

    /// <summary>
    /// ImpactOperation 处理器：按规则声明是否可处理，并在命中后执行。
    /// </summary>
    public interface IImpactOperationHandler
    {
        bool CanHandle(CombatImpactOperation operation);

        bool Execute(CombatImpactOperationExecutionContext context);
    }

    public interface IImpactOperationHandlerRegistry
    {
        IReadOnlyList<IImpactOperationHandler> Handlers { get; }
    }

    public static class CombatImpactOperationHandlerRegistryHub
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<IImpactOperationHandlerRegistry> Registries = new List<IImpactOperationHandlerRegistry>();
        private static readonly IImpactOperationHandlerRegistry DefaultRegistry = new DefaultImpactOperationHandlerRegistry();

        public static void Register(IImpactOperationHandlerRegistry registry)
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

        public static IReadOnlyList<IImpactOperationHandlerRegistry> Snapshot()
        {
            lock (SyncRoot)
            {
                return Registries.ToArray();
            }
        }

        internal static CompositeCombatImpactOperationHandlerRegistry CreateComposite(
            IEnumerable<IImpactOperationHandlerRegistry> runtimeRegistries = null,
            IEnumerable<IImpactOperationHandler> runtimeHandlers = null)
        {
            return new CompositeCombatImpactOperationHandlerRegistry(DefaultRegistry, Registries, runtimeRegistries, runtimeHandlers);
        }
    }

    internal sealed class CompositeCombatImpactOperationHandlerRegistry
    {
        private readonly IImpactOperationHandler[] _handlers;

        public CompositeCombatImpactOperationHandlerRegistry(
            IImpactOperationHandlerRegistry defaultRegistry,
            IEnumerable<IImpactOperationHandlerRegistry> globalRegistries,
            IEnumerable<IImpactOperationHandlerRegistry> runtimeRegistries,
            IEnumerable<IImpactOperationHandler> runtimeHandlers)
        {
            var handlers = new List<IImpactOperationHandler>(32);
            AppendRegistry(handlers, runtimeRegistries);
            Append(handlers, runtimeHandlers);
            AppendRegistry(handlers, globalRegistries);
            AppendRegistry(handlers, defaultRegistry == null ? null : new[] { defaultRegistry });
            _handlers = handlers.ToArray();
        }

        public bool TryExecute(CombatImpactOperationExecutionContext context)
        {
            var operation = context.Operation;
            if (operation == null)
            {
                return false;
            }

            for (var i = 0; i < _handlers.Length; i++)
            {
                var handler = _handlers[i];
                if (handler == null || !handler.CanHandle(operation))
                {
                    continue;
                }

                if (handler.Execute(context))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendRegistry(
            IList<IImpactOperationHandler> handlers,
            IEnumerable<IImpactOperationHandlerRegistry> registries)
        {
            if (handlers == null || registries == null)
            {
                return;
            }

            foreach (var registry in registries)
            {
                if (registry?.Handlers == null)
                {
                    continue;
                }

                Append(handlers, registry.Handlers);
            }
        }

        private static void Append(IList<IImpactOperationHandler> handlers, IEnumerable<IImpactOperationHandler> source)
        {
            if (handlers == null || source == null)
            {
                return;
            }

            foreach (var handler in source)
            {
                if (handler != null)
                {
                    handlers.Add(handler);
                }
            }
        }
    }

    internal static class ImpactOperationHandlerCompatibility
    {
        public static IReadOnlyList<IImpactOperationHandler> WrapLegacyResolvers(IReadOnlyList<ICombatImpactResolver> resolvers)
        {
            if (resolvers == null || resolvers.Count == 0)
            {
                return Array.Empty<IImpactOperationHandler>();
            }

            var handlers = new List<IImpactOperationHandler>(resolvers.Count);
            for (var i = 0; i < resolvers.Count; i++)
            {
                if (resolvers[i] != null)
                {
                    handlers.Add(new LegacyImpactResolverHandler(resolvers[i]));
                }
            }

            return handlers.ToArray();
        }

        private sealed class LegacyImpactResolverHandler : IImpactOperationHandler
        {
            private readonly ICombatImpactResolver _resolver;

            public LegacyImpactResolverHandler(ICombatImpactResolver resolver)
            {
                _resolver = resolver;
            }

            public bool CanHandle(CombatImpactOperation operation)
            {
                return _resolver != null && _resolver.CanResolve(operation);
            }

            public bool Execute(CombatImpactOperationExecutionContext context)
            {
                if (_resolver == null)
                {
                    return false;
                }

                _resolver.Resolve(context.Attempt, context.Impact, context.Operation, context.Runtime);
                return true;
            }
        }
    }

    internal sealed class DefaultImpactOperationHandlerRegistry : IImpactOperationHandlerRegistry
    {
        private static readonly IImpactOperationHandler[] HandlerList =
        {
            new ResourceDeltaImpactOperationHandler(),
            new ApplyEffectImpactOperationHandler(),
            new RemoveEffectsByTagImpactOperationHandler(),
            new RemoveEffectByIdImpactOperationHandler(),
            new CueImpactOperationHandler(),
        };

        public IReadOnlyList<IImpactOperationHandler> Handlers => HandlerList;
    }

    internal sealed class ResourceDeltaImpactOperationHandler : IImpactOperationHandler
    {
        public bool CanHandle(CombatImpactOperation operation)
        {
            return operation != null && operation.Type == CombatImpactOperationType.ResourceDelta;
        }

        public bool Execute(CombatImpactOperationExecutionContext context)
        {
            context.Runtime?.ResolveBuiltInImpact(context.Attempt, context.Impact, context.Operation, context.Record);
            return true;
        }
    }

    internal sealed class ApplyEffectImpactOperationHandler : IImpactOperationHandler
    {
        public bool CanHandle(CombatImpactOperation operation)
        {
            return operation != null && operation.Type == CombatImpactOperationType.ApplyEffect;
        }

        public bool Execute(CombatImpactOperationExecutionContext context)
        {
            context.Runtime?.ResolveBuiltInImpact(context.Attempt, context.Impact, context.Operation, context.Record);
            return true;
        }
    }

    internal sealed class RemoveEffectsByTagImpactOperationHandler : IImpactOperationHandler
    {
        public bool CanHandle(CombatImpactOperation operation)
        {
            return operation != null && operation.Type == CombatImpactOperationType.RemoveEffectsByTag;
        }

        public bool Execute(CombatImpactOperationExecutionContext context)
        {
            context.Runtime?.ResolveBuiltInImpact(context.Attempt, context.Impact, context.Operation, context.Record);
            return true;
        }
    }

    internal sealed class RemoveEffectByIdImpactOperationHandler : IImpactOperationHandler
    {
        public bool CanHandle(CombatImpactOperation operation)
        {
            return operation != null && operation.Type == CombatImpactOperationType.RemoveEffectById;
        }

        public bool Execute(CombatImpactOperationExecutionContext context)
        {
            context.Runtime?.ResolveBuiltInImpact(context.Attempt, context.Impact, context.Operation, context.Record);
            return true;
        }
    }

    internal sealed class CueImpactOperationHandler : IImpactOperationHandler
    {
        public bool CanHandle(CombatImpactOperation operation)
        {
            return operation != null && operation.Type == CombatImpactOperationType.Cue;
        }

        public bool Execute(CombatImpactOperationExecutionContext context)
        {
            context.Runtime?.ResolveBuiltInImpact(context.Attempt, context.Impact, context.Operation, context.Record);
            return true;
        }
    }
}
