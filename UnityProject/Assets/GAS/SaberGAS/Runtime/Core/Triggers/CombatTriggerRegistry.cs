using System.Collections.Generic;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Effects;

namespace Saber.GAS.Triggers
{
    /// <summary>
    /// 观察者 Trigger 的全局注册表。
    /// 负责把非 OwnerOnly 的 Trigger 按观察模式和事件类型索引起来，供运行时快速收集候选。
    /// </summary>
    internal sealed class CombatTriggerRegistry
    {
        /// <summary>
        /// 按事件类型索引“观察施加者”的 Trigger 候选列表。
        /// </summary>
        private readonly Dictionary<CombatTriggerEventKind, List<TriggerCandidate>> _instigatorObservers =
            new Dictionary<CombatTriggerEventKind, List<TriggerCandidate>>();

        /// <summary>
        /// 按事件类型索引“观察目标”的 Trigger 候选列表。
        /// </summary>
        private readonly Dictionary<CombatTriggerEventKind, List<TriggerCandidate>> _targetObservers =
            new Dictionary<CombatTriggerEventKind, List<TriggerCandidate>>();

        /// <summary>
        /// 注册一个来自 Actor 本体的观察者 Trigger。
        /// </summary>
        public void RegisterActorTrigger(CombatActorState ownerActor, ActiveTriggerInstance triggerInstance)
        {
            Register(ownerActor, TriggerSourceKind.Actor, null, null, triggerInstance);
        }

        /// <summary>
        /// 注册一个来自效果实例的观察者 Trigger。
        /// </summary>
        public void RegisterEffectTrigger(CombatActorState ownerActor, ActiveEffect sourceEffect, ActiveTriggerInstance triggerInstance)
        {
            Register(ownerActor, TriggerSourceKind.Effect, sourceEffect, null, triggerInstance);
        }

        /// <summary>
        /// 注册一个来自技能实例的观察者 Trigger。
        /// </summary>
        public void RegisterAbilityTrigger(CombatActorState ownerActor, ActiveAbilityInstance sourceAbilityInstance, ActiveTriggerInstance triggerInstance)
        {
            Register(ownerActor, TriggerSourceKind.Ability, null, sourceAbilityInstance, triggerInstance);
        }

        /// <summary>
        /// 从观察者索引中移除一个 Trigger 实例。
        /// </summary>
        public void Unregister(ActiveTriggerInstance triggerInstance)
        {
            if (triggerInstance == null)
            {
                return;
            }

            Remove(_targetObservers, triggerInstance);
            Remove(_instigatorObservers, triggerInstance);
        }

        /// <summary>
        /// 根据当前事件上下文收集观察者 Trigger 候选。
        /// </summary>
        public void Collect(CombatTriggerContext context, IList<TriggerCandidate> results)
        {
            if (context == null || results == null)
            {
                return;
            }

            if (!context.TargetActorId.IsEmpty)
            {
                Append(_targetObservers, context.EventKind, results);
            }

            if (!context.InstigatorActorId.IsEmpty)
            {
                Append(_instigatorObservers, context.EventKind, results);
            }
        }

        /// <summary>
        /// 清空整个观察者索引。
        /// </summary>
        public void Reset()
        {
            _targetObservers.Clear();
            _instigatorObservers.Clear();
        }

        /// <summary>
        /// 把一个观察者 Trigger 注册到对应的索引桶。
        /// </summary>
        private void Register(
            CombatActorState ownerActor,
            TriggerSourceKind sourceKind,
            ActiveEffect sourceEffect,
            ActiveAbilityInstance sourceAbilityInstance,
            ActiveTriggerInstance triggerInstance)
        {
            if (ownerActor == null || triggerInstance == null || triggerInstance.Definition == null)
            {
                return;
            }

            var definition = triggerInstance.Definition;
            //不需要特意注册，自己会调OwnerOnly
            if (definition.CollectionMode == TriggerCollectionMode.OwnerOnly)
            {
                return;
            }

            var lookup = GetLookup(definition.CollectionMode);
            if (lookup == null)
            {
                return;
            }

            var candidate = new TriggerCandidate
            {
                OwnerActor = ownerActor,
                SourceKind = sourceKind,
                SourceEffect = sourceEffect,
                SourceAbilityInstance = sourceAbilityInstance,
                TriggerInstance = triggerInstance,
            };

            var registrationEvent = NormalizeEvent(definition.EventKind);
            List<TriggerCandidate> list;
            if (!lookup.TryGetValue(registrationEvent, out list))
            {
                list = new List<TriggerCandidate>();
                lookup.Add(registrationEvent, list);
            }

            list.Add(candidate);
        }

        /// <summary>
        /// 按收集模式返回对应的观察者索引。
        /// </summary>
        private Dictionary<CombatTriggerEventKind, List<TriggerCandidate>> GetLookup(TriggerCollectionMode mode)
        {
            switch (mode)
            {
                case TriggerCollectionMode.ObserveTarget:
                    return _targetObservers;
                case TriggerCollectionMode.ObserveInstigator:
                    return _instigatorObservers;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 从指定索引中移除某个 Trigger 实例。
        /// </summary>
        private static void Remove(
            IDictionary<CombatTriggerEventKind, List<TriggerCandidate>> lookup,
            ActiveTriggerInstance triggerInstance)
        {
            foreach (var pair in lookup)
            {
                var list = pair.Value;
                for (var index = list.Count - 1; index >= 0; index--)
                {
                    if (ReferenceEquals(list[index].TriggerInstance, triggerInstance))
                    {
                        list.RemoveAt(index);
                    }
                }
            }
        }

        /// <summary>
        /// 把某个事件桶里的候选追加到结果缓冲。
        /// </summary>
        private static void Append(
            IReadOnlyDictionary<CombatTriggerEventKind, List<TriggerCandidate>> lookup,
            CombatTriggerEventKind eventKind,
            IList<TriggerCandidate> results)
        {
            List<TriggerCandidate> list;
            if (!lookup.TryGetValue(NormalizeEvent(eventKind), out list))
            {
                return;
            }

            for (var index = 0; index < list.Count; index++)
            {
                results.Add(list[index]);
            }
        }

        /// <summary>
        /// 归一化注册事件。
        /// 例如资源阈值 Trigger 会统一挂到资源变化事件桶下。
        /// </summary>
        private static CombatTriggerEventKind NormalizeEvent(CombatTriggerEventKind eventKind)
        {
            return eventKind == CombatTriggerEventKind.OnResourceThresholdCrossed
                ? CombatTriggerEventKind.OnResourceChanged
                : eventKind;
        }
    }
}
