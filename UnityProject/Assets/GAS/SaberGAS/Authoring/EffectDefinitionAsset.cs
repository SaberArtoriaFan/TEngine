using System;
using Saber.GAS.Effects;
using Saber.GAS.Semantics;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    /// <summary>
    /// EffectDefinition 的 ScriptableObject authoring 入口。
    /// </summary>
    [CreateAssetMenu(menuName = "Saber.GAS/Effect Definition", fileName = "Effect_")]
    public sealed class EffectDefinitionAsset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private string _effectId;

        [Header("Tags")]
        [SerializeField]
        private string[] _effectTags = Array.Empty<string>();
        [SerializeField]
        private string[] _grantedTags = Array.Empty<string>();
        [SerializeField]
        private string[] _requiredTargetTags = Array.Empty<string>();
        [SerializeField]
        private string[] _blockedTargetTags = Array.Empty<string>();
        [SerializeField]
        private string[] _removedTargetEffectTags = Array.Empty<string>();
        [SerializeField]
        private EffectDefinitionAsset[] _removedTargetEffects = Array.Empty<EffectDefinitionAsset>();

        [Header("Payload")]
        [SerializeField]
        private AttributeModifierAuthoringData[] _attributeModifiers = Array.Empty<AttributeModifierAuthoringData>();
        [SerializeField]
        private ResourceDeltaAuthoringData[] _instantResourceDeltas = Array.Empty<ResourceDeltaAuthoringData>();
        [SerializeField]
        private ResourceDeltaAuthoringData[] _periodicResourceDeltas = Array.Empty<ResourceDeltaAuthoringData>();
        [SerializeField]
        private ShieldSemanticAuthoringData[] _shieldSemantics = Array.Empty<ShieldSemanticAuthoringData>();
        [SerializeField]
        private TriggerDefinitionAsset[] _triggers = Array.Empty<TriggerDefinitionAsset>();

        [Header("Timing")]
        [SerializeField]
        private long _durationTicks;
        [SerializeField]
        private long _periodTicks;
        [SerializeField]
        private int _maxStacks = 1;
        [SerializeField]
        private EffectDurationPolicy _durationPolicy = EffectDurationPolicy.Instant;
        [SerializeField]
        private EffectStackPolicy _stackPolicy = EffectStackPolicy.RefreshDuration;

        public EffectDefinition BuildDefinition(CombatAuthoringBuildContext context = null)
        {
            var buildContext = context ?? new CombatAuthoringBuildContext();
            EffectDefinition cachedDefinition;
            if (buildContext.TryGetEffect(this, out cachedDefinition))
            {
                return cachedDefinition;
            }

            var effectDefinition = new EffectDefinition(
                CombatAuthoringUtility.RequireEffectId(_effectId, name, nameof(_effectId)));
            buildContext.Cache(this, effectDefinition);

            CombatAuthoringUtility.AddTags(effectDefinition.EffectTags, _effectTags);
            CombatAuthoringUtility.AddTags(effectDefinition.GrantedTags, _grantedTags);
            CombatAuthoringUtility.AddTags(effectDefinition.RequiredTargetTags, _requiredTargetTags);
            CombatAuthoringUtility.AddTags(effectDefinition.BlockedTargetTags, _blockedTargetTags);
            CombatAuthoringUtility.AddTags(effectDefinition.RemovedTargetEffectTags, _removedTargetEffectTags);

            for (var i = 0; i < _removedTargetEffects.Length; i++)
            {
                var removedEffect = _removedTargetEffects[i];
                if (removedEffect == null)
                {
                    continue;
                }

                effectDefinition.RemovedTargetEffectIds.Add(buildContext.BuildEffect(removedEffect).Id);
            }

            for (var i = 0; i < _attributeModifiers.Length; i++)
            {
                var modifier = _attributeModifiers[i];
                if (modifier == null)
                {
                    continue;
                }

                effectDefinition.AttributeModifiers.Add(modifier.Build(name));
            }

            for (var i = 0; i < _instantResourceDeltas.Length; i++)
            {
                var delta = _instantResourceDeltas[i];
                if (delta == null)
                {
                    continue;
                }

                effectDefinition.InstantResourceDeltas.Add(delta.Build(name));
            }

            for (var i = 0; i < _periodicResourceDeltas.Length; i++)
            {
                var delta = _periodicResourceDeltas[i];
                if (delta == null)
                {
                    continue;
                }

                effectDefinition.PeriodicResourceDeltas.Add(delta.Build(name));
            }

            if (_shieldSemantics.Length > 0)
            {
                var semantics = new CombatSemanticEffectExtension();
                for (var i = 0; i < _shieldSemantics.Length; i++)
                {
                    var shield = _shieldSemantics[i];
                    if (shield == null)
                    {
                        continue;
                    }

                    semantics.Shields.Add(shield.Build(name));
                }

                if (semantics.Shields.Count > 0)
                {
                    effectDefinition.Extensions.Add(semantics);
                }
            }

            for (var i = 0; i < _triggers.Length; i++)
            {
                var trigger = _triggers[i];
                if (trigger == null)
                {
                    continue;
                }

                effectDefinition.Triggers.Add(buildContext.BuildTrigger(trigger));
            }

            effectDefinition.DurationTicks = _durationTicks < 0 ? 0 : _durationTicks;
            effectDefinition.PeriodTicks = _periodTicks < 0 ? 0 : _periodTicks;
            effectDefinition.MaxStacks = _maxStacks < 1 ? 1 : _maxStacks;
            effectDefinition.DurationPolicy = _durationPolicy;
            effectDefinition.StackPolicy = _stackPolicy;
            return effectDefinition;
        }
    }
}
