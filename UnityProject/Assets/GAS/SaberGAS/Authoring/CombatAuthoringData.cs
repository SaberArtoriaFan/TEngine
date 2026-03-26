using System;
using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Attributes;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Resources;
using Saber.GAS.Runtime;
using Saber.GAS.Semantics;
using Saber.GAS.Tags;
using Saber.GAS.Triggers;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    internal static class CombatAuthoringUtility
    {
        public static string RequireString(string value, string ownerName, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(string.Format("{0}: 字段 {1} 不能为空。", ownerName, fieldName));
            }

            return value.Trim();
        }

        public static ActorId RequireActorId(string value, string ownerName, string fieldName)
        {
            return new ActorId(RequireString(value, ownerName, fieldName));
        }

        public static AbilityId RequireAbilityId(string value, string ownerName, string fieldName)
        {
            return new AbilityId(RequireString(value, ownerName, fieldName));
        }

        public static EffectId RequireEffectId(string value, string ownerName, string fieldName)
        {
            return new EffectId(RequireString(value, ownerName, fieldName));
        }

        public static TriggerId RequireTriggerId(string value, string ownerName, string fieldName)
        {
            return new TriggerId(RequireString(value, ownerName, fieldName));
        }

        public static AttributeId RequireAttributeId(string value, string ownerName, string fieldName)
        {
            return new AttributeId(RequireString(value, ownerName, fieldName));
        }

        public static ResourceId RequireResourceId(string value, string ownerName, string fieldName)
        {
            return new ResourceId(RequireString(value, ownerName, fieldName));
        }

        public static TeamId OptionalTeamId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? TeamId.Empty : new TeamId(value.Trim());
        }

        public static GameplayTag OptionalTag(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? default(GameplayTag) : new GameplayTag(value.Trim());
        }

        public static void AddTags(GameplayTagContainer container, string[] tags)
        {
            if (container == null || tags == null)
            {
                return;
            }

            for (var i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                container.Add(new GameplayTag(tag.Trim()));
            }
        }
    }

    /// <summary>
    /// Unity Inspector 里使用的固定点数值包装。
    /// </summary>
    [Serializable]
    public struct FixedPointValue
    {
        [SerializeField]
        private float _value;

        public FixedPointValue(float value)
        {
            _value = value;
        }

        public float Value => _value;

        public FP ToFixedPoint()
        {
            return _value;
        }

        public static implicit operator FP(FixedPointValue value)
        {
            return value.ToFixedPoint();
        }

        public override string ToString()
        {
            return _value.ToString("0.#####");
        }
    }

    /// <summary>
    /// Unity Inspector 里使用的三维固定点坐标。
    /// </summary>
    [Serializable]
    public struct WorldPositionAuthoringData
    {
        [SerializeField]
        private float _x;
        [SerializeField]
        private float _y;
        [SerializeField]
        private float _z;

        public WorldPositionAuthoringData(float x, float y, float z)
        {
            _x = x;
            _y = y;
            _z = z;
        }

        public WorldPosition ToWorldPosition()
        {
            return new WorldPosition(_x, _y, _z);
        }
    }

    [Serializable]
    public sealed class AttributeBaseValueAuthoringData
    {
        [SerializeField]
        private string _attributeId;
        [SerializeField]
        private FixedPointValue _baseValue;

        public void ApplyTo(AttributeSet attributeSet, string ownerName)
        {
            if (attributeSet == null || string.IsNullOrWhiteSpace(_attributeId))
            {
                return;
            }

            attributeSet.SetBase(
                CombatAuthoringUtility.RequireAttributeId(_attributeId, ownerName, nameof(_attributeId)),
                _baseValue.ToFixedPoint());
        }
    }

    [Serializable]
    public sealed class ResourceStateAuthoringData
    {
        [SerializeField]
        private string _resourceId;
        [SerializeField]
        private FixedPointValue _current;
        [SerializeField]
        private FixedPointValue _max;
        [SerializeField]
        private FixedPointValue _regenPerTick;

        public void ApplyTo(ResourceSet resourceSet, string ownerName)
        {
            if (resourceSet == null || string.IsNullOrWhiteSpace(_resourceId))
            {
                return;
            }

            var resourceId = CombatAuthoringUtility.RequireResourceId(_resourceId, ownerName, nameof(_resourceId));
            var resourceValue = resourceSet.GetOrCreate(resourceId, _current.ToFixedPoint(), _max.ToFixedPoint());
            resourceValue.SetMax(_max.ToFixedPoint());
            resourceValue.SetCurrent(_current.ToFixedPoint());
            resourceValue.SetRegenPerTick(_regenPerTick.ToFixedPoint());
        }
    }

    [Serializable]
    public sealed class ResourceCostAuthoringData
    {
        [SerializeField]
        private string _resourceId;
        [SerializeField]
        private FixedPointValue _amount;

        public ResourceCost Build(string ownerName)
        {
            return new ResourceCost(
                CombatAuthoringUtility.RequireResourceId(_resourceId, ownerName, nameof(_resourceId)),
                _amount.ToFixedPoint());
        }
    }

    [Serializable]
    public sealed class ResourceDeltaAuthoringData
    {
        [SerializeField]
        private string _resourceId;
        [SerializeField]
        private FixedPointValue _amount;

        public ResourceDeltaDefinition Build(string ownerName)
        {
            return new ResourceDeltaDefinition(
                CombatAuthoringUtility.RequireResourceId(_resourceId, ownerName, nameof(_resourceId)),
                _amount.ToFixedPoint());
        }
    }

    [Serializable]
    public sealed class AttributeModifierAuthoringData
    {
        [SerializeField]
        private string _attributeId;
        [SerializeField]
        private AttributeModifierType _modifierType = AttributeModifierType.Add;
        [SerializeField]
        private FixedPointValue _magnitude;

        public AttributeModifierDefinition Build(string ownerName)
        {
            return new AttributeModifierDefinition(
                CombatAuthoringUtility.RequireAttributeId(_attributeId, ownerName, nameof(_attributeId)),
                _modifierType,
                _magnitude.ToFixedPoint());
        }
    }

    [Serializable]
    public sealed class ShieldSemanticAuthoringData
    {
        [SerializeField]
        private string _name;
        [SerializeField]
        private string _protectedResourceId;
        [SerializeField]
        private FixedPointValue _capacity;
        [SerializeField]
        private string[] _requiredImpactTags = Array.Empty<string>();
        [SerializeField]
        private string[] _blockedImpactTags = Array.Empty<string>();
        [SerializeField]
        private bool _refreshCapacityOnReapply = true;
        [SerializeField]
        private bool _removeSourceEffectWhenDepleted = true;

        public ShieldSemanticDefinition Build(string ownerName)
        {
            var definition = new ShieldSemanticDefinition(
                CombatAuthoringUtility.RequireResourceId(_protectedResourceId, ownerName, nameof(_protectedResourceId)),
                _capacity.ToFixedPoint())
            {
                Name = string.IsNullOrWhiteSpace(_name) ? null : _name.Trim(),
                RefreshCapacityOnReapply = _refreshCapacityOnReapply,
                RemoveSourceEffectWhenDepleted = _removeSourceEffectWhenDepleted,
            };

            CombatAuthoringUtility.AddTags(definition.RequiredImpactTags, _requiredImpactTags);
            CombatAuthoringUtility.AddTags(definition.BlockedImpactTags, _blockedImpactTags);
            return definition;
        }
    }

    [Serializable]
    public sealed class AbilityTargetingAuthoringData
    {
        [SerializeField]
        private AbilityTargetKind _kind = AbilityTargetKind.None;
        [SerializeField]
        private AbilityTargetFlags _allowedFlags = AbilityTargetFlags.None;
        [SerializeField]
        private FixedPointValue _maxRange;
        [SerializeField]
        private string[] _requiredSourceTags = Array.Empty<string>();
        [SerializeField]
        private string[] _requiredTargetTags = Array.Empty<string>();
        [SerializeField]
        private string[] _blockedSourceTags = Array.Empty<string>();
        [SerializeField]
        private string[] _blockedTargetTags = Array.Empty<string>();

        public void ApplyTo(AbilityTargetingDefinition targeting)
        {
            if (targeting == null)
            {
                return;
            }

            targeting.Kind = _kind;
            targeting.AllowedFlags = _allowedFlags;
            targeting.MaxRange = _maxRange.ToFixedPoint();
            CombatAuthoringUtility.AddTags(targeting.RequiredSourceTags, _requiredSourceTags);
            CombatAuthoringUtility.AddTags(targeting.RequiredTargetTags, _requiredTargetTags);
            CombatAuthoringUtility.AddTags(targeting.BlockedSourceTags, _blockedSourceTags);
            CombatAuthoringUtility.AddTags(targeting.BlockedTargetTags, _blockedTargetTags);
        }
    }

    [Serializable]
    public sealed class TriggerThresholdAuthoringData
    {
        [SerializeField]
        private string _resourceId;
        [SerializeField]
        private FixedPointValue _value;
        [SerializeField]
        private TriggerThresholdDirection _direction = TriggerThresholdDirection.None;

        public void ApplyTo(TriggerThresholdDefinition threshold, string ownerName)
        {
            if (threshold == null)
            {
                return;
            }

            threshold.ResourceId = string.IsNullOrWhiteSpace(_resourceId)
                ? ResourceId.Empty
                : CombatAuthoringUtility.RequireResourceId(_resourceId, ownerName, nameof(_resourceId));
            threshold.Value = _value.ToFixedPoint();
            threshold.Direction = _direction;
        }
    }

    [Serializable]
    public sealed class CombatImpactOperationAuthoringData
    {
        [SerializeField]
        private CombatImpactOperationType _type = CombatImpactOperationType.ResourceDelta;
        [SerializeField]
        private string _resourceId;
        [SerializeField]
        private EffectDefinitionAsset _effectAsset;
        [SerializeField]
        private string _tag;
        [SerializeField]
        private FixedPointValue _amount;
        [SerializeField]
        private string _cueName;

        public CombatImpactOperation Build(CombatAuthoringBuildContext context, string ownerName)
        {
            if (_type == CombatImpactOperationType.ApplyEffect)
            {
                throw new InvalidOperationException(
                    string.Format("{0}: AddImpactOperation 当前不支持直接配置 ApplyEffect，请改用 TriggerActionKind.ApplyEffect。", ownerName));
            }

            var operation = new CombatImpactOperation
            {
                Type = _type,
                Amount = _amount.ToFixedPoint(),
                CueName = string.IsNullOrWhiteSpace(_cueName) ? null : _cueName.Trim(),
            };

            if (!string.IsNullOrWhiteSpace(_resourceId))
            {
                operation.ResourceId = CombatAuthoringUtility.RequireResourceId(_resourceId, ownerName, nameof(_resourceId));
            }

            if (_effectAsset != null)
            {
                operation.EffectId = context.BuildEffect(_effectAsset).Id;
            }

            operation.Tag = CombatAuthoringUtility.OptionalTag(_tag);
            return operation;
        }
    }

    [Serializable]
    public sealed class TriggerActionAuthoringData
    {
        [SerializeField]
        private TriggerActionKind _kind = TriggerActionKind.None;
        [SerializeField]
        private TriggerActorReference _sourceActor = TriggerActorReference.Owner;
        [SerializeField]
        private TriggerActorReference _targetActor = TriggerActorReference.None;
        [SerializeField]
        private AbilityDefinitionAsset _triggeredAbility;
        [SerializeField]
        private EffectDefinitionAsset _effectAsset;
        [SerializeField]
        private string _effectTag;
        [SerializeField]
        private string _resourceId;
        [SerializeField]
        private FixedPointValue _resourceAmount;
        [SerializeField]
        private string _tag;
        [SerializeField]
        private CombatImpactOperationAuthoringData _operationTemplate;
        [SerializeField]
        private FixedPointValue _magnitudeMultiplier = new FixedPointValue(1f);
        [SerializeField]
        private string _cueName;
        [SerializeField]
        private AbilityDefinitionAsset _abilityToCancel;
        [SerializeField]
        private int _customActionId;

        public void ApplyTo(TriggerActionDefinition action, CombatAuthoringBuildContext context, string ownerName)
        {
            if (action == null)
            {
                return;
            }

            action.Kind = _kind;
            action.SourceActor = _sourceActor;
            action.TargetActor = _targetActor;
            action.EffectTag = CombatAuthoringUtility.OptionalTag(_effectTag);
            action.ResourceId = string.IsNullOrWhiteSpace(_resourceId)
                ? ResourceId.Empty
                : CombatAuthoringUtility.RequireResourceId(_resourceId, ownerName, nameof(_resourceId));
            action.ResourceAmount = _resourceAmount.ToFixedPoint();
            action.Tag = CombatAuthoringUtility.OptionalTag(_tag);
            action.MagnitudeMultiplier = _magnitudeMultiplier.ToFixedPoint();
            action.CueName = string.IsNullOrWhiteSpace(_cueName) ? null : _cueName.Trim();
            action.CustomActionId = _customActionId;

            if (_triggeredAbility != null)
            {
                action.TriggeredAbilityId = context.BuildAbility(_triggeredAbility).Id;
            }

            if (_effectAsset != null)
            {
                var effectDefinition = context.BuildEffect(_effectAsset);
                action.EffectDefinition = effectDefinition;
                action.EffectId = effectDefinition.Id;
            }

            if (_operationTemplate != null)
            {
                action.OperationTemplate = _operationTemplate.Build(context, ownerName);
            }

            if (_abilityToCancel != null)
            {
                action.AbilityToCancelId = context.BuildAbility(_abilityToCancel).Id;
            }
        }
    }

    [Serializable]
    public sealed class CombatActorSpawnAuthoringData
    {
        [SerializeField]
        private bool _enabled = true;
        [SerializeField]
        private CombatActorTemplateAsset _template;
        [SerializeField]
        private string _actorIdOverride;
        [SerializeField]
        private string _teamIdOverride;
        [SerializeField]
        private bool _overridePosition;
        [SerializeField]
        private WorldPositionAuthoringData _positionOverride;

        public bool Enabled => _enabled;

        public void Spawn(CombatRuntime runtime, CombatAuthoringBuildContext context, string ownerName)
        {
            if (!_enabled || runtime == null || _template == null)
            {
                return;
            }

            var actorId = string.IsNullOrWhiteSpace(_actorIdOverride)
                ? _template.GetDefaultActorId()
                : CombatAuthoringUtility.RequireActorId(_actorIdOverride, ownerName, nameof(_actorIdOverride));

            var actor = runtime.AddActor(actorId);
            CombatAuthoringInstaller.ApplyTemplate(runtime, actor, _template, context);

            if (!string.IsNullOrWhiteSpace(_teamIdOverride))
            {
                actor.TeamId = new TeamId(_teamIdOverride.Trim());
            }

            if (_overridePosition)
            {
                actor.Position = _positionOverride.ToWorldPosition();
            }
        }
    }
}
