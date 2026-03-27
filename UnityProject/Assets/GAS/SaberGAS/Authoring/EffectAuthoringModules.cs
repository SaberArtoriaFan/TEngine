using System;
using System.Collections.Generic;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Resources;
using Saber.GAS.Runtime;
using Saber.GAS.Semantics;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    [Serializable, GasAuthoringModule("基础信息", 0)]
    public sealed class EffectIdentityModule : EffectAuthoringModule
    {
        [SerializeField, InspectorName("效果 Id")]
        private string _effectId;

        public string EffectId
        {
            get => _effectId;
            set => _effectId = value;
        }

        public EffectId BuildId(string ownerName)
        {
            return CombatAuthoringUtility.RequireEffectId(_effectId, ownerName, nameof(_effectId));
        }

        public override void ApplyTo(
            EffectDefinition definition,
            EffectDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
        }
    }

    [Serializable, GasAuthoringModule("局部标签词库", 10)]
    public sealed class EffectLocalTagLibraryModule : EffectAuthoringModule, ICombatLocalTagModule
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
            EffectDefinition definition,
            EffectDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
        }
    }

    [Serializable, GasAuthoringModule("标签", 20)]
    public sealed class EffectTagRulesModule : EffectAuthoringModule
    {
        [SerializeField, InspectorName("效果标签")]
        private string[] _effectTags = Array.Empty<string>();
        [SerializeField, InspectorName("授予标签")]
        private string[] _grantedTags = Array.Empty<string>();
        [SerializeField, InspectorName("目标所需标签")]
        private string[] _requiredTargetTags = Array.Empty<string>();
        [SerializeField, InspectorName("目标阻断标签")]
        private string[] _blockedTargetTags = Array.Empty<string>();

        public string[] EffectTags
        {
            get => _effectTags;
            set => _effectTags = value ?? Array.Empty<string>();
        }

        public string[] GrantedTags
        {
            get => _grantedTags;
            set => _grantedTags = value ?? Array.Empty<string>();
        }

        public string[] RequiredTargetTags
        {
            get => _requiredTargetTags;
            set => _requiredTargetTags = value ?? Array.Empty<string>();
        }

        public string[] BlockedTargetTags
        {
            get => _blockedTargetTags;
            set => _blockedTargetTags = value ?? Array.Empty<string>();
        }

        public override void ApplyTo(
            EffectDefinition definition,
            EffectDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            CombatAuthoringUtility.AddTags(definition.EffectTags, _effectTags);
            CombatAuthoringUtility.AddTags(definition.GrantedTags, _grantedTags);
            CombatAuthoringUtility.AddTags(definition.RequiredTargetTags, _requiredTargetTags);
            CombatAuthoringUtility.AddTags(definition.BlockedTargetTags, _blockedTargetTags);
        }
    }

    [Serializable, GasAuthoringModule("移除规则", 30)]
    public sealed class EffectRemovalModule : EffectAuthoringModule
    {
        [SerializeField, InspectorName("移除目标效果标签")]
        private string[] _removedTargetEffectTags = Array.Empty<string>();
        [SerializeField, InspectorName("移除目标效果资产")]
        private EffectDefinitionAsset[] _removedTargetEffects = Array.Empty<EffectDefinitionAsset>();

        public string[] RemovedTargetEffectTags
        {
            get => _removedTargetEffectTags;
            set => _removedTargetEffectTags = value ?? Array.Empty<string>();
        }

        public IReadOnlyList<EffectDefinitionAsset> RemovedTargetEffects => _removedTargetEffects;

        public EffectDefinitionAsset[] RemovedTargetEffectsArray
        {
            get => _removedTargetEffects;
            set => _removedTargetEffects = value ?? Array.Empty<EffectDefinitionAsset>();
        }

        public bool AddRemovedEffect(EffectDefinitionAsset effect)
        {
            return AppendUnique(ref _removedTargetEffects, effect);
        }

        public bool RemoveRemovedEffect(EffectDefinitionAsset effect)
        {
            return RemoveReference(ref _removedTargetEffects, effect);
        }

        public override void ApplyTo(
            EffectDefinition definition,
            EffectDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            CombatAuthoringUtility.AddTags(definition.RemovedTargetEffectTags, _removedTargetEffectTags);
            if (context == null)
            {
                return;
            }

            for (var i = 0; i < _removedTargetEffects.Length; i++)
            {
                var removedEffect = _removedTargetEffects[i];
                if (removedEffect != null)
                {
                    definition.RemovedTargetEffectIds.Add(context.BuildEffect(removedEffect).Id);
                }
            }
        }

        private static bool AppendUnique(ref EffectDefinitionAsset[] values, EffectDefinitionAsset effect)
        {
            if (effect == null)
            {
                return false;
            }

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

    [Serializable, GasAuthoringModule("属性修正", 40)]
    public sealed class EffectAttributePayloadModule : EffectAuthoringModule
    {
        [SerializeField, InspectorName("属性修正")]
        private AttributeModifierAuthoringData[] _attributeModifiers = Array.Empty<AttributeModifierAuthoringData>();

        public AttributeModifierAuthoringData[] AttributeModifiers
        {
            get => _attributeModifiers;
            set => _attributeModifiers = value ?? Array.Empty<AttributeModifierAuthoringData>();
        }

        public override void ApplyTo(
            EffectDefinition definition,
            EffectDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            for (var i = 0; i < _attributeModifiers.Length; i++)
            {
                var modifier = _attributeModifiers[i];
                if (modifier != null)
                {
                    definition.AttributeModifiers.Add(modifier.Build(owner == null ? string.Empty : owner.name));
                }
            }
        }
    }

    [Serializable, GasAuthoringModule("资源变化", 50)]
    public sealed class EffectResourcePayloadModule : EffectAuthoringModule
    {
        [SerializeField, InspectorName("即时资源变化")]
        private ResourceDeltaAuthoringData[] _instantResourceDeltas = Array.Empty<ResourceDeltaAuthoringData>();
        [SerializeField, InspectorName("周期资源变化")]
        private ResourceDeltaAuthoringData[] _periodicResourceDeltas = Array.Empty<ResourceDeltaAuthoringData>();

        public ResourceDeltaAuthoringData[] InstantResourceDeltas
        {
            get => _instantResourceDeltas;
            set => _instantResourceDeltas = value ?? Array.Empty<ResourceDeltaAuthoringData>();
        }

        public ResourceDeltaAuthoringData[] PeriodicResourceDeltas
        {
            get => _periodicResourceDeltas;
            set => _periodicResourceDeltas = value ?? Array.Empty<ResourceDeltaAuthoringData>();
        }

        public override void ApplyTo(
            EffectDefinition definition,
            EffectDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            AppendDeltas(definition.InstantResourceDeltas, _instantResourceDeltas, owner);
            AppendDeltas(definition.PeriodicResourceDeltas, _periodicResourceDeltas, owner);
        }

        private static void AppendDeltas(
            IList<ResourceDeltaDefinition> destination,
            ResourceDeltaAuthoringData[] source,
            EffectDefinitionAsset owner)
        {
            if (destination == null || source == null)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var delta = source[i];
                if (delta != null)
                {
                    destination.Add(delta.Build(owner == null ? string.Empty : owner.name));
                }
            }
        }
    }

    [Serializable, GasAuthoringModule("\u6295\u5c04\u7269\u8f7d\u8377", 55)]
    public sealed class EffectProjectilePayloadModule : EffectAuthoringModule
    {
        [SerializeField, InspectorName("\u6295\u5c04\u7269\u5b9a\u4e49")]
        private ProjectileSpawnAuthoringData[] _projectiles = Array.Empty<ProjectileSpawnAuthoringData>();

        public ProjectileSpawnAuthoringData[] Projectiles
        {
            get => _projectiles;
            set => _projectiles = value ?? Array.Empty<ProjectileSpawnAuthoringData>();
        }

        public override void ApplyTo(
            EffectDefinition definition,
            EffectDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            for (var i = 0; i < _projectiles.Length; i++)
            {
                var projectile = _projectiles[i];
                if (projectile == null)
                {
                    continue;
                }

                definition.ImpactOperations.Add(new CombatImpactOperation
                {
                    Type = CombatImpactOperationType.SpawnProjectile,
                    Projectile = projectile.Build(context, string.Format("{0}.Projectile[{1}]", owner == null ? string.Empty : owner.name, i)),
                });
            }
        }
    }

    [Serializable, GasAuthoringModule("\u62a4\u76fe\u8bed\u4e49", 60)]
    public sealed class EffectShieldPayloadModule : EffectAuthoringModule
    {
        [SerializeField, InspectorName("\u62a4\u76fe\u8bed\u4e49")]
        private ShieldSemanticAuthoringData[] _shieldSemantics = Array.Empty<ShieldSemanticAuthoringData>();

        public ShieldSemanticAuthoringData[] ShieldSemantics
        {
            get => _shieldSemantics;
            set => _shieldSemantics = value ?? Array.Empty<ShieldSemanticAuthoringData>();
        }

        public override void ApplyTo(
            EffectDefinition definition,
            EffectDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null || _shieldSemantics == null || _shieldSemantics.Length == 0)
            {
                return;
            }

            var semantics = new CombatSemanticEffectExtension();
            for (var i = 0; i < _shieldSemantics.Length; i++)
            {
                var shield = _shieldSemantics[i];
                if (shield != null)
                {
                    semantics.Shields.Add(shield.Build(owner == null ? string.Empty : owner.name));
                }
            }

            if (semantics.Shields.Count > 0)
            {
                definition.Extensions.Add(semantics);
            }
        }
    }

    [Serializable, GasAuthoringModule("时序规则", 70)]
    public sealed class EffectTimingModule : EffectAuthoringModule
    {
        [SerializeField, InspectorName("持续 Tick")]
        private long _durationTicks;
        [SerializeField, InspectorName("周期 Tick")]
        private long _periodTicks;
        [SerializeField, InspectorName("最大层数")]
        private int _maxStacks = 1;
        [SerializeField, InspectorName("持续策略")]
        private EffectDurationPolicy _durationPolicy = EffectDurationPolicy.Instant;
        [SerializeField, InspectorName("叠层策略")]
        private EffectStackPolicy _stackPolicy = EffectStackPolicy.RefreshDuration;

        public long DurationTicks
        {
            get => _durationTicks;
            set => _durationTicks = value;
        }

        public long PeriodTicks
        {
            get => _periodTicks;
            set => _periodTicks = value;
        }

        public int MaxStacks
        {
            get => _maxStacks;
            set => _maxStacks = value;
        }

        public EffectDurationPolicy DurationPolicy
        {
            get => _durationPolicy;
            set => _durationPolicy = value;
        }

        public EffectStackPolicy StackPolicy
        {
            get => _stackPolicy;
            set => _stackPolicy = value;
        }

        public override void ApplyTo(
            EffectDefinition definition,
            EffectDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            definition.DurationTicks = _durationTicks < 0 ? 0 : _durationTicks;
            definition.PeriodTicks = _periodTicks < 0 ? 0 : _periodTicks;
            definition.MaxStacks = _maxStacks < 1 ? 1 : _maxStacks;
            definition.DurationPolicy = _durationPolicy;
            definition.StackPolicy = _stackPolicy;
        }
    }

    [Serializable, GasAuthoringModule("触发器载荷", 80)]
    public sealed class EffectTriggerModule : EffectAuthoringModule
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
            EffectDefinition definition,
            EffectDefinitionAsset owner,
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
