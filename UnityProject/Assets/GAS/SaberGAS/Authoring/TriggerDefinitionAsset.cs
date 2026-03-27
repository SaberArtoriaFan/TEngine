using System;
using System.Collections.Generic;
using Saber.GAS.Runtime;
using Saber.GAS.Triggers;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    [CreateAssetMenu(menuName = "Saber.GAS/Trigger Definition", fileName = "Trigger_")]
    public sealed class TriggerDefinitionAsset : ScriptableObject
    {
        [SerializeReference]
        private List<TriggerAuthoringModule> _modules = new List<TriggerAuthoringModule>();

        public IReadOnlyList<TriggerAuthoringModule> Modules
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
            _modules ??= new List<TriggerAuthoringModule>();
            if (_modules.Count > 0)
            {
                return false;
            }

            _modules.Add(new TriggerIdentityModule());
            return true;
        }

        public T GetModule<T>() where T : TriggerAuthoringModule
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

        public T GetOrAddModule<T>() where T : TriggerAuthoringModule, new()
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

        public TriggerDefinition BuildDefinition(CombatAuthoringBuildContext context = null)
        {
            EnsureModulesInitialized();

            var buildContext = context ?? new CombatAuthoringBuildContext();
            if (buildContext.TryGetTrigger(this, out TriggerDefinition cachedDefinition))
            {
                return cachedDefinition;
            }

            var identity = GetModule<TriggerIdentityModule>();
            if (identity == null)
            {
                throw new InvalidOperationException(string.Format("{0}: 缺少 TriggerIdentityModule。", name));
            }

            var triggerDefinition = new TriggerDefinition(identity.BuildId(name))
            {
                Name = identity.ResolveDisplayName(name),
                SourceKind = TriggerSourceKind.Actor,
                EventKind = CombatTriggerEventKind.Manual,
                Timing = CombatTriggerTiming.Manual,
                CollectionMode = TriggerCollectionMode.OwnerOnly,
                Priority = 0,
                RelationFilter = CombatActorRelationFlags.Any,
                TargetRelationFilter = CombatActorRelationFlags.Any,
                InstigatorRelationFilter = CombatActorRelationFlags.Any,
                ObserverMaxDistance = 0,
                CooldownTicks = 0,
                MaxTriggerCountPerTick = 0,
                MaxTriggerCountTotal = 0,
                InitialCharges = 0,
                ConsumeSourceEffectOnTrigger = false,
                RemoveSourceEffectOnTrigger = false,
                DisableWhenSourceDead = false,
                DisableWhenTargetDead = false,
                EnabledOnCreate = true,
            };
            buildContext.Cache(this, triggerDefinition);

            for (var i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.ApplyTo(triggerDefinition, this, buildContext);
            }

            return triggerDefinition;
        }

        private void Reset()
        {
            _modules = new List<TriggerAuthoringModule>
            {
                new TriggerIdentityModule(),
            };
        }

        private void OnEnable()
        {
            _modules ??= new List<TriggerAuthoringModule>();
        }
    }
}
