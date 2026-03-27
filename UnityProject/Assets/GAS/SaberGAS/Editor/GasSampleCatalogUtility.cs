using Saber.GAS.Authoring;
using Saber.GAS.Effects;
using Saber.GAS.Triggers;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Editor
{
    public static class GasSampleCatalogUtility
    {
        public const string QuickStartDocumentPath = "Assets/GAS/SaberGAS/Docs/18_GAS快速开始.md";

        public const string ModuleExtensionGuideDocumentPath = "Assets/GAS/SaberGAS/Docs/19_GAS模块扩展指南.md";

        private const string SampleFolder = "Assets/GAS/SaberGAS/Samples";

        [MenuItem("Saber.GAS/Create Workbench Sample Catalog")]
        public static void CreateWorkbenchSampleCatalogMenu()
        {
            CreateWorkbenchSampleCatalog();
        }

        [MenuItem("Saber.GAS/Open Quick Start Guide")]
        public static void OpenQuickStartGuideMenu()
        {
            GasEditorUtility.OpenAssetAtPath(QuickStartDocumentPath);
        }

        [MenuItem("Saber.GAS/Open Module Extension Guide")]
        public static void OpenModuleExtensionGuideMenu()
        {
            GasEditorUtility.OpenAssetAtPath(ModuleExtensionGuideDocumentPath);
        }

        public static CombatDefinitionCatalogAsset CreateWorkbenchSampleCatalog()
        {
            EnsureFolder(SampleFolder);

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(SampleFolder + "/GASWorkbenchSample.asset");
            var catalog = ScriptableObject.CreateInstance<CombatDefinitionCatalogAsset>();
            catalog.name = "GASWorkbenchSample";
            AssetDatabase.CreateAsset(catalog, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);

            var instantDamageAbility = GasAuthoringTemplateUtility.CreateInstantDamageAbilityTemplate(catalog, false);
            var buffEffect = GasAuthoringTemplateUtility.CreateBuffEffectTemplate(catalog, false);
            var passiveTrigger = GasAuthoringTemplateUtility.CreatePassiveTriggerTemplate(catalog, false);
            var actorTemplate = GasAuthoringTemplateUtility.CreateBlankActorTemplate(catalog, false);

            ConfigureCatalog(catalog);
            ConfigureAbility(instantDamageAbility);
            ConfigureBuffEffect(buffEffect);
            ConfigurePassiveTrigger(passiveTrigger, buffEffect);
            ConfigureActorTemplate(actorTemplate, instantDamageAbility, passiveTrigger);
            ConfigureInitialActor(catalog, actorTemplate);

            GasEditorUtility.SyncEmbeddedDefinitions(catalog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            GasEditorUtility.FocusAsset(catalog);
            GasWorkbenchWindow.Open(catalog);
            return catalog;
        }

        private static void ConfigureCatalog(CombatDefinitionCatalogAsset catalog)
        {
            if (catalog == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(catalog);
            serializedObject.FindProperty("_randomSeed").intValue = 20260326;
            GasEditorUtility.SetStringArray(serializedObject, "_worldTags", "sample.workbench", "combat.demo");
            SetTagDefinitions(
                serializedObject.FindProperty("_globalTagDefinitions"),
                new TagDefinition("sample.workbench", "Sample", CombatTagUsage.Shared, "示例 Catalog 的根标签。"),
                new TagDefinition("combat.demo", "Sample", CombatTagUsage.Shared, "用于标记当前演示场景。"),
                new TagDefinition("unit.player", "Actor", CombatTagUsage.Actor | CombatTagUsage.Trigger, "玩家单位常用标签。"),
                new TagDefinition("class.mage", "Actor", CombatTagUsage.Actor, "法师职业示例标签。"),
                new TagDefinition("ability.offense", "Ability", CombatTagUsage.Ability, "进攻型 Ability 的公共标签。"),
                new TagDefinition("cooldown.firebolt", "Ability", CombatTagUsage.Ability, "FireBolt 的冷却标签。"),
                new TagDefinition("effect.buff", "Effect", CombatTagUsage.Effect, "Buff 类效果的公共标签。"),
                new TagDefinition("state.focused", "State", CombatTagUsage.Effect | CombatTagUsage.Trigger, "专注状态，常被效果与触发器共同引用。"));
            SetResourceDefinitions(
                serializedObject.FindProperty("_globalResourceDefinitions"),
                new ResourceDefinition("health", "Combat", "生命值，伤害和治疗最常用的资源。"),
                new ResourceDefinition("mana", "Combat", "法力值，用于施法消耗演示。"));
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void ConfigureAbility(AbilityDefinitionAsset ability)
        {
            if (ability == null)
            {
                return;
            }

            ability.name = "Ability_FireBolt";
            ability.EnsureModulesInitialized();

            var identity = ability.GetOrAddModule<AbilityIdentityModule>();
            identity.AbilityId = "ability.sample.firebolt";
            identity.DisplayName = "Fire Bolt";

            var tagRules = ability.GetOrAddModule<AbilityTagRulesModule>();
            tagRules.AbilityTags = new[] { "ability.offense", "sample.fire" };

            var costCooldown = ability.GetOrAddModule<AbilityCostCooldownModule>();
            costCooldown.CooldownTag = "cooldown.firebolt";
            costCooldown.CooldownTicks = 60;
            costCooldown.Costs = new[]
            {
                new ResourceCostAuthoringData("mana", new FixedPointValue(20f)),
            };

            ability.GetOrAddModule<AbilityLocalTagLibraryModule>().Definitions = new[]
            {
                new CombatTagDefinitionAuthoringData("sample.fire", "Ability Internal", CombatTagUsage.Ability, "Ability-local fire tag."),
                new CombatTagDefinitionAuthoringData("state.firebolt.channeling", "Ability Internal", CombatTagUsage.Ability | CombatTagUsage.Trigger, "Shared local tag for Fire Bolt channeling state."),
            };

            EditorUtility.SetDirty(ability);
        }

        private static void ConfigureBuffEffect(EffectDefinitionAsset effect)
        {
            if (effect == null)
            {
                return;
            }

            effect.name = "Effect_BattleFocus";
            effect.EnsureModulesInitialized();

            effect.GetOrAddModule<EffectIdentityModule>().EffectId = "effect.sample.battlefocus";

            var tagRules = effect.GetOrAddModule<EffectTagRulesModule>();
            tagRules.EffectTags = new[] { "effect.buff", "sample.focus" };
            tagRules.GrantedTags = new[] { "state.focused", "state.buff.sample" };

            var timing = effect.GetOrAddModule<EffectTimingModule>();
            timing.DurationPolicy = EffectDurationPolicy.Timed;
            timing.DurationTicks = 180;
            timing.PeriodTicks = 30;

            effect.GetOrAddModule<EffectResourcePayloadModule>().PeriodicResourceDeltas = new[]
            {
                new ResourceDeltaAuthoringData("health", new FixedPointValue(5f)),
            };

            effect.GetOrAddModule<EffectLocalTagLibraryModule>().Definitions = new[]
            {
                new CombatTagDefinitionAuthoringData("sample.focus", "Effect Internal", CombatTagUsage.Effect, "Effect-local focus tag."),
                new CombatTagDefinitionAuthoringData("state.buff.sample", "Effect Internal", CombatTagUsage.Effect | CombatTagUsage.Trigger, "Shared local tag between the sample effect and trigger."),
            };

            EditorUtility.SetDirty(effect);
        }

        private static void ConfigurePassiveTrigger(TriggerDefinitionAsset trigger, EffectDefinitionAsset buffEffect)
        {
            if (trigger == null)
            {
                return;
            }

            trigger.name = "Trigger_BattleFocusPulse";
            trigger.EnsureModulesInitialized();

            var identity = trigger.GetOrAddModule<TriggerIdentityModule>();
            identity.TriggerId = "trigger.sample.battlefocus_pulse";
            identity.DisplayName = "Battle Focus Pulse";

            var routing = trigger.GetOrAddModule<TriggerRoutingModule>();
            routing.SourceKind = TriggerSourceKind.Actor;
            routing.EventKind = CombatTriggerEventKind.OnTick;
            routing.Timing = CombatTriggerTiming.EndOfStage;
            routing.CollectionMode = TriggerCollectionMode.OwnerOnly;

            trigger.GetOrAddModule<TriggerTagFilterModule>().RequiredOwnerTags = new[] { "unit.player" };
            trigger.GetOrAddModule<TriggerLocalTagLibraryModule>().Definitions = new[]
            {
                new CombatTagDefinitionAuthoringData("trigger.battlefocus.pulse", "Trigger Internal", CombatTagUsage.Trigger, "Trigger-local execution marker."),
            };

            trigger.GetOrAddModule<TriggerActionModule>().Action = new TriggerActionAuthoringData
            {
                Kind = TriggerActionKind.ApplyEffect,
                SourceActor = TriggerActorReference.Owner,
                TargetActor = TriggerActorReference.Owner,
                EffectAsset = buffEffect,
            };

            EditorUtility.SetDirty(trigger);
        }

        private static void ConfigureActorTemplate(
            CombatActorTemplateAsset actorTemplate,
            AbilityDefinitionAsset grantedAbility,
            TriggerDefinitionAsset grantedTrigger)
        {
            if (actorTemplate == null)
            {
                return;
            }

            actorTemplate.name = "ActorTemplate_Hero";
            var serializedObject = new SerializedObject(actorTemplate);
            GasEditorUtility.SetString(serializedObject, "_defaultActorId", "actor.sample.hero");
            GasEditorUtility.SetString(serializedObject, "_defaultTeamId", "player");
            GasEditorUtility.SetStringArray(serializedObject, "_initialTags", "unit.player", "sample.hero", "class.mage");
            GasEditorUtility.SetObjectArray(serializedObject, "_grantedAbilities", grantedAbility);
            GasEditorUtility.SetObjectArray(serializedObject, "_actorTriggers", grantedTrigger);

            var resources = serializedObject.FindProperty("_resources");
            resources.arraySize = 2;
            resources.GetArrayElementAtIndex(0).FindPropertyRelative("_resourceId").stringValue = "health";
            resources.GetArrayElementAtIndex(0).FindPropertyRelative("_current").FindPropertyRelative("_value").floatValue = 100f;
            resources.GetArrayElementAtIndex(0).FindPropertyRelative("_max").FindPropertyRelative("_value").floatValue = 100f;
            resources.GetArrayElementAtIndex(1).FindPropertyRelative("_resourceId").stringValue = "mana";
            resources.GetArrayElementAtIndex(1).FindPropertyRelative("_current").FindPropertyRelative("_value").floatValue = 60f;
            resources.GetArrayElementAtIndex(1).FindPropertyRelative("_max").FindPropertyRelative("_value").floatValue = 60f;

            var position = serializedObject.FindProperty("_defaultPosition");
            position.FindPropertyRelative("_x").floatValue = 0f;
            position.FindPropertyRelative("_y").floatValue = 0f;
            position.FindPropertyRelative("_z").floatValue = 0f;

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(actorTemplate);
        }

        private static void ConfigureInitialActor(CombatDefinitionCatalogAsset catalog, CombatActorTemplateAsset actorTemplate)
        {
            if (catalog == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(catalog);
            var initialActors = serializedObject.FindProperty("_initialActors");
            initialActors.arraySize = 1;
            var actor = initialActors.GetArrayElementAtIndex(0);
            actor.FindPropertyRelative("_enabled").boolValue = true;
            actor.FindPropertyRelative("_template").objectReferenceValue = actorTemplate;
            actor.FindPropertyRelative("_actorIdOverride").stringValue = "actor.sample.hero.spawned";
            actor.FindPropertyRelative("_teamIdOverride").stringValue = "player";
            actor.FindPropertyRelative("_overridePosition").boolValue = true;
            var position = actor.FindPropertyRelative("_positionOverride");
            position.FindPropertyRelative("_x").floatValue = 0f;
            position.FindPropertyRelative("_y").floatValue = 0f;
            position.FindPropertyRelative("_z").floatValue = 0f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void SetTagDefinitions(SerializedProperty property, params TagDefinition[] definitions)
        {
            if (property == null || !property.isArray)
            {
                return;
            }

            property.arraySize = definitions.Length;
            for (var i = 0; i < definitions.Length; i++)
            {
                var entry = property.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_tag").stringValue = definitions[i].Tag;
                entry.FindPropertyRelative("_group").stringValue = definitions[i].Group;
                entry.FindPropertyRelative("_usage").intValue = (int)definitions[i].Usage;
                entry.FindPropertyRelative("_note").stringValue = definitions[i].Note;
            }
        }

        private static void SetResourceDefinitions(SerializedProperty property, params ResourceDefinition[] definitions)
        {
            if (property == null || !property.isArray)
            {
                return;
            }

            property.arraySize = definitions.Length;
            for (var i = 0; i < definitions.Length; i++)
            {
                var entry = property.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_resourceId").stringValue = definitions[i].ResourceId;
                entry.FindPropertyRelative("_group").stringValue = definitions[i].Group;
                entry.FindPropertyRelative("_note").stringValue = definitions[i].Note;
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            var segments = folderPath.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private readonly struct TagDefinition
        {
            public TagDefinition(string tag, string group, CombatTagUsage usage, string note)
            {
                Tag = tag;
                Group = group;
                Usage = usage;
                Note = note;
            }

            public string Tag { get; }

            public string Group { get; }

            public CombatTagUsage Usage { get; }

            public string Note { get; }
        }

        private readonly struct ResourceDefinition
        {
            public ResourceDefinition(string resourceId, string group, string note)
            {
                ResourceId = resourceId;
                Group = group;
                Note = note;
            }

            public string ResourceId { get; }

            public string Group { get; }

            public string Note { get; }
        }
    }
}
