using System;
using Saber.GAS.Abilities;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    /// <summary>
    /// AbilityDefinition 的 ScriptableObject authoring 入口。
    /// </summary>
    [CreateAssetMenu(menuName = "Saber.GAS/Ability Definition", fileName = "Ability_")]
    public sealed class AbilityDefinitionAsset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private string _abilityId;
        [SerializeField]
        private string _displayName;

        [Header("Activation")]
        [SerializeField]
        private AbilityActivationMode _activationMode = AbilityActivationMode.Instant;
        [SerializeField]
        private long _castDurationTicks;
        [SerializeField]
        private long _activeDurationTicks;
        [SerializeField]
        private long _intervalTicks;
        [SerializeField]
        private bool _executeEffectsOnActivate = true;
        [SerializeField]
        private bool _cancelOnSourceDeath = true;
        [SerializeField]
        private bool _autoActivatePassive = true;

        [Header("Tags")]
        [SerializeField]
        private string[] _abilityTags = Array.Empty<string>();
        [SerializeField]
        private string[] _grantedTagsWhileActive = Array.Empty<string>();
        [SerializeField]
        private string[] _activationRequiredTags = Array.Empty<string>();
        [SerializeField]
        private string[] _activationBlockedTags = Array.Empty<string>();

        [Header("Targeting")]
        [SerializeField]
        private AbilityTargetingAuthoringData _targeting = new AbilityTargetingAuthoringData();

        [Header("Costs & Cooldown")]
        [SerializeField]
        private ResourceCostAuthoringData[] _costs = Array.Empty<ResourceCostAuthoringData>();
        [SerializeField]
        private long _cooldownTicks;
        [SerializeField]
        private string _cooldownTag;

        [Header("Payload")]
        [SerializeField]
        private EffectDefinitionAsset[] _effects = Array.Empty<EffectDefinitionAsset>();
        [SerializeField]
        private EffectDefinitionAsset[] _periodicEffects = Array.Empty<EffectDefinitionAsset>();
        [SerializeField]
        private EffectDefinitionAsset[] _endEffects = Array.Empty<EffectDefinitionAsset>();
        [SerializeField]
        private TriggerDefinitionAsset[] _triggers = Array.Empty<TriggerDefinitionAsset>();

        public AbilityDefinition BuildDefinition(CombatAuthoringBuildContext context = null)
        {
            var buildContext = context ?? new CombatAuthoringBuildContext();
            AbilityDefinition cachedDefinition;
            if (buildContext.TryGetAbility(this, out cachedDefinition))
            {
                return cachedDefinition;
            }

            var abilityDefinition = new AbilityDefinition(
                CombatAuthoringUtility.RequireAbilityId(_abilityId, name, nameof(_abilityId)))
            {
                Name = string.IsNullOrWhiteSpace(_displayName) ? name : _displayName.Trim(),
                ActivationMode = _activationMode,
                Cooldown = new AbilityCooldownDefinition(
                    _cooldownTicks < 0 ? 0 : _cooldownTicks,
                    CombatAuthoringUtility.OptionalTag(_cooldownTag)),
                CastDurationTicks = _castDurationTicks < 0 ? 0 : _castDurationTicks,
                ActiveDurationTicks = _activeDurationTicks < 0 ? 0 : _activeDurationTicks,
                IntervalTicks = _intervalTicks < 0 ? 0 : _intervalTicks,
                ExecuteEffectsOnActivate = _executeEffectsOnActivate,
                CancelOnSourceDeath = _cancelOnSourceDeath,
                AutoActivatePassive = _autoActivatePassive,
            };
            buildContext.Cache(this, abilityDefinition);

            CombatAuthoringUtility.AddTags(abilityDefinition.AbilityTags, _abilityTags);
            CombatAuthoringUtility.AddTags(abilityDefinition.GrantedTagsWhileActive, _grantedTagsWhileActive);
            CombatAuthoringUtility.AddTags(abilityDefinition.ActivationRequiredTags, _activationRequiredTags);
            CombatAuthoringUtility.AddTags(abilityDefinition.ActivationBlockedTags, _activationBlockedTags);

            if (_targeting != null)
            {
                _targeting.ApplyTo(abilityDefinition.Targeting);
            }

            for (var i = 0; i < _costs.Length; i++)
            {
                var cost = _costs[i];
                if (cost == null)
                {
                    continue;
                }

                abilityDefinition.Costs.Add(cost.Build(name));
            }

            for (var i = 0; i < _effects.Length; i++)
            {
                var effect = _effects[i];
                if (effect == null)
                {
                    continue;
                }

                abilityDefinition.Effects.Add(buildContext.BuildEffect(effect));
            }

            for (var i = 0; i < _periodicEffects.Length; i++)
            {
                var effect = _periodicEffects[i];
                if (effect == null)
                {
                    continue;
                }

                abilityDefinition.PeriodicEffects.Add(buildContext.BuildEffect(effect));
            }

            for (var i = 0; i < _endEffects.Length; i++)
            {
                var effect = _endEffects[i];
                if (effect == null)
                {
                    continue;
                }

                abilityDefinition.EndEffects.Add(buildContext.BuildEffect(effect));
            }

            for (var i = 0; i < _triggers.Length; i++)
            {
                var trigger = _triggers[i];
                if (trigger == null)
                {
                    continue;
                }

                abilityDefinition.Triggers.Add(buildContext.BuildTrigger(trigger));
            }

            return abilityDefinition;
        }
    }
}
