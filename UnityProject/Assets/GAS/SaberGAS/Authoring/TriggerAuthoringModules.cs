using System;
using System.Collections.Generic;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;
using Saber.GAS.Triggers;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    [Serializable, GasAuthoringModule("基础信息", 0)]
    public sealed class TriggerIdentityModule : TriggerAuthoringModule
    {
        [SerializeField, InspectorName("触发器 Id")]
        private string _triggerId;
        [SerializeField, InspectorName("显示名称")]
        private string _displayName;

        public string TriggerId
        {
            get => _triggerId;
            set => _triggerId = value;
        }

        public string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        public TriggerId BuildId(string ownerName)
        {
            return CombatAuthoringUtility.RequireTriggerId(_triggerId, ownerName, nameof(_triggerId));
        }

        public string ResolveDisplayName(string ownerName)
        {
            return string.IsNullOrWhiteSpace(_displayName) ? ownerName : _displayName.Trim();
        }

        public override void ApplyTo(
            TriggerDefinition definition,
            TriggerDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition != null)
            {
                definition.Name = ResolveDisplayName(owner == null ? string.Empty : owner.name);
            }
        }
    }

    [Serializable, GasAuthoringModule("局部标签词库", 10)]
    public sealed class TriggerLocalTagLibraryModule : TriggerAuthoringModule, ICombatLocalTagModule
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
            TriggerDefinition definition,
            TriggerDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
        }
    }

    [Serializable, GasAuthoringModule("路由", 20)]
    public sealed class TriggerRoutingModule : TriggerAuthoringModule
    {
        [SerializeField, InspectorName("来源类型")]
        private TriggerSourceKind _sourceKind = TriggerSourceKind.Actor;
        [SerializeField, InspectorName("事件类型")]
        private CombatTriggerEventKind _eventKind = CombatTriggerEventKind.Manual;
        [SerializeField, InspectorName("触发时机")]
        private CombatTriggerTiming _timing = CombatTriggerTiming.Manual;
        [SerializeField, InspectorName("收集模式")]
        private TriggerCollectionMode _collectionMode = TriggerCollectionMode.OwnerOnly;
        [SerializeField, InspectorName("优先级")]
        private int _priority;

        public TriggerSourceKind SourceKind
        {
            get => _sourceKind;
            set => _sourceKind = value;
        }

        public CombatTriggerEventKind EventKind
        {
            get => _eventKind;
            set => _eventKind = value;
        }

        public CombatTriggerTiming Timing
        {
            get => _timing;
            set => _timing = value;
        }

        public TriggerCollectionMode CollectionMode
        {
            get => _collectionMode;
            set => _collectionMode = value;
        }

        public int Priority
        {
            get => _priority;
            set => _priority = value;
        }

        public override void ApplyTo(
            TriggerDefinition definition,
            TriggerDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            definition.SourceKind = _sourceKind;
            definition.EventKind = _eventKind;
            definition.Timing = _timing;
            definition.CollectionMode = _collectionMode;
            definition.Priority = _priority;
        }
    }

    [Serializable, GasAuthoringModule("标签过滤", 30)]
    public sealed class TriggerTagFilterModule : TriggerAuthoringModule
    {
        [SerializeField, InspectorName("拥有者所需标签")]
        private string[] _requiredOwnerTags = Array.Empty<string>();
        [SerializeField, InspectorName("拥有者阻断标签")]
        private string[] _blockedOwnerTags = Array.Empty<string>();
        [SerializeField, InspectorName("施加者所需标签")]
        private string[] _requiredInstigatorTags = Array.Empty<string>();
        [SerializeField, InspectorName("施加者阻断标签")]
        private string[] _blockedInstigatorTags = Array.Empty<string>();
        [SerializeField, InspectorName("目标所需标签")]
        private string[] _requiredTargetTags = Array.Empty<string>();
        [SerializeField, InspectorName("目标阻断标签")]
        private string[] _blockedTargetTags = Array.Empty<string>();
        [SerializeField, InspectorName("传入 Ability 所需标签")]
        private string[] _requiredIncomingAbilityTags = Array.Empty<string>();
        [SerializeField, InspectorName("传入 Ability 阻断标签")]
        private string[] _blockedIncomingAbilityTags = Array.Empty<string>();
        [SerializeField, InspectorName("传入 Effect 所需标签")]
        private string[] _requiredIncomingEffectTags = Array.Empty<string>();
        [SerializeField, InspectorName("传入 Effect 阻断标签")]
        private string[] _blockedIncomingEffectTags = Array.Empty<string>();
        [SerializeField, InspectorName("Impact 所需标签")]
        private string[] _requiredImpactTags = Array.Empty<string>();
        [SerializeField, InspectorName("Impact 阻断标签")]
        private string[] _blockedImpactTags = Array.Empty<string>();

        public string[] RequiredOwnerTags
        {
            get => _requiredOwnerTags;
            set => _requiredOwnerTags = value ?? Array.Empty<string>();
        }

        public string[] BlockedOwnerTags
        {
            get => _blockedOwnerTags;
            set => _blockedOwnerTags = value ?? Array.Empty<string>();
        }

        public string[] RequiredInstigatorTags
        {
            get => _requiredInstigatorTags;
            set => _requiredInstigatorTags = value ?? Array.Empty<string>();
        }

        public string[] BlockedInstigatorTags
        {
            get => _blockedInstigatorTags;
            set => _blockedInstigatorTags = value ?? Array.Empty<string>();
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

        public string[] RequiredIncomingAbilityTags
        {
            get => _requiredIncomingAbilityTags;
            set => _requiredIncomingAbilityTags = value ?? Array.Empty<string>();
        }

        public string[] BlockedIncomingAbilityTags
        {
            get => _blockedIncomingAbilityTags;
            set => _blockedIncomingAbilityTags = value ?? Array.Empty<string>();
        }

        public string[] RequiredIncomingEffectTags
        {
            get => _requiredIncomingEffectTags;
            set => _requiredIncomingEffectTags = value ?? Array.Empty<string>();
        }

        public string[] BlockedIncomingEffectTags
        {
            get => _blockedIncomingEffectTags;
            set => _blockedIncomingEffectTags = value ?? Array.Empty<string>();
        }

        public string[] RequiredImpactTags
        {
            get => _requiredImpactTags;
            set => _requiredImpactTags = value ?? Array.Empty<string>();
        }

        public string[] BlockedImpactTags
        {
            get => _blockedImpactTags;
            set => _blockedImpactTags = value ?? Array.Empty<string>();
        }

        public override void ApplyTo(
            TriggerDefinition definition,
            TriggerDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            CombatAuthoringUtility.AddTags(definition.RequiredOwnerTags, _requiredOwnerTags);
            CombatAuthoringUtility.AddTags(definition.BlockedOwnerTags, _blockedOwnerTags);
            CombatAuthoringUtility.AddTags(definition.RequiredInstigatorTags, _requiredInstigatorTags);
            CombatAuthoringUtility.AddTags(definition.BlockedInstigatorTags, _blockedInstigatorTags);
            CombatAuthoringUtility.AddTags(definition.RequiredTargetTags, _requiredTargetTags);
            CombatAuthoringUtility.AddTags(definition.BlockedTargetTags, _blockedTargetTags);
            CombatAuthoringUtility.AddTags(definition.RequiredIncomingAbilityTags, _requiredIncomingAbilityTags);
            CombatAuthoringUtility.AddTags(definition.BlockedIncomingAbilityTags, _blockedIncomingAbilityTags);
            CombatAuthoringUtility.AddTags(definition.RequiredIncomingEffectTags, _requiredIncomingEffectTags);
            CombatAuthoringUtility.AddTags(definition.BlockedIncomingEffectTags, _blockedIncomingEffectTags);
            CombatAuthoringUtility.AddTags(definition.RequiredImpactTags, _requiredImpactTags);
            CombatAuthoringUtility.AddTags(definition.BlockedImpactTags, _blockedImpactTags);
        }
    }

    [Serializable, GasAuthoringModule("关系限制", 40)]
    public sealed class TriggerRelationRulesModule : TriggerAuthoringModule
    {
        [SerializeField, InspectorName("关系过滤")]
        private CombatActorRelationFlags _relationFilter = CombatActorRelationFlags.Any;
        [SerializeField, InspectorName("目标关系过滤")]
        private CombatActorRelationFlags _targetRelationFilter = CombatActorRelationFlags.Any;
        [SerializeField, InspectorName("施加者关系过滤")]
        private CombatActorRelationFlags _instigatorRelationFilter = CombatActorRelationFlags.Any;
        [SerializeField, InspectorName("观察半径上限")]
        private FixedPointValue _observerMaxDistance;

        public CombatActorRelationFlags RelationFilter
        {
            get => _relationFilter;
            set => _relationFilter = value;
        }

        public CombatActorRelationFlags TargetRelationFilter
        {
            get => _targetRelationFilter;
            set => _targetRelationFilter = value;
        }

        public CombatActorRelationFlags InstigatorRelationFilter
        {
            get => _instigatorRelationFilter;
            set => _instigatorRelationFilter = value;
        }

        public FixedPointValue ObserverMaxDistance
        {
            get => _observerMaxDistance;
            set => _observerMaxDistance = value;
        }

        public override void ApplyTo(
            TriggerDefinition definition,
            TriggerDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            definition.RelationFilter = _relationFilter;
            definition.TargetRelationFilter = _targetRelationFilter;
            definition.InstigatorRelationFilter = _instigatorRelationFilter;
            definition.ObserverMaxDistance = _observerMaxDistance.ToFixedPoint();
        }
    }

    [Serializable, GasAuthoringModule("限制与生命周期", 50)]
    public sealed class TriggerLimitModule : TriggerAuthoringModule
    {
        [SerializeField, InspectorName("冷却 Tick")]
        private long _cooldownTicks;
        [SerializeField, InspectorName("每 Tick 最大触发次数")]
        private int _maxTriggerCountPerTick;
        [SerializeField, InspectorName("总触发次数上限")]
        private int _maxTriggerCountTotal;
        [SerializeField, InspectorName("初始充能")]
        private int _initialCharges;
        [SerializeField, InspectorName("触发时消耗来源 Effect")]
        private bool _consumeSourceEffectOnTrigger;
        [SerializeField, InspectorName("触发时移除来源 Effect")]
        private bool _removeSourceEffectOnTrigger;
        [SerializeField, InspectorName("来源死亡时禁用")]
        private bool _disableWhenSourceDead;
        [SerializeField, InspectorName("目标死亡时禁用")]
        private bool _disableWhenTargetDead;
        [SerializeField, InspectorName("创建时启用")]
        private bool _enabledOnCreate = true;

        public long CooldownTicks
        {
            get => _cooldownTicks;
            set => _cooldownTicks = value;
        }

        public int MaxTriggerCountPerTick
        {
            get => _maxTriggerCountPerTick;
            set => _maxTriggerCountPerTick = value;
        }

        public int MaxTriggerCountTotal
        {
            get => _maxTriggerCountTotal;
            set => _maxTriggerCountTotal = value;
        }

        public int InitialCharges
        {
            get => _initialCharges;
            set => _initialCharges = value;
        }

        public bool ConsumeSourceEffectOnTrigger
        {
            get => _consumeSourceEffectOnTrigger;
            set => _consumeSourceEffectOnTrigger = value;
        }

        public bool RemoveSourceEffectOnTrigger
        {
            get => _removeSourceEffectOnTrigger;
            set => _removeSourceEffectOnTrigger = value;
        }

        public bool DisableWhenSourceDead
        {
            get => _disableWhenSourceDead;
            set => _disableWhenSourceDead = value;
        }

        public bool DisableWhenTargetDead
        {
            get => _disableWhenTargetDead;
            set => _disableWhenTargetDead = value;
        }

        public bool EnabledOnCreate
        {
            get => _enabledOnCreate;
            set => _enabledOnCreate = value;
        }

        public override void ApplyTo(
            TriggerDefinition definition,
            TriggerDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            if (definition == null)
            {
                return;
            }

            definition.CooldownTicks = _cooldownTicks < 0 ? 0 : _cooldownTicks;
            definition.MaxTriggerCountPerTick = _maxTriggerCountPerTick < 0 ? 0 : _maxTriggerCountPerTick;
            definition.MaxTriggerCountTotal = _maxTriggerCountTotal < 0 ? 0 : _maxTriggerCountTotal;
            definition.InitialCharges = _initialCharges < 0 ? 0 : _initialCharges;
            definition.ConsumeSourceEffectOnTrigger = _consumeSourceEffectOnTrigger;
            definition.RemoveSourceEffectOnTrigger = _removeSourceEffectOnTrigger;
            definition.DisableWhenSourceDead = _disableWhenSourceDead;
            definition.DisableWhenTargetDead = _disableWhenTargetDead;
            definition.EnabledOnCreate = _enabledOnCreate;
        }
    }

    [Serializable, GasAuthoringModule("阈值", 60)]
    public sealed class TriggerThresholdModule : TriggerAuthoringModule
    {
        [SerializeField, InspectorName("阈值设置")]
        private TriggerThresholdAuthoringData _threshold = new TriggerThresholdAuthoringData();

        public TriggerThresholdAuthoringData Threshold
        {
            get => _threshold;
            set => _threshold = value ?? new TriggerThresholdAuthoringData();
        }

        public override void ApplyTo(
            TriggerDefinition definition,
            TriggerDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            _threshold?.ApplyTo(definition?.Threshold, owner == null ? string.Empty : owner.name);
        }
    }

    [Serializable, GasAuthoringModule("动作", 70)]
    public sealed class TriggerActionModule : TriggerAuthoringModule
    {
        [SerializeField, InspectorName("动作设置")]
        private TriggerActionAuthoringData _action = new TriggerActionAuthoringData();

        public TriggerActionAuthoringData Action
        {
            get => _action;
            set => _action = value ?? new TriggerActionAuthoringData();
        }

        public TriggerActionKind Kind
        {
            get => _action == null ? TriggerActionKind.None : _action.Kind;
            set
            {
                if (_action == null)
                {
                    _action = new TriggerActionAuthoringData();
                }

                _action.Kind = value;
            }
        }

        public AbilityDefinitionAsset TriggeredAbility
        {
            get => _action?.TriggeredAbility;
            set
            {
                if (_action == null)
                {
                    _action = new TriggerActionAuthoringData();
                }

                _action.TriggeredAbility = value;
            }
        }

        public EffectDefinitionAsset EffectAsset
        {
            get => _action?.EffectAsset;
            set
            {
                if (_action == null)
                {
                    _action = new TriggerActionAuthoringData();
                }

                _action.EffectAsset = value;
            }
        }

        public AbilityDefinitionAsset AbilityToCancel
        {
            get => _action?.AbilityToCancel;
            set
            {
                if (_action == null)
                {
                    _action = new TriggerActionAuthoringData();
                }

                _action.AbilityToCancel = value;
            }
        }

        public override void ApplyTo(
            TriggerDefinition definition,
            TriggerDefinitionAsset owner,
            CombatAuthoringBuildContext context)
        {
            _action?.ApplyTo(definition?.Action, context, owner == null ? string.Empty : owner.name);
        }
    }
}
