using System;
using System.Collections.Generic;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Attributes;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Resources;
using Saber.GAS.Runtime;
using Saber.GAS.Tags;
using Saber.GAS.Triggers;

namespace Saber.GAS.Serialization
{
    /// <summary>
    /// 基于显式对象图重建的深拷贝提供者。
    /// 当前主要负责克隆 CombatWorldState 及其嵌套运行时状态。
    /// </summary>
    public sealed class FastClonerDeepCloneProvider : IDeepCloneProvider
    {
        /// <summary>
        /// 负责克隆语义扩展定义与运行时状态的扩展处理器集合。
        /// </summary>
        private readonly IReadOnlyList<ICombatExtensionCloneHandler> _cloneHandlers;

        /// <summary>
        /// 创建一个不带额外扩展克隆处理器的深拷贝提供者。
        /// </summary>
        public FastClonerDeepCloneProvider()
            : this(null)
        {
        }

        /// <summary>
        /// 使用指定扩展克隆处理器创建深拷贝提供者。
        /// </summary>
        public FastClonerDeepCloneProvider(IReadOnlyList<ICombatExtensionCloneHandler> cloneHandlers)
        {
            var assemblyExtensions = new CompositeCombatAssemblyExtensions(
                CombatAssemblyExtensionRegistryHub.Snapshot(),
                null,
                null,
                null,
                null,
                cloneHandlers);
            _cloneHandlers = assemblyExtensions.ExtensionCloneHandlers;
        }

        /// <summary>
        /// 深拷贝当前支持的对象。
        /// </summary>
        public T Clone<T>(T source)
        {
            if (source is null)
            {
                return source;
            }

            if (source is CombatWorldState worldState)
            {
                return (T)(object)CloneWorldState(worldState);
            }

            throw new InvalidOperationException(
                $"FastClonerDeepCloneProvider currently supports '{typeof(CombatWorldState).FullName}' only, but received '{typeof(T).FullName}'.");
        }

        /// <summary>
        /// 深拷贝一份完整的战斗世界状态。
        /// </summary>
        private CombatWorldState CloneWorldState(CombatWorldState source)
        {
            var random = default(DeterministicRandom);
            random._state = source.Random.State;

            var clone = new CombatWorldState
            {
                CurrentTick = source.CurrentTick,
                WorldTags = CloneTagContainer(source.WorldTags),
                Random = random,
            };

            clone._nextAbilityInstanceId = source._nextAbilityInstanceId;

            var abilityDefinitionMap = new Dictionary<AbilityDefinition, AbilityDefinition>();
            var effectDefinitionMap = new Dictionary<EffectDefinition, EffectDefinition>();
            var triggerDefinitionMap = new Dictionary<TriggerDefinition, TriggerDefinition>();
            var abilityByIdMap = new Dictionary<AbilityId, AbilityDefinition>();

            foreach (var pair in source.AbilityLookup)
            {
                var abilityClone = CloneAbilityDefinition(pair.Value, abilityDefinitionMap, effectDefinitionMap, triggerDefinitionMap);
                abilityByIdMap[pair.Key] = abilityClone;
                clone.AbilityLookup[pair.Key] = abilityClone;
            }

            foreach (var pair in source.ActorLookup)
            {
                var actorClone = CloneActorState(pair.Value, abilityByIdMap, effectDefinitionMap, triggerDefinitionMap);
                clone.ActorLookup[pair.Key] = actorClone;
            }

            foreach (var globalTrigger in source.GlobalTriggers)
            {
                clone.GlobalTriggers.Add(CloneActiveTriggerInstance(globalTrigger, triggerDefinitionMap, effectDefinitionMap));
            }

            return clone;
        }

        /// <summary>
        /// 深拷贝一个 Actor 运行时状态。
        /// </summary>
        private CombatActorState CloneActorState(
            CombatActorState source,
            IReadOnlyDictionary<AbilityId, AbilityDefinition> abilityByIdMap,
            IDictionary<EffectDefinition, EffectDefinition> effectDefinitionMap,
            IDictionary<TriggerDefinition, TriggerDefinition> triggerDefinitionMap)
        {
            var clone = new CombatActorState(source.ActorId)
            {
                TeamId = source.TeamId,
                Position = source.Position,
                IsAlive = source.IsAlive,
                Tags = CloneTagContainer(source.Tags),
                Attributes = new AttributeSet(),
                Resources = CloneResourceSet(source.Resources),
                GrantedAbilities = new List<AbilityId>(source.GrantedAbilities),
                ActiveEffects = new List<ActiveEffect>(),
                ActiveAbilityInstances = new List<ActiveAbilityInstance>(),
                ActiveTriggers = new List<ActiveTriggerInstance>(),
            };

            foreach (var cooldownPair in source.CooldownEndTicks)
            {
                clone.CooldownEndTicks[cooldownPair.Key] = cooldownPair.Value;
            }

            foreach (var tagPair in source.TagReferenceCounts)
            {
                clone.TagReferenceCounts[tagPair.Key] = tagPair.Value;
            }

            foreach (var trigger in source.ActiveTriggers)
            {
                clone.ActiveTriggers.Add(CloneActiveTriggerInstance(trigger, triggerDefinitionMap, effectDefinitionMap));
            }

            var activeEffectMap = new Dictionary<ActiveEffect, ActiveEffect>();
            foreach (var activeEffect in source.ActiveEffects)
            {
                var activeEffectClone = CloneActiveEffect(activeEffect, effectDefinitionMap, triggerDefinitionMap);
                activeEffectMap[activeEffect] = activeEffectClone;
                clone.ActiveEffects.Add(activeEffectClone);
            }

            clone.Attributes = CloneAttributeSet(source.Attributes, activeEffectMap);

            foreach (var instance in source.ActiveAbilityInstances)
            {
                clone.ActiveAbilityInstances.Add(CloneActiveAbilityInstance(instance, abilityByIdMap, triggerDefinitionMap, effectDefinitionMap));
            }

            return clone;
        }

        /// <summary>
        /// 深拷贝一组属性集合及其修正来源。
        /// </summary>
        private AttributeSet CloneAttributeSet(AttributeSet source, IReadOnlyDictionary<ActiveEffect, ActiveEffect> activeEffectMap)
        {
            var clone = new AttributeSet();
            foreach (var pair in source.Values)
            {
                var valueClone = new AttributeValue
                {
                    BaseValue = pair.Value.BaseValue,
                };

                foreach (var modifier in pair.Value.ModifierList)
                {
                    valueClone.ModifierList.Add(CloneAttributeModifier(modifier, activeEffectMap));
                }

                clone.Values[pair.Key] = valueClone;
            }

            return clone;
        }

        /// <summary>
        /// 深拷贝单条属性修正，并在需要时重定向其效果来源引用。
        /// </summary>
        private AttributeModifier CloneAttributeModifier(AttributeModifier source, IReadOnlyDictionary<ActiveEffect, ActiveEffect> activeEffectMap)
        {
            object clonedSource = source.Source;
            if (source.Source is ActiveEffect activeEffect && activeEffectMap.TryGetValue(activeEffect, out var mappedEffect))
            {
                clonedSource = mappedEffect;
            }

            return new AttributeModifier
            {
                AttributeId = source.AttributeId,
                ModifierType = source.ModifierType,
                Magnitude = source.Magnitude,
                Source = clonedSource,
            };
        }

        /// <summary>
        /// 深拷贝一个持续效果实例。
        /// </summary>
        private ActiveEffect CloneActiveEffect(
            ActiveEffect source,
            IDictionary<EffectDefinition, EffectDefinition> effectDefinitionMap,
            IDictionary<TriggerDefinition, TriggerDefinition> triggerDefinitionMap)
        {
            var clone = new ActiveEffect
            {
                Spec = CloneEffectSpec(source.Spec, effectDefinitionMap, triggerDefinitionMap),
                StartTick = source.StartTick,
                LastPeriodicTick = source.LastPeriodicTick,
                ExpireTick = source.ExpireTick,
                ActiveTriggers = new List<ActiveTriggerInstance>(),
                ExtensionStates = new List<ICombatActiveEffectExtensionState>(),
            };

            foreach (var trigger in source.ActiveTriggers)
            {
                clone.ActiveTriggers.Add(CloneActiveTriggerInstance(trigger, triggerDefinitionMap, effectDefinitionMap));
            }

            foreach (var extensionState in source.ExtensionStates)
            {
                clone.ExtensionStates.Add(CloneActiveEffectState(extensionState));
            }

            return clone;
        }

        /// <summary>
        /// 深拷贝一次效果快照。
        /// </summary>
        private EffectSpec CloneEffectSpec(
            EffectSpec source,
            IDictionary<EffectDefinition, EffectDefinition> effectDefinitionMap,
            IDictionary<TriggerDefinition, TriggerDefinition> triggerDefinitionMap)
        {
            return new EffectSpec
            {
                Definition = CloneEffectDefinition(source.Definition, effectDefinitionMap, triggerDefinitionMap),
                SourceActorId = source.SourceActorId,
                TargetActorId = source.TargetActorId,
                AppliedTick = source.AppliedTick,
                Stacks = source.Stacks,
            };
        }

        /// <summary>
        /// 深拷贝一个效果定义，并递归克隆其扩展与 Trigger 定义。
        /// </summary>
        private EffectDefinition CloneEffectDefinition(
            EffectDefinition source,
            IDictionary<EffectDefinition, EffectDefinition> effectDefinitionMap,
            IDictionary<TriggerDefinition, TriggerDefinition> triggerDefinitionMap)
        {
            if (source == null)
            {
                return null;
            }

            EffectDefinition existing;
            if (effectDefinitionMap.TryGetValue(source, out existing))
            {
                return existing;
            }

            var clone = new EffectDefinition(source.Id)
            {
                DurationTicks = source.DurationTicks,
                PeriodTicks = source.PeriodTicks,
                MaxStacks = source.MaxStacks,
                DurationPolicy = source.DurationPolicy,
                StackPolicy = source.StackPolicy,
            };

            effectDefinitionMap[source] = clone;

            clone.EffectTags = CloneTagContainer(source.EffectTags);
            clone.GrantedTags = CloneTagContainer(source.GrantedTags);
            clone.RequiredTargetTags = CloneTagContainer(source.RequiredTargetTags);
            clone.BlockedTargetTags = CloneTagContainer(source.BlockedTargetTags);
            clone.RemovedTargetEffectTags = CloneTagContainer(source.RemovedTargetEffectTags);

            foreach (var effectId in source.RemovedTargetEffectIds)
            {
                clone.RemovedTargetEffectIds.Add(effectId);
            }

            foreach (var modifier in source.AttributeModifiers)
            {
                clone.AttributeModifiers.Add(modifier);
            }

            foreach (var delta in source.InstantResourceDeltas)
            {
                clone.InstantResourceDeltas.Add(delta);
            }

            foreach (var delta in source.PeriodicResourceDeltas)
            {
                clone.PeriodicResourceDeltas.Add(delta);
            }

            foreach (var extension in source.Extensions)
            {
                clone.Extensions.Add(CloneEffectExtension(extension));
            }

            foreach (var trigger in source.Triggers)
            {
                clone.Triggers.Add(CloneTriggerDefinition(trigger, triggerDefinitionMap, effectDefinitionMap));
            }

            return clone;
        }

        /// <summary>
        /// 深拷贝一个运行中的技能实例。
        /// </summary>
        private ActiveAbilityInstance CloneActiveAbilityInstance(
            ActiveAbilityInstance source,
            IReadOnlyDictionary<AbilityId, AbilityDefinition> abilityByIdMap,
            IDictionary<TriggerDefinition, TriggerDefinition> triggerDefinitionMap,
            IDictionary<EffectDefinition, EffectDefinition> effectDefinitionMap)
        {
            var clone = new ActiveAbilityInstance
            {
                InstanceId = source.InstanceId,
                SourceActorId = source.SourceActorId,
                Ability = abilityByIdMap.TryGetValue(source.Ability.Id, out var mappedAbility)
                    ? mappedAbility
                    : null,
                CreatedTick = source.CreatedTick,
                ResolveTick = source.ResolveTick,
                ActivatedTick = source.ActivatedTick,
                NextPeriodicTick = source.NextPeriodicTick,
                ExpireTick = source.ExpireTick,
                State = source.State,
                InitialExecutionCompleted = source.InitialExecutionCompleted,
                EndExecutionCompleted = source.EndExecutionCompleted,
                LockedTargetActorIds = new List<ActorId>(source.LockedTargetActorIds),
                LockedTargetPoint = source.LockedTargetPoint,
                ActiveTriggers = new List<ActiveTriggerInstance>(),
            };

            foreach (var trigger in source.ActiveTriggers)
            {
                clone.ActiveTriggers.Add(CloneActiveTriggerInstance(trigger, triggerDefinitionMap, effectDefinitionMap));
            }

            return clone;
        }

        /// <summary>
        /// 深拷贝一个技能定义。
        /// </summary>
        private AbilityDefinition CloneAbilityDefinition(
            AbilityDefinition source,
            IDictionary<AbilityDefinition, AbilityDefinition> abilityDefinitionMap,
            IDictionary<EffectDefinition, EffectDefinition> effectDefinitionMap,
            IDictionary<TriggerDefinition, TriggerDefinition> triggerDefinitionMap)
        {
            if (source == null)
            {
                return null;
            }

            AbilityDefinition existing;
            if (abilityDefinitionMap.TryGetValue(source, out existing))
            {
                return existing;
            }

            var clone = new AbilityDefinition(source.Id)
            {
                Name = source.Name,
                ActivationMode = source.ActivationMode,
                Cooldown = source.Cooldown,
                Targeting = CloneAbilityTargetingDefinition(source.Targeting),
                CastDurationTicks = source.CastDurationTicks,
                ActiveDurationTicks = source.ActiveDurationTicks,
                IntervalTicks = source.IntervalTicks,
                ExecuteEffectsOnActivate = source.ExecuteEffectsOnActivate,
                CancelOnSourceDeath = source.CancelOnSourceDeath,
                AutoActivatePassive = source.AutoActivatePassive,
            };

            abilityDefinitionMap[source] = clone;

            clone.AbilityTags = CloneTagContainer(source.AbilityTags);
            clone.GrantedTagsWhileActive = CloneTagContainer(source.GrantedTagsWhileActive);
            clone.ActivationRequiredTags = CloneTagContainer(source.ActivationRequiredTags);
            clone.ActivationBlockedTags = CloneTagContainer(source.ActivationBlockedTags);

            foreach (var cost in source.Costs)
            {
                clone.Costs.Add(cost);
            }

            foreach (var effect in source.Effects)
            {
                clone.Effects.Add(CloneEffectDefinition(effect, effectDefinitionMap, triggerDefinitionMap));
            }

            foreach (var effect in source.PeriodicEffects)
            {
                clone.PeriodicEffects.Add(CloneEffectDefinition(effect, effectDefinitionMap, triggerDefinitionMap));
            }

            foreach (var effect in source.EndEffects)
            {
                clone.EndEffects.Add(CloneEffectDefinition(effect, effectDefinitionMap, triggerDefinitionMap));
            }

            foreach (var trigger in source.Triggers)
            {
                clone.Triggers.Add(CloneTriggerDefinition(trigger, triggerDefinitionMap, effectDefinitionMap));
            }

            return clone;
        }

        /// <summary>
        /// 深拷贝技能目标配置。
        /// </summary>
        private AbilityTargetingDefinition CloneAbilityTargetingDefinition(AbilityTargetingDefinition source)
        {
            return new AbilityTargetingDefinition
            {
                Kind = source.Kind,
                AllowedFlags = source.AllowedFlags,
                MaxRange = source.MaxRange,
                RequiredSourceTags = CloneTagContainer(source.RequiredSourceTags),
                RequiredTargetTags = CloneTagContainer(source.RequiredTargetTags),
                BlockedSourceTags = CloneTagContainer(source.BlockedSourceTags),
                BlockedTargetTags = CloneTagContainer(source.BlockedTargetTags),
            };
        }

        /// <summary>
        /// 深拷贝一个 Trigger 定义。
        /// </summary>
        private TriggerDefinition CloneTriggerDefinition(
            TriggerDefinition source,
            IDictionary<TriggerDefinition, TriggerDefinition> triggerDefinitionMap,
            IDictionary<EffectDefinition, EffectDefinition> effectDefinitionMap)
        {
            if (source == null)
            {
                return null;
            }

            TriggerDefinition existing;
            if (triggerDefinitionMap.TryGetValue(source, out existing))
            {
                return existing;
            }

            var clone = new TriggerDefinition(source.Id)
            {
                Name = source.Name,
                SourceKind = source.SourceKind,
                EventKind = source.EventKind,
                Timing = source.Timing,
                CollectionMode = source.CollectionMode,
                Priority = source.Priority,
                RelationFilter = source.RelationFilter,
                TargetRelationFilter = source.TargetRelationFilter,
                InstigatorRelationFilter = source.InstigatorRelationFilter,
                ObserverMaxDistance = source.ObserverMaxDistance,
                CooldownTicks = source.CooldownTicks,
                MaxTriggerCountPerTick = source.MaxTriggerCountPerTick,
                MaxTriggerCountTotal = source.MaxTriggerCountTotal,
                InitialCharges = source.InitialCharges,
                ConsumeSourceEffectOnTrigger = source.ConsumeSourceEffectOnTrigger,
                RemoveSourceEffectOnTrigger = source.RemoveSourceEffectOnTrigger,
                DisableWhenSourceDead = source.DisableWhenSourceDead,
                DisableWhenTargetDead = source.DisableWhenTargetDead,
                EnabledOnCreate = source.EnabledOnCreate,
            };

            triggerDefinitionMap[source] = clone;

            clone.RequiredOwnerTags = CloneTagContainer(source.RequiredOwnerTags);
            clone.BlockedOwnerTags = CloneTagContainer(source.BlockedOwnerTags);
            clone.RequiredInstigatorTags = CloneTagContainer(source.RequiredInstigatorTags);
            clone.BlockedInstigatorTags = CloneTagContainer(source.BlockedInstigatorTags);
            clone.RequiredTargetTags = CloneTagContainer(source.RequiredTargetTags);
            clone.BlockedTargetTags = CloneTagContainer(source.BlockedTargetTags);
            clone.RequiredIncomingAbilityTags = CloneTagContainer(source.RequiredIncomingAbilityTags);
            clone.BlockedIncomingAbilityTags = CloneTagContainer(source.BlockedIncomingAbilityTags);
            clone.RequiredIncomingEffectTags = CloneTagContainer(source.RequiredIncomingEffectTags);
            clone.BlockedIncomingEffectTags = CloneTagContainer(source.BlockedIncomingEffectTags);
            clone.RequiredImpactTags = CloneTagContainer(source.RequiredImpactTags);
            clone.BlockedImpactTags = CloneTagContainer(source.BlockedImpactTags);
            clone.Threshold = CloneThresholdDefinition(source.Threshold);
            clone.Action = CloneTriggerActionDefinition(source.Action, effectDefinitionMap, triggerDefinitionMap);

            return clone;
        }

        /// <summary>
        /// 深拷贝一个阈值触发定义。
        /// </summary>
        private TriggerThresholdDefinition CloneThresholdDefinition(TriggerThresholdDefinition source)
        {
            return source == null
                ? new TriggerThresholdDefinition()
                : new TriggerThresholdDefinition(source.ResourceId, source.Value, source.Direction);
        }

        /// <summary>
        /// 深拷贝一个 Trigger 动作定义。
        /// </summary>
        private TriggerActionDefinition CloneTriggerActionDefinition(
            TriggerActionDefinition source,
            IDictionary<EffectDefinition, EffectDefinition> effectDefinitionMap,
            IDictionary<TriggerDefinition, TriggerDefinition> triggerDefinitionMap)
        {
            if (source == null)
            {
                return new TriggerActionDefinition();
            }

            return new TriggerActionDefinition
            {
                Kind = source.Kind,
                SourceActor = source.SourceActor,
                TargetActor = source.TargetActor,
                TriggeredAbilityId = source.TriggeredAbilityId,
                EffectDefinition = CloneEffectDefinition(source.EffectDefinition, effectDefinitionMap, triggerDefinitionMap),
                EffectId = source.EffectId,
                EffectTag = source.EffectTag,
                ResourceId = source.ResourceId,
                ResourceAmount = source.ResourceAmount,
                Tag = source.Tag,
                OperationTemplate = CloneImpactOperation(source.OperationTemplate, effectDefinitionMap, triggerDefinitionMap),
                MagnitudeMultiplier = source.MagnitudeMultiplier,
                CueName = source.CueName,
                AbilityToCancelId = source.AbilityToCancelId,
                CustomActionId = source.CustomActionId,
                CustomPayload = source.CustomPayload,
            };
        }

        /// <summary>
        /// 深拷贝一个 Impact 操作定义。
        /// </summary>
        private CombatImpactOperation CloneImpactOperation(
            CombatImpactOperation source,
            IDictionary<EffectDefinition, EffectDefinition> effectDefinitionMap,
            IDictionary<TriggerDefinition, TriggerDefinition> triggerDefinitionMap)
        {
            if (source == null)
            {
                return null;
            }

            return new CombatImpactOperation
            {
                Type = source.Type,
                ResourceId = source.ResourceId,
                EffectId = source.EffectId,
                Tag = source.Tag,
                Amount = source.Amount,
                EffectSpec = source.EffectSpec == null
                    ? null
                    : CloneEffectSpec(source.EffectSpec, effectDefinitionMap, triggerDefinitionMap),
                CueName = source.CueName,
                Payload = source.Payload,
            };
        }

        /// <summary>
        /// 深拷贝一个 Trigger 运行时实例。
        /// </summary>
        private ActiveTriggerInstance CloneActiveTriggerInstance(
            ActiveTriggerInstance source,
            IDictionary<TriggerDefinition, TriggerDefinition> triggerDefinitionMap,
            IDictionary<EffectDefinition, EffectDefinition> effectDefinitionMap)
        {
            return new ActiveTriggerInstance
            {
                Definition = CloneTriggerDefinition(source.Definition, triggerDefinitionMap, effectDefinitionMap),
                LastTriggeredTick = source.LastTriggeredTick,
                CooldownEndTick = source.CooldownEndTick,
                CountResetTick = source.CountResetTick,
                TriggerCountThisTick = source.TriggerCountThisTick,
                TriggerCountTotal = source.TriggerCountTotal,
                RemainingCharges = source.RemainingCharges,
                Enabled = source.Enabled,
            };
        }

        /// <summary>
        /// 深拷贝一个 Tag 容器。
        /// </summary>
        private GameplayTagContainer CloneTagContainer(GameplayTagContainer source)
        {
            return source == null ? new GameplayTagContainer() : new GameplayTagContainer(source);
        }

        /// <summary>
        /// 通过扩展克隆处理器深拷贝效果扩展定义。
        /// </summary>
        private ICombatEffectExtensionDefinition CloneEffectExtension(ICombatEffectExtensionDefinition source)
        {
            if (source == null)
            {
                return null;
            }

            for (var i = 0; i < _cloneHandlers.Count; i++)
            {
                var handler = _cloneHandlers[i];
                if (handler.CanCloneEffectExtension(source))
                {
                    return handler.CloneEffectExtension(source);
                }
            }

            throw new InvalidOperationException($"No extension clone handler can clone effect extension type '{source.GetType().FullName}'.");
        }

        /// <summary>
        /// 通过扩展克隆处理器深拷贝效果运行时扩展状态。
        /// </summary>
        private ICombatActiveEffectExtensionState CloneActiveEffectState(ICombatActiveEffectExtensionState source)
        {
            if (source == null)
            {
                return null;
            }

            for (var i = 0; i < _cloneHandlers.Count; i++)
            {
                var handler = _cloneHandlers[i];
                if (handler.CanCloneActiveEffectState(source))
                {
                    return handler.CloneActiveEffectState(source);
                }
            }

            throw new InvalidOperationException($"No extension clone handler can clone active effect state type '{source.GetType().FullName}'.");
        }

        /// <summary>
        /// 深拷贝资源集合。
        /// </summary>
        private ResourceSet CloneResourceSet(ResourceSet source)
        {
            var clone = new ResourceSet();
            foreach (var pair in source.Values)
            {
                clone.Values[pair.Key] = new ResourceValue
                {
                    Current = pair.Value.Current,
                    Max = pair.Value.Max,
                    RegenPerTick = pair.Value.RegenPerTick,
                };
            }

            return clone;
        }
    }
}
