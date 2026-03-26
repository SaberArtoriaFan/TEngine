using System;
using Saber.GAS.Runtime;
using Saber.GAS.Triggers;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    /// <summary>
    /// TriggerDefinition 的 ScriptableObject authoring 入口。
    /// </summary>
    [CreateAssetMenu(menuName = "Saber.GAS/Trigger Definition", fileName = "Trigger_")]
    public sealed class TriggerDefinitionAsset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private string _triggerId;
        [SerializeField]
        private string _displayName;

        [Header("Routing")]
        [SerializeField]
        private TriggerSourceKind _sourceKind = TriggerSourceKind.Actor;
        [SerializeField]
        private CombatTriggerEventKind _eventKind = CombatTriggerEventKind.Manual;
        [SerializeField]
        private CombatTriggerTiming _timing = CombatTriggerTiming.Manual;
        [SerializeField]
        private TriggerCollectionMode _collectionMode = TriggerCollectionMode.OwnerOnly;
        [SerializeField]
        private int _priority;

        [Header("Owner Tags")]
        [SerializeField]
        private string[] _requiredOwnerTags = Array.Empty<string>();
        [SerializeField]
        private string[] _blockedOwnerTags = Array.Empty<string>();

        [Header("Instigator Tags")]
        [SerializeField]
        private string[] _requiredInstigatorTags = Array.Empty<string>();
        [SerializeField]
        private string[] _blockedInstigatorTags = Array.Empty<string>();

        [Header("Target Tags")]
        [SerializeField]
        private string[] _requiredTargetTags = Array.Empty<string>();
        [SerializeField]
        private string[] _blockedTargetTags = Array.Empty<string>();

        [Header("Incoming Ability Tags")]
        [SerializeField]
        private string[] _requiredIncomingAbilityTags = Array.Empty<string>();
        [SerializeField]
        private string[] _blockedIncomingAbilityTags = Array.Empty<string>();

        [Header("Incoming Effect Tags")]
        [SerializeField]
        private string[] _requiredIncomingEffectTags = Array.Empty<string>();
        [SerializeField]
        private string[] _blockedIncomingEffectTags = Array.Empty<string>();

        [Header("Impact Tags")]
        [SerializeField]
        private string[] _requiredImpactTags = Array.Empty<string>();
        [SerializeField]
        private string[] _blockedImpactTags = Array.Empty<string>();

        [Header("Relation")]
        [SerializeField]
        private CombatActorRelationFlags _relationFilter = CombatActorRelationFlags.Any;
        [SerializeField]
        private CombatActorRelationFlags _targetRelationFilter = CombatActorRelationFlags.Any;
        [SerializeField]
        private CombatActorRelationFlags _instigatorRelationFilter = CombatActorRelationFlags.Any;
        [SerializeField]
        private FixedPointValue _observerMaxDistance;

        [Header("Limits")]
        [SerializeField]
        private long _cooldownTicks;
        [SerializeField]
        private int _maxTriggerCountPerTick;
        [SerializeField]
        private int _maxTriggerCountTotal;
        [SerializeField]
        private int _initialCharges;
        [SerializeField]
        private bool _consumeSourceEffectOnTrigger;
        [SerializeField]
        private bool _removeSourceEffectOnTrigger;
        [SerializeField]
        private bool _disableWhenSourceDead;
        [SerializeField]
        private bool _disableWhenTargetDead;
        [SerializeField]
        private bool _enabledOnCreate = true;

        [Header("Threshold")]
        [SerializeField]
        private TriggerThresholdAuthoringData _threshold = new TriggerThresholdAuthoringData();

        [Header("Action")]
        [SerializeField]
        private TriggerActionAuthoringData _action = new TriggerActionAuthoringData();

        public TriggerDefinition BuildDefinition(CombatAuthoringBuildContext context = null)
        {
            var buildContext = context ?? new CombatAuthoringBuildContext();
            TriggerDefinition cachedDefinition;
            if (buildContext.TryGetTrigger(this, out cachedDefinition))
            {
                return cachedDefinition;
            }

            var triggerDefinition = new TriggerDefinition(
                CombatAuthoringUtility.RequireTriggerId(_triggerId, name, nameof(_triggerId)));
            buildContext.Cache(this, triggerDefinition);

            triggerDefinition.Name = string.IsNullOrWhiteSpace(_displayName) ? name : _displayName.Trim();
            triggerDefinition.SourceKind = _sourceKind;
            triggerDefinition.EventKind = _eventKind;
            triggerDefinition.Timing = _timing;
            triggerDefinition.CollectionMode = _collectionMode;
            triggerDefinition.Priority = _priority;
            triggerDefinition.RelationFilter = _relationFilter;
            triggerDefinition.TargetRelationFilter = _targetRelationFilter;
            triggerDefinition.InstigatorRelationFilter = _instigatorRelationFilter;
            triggerDefinition.ObserverMaxDistance = _observerMaxDistance.ToFixedPoint();
            triggerDefinition.CooldownTicks = _cooldownTicks < 0 ? 0 : _cooldownTicks;
            triggerDefinition.MaxTriggerCountPerTick = _maxTriggerCountPerTick < 0 ? 0 : _maxTriggerCountPerTick;
            triggerDefinition.MaxTriggerCountTotal = _maxTriggerCountTotal < 0 ? 0 : _maxTriggerCountTotal;
            triggerDefinition.InitialCharges = _initialCharges < 0 ? 0 : _initialCharges;
            triggerDefinition.ConsumeSourceEffectOnTrigger = _consumeSourceEffectOnTrigger;
            triggerDefinition.RemoveSourceEffectOnTrigger = _removeSourceEffectOnTrigger;
            triggerDefinition.DisableWhenSourceDead = _disableWhenSourceDead;
            triggerDefinition.DisableWhenTargetDead = _disableWhenTargetDead;
            triggerDefinition.EnabledOnCreate = _enabledOnCreate;

            CombatAuthoringUtility.AddTags(triggerDefinition.RequiredOwnerTags, _requiredOwnerTags);
            CombatAuthoringUtility.AddTags(triggerDefinition.BlockedOwnerTags, _blockedOwnerTags);
            CombatAuthoringUtility.AddTags(triggerDefinition.RequiredInstigatorTags, _requiredInstigatorTags);
            CombatAuthoringUtility.AddTags(triggerDefinition.BlockedInstigatorTags, _blockedInstigatorTags);
            CombatAuthoringUtility.AddTags(triggerDefinition.RequiredTargetTags, _requiredTargetTags);
            CombatAuthoringUtility.AddTags(triggerDefinition.BlockedTargetTags, _blockedTargetTags);
            CombatAuthoringUtility.AddTags(triggerDefinition.RequiredIncomingAbilityTags, _requiredIncomingAbilityTags);
            CombatAuthoringUtility.AddTags(triggerDefinition.BlockedIncomingAbilityTags, _blockedIncomingAbilityTags);
            CombatAuthoringUtility.AddTags(triggerDefinition.RequiredIncomingEffectTags, _requiredIncomingEffectTags);
            CombatAuthoringUtility.AddTags(triggerDefinition.BlockedIncomingEffectTags, _blockedIncomingEffectTags);
            CombatAuthoringUtility.AddTags(triggerDefinition.RequiredImpactTags, _requiredImpactTags);
            CombatAuthoringUtility.AddTags(triggerDefinition.BlockedImpactTags, _blockedImpactTags);

            if (_threshold != null)
            {
                _threshold.ApplyTo(triggerDefinition.Threshold, name);
            }

            if (_action != null)
            {
                _action.ApplyTo(triggerDefinition.Action, buildContext, name);
            }

            return triggerDefinition;
        }
    }
}
