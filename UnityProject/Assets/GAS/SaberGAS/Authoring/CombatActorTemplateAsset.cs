using System;
using System.Collections.Generic;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Foundation;
using Saber.GAS.Triggers;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    /// <summary>
    /// 一份战斗单位模板资产，负责把配置资产里的基础状态写入运行时 Actor。
    /// </summary>
    [CreateAssetMenu(menuName = "Saber.GAS/Actor Template", fileName = "ActorTemplate_")]
    public sealed class CombatActorTemplateAsset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private string _defaultActorId;
        [SerializeField]
        private string _defaultTeamId;
        [SerializeField]
        private WorldPositionAuthoringData _defaultPosition;

        [Header("State")]
        [SerializeField]
        private string[] _initialTags = Array.Empty<string>();
        [SerializeField]
        private AttributeBaseValueAuthoringData[] _attributes = Array.Empty<AttributeBaseValueAuthoringData>();
        [SerializeField]
        private ResourceStateAuthoringData[] _resources = Array.Empty<ResourceStateAuthoringData>();

        [Header("Runtime Grants")]
        [SerializeField]
        private AbilityDefinitionAsset[] _grantedAbilities = Array.Empty<AbilityDefinitionAsset>();
        [SerializeField]
        private TriggerDefinitionAsset[] _actorTriggers = Array.Empty<TriggerDefinitionAsset>();

        public ActorId GetDefaultActorId()
        {
            return CombatAuthoringUtility.RequireActorId(_defaultActorId, name, nameof(_defaultActorId));
        }

        public TeamId GetDefaultTeamId()
        {
            return CombatAuthoringUtility.OptionalTeamId(_defaultTeamId);
        }

        public WorldPosition GetDefaultPosition()
        {
            return _defaultPosition.ToWorldPosition();
        }

        public void WarmupDefinitions(CombatAuthoringBuildContext context = null)
        {
            var buildContext = context ?? new CombatAuthoringBuildContext();
            BuildGrantedAbilities(buildContext);
            BuildActorTriggers(buildContext);
        }

        public IReadOnlyList<AbilityDefinition> BuildGrantedAbilities(CombatAuthoringBuildContext context = null)
        {
            var buildContext = context ?? new CombatAuthoringBuildContext();
            var definitions = new List<AbilityDefinition>();
            for (var i = 0; i < _grantedAbilities.Length; i++)
            {
                var ability = _grantedAbilities[i];
                if (ability == null)
                {
                    continue;
                }

                definitions.Add(buildContext.BuildAbility(ability));
            }

            return definitions;
        }

        public IReadOnlyList<TriggerDefinition> BuildActorTriggers(CombatAuthoringBuildContext context = null)
        {
            var buildContext = context ?? new CombatAuthoringBuildContext();
            var definitions = new List<TriggerDefinition>();
            for (var i = 0; i < _actorTriggers.Length; i++)
            {
                var trigger = _actorTriggers[i];
                if (trigger == null)
                {
                    continue;
                }

                definitions.Add(buildContext.BuildTrigger(trigger));
            }

            return definitions;
        }

        public void ApplyTo(CombatActorState actor, CombatAuthoringBuildContext context = null)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            var buildContext = context ?? new CombatAuthoringBuildContext();
            actor.TeamId = GetDefaultTeamId();
            actor.Position = GetDefaultPosition();
            actor.IsAlive = true;

            for (var i = 0; i < _initialTags.Length; i++)
            {
                var tag = CombatAuthoringUtility.OptionalTag(_initialTags[i]);
                if (!string.IsNullOrWhiteSpace(tag.Value))
                {
                    actor.AddTagReference(tag);
                }
            }

            for (var i = 0; i < _attributes.Length; i++)
            {
                var attribute = _attributes[i];
                if (attribute == null)
                {
                    continue;
                }

                attribute.ApplyTo(actor.Attributes, name);
            }

            for (var i = 0; i < _resources.Length; i++)
            {
                var resource = _resources[i];
                if (resource == null)
                {
                    continue;
                }

                resource.ApplyTo(actor.Resources, name);
            }

            var grantedAbilities = BuildGrantedAbilities(buildContext);
            for (var i = 0; i < grantedAbilities.Count; i++)
            {
                actor.GrantAbility(grantedAbilities[i].Id);
            }
        }
    }
}
