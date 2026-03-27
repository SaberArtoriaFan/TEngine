using System;
using System.Collections.Generic;
using Saber.GAS.Abilities;
using Saber.GAS.Foundation;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    public enum AbilityEffectModuleSlot
    {
        Execute = 0,
        Periodic = 1,
        End = 2,
    }

    [Serializable, GasAuthoringModule("基础信息", 0)]
    public sealed class AbilityIdentityModule : AbilityAuthoringModule
    {
        [SerializeField, InspectorName("能力 Id")]
        private string _abilityId;
        [SerializeField, InspectorName("显示名称")]
        private string _displayName;

        public string AbilityId
        {
            get => _abilityId;
            set => _abilityId = value;
        }

        public string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        public AbilityId BuildId(string ownerName)
        {
            return CombatAuthoringUtility.RequireAbilityId(_abilityId, ownerName, nameof(_abilityId));
        }

        public string ResolveDisplayName(string ownerName)
        {
            return string.IsNullOrWhiteSpace(_displayName) ? ownerName : _displayName.Trim();
        }

        public override void ApplyTo(
            AbilityDefinition definition,
            AbilityDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            definition.Name = ResolveDisplayName(owner == null ? string.Empty : owner.name);
        }
    }

    [Serializable, GasAuthoringModule("激活规则", 10)]
    public sealed class AbilityActivationModule : AbilityAuthoringModule
    {
        [SerializeField, InspectorName("激活模式")]
        private AbilityActivationMode _activationMode = AbilityActivationMode.Instant;
        [SerializeField, InspectorName("施法时长 Tick")]
        private long _castDurationTicks;
        [SerializeField, InspectorName("持续时长 Tick")]
        private long _activeDurationTicks;
        [SerializeField, InspectorName("周期 Tick")]
        private long _intervalTicks;
        [SerializeField, InspectorName("激活时立即执行效果")]
        private bool _executeEffectsOnActivate = true;
        [SerializeField, InspectorName("施法者死亡时取消")]
        private bool _cancelOnSourceDeath = true;
        [SerializeField, InspectorName("被动能力自动激活")]
        private bool _autoActivatePassive = true;

        public AbilityActivationMode ActivationMode
        {
            get => _activationMode;
            set => _activationMode = value;
        }

        public long CastDurationTicks
        {
            get => _castDurationTicks;
            set => _castDurationTicks = value;
        }

        public long ActiveDurationTicks
        {
            get => _activeDurationTicks;
            set => _activeDurationTicks = value;
        }

        public long IntervalTicks
        {
            get => _intervalTicks;
            set => _intervalTicks = value;
        }

        public bool ExecuteEffectsOnActivate
        {
            get => _executeEffectsOnActivate;
            set => _executeEffectsOnActivate = value;
        }

        public bool CancelOnSourceDeath
        {
            get => _cancelOnSourceDeath;
            set => _cancelOnSourceDeath = value;
        }

        public bool AutoActivatePassive
        {
            get => _autoActivatePassive;
            set => _autoActivatePassive = value;
        }

        public override void ApplyTo(
            AbilityDefinition definition,
            AbilityDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            definition.ActivationMode = _activationMode;
            definition.CastDurationTicks = _castDurationTicks < 0 ? 0 : _castDurationTicks;
            definition.ActiveDurationTicks = _activeDurationTicks < 0 ? 0 : _activeDurationTicks;
            definition.IntervalTicks = _intervalTicks < 0 ? 0 : _intervalTicks;
            definition.ExecuteEffectsOnActivate = _executeEffectsOnActivate;
            definition.CancelOnSourceDeath = _cancelOnSourceDeath;
            definition.AutoActivatePassive = _autoActivatePassive;
        }
    }

    [Serializable, GasAuthoringModule("局部标签词库", 20)]
    public sealed class AbilityLocalTagLibraryModule : AbilityAuthoringModule, ICombatLocalTagModule
    {
        [SerializeField, InspectorName("局部标签定义")]
        private CombatTagDefinitionAuthoringData[] _localTagDefinitions = Array.Empty<CombatTagDefinitionAuthoringData>();

        public IReadOnlyList<CombatTagDefinitionAuthoringData> LocalTagDefinitions => _localTagDefinitions;

        public CombatTagDefinitionAuthoringData[] Definitions
        {
            get => _localTagDefinitions;
            set => _localTagDefinitions = value ?? Array.Empty<CombatTagDefinitionAuthoringData>();
        }

        public override void ApplyTo(
            AbilityDefinition definition,
            AbilityDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
        }
    }

    [Serializable, GasAuthoringModule("标签与过滤", 30)]
    public sealed class AbilityTagRulesModule : AbilityAuthoringModule
    {
        [SerializeField, InspectorName("能力标签")]
        private string[] _abilityTags = Array.Empty<string>();
        [SerializeField, InspectorName("激活期间授予标签")]
        private string[] _grantedTagsWhileActive = Array.Empty<string>();
        [SerializeField, InspectorName("激活所需标签")]
        private string[] _activationRequiredTags = Array.Empty<string>();
        [SerializeField, InspectorName("激活阻断标签")]
        private string[] _activationBlockedTags = Array.Empty<string>();

        public string[] AbilityTags
        {
            get => _abilityTags;
            set => _abilityTags = value ?? Array.Empty<string>();
        }

        public string[] GrantedTagsWhileActive
        {
            get => _grantedTagsWhileActive;
            set => _grantedTagsWhileActive = value ?? Array.Empty<string>();
        }

        public string[] ActivationRequiredTags
        {
            get => _activationRequiredTags;
            set => _activationRequiredTags = value ?? Array.Empty<string>();
        }

        public string[] ActivationBlockedTags
        {
            get => _activationBlockedTags;
            set => _activationBlockedTags = value ?? Array.Empty<string>();
        }

        public override void ApplyTo(
            AbilityDefinition definition,
            AbilityDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            CombatAuthoringUtility.AddTags(definition.AbilityTags, _abilityTags);
            CombatAuthoringUtility.AddTags(definition.GrantedTagsWhileActive, _grantedTagsWhileActive);
            CombatAuthoringUtility.AddTags(definition.ActivationRequiredTags, _activationRequiredTags);
            CombatAuthoringUtility.AddTags(definition.ActivationBlockedTags, _activationBlockedTags);
        }
    }

    [Serializable, GasAuthoringModule("目标规则", 40)]
    public sealed class AbilityTargetingModule : AbilityAuthoringModule
    {
        [SerializeField, InspectorName("目标设置")]
        private AbilityTargetingAuthoringData _targeting = new AbilityTargetingAuthoringData();

        public AbilityTargetingAuthoringData Targeting
        {
            get => _targeting;
            set => _targeting = value ?? new AbilityTargetingAuthoringData();
        }

        public override void ApplyTo(
            AbilityDefinition definition,
            AbilityDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            _targeting?.ApplyTo(definition?.Targeting);
        }
    }

    [Serializable, GasAuthoringModule("消耗与冷却", 50)]
    public sealed class AbilityCostCooldownModule : AbilityAuthoringModule
    {
        [SerializeField, InspectorName("资源消耗")]
        private ResourceCostAuthoringData[] _costs = Array.Empty<ResourceCostAuthoringData>();
        [SerializeField, InspectorName("冷却 Tick")]
        private long _cooldownTicks;
        [SerializeField, InspectorName("冷却标签")]
        private string _cooldownTag;

        public ResourceCostAuthoringData[] Costs
        {
            get => _costs;
            set => _costs = value ?? Array.Empty<ResourceCostAuthoringData>();
        }

        public long CooldownTicks
        {
            get => _cooldownTicks;
            set => _cooldownTicks = value;
        }

        public string CooldownTag
        {
            get => _cooldownTag;
            set => _cooldownTag = value;
        }

        public override void ApplyTo(
            AbilityDefinition definition,
            AbilityDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            definition.Cooldown = new AbilityCooldownDefinition(
                _cooldownTicks < 0 ? 0 : _cooldownTicks,
                CombatAuthoringUtility.OptionalTag(_cooldownTag));

            for (var i = 0; i < _costs.Length; i++)
            {
                var cost = _costs[i];
                if (cost != null)
                {
                    definition.Costs.Add(cost.Build(owner == null ? string.Empty : owner.name));
                }
            }
        }
    }

    [Serializable, GasAuthoringModule("效果载荷", 60)]
    public sealed class AbilityEffectPayloadModule : AbilityAuthoringModule
    {
        [SerializeField, InspectorName("激活时效果")]
        private EffectDefinitionAsset[] _effects = Array.Empty<EffectDefinitionAsset>();
        [SerializeField, InspectorName("周期效果")]
        private EffectDefinitionAsset[] _periodicEffects = Array.Empty<EffectDefinitionAsset>();
        [SerializeField, InspectorName("结束效果")]
        private EffectDefinitionAsset[] _endEffects = Array.Empty<EffectDefinitionAsset>();

        public IReadOnlyList<EffectDefinitionAsset> Effects => _effects;

        public IReadOnlyList<EffectDefinitionAsset> PeriodicEffects => _periodicEffects;

        public IReadOnlyList<EffectDefinitionAsset> EndEffects => _endEffects;

        public EffectDefinitionAsset[] ImmediateEffectsArray
        {
            get => _effects;
            set => _effects = value ?? Array.Empty<EffectDefinitionAsset>();
        }

        public EffectDefinitionAsset[] PeriodicEffectsArray
        {
            get => _periodicEffects;
            set => _periodicEffects = value ?? Array.Empty<EffectDefinitionAsset>();
        }

        public EffectDefinitionAsset[] EndEffectsArray
        {
            get => _endEffects;
            set => _endEffects = value ?? Array.Empty<EffectDefinitionAsset>();
        }

        public bool AddEffect(AbilityEffectModuleSlot slot, EffectDefinitionAsset effect)
        {
            if (effect == null)
            {
                return false;
            }

            return slot switch
            {
                AbilityEffectModuleSlot.Execute => AppendUnique(ref _effects, effect),
                AbilityEffectModuleSlot.Periodic => AppendUnique(ref _periodicEffects, effect),
                AbilityEffectModuleSlot.End => AppendUnique(ref _endEffects, effect),
                _ => false,
            };
        }

        public bool RemoveEffect(AbilityEffectModuleSlot slot, EffectDefinitionAsset effect)
        {
            if (effect == null)
            {
                return false;
            }

            return slot switch
            {
                AbilityEffectModuleSlot.Execute => RemoveReference(ref _effects, effect),
                AbilityEffectModuleSlot.Periodic => RemoveReference(ref _periodicEffects, effect),
                AbilityEffectModuleSlot.End => RemoveReference(ref _endEffects, effect),
                _ => false,
            };
        }

        public override void ApplyTo(
            AbilityDefinition definition,
            AbilityDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null || context == null)
            {
                return;
            }

            AppendEffects(definition.Effects, _effects, context);
            AppendEffects(definition.PeriodicEffects, _periodicEffects, context);
            AppendEffects(definition.EndEffects, _endEffects, context);
        }

        private static void AppendEffects(
            IList<Saber.GAS.Effects.EffectDefinition> destination,
            EffectDefinitionAsset[] source,
            CombatAuthoringBuildContext context)
        {
            if (destination == null || source == null)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var effect = source[i];
                if (effect != null)
                {
                    destination.Add(context.BuildEffect(effect));
                }
            }
        }

        private static bool AppendUnique(ref EffectDefinitionAsset[] values, EffectDefinitionAsset effect)
        {
            if (values == null)
            {
                values = Array.Empty<EffectDefinitionAsset>();
            }

            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] == effect)
                {
                    return false;
                }
            }

            Array.Resize(ref values, values.Length + 1);
            values[^1] = effect;
            return true;
        }

        private static bool RemoveReference(ref EffectDefinitionAsset[] values, EffectDefinitionAsset effect)
        {
            if (values == null || values.Length == 0)
            {
                return false;
            }

            var index = Array.IndexOf(values, effect);
            if (index < 0)
            {
                return false;
            }

            var next = new EffectDefinitionAsset[values.Length - 1];
            if (index > 0)
            {
                Array.Copy(values, 0, next, 0, index);
            }

            if (index < values.Length - 1)
            {
                Array.Copy(values, index + 1, next, index, values.Length - index - 1);
            }

            values = next;
            return true;
        }
    }

    [Serializable, GasAuthoringModule("触发器载荷", 70)]
    public sealed class AbilityTriggerModule : AbilityAuthoringModule
    {
        [SerializeField, InspectorName("触发器")]
        private TriggerDefinitionAsset[] _triggers = Array.Empty<TriggerDefinitionAsset>();

        public IReadOnlyList<TriggerDefinitionAsset> Triggers => _triggers;

        public TriggerDefinitionAsset[] TriggerArray
        {
            get => _triggers;
            set => _triggers = value ?? Array.Empty<TriggerDefinitionAsset>();
        }

        public bool AddTrigger(TriggerDefinitionAsset trigger)
        {
            return AppendUnique(ref _triggers, trigger);
        }

        public bool RemoveTrigger(TriggerDefinitionAsset trigger)
        {
            return RemoveReference(ref _triggers, trigger);
        }

        public override void ApplyTo(
            AbilityDefinition definition,
            AbilityDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null || context == null)
            {
                return;
            }

            for (var i = 0; i < _triggers.Length; i++)
            {
                var trigger = _triggers[i];
                if (trigger != null)
                {
                    definition.Triggers.Add(context.BuildTrigger(trigger));
                }
            }
        }

        private static bool AppendUnique(ref TriggerDefinitionAsset[] values, TriggerDefinitionAsset trigger)
        {
            if (trigger == null)
            {
                return false;
            }

            if (values == null)
            {
                values = Array.Empty<TriggerDefinitionAsset>();
            }

            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] == trigger)
                {
                    return false;
                }
            }

            Array.Resize(ref values, values.Length + 1);
            values[^1] = trigger;
            return true;
        }

        private static bool RemoveReference(ref TriggerDefinitionAsset[] values, TriggerDefinitionAsset trigger)
        {
            if (values == null || values.Length == 0)
            {
                return false;
            }

            var index = Array.IndexOf(values, trigger);
            if (index < 0)
            {
                return false;
            }

            var next = new TriggerDefinitionAsset[values.Length - 1];
            if (index > 0)
            {
                Array.Copy(values, 0, next, 0, index);
            }

            if (index < values.Length - 1)
            {
                Array.Copy(values, index + 1, next, index, values.Length - index - 1);
            }

            values = next;
            return true;
        }
    }
}
