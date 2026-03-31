using System;
using System.Collections.Generic;
using Saber.GAS.Runtime;

namespace Saber.GAS.Examples
{
    /// <summary>
    /// Observer channel sample for UI/log side subscriptions.
    /// </summary>
    public static class CombatDomainEventObserverExamples
    {
        public static IDisposable AttachObserverLogger(CombatRuntime runtime, Action<string> logger = null)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            return runtime.DomainEventBus.Subscribe(CombatDomainEventChannel.Observer, domainEvent =>
            {
                var combatEvent = domainEvent.CombatEvent;
                var message = $"[{domainEvent.Sequence}] {combatEvent.Kind} Actor={combatEvent.ActorId.Value} Msg={combatEvent.Message}";
                if (logger != null)
                {
                    logger(message);
                }
            });
        }
    }

    /// <summary>
    /// In-memory observer recorder used by examples and smoke checks.
    /// </summary>
    public sealed class CombatDomainEventRecorder : IDisposable
    {
        private readonly List<CombatDomainEvent> _events = new List<CombatDomainEvent>();
        private readonly IDisposable _subscription;
        private readonly int _maxCount;

        public CombatDomainEventRecorder(CombatRuntime runtime, int maxCount = 128)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            if (maxCount <= 0)
            {
                maxCount = 1;
            }

            _maxCount = maxCount;
            _subscription = runtime.DomainEventBus.Subscribe(CombatDomainEventChannel.Observer, OnEvent);
        }

        public IReadOnlyList<CombatDomainEvent> Events => _events;

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private void OnEvent(CombatDomainEvent domainEvent)
        {
            if (_events.Count >= _maxCount)
            {
                _events.RemoveAt(0);
            }

            _events.Add(domainEvent);
        }
    }
}
