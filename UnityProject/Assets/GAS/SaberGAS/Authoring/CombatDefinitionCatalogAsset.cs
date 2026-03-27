using System;
using System.Collections.Generic;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Saber.GAS.Authoring
{
    /// <summary>
    /// 一份用于集中承载 GAS 配置、自动收集内嵌子资源并一键初始化运行时的根配置资产。
    /// </summary>
    [CreateAssetMenu(menuName = "Saber.GAS/Combat System Config", fileName = "CombatSystem_")]
    public sealed class CombatDefinitionCatalogAsset : ScriptableObject
    {
        [Header("Bootstrap")]
        [SerializeField]
        private uint _randomSeed = 1u;
        [SerializeField]
        private string[] _worldTags = Array.Empty<string>();
        [SerializeField]
        private CombatTagDefinitionAuthoringData[] _globalTagDefinitions = Array.Empty<CombatTagDefinitionAuthoringData>();
        [SerializeField]
        private CombatResourceDefinitionAuthoringData[] _globalResourceDefinitions = Array.Empty<CombatResourceDefinitionAuthoringData>();
        [SerializeField]
        private CombatActorSpawnAuthoringData[] _initialActors = Array.Empty<CombatActorSpawnAuthoringData>();

        [Header("Collected Embedded Definitions")]
        [SerializeField, HideInInspector]
        private AbilityDefinitionAsset[] _abilities = Array.Empty<AbilityDefinitionAsset>();
        [SerializeField, HideInInspector]
        private EffectDefinitionAsset[] _effects = Array.Empty<EffectDefinitionAsset>();
        [SerializeField, HideInInspector]
        private TriggerDefinitionAsset[] _triggers = Array.Empty<TriggerDefinitionAsset>();
        [SerializeField, HideInInspector]
        private CombatActorTemplateAsset[] _actorTemplates = Array.Empty<CombatActorTemplateAsset>();

        /// <summary>
        /// 使用当前根配置直接创建一套可运行的 GAS 系统。
        /// </summary>
        public CombatSystemInstance Initialize(CombatRuntimeOptions runtimeOptions = null)
        {
            var buildContext = new CombatAuthoringBuildContext();
            var worldState = CreateWorldState(buildContext);
            var runtime = new CombatRuntime(worldState, runtimeOptions);
            SpawnInitialActors(runtime, buildContext);
            return new CombatSystemInstance(this, buildContext, worldState, runtime);
        }

        public IReadOnlyList<CombatTagDefinitionAuthoringData> GlobalTagDefinitions => _globalTagDefinitions;

        public IReadOnlyList<CombatResourceDefinitionAuthoringData> GlobalResourceDefinitions => _globalResourceDefinitions;

        /// <summary>
        /// 按当前配置创建一份已经完成定义注册和世界初值写入的世界状态。
        /// </summary>
        public CombatWorldState CreateWorldState(CombatAuthoringBuildContext context = null)
        {
            var buildContext = context ?? new CombatAuthoringBuildContext();
            var worldState = new CombatWorldState
            {
                Random = new DeterministicRandom(_randomSeed == 0 ? 1u : _randomSeed),
            };

            CombatAuthoringUtility.AddTags(worldState.WorldTags, _worldTags);
            RegisterInto(worldState, buildContext);
            return worldState;
        }

        public void Warmup(CombatAuthoringBuildContext context = null)
        {
            var buildContext = context ?? new CombatAuthoringBuildContext();

            for (var i = 0; i < _effects.Length; i++)
            {
                if (_effects[i] != null)
                {
                    buildContext.BuildEffect(_effects[i]);
                }
            }

            for (var i = 0; i < _triggers.Length; i++)
            {
                if (_triggers[i] != null)
                {
                    buildContext.BuildTrigger(_triggers[i]);
                }
            }

            for (var i = 0; i < _abilities.Length; i++)
            {
                if (_abilities[i] != null)
                {
                    buildContext.BuildAbility(_abilities[i]);
                }
            }

            for (var i = 0; i < _actorTemplates.Length; i++)
            {
                if (_actorTemplates[i] != null)
                {
                    _actorTemplates[i].WarmupDefinitions(buildContext);
                }
            }
        }

        public void RegisterInto(CombatWorldState worldState, CombatAuthoringBuildContext context = null)
        {
            if (worldState == null)
            {
                throw new ArgumentNullException(nameof(worldState));
            }

            var buildContext = context ?? new CombatAuthoringBuildContext();
            Warmup(buildContext);

            foreach (var ability in buildContext.BuiltAbilities)
            {
                worldState.AddAbility(ability);
            }
        }

        private void SpawnInitialActors(CombatRuntime runtime, CombatAuthoringBuildContext context)
        {
            if (runtime == null || _initialActors == null)
            {
                return;
            }

            for (var i = 0; i < _initialActors.Length; i++)
            {
                var initialActor = _initialActors[i];
                if (initialActor == null || !initialActor.Enabled)
                {
                    continue;
                }

                initialActor.Spawn(runtime, context, string.Format("{0} InitialActors[{1}]", name, i));
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Saber.GAS/同步内嵌配置")]
        private void SyncEmbeddedDefinitionsContextMenu()
        {
            CollectEmbeddedDefinitions(true);
        }

        [ContextMenu("Saber.GAS/新增 Ability 子配置")]
        private void CreateEmbeddedAbility()
        {
            CreateEmbeddedSubAsset<AbilityDefinitionAsset>("Ability");
        }

        [ContextMenu("Saber.GAS/新增 Effect 子配置")]
        private void CreateEmbeddedEffect()
        {
            CreateEmbeddedSubAsset<EffectDefinitionAsset>("Effect");
        }

        [ContextMenu("Saber.GAS/新增 Trigger 子配置")]
        private void CreateEmbeddedTrigger()
        {
            CreateEmbeddedSubAsset<TriggerDefinitionAsset>("Trigger");
        }

        [ContextMenu("Saber.GAS/新增 ActorTemplate 子配置")]
        private void CreateEmbeddedActorTemplate()
        {
            CreateEmbeddedSubAsset<CombatActorTemplateAsset>("ActorTemplate");
        }

        private void OnValidate()
        {
            CollectEmbeddedDefinitions(false);
        }

        private void CollectEmbeddedDefinitions(bool saveAssets)
        {
            var assetPath = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var abilities = CollectSubAssets<AbilityDefinitionAsset>(assets);
            var effects = CollectSubAssets<EffectDefinitionAsset>(assets);
            var triggers = CollectSubAssets<TriggerDefinitionAsset>(assets);
            var actorTemplates = CollectSubAssets<CombatActorTemplateAsset>(assets);

            if (IsSameArray(_abilities, abilities) &&
                IsSameArray(_effects, effects) &&
                IsSameArray(_triggers, triggers) &&
                IsSameArray(_actorTemplates, actorTemplates))
            {
                return;
            }

            _abilities = abilities;
            _effects = effects;
            _triggers = triggers;
            _actorTemplates = actorTemplates;

            EditorUtility.SetDirty(this);
            if (saveAssets)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath);
            }
        }

        private void CreateEmbeddedSubAsset<T>(string prefix) where T : ScriptableObject
        {
            var assetPath = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            var subAsset = CreateInstance<T>();
            subAsset.name = GenerateEmbeddedName(prefix, assetPath);
            AssetDatabase.AddObjectToAsset(subAsset, this);
            EditorUtility.SetDirty(subAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);
            CollectEmbeddedDefinitions(true);
            Selection.activeObject = subAsset;
        }

        private static T[] CollectSubAssets<T>(UnityEngine.Object[] assets) where T : ScriptableObject
        {
            var results = new List<T>();
            if (assets == null)
            {
                return results.ToArray();
            }

            for (var i = 0; i < assets.Length; i++)
            {
                var asset = assets[i] as T;
                if (asset == null)
                {
                    continue;
                }

                results.Add(asset);
            }

            results.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return results.ToArray();
        }

        private static bool IsSameArray<T>(T[] left, T[] right) where T : UnityEngine.Object
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static string GenerateEmbeddedName(string prefix, string assetPath)
        {
            var existingAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var index = 1;
            while (true)
            {
                var candidate = string.Format("{0}_{1:D2}", prefix, index);
                var exists = false;
                for (var i = 0; i < existingAssets.Length; i++)
                {
                    if (existingAssets[i] != null && string.Equals(existingAssets[i].name, candidate, StringComparison.Ordinal))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    return candidate;
                }

                index += 1;
            }
        }
#endif
    }
}
