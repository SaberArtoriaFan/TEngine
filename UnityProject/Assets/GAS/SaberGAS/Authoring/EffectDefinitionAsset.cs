using System;
using System.Collections.Generic;
using Saber.GAS.Effects;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    [CreateAssetMenu(menuName = "Saber.GAS/Effect Definition", fileName = "Effect_")]
    public sealed class EffectDefinitionAsset : ScriptableObject
    {
        [SerializeReference]
        private List<EffectAuthoringModule> _modules = new List<EffectAuthoringModule>();

        public IReadOnlyList<EffectAuthoringModule> Modules
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
            _modules ??= new List<EffectAuthoringModule>();
            if (_modules.Count > 0)
            {
                return false;
            }

            _modules.Add(new EffectIdentityModule());
            return true;
        }

        public T GetModule<T>() where T : EffectAuthoringModule
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

        public T GetOrAddModule<T>() where T : EffectAuthoringModule, new()
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

        public EffectDefinition BuildDefinition(CombatAuthoringBuildContext context = null)
        {
            EnsureModulesInitialized();

            var buildContext = context ?? new CombatAuthoringBuildContext();
            if (buildContext.TryGetEffect(this, out EffectDefinition cachedDefinition))
            {
                return cachedDefinition;
            }

            var identity = GetModule<EffectIdentityModule>();
            if (identity == null)
            {
                throw new InvalidOperationException(string.Format("{0}: 缺少 EffectIdentityModule。", name));
            }

            var effectDefinition = new EffectDefinition(identity.BuildId(name))
            {
                DurationTicks = 0,
                PeriodTicks = 0,
                MaxStacks = 1,
                DurationPolicy = EffectDurationPolicy.Instant,
                StackPolicy = EffectStackPolicy.RefreshDuration,
            };
            buildContext.Cache(this, effectDefinition);

            for (var i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.ApplyTo(effectDefinition, this, buildContext);
            }

            return effectDefinition;
        }

        private void Reset()
        {
            _modules = new List<EffectAuthoringModule>
            {
                new EffectIdentityModule(),
            };
        }

        private void OnEnable()
        {
            _modules ??= new List<EffectAuthoringModule>();
        }
    }
}
