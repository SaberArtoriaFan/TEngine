using System;
using System.Collections.Generic;

namespace Saber.GAS.Runtime
{
    public enum CombatDomainEventChannel
    {
        Deterministic = 0,
        Observer = 1,
    }

    public readonly struct CombatDomainEvent
    {
        public CombatDomainEvent(CombatDomainEventChannel channel, CombatEvent combatEvent, long sequence)
        {
            Channel = channel;
            CombatEvent = combatEvent;
            Sequence = sequence;
        }

        public CombatDomainEventChannel Channel { get; }

        public CombatEvent CombatEvent { get; }

        public long Sequence { get; }
    }

    /// <summary>
    /// Runtime 领域事件总线：
    /// Deterministic 通道用于规则链，Observer 通道用于 UI/日志/埋点。
    /// </summary>
    public interface ICombatDomainEventBus : ICombatEventSink
    {
        IDisposable Subscribe(CombatDomainEventChannel channel, Action<CombatDomainEvent> observer);

        void AddSink(CombatDomainEventChannel channel, ICombatEventSink sink);
    }

    public sealed class CombatDomainEventBus : ICombatDomainEventBus
    {
        private readonly object _syncRoot = new object();
        private readonly List<Action<CombatDomainEvent>> _deterministicObservers = new List<Action<CombatDomainEvent>>();
        private readonly List<Action<CombatDomainEvent>> _observerObservers = new List<Action<CombatDomainEvent>>();
        private readonly List<ICombatEventSink> _deterministicSinks = new List<ICombatEventSink>();
        private readonly List<ICombatEventSink> _observerSinks = new List<ICombatEventSink>();
        private long _sequence;

        public IDisposable Subscribe(CombatDomainEventChannel channel, Action<CombatDomainEvent> observer)
        {
            if (observer == null)
            {
                return EmptyDisposable.Instance;
            }

            lock (_syncRoot)
            {
                var list = GetObserverList(channel);
                list.Add(observer);
            }

            return new Subscription(this, channel, observer);
        }

        public void AddSink(CombatDomainEventChannel channel, ICombatEventSink sink)
        {
            if (sink == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                var list = GetSinkList(channel);
                for (var i = 0; i < list.Count; i++)
                {
                    if (ReferenceEquals(list[i], sink))
                    {
                        return;
                    }
                }

                list.Add(sink);
            }
        }

        public void Publish(CombatEvent combatEvent)
        {
            if (combatEvent == null)
            {
                return;
            }

            var sequence = ++_sequence;
            PublishChannel(
                CombatDomainEventChannel.Deterministic,
                combatEvent,
                sequence,
                _deterministicObservers,
                _deterministicSinks);
            PublishChannel(
                CombatDomainEventChannel.Observer,
                combatEvent,
                sequence,
                _observerObservers,
                _observerSinks);
        }

        private void PublishChannel(
            CombatDomainEventChannel channel,
            CombatEvent combatEvent,
            long sequence,
            List<Action<CombatDomainEvent>> observerList,
            List<ICombatEventSink> sinkList)
        {
            Action<CombatDomainEvent>[] observers;
            ICombatEventSink[] sinks;
            lock (_syncRoot)
            {
                observers = observerList.ToArray();
                sinks = sinkList.ToArray();
            }

            var domainEvent = new CombatDomainEvent(channel, combatEvent, sequence);
            for (var i = 0; i < observers.Length; i++)
            {
                observers[i]?.Invoke(domainEvent);
            }

            for (var i = 0; i < sinks.Length; i++)
            {
                sinks[i]?.Publish(combatEvent);
            }
        }

        private List<Action<CombatDomainEvent>> GetObserverList(CombatDomainEventChannel channel)
        {
            return channel == CombatDomainEventChannel.Deterministic
                ? _deterministicObservers
                : _observerObservers;
        }

        private List<ICombatEventSink> GetSinkList(CombatDomainEventChannel channel)
        {
            return channel == CombatDomainEventChannel.Deterministic
                ? _deterministicSinks
                : _observerSinks;
        }

        private void Unsubscribe(CombatDomainEventChannel channel, Action<CombatDomainEvent> observer)
        {
            if (observer == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                GetObserverList(channel).Remove(observer);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly CombatDomainEventBus _owner;
            private readonly CombatDomainEventChannel _channel;
            private readonly Action<CombatDomainEvent> _observer;
            private bool _disposed;

            public Subscription(
                CombatDomainEventBus owner,
                CombatDomainEventChannel channel,
                Action<CombatDomainEvent> observer)
            {
                _owner = owner;
                _channel = channel;
                _observer = observer;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner?.Unsubscribe(_channel, _observer);
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();

            public void Dispose()
            {
            }
        }
    }
}
