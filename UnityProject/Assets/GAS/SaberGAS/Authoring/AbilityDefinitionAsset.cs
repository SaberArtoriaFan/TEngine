using System;
using System.Collections.Generic;
using Saber.GAS.Abilities;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    [CreateAssetMenu(menuName = "Saber.GAS/Ability Definition", fileName = "Ability_")]
    public sealed class AbilityDefinitionAsset : ScriptableObject
    {
        [SerializeReference]
        private List<AbilityAuthoringModule> _modules = new List<AbilityAuthoringModule>();

        public IReadOnlyList<AbilityAuthoringModule> Modules
        {
            get
            {
                EnsureModulesInitialized();
                return _modules;
            }
        }

        public IReadOnlyList<CombatTagDefinitionAuthoringData> LocalTagDefinitions
        {
            get
            {
                EnsureModulesInitialized();
                return GasAuthoringModuleUtility.CollectLocalTagDefinitions(_modules);
            }
        }

        public bool EnsureModulesInitialized()
        {
            _modules ??= new List<AbilityAuthoringModule>();
            if (_modules.Count > 0)
            {
                return false;
            }

            _modules.Add(new AbilityIdentityModule());
            return true;
        }

        public T GetModule<T>() where T : AbilityAuthoringModule
        {
            EnsureModulesInitialized();
            for (var i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is T typed)
                {
                    return typed;
                }
            }

            return null;
        }

        public T GetOrAddModule<T>() where T : AbilityAuthoringModule, new()
        {
            var existing = GetModule<T>();
            if (existing != null)
            {
                return existing;
            }

            var created = new T();
            _modules.Add(created);
            return created;
        }

        public AbilityDefinition BuildDefinition(CombatAuthoringBuildContext context = null)
        {
            EnsureModulesInitialized();

            var buildContext = context ?? new CombatAuthoringBuildContext();
            if (buildContext.TryGetAbility(this, out AbilityDefinition cachedDefinition))
            {
                return cachedDefinition;
            }

            var identity = GetModule<AbilityIdentityModule>();
            if (identity == null)
            {
                throw new InvalidOperationException(string.Format("{0}: 缺少 AbilityIdentityModule。", name));
            }

            var abilityDefinition = new AbilityDefinition(identity.BuildId(name))
            {
                Name = identity.ResolveDisplayName(name),
                ActivationMode = AbilityActivationMode.Instant,
                Cooldown = new AbilityCooldownDefinition(0, CombatAuthoringUtility.OptionalTag(null)),
                CastDurationTicks = 0,
                ActiveDurationTicks = 0,
                IntervalTicks = 0,
                ExecuteEffectsOnActivate = true,
                CancelOnSourceDeath = true,
                AutoActivatePassive = true,
            };
            buildContext.Cache(this, abilityDefinition);

            for (var i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.ApplyTo(abilityDefinition, this, buildContext);
            }

            return abilityDefinition;
        }

        private void Reset()
        {
            _modules = new List<AbilityAuthoringModule>
            {
                new AbilityIdentityModule(),
            };
        }

        private void OnEnable()
        {
            _modules ??= new List<AbilityAuthoringModule>();
        }
    }
}
