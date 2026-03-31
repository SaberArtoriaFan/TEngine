using System;
using System.Collections.Generic;
using System.Linq;
using Saber.GAS.Authoring;
using Saber.GAS.Triggers;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Editor
{
    internal static class GasModuleEditorUtility
    {
        static GasModuleEditorUtility()
        {
            RegisterDefaultModuleDrawers();
        }

        private static void RegisterDefaultModuleDrawers()
        {
            GasModuleDrawerRegistry.Register<AbilityLocalTagLibraryModule>((property, catalog, ownerAsset) =>
            {
                GasEditorUtility.DrawTagDefinitionLibrary(
                    property.FindPropertyRelative("_localTagDefinitions"),
                    "Local Tag Library",
                    "Tags that are only used inside this ability.",
                    CombatTagUsage.Ability);
            });
            GasModuleDrawerRegistry.Register<AbilityTagRulesModule>(DrawAbilityTagRules);
            GasModuleDrawerRegistry.Register<AbilityTargetingModule>((property, catalog, ownerAsset) =>
                DrawAbilityTargeting(property.FindPropertyRelative("_targeting"), catalog, ownerAsset));
            GasModuleDrawerRegistry.Register<AbilityCostCooldownModule>(DrawAbilityCostCooldown);

            GasModuleDrawerRegistry.Register<EffectLocalTagLibraryModule>((property, catalog, ownerAsset) =>
            {
                GasEditorUtility.DrawTagDefinitionLibrary(
                    property.FindPropertyRelative("_localTagDefinitions"),
                    "Local Tag Library",
                    "Tags that are only used inside this effect.",
                    CombatTagUsage.Effect);
            });
            GasModuleDrawerRegistry.Register<EffectTagRulesModule>(DrawEffectTagRules);
            GasModuleDrawerRegistry.Register<EffectRemovalModule>(DrawEffectRemoval);

            GasModuleDrawerRegistry.Register<TriggerLocalTagLibraryModule>((property, catalog, ownerAsset) =>
            {
                GasEditorUtility.DrawTagDefinitionLibrary(
                    property.FindPropertyRelative("_localTagDefinitions"),
                    "Local Tag Library",
                    "Tags that are only used inside this trigger.",
                    CombatTagUsage.Trigger);
            });
            GasModuleDrawerRegistry.Register<TriggerTagFilterModule>(DrawTriggerTagFilters);
            GasModuleDrawerRegistry.Register<TriggerThresholdModule>((property, catalog, ownerAsset) =>
                DrawTriggerThreshold(property.FindPropertyRelative("_threshold"), catalog));
            GasModuleDrawerRegistry.Register<TriggerActionModule>((property, catalog, ownerAsset) =>
                DrawTriggerAction(property.FindPropertyRelative("_action"), catalog, ownerAsset));
        }

        public static void DrawModuleList<TModule>(
            SerializedObject serializedObject,
            SerializedProperty modulesProperty,
            string title,
            string emptyMessage)
            where TModule : CombatAuthoringModule
        {
            GasEditorUtility.DrawSection(title, "Only keep modules that this asset really needs.", () =>
            {
                if (modulesProperty == null || !modulesProperty.isArray)
                {
                    EditorGUILayout.HelpBox("Module list is unavailable.", MessageType.Warning);
                    return;
                }

                if (modulesProperty.arraySize == 0)
                {
                    EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
                }

                for (var index = 0; index < modulesProperty.arraySize; index++)
                {
                    DrawModuleElement(serializedObject, modulesProperty, index);
                }

                if (GUILayout.Button("Add Module"))
                {
                    ShowAddModuleMenu<TModule>(serializedObject, modulesProperty);
                }
            });
        }

        private static void DrawModuleElement(
            SerializedObject serializedObject,
            SerializedProperty modulesProperty,
            int index)
        {
            var element = modulesProperty.GetArrayElementAtIndex(index);
            var title = GetModuleTitle(element);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(index == 0))
            {
                if (GUILayout.Button("Up", GUILayout.Width(48f)))
                {
                    modulesProperty.MoveArrayElement(index, index - 1);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUI.DisabledScope(index >= modulesProperty.arraySize - 1))
            {
                if (GUILayout.Button("Down", GUILayout.Width(48f)))
                {
                    modulesProperty.MoveArrayElement(index, index + 1);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button("Remove", GUILayout.Width(64f)))
            {
                modulesProperty.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();

            DrawModuleBody(element);
            EditorGUILayout.EndVertical();
        }

        private static void ShowAddModuleMenu<TModule>(
            SerializedObject serializedObject,
            SerializedProperty modulesProperty)
            where TModule : CombatAuthoringModule
        {
            var menu = new GenericMenu();
            var existingTypes = modulesProperty
                .ToEnumerable()
                .Select(property => property.managedReferenceValue?.GetType())
                .Where(type => type != null)
                .ToArray();

            var moduleTypes = TypeCache.GetTypesDerivedFrom<TModule>()
                .Where(type => !type.IsAbstract && !type.IsGenericType)
                .OrderBy(GetModuleOrder)
                .ThenBy(GetModuleTitle)
                .ToArray();

            for (var index = 0; index < moduleTypes.Length; index++)
            {
                var moduleType = moduleTypes[index];
                var title = GetModuleTitle(moduleType);
                var alreadyAdded = existingTypes.Any(type => type == moduleType);
                if (alreadyAdded)
                {
                    menu.AddDisabledItem(new GUIContent($"{title} (Added)"));
                    continue;
                }

                menu.AddItem(new GUIContent(title), false, () =>
                {
                    var nextIndex = modulesProperty.arraySize;
                    modulesProperty.InsertArrayElementAtIndex(nextIndex);
                    modulesProperty.GetArrayElementAtIndex(nextIndex).managedReferenceValue = Activator.CreateInstance(moduleType);
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(serializedObject.targetObject);
                });
            }

            menu.ShowAsContext();
        }

        private static void DrawModuleBody(SerializedProperty property)
        {
            if (property == null || property.managedReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Module instance is null.", MessageType.Warning);
                return;
            }

            var ownerAsset = property.serializedObject.targetObject;
            var catalog = GasEditorUtility.FindCatalog(ownerAsset);
            if (GasModuleDrawerRegistry.TryDraw(property, catalog, ownerAsset))
            {
                return;
            }

            DrawChildProperties(property);
        }

        private static void DrawAbilityTagRules(
            SerializedProperty property,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset)
        {
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative("_abilityTags"),
                catalog,
                ownerAsset,
                "Ability Tags",
                CombatTagUsage.Ability,
                "No ability tags.");
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative("_grantedTagsWhileActive"),
                catalog,
                ownerAsset,
                "Granted While Active",
                GasEditorUtility.RuntimeTagUsage,
                "No granted tags.");
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative("_activationRequiredTags"),
                catalog,
                ownerAsset,
                "Required Tags",
                GasEditorUtility.RuntimeTagUsage,
                "No required tags.");
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative("_activationBlockedTags"),
                catalog,
                ownerAsset,
                "Blocked Tags",
                GasEditorUtility.RuntimeTagUsage,
                "No blocked tags.");
        }

        private static void DrawAbilityTargeting(
            SerializedProperty targeting,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset)
        {
            if (targeting == null)
            {
                return;
            }

            GasEditorUtility.DrawProperty(targeting.FindPropertyRelative("_kind"), "Target Kind");
            GasEditorUtility.DrawProperty(targeting.FindPropertyRelative("_allowedFlags"), "Allowed Flags");
            GasEditorUtility.DrawProperty(targeting.FindPropertyRelative("_maxRange"), "Max Range", true);
            GasEditorUtility.DrawTagEditor(
                targeting.FindPropertyRelative("_requiredSourceTags"),
                catalog,
                ownerAsset,
                "Required Source Tags",
                GasEditorUtility.RuntimeTagUsage,
                "No required source tags.");
            GasEditorUtility.DrawTagEditor(
                targeting.FindPropertyRelative("_requiredTargetTags"),
                catalog,
                ownerAsset,
                "Required Target Tags",
                GasEditorUtility.RuntimeTagUsage,
                "No required target tags.");
            GasEditorUtility.DrawTagEditor(
                targeting.FindPropertyRelative("_blockedSourceTags"),
                catalog,
                ownerAsset,
                "Blocked Source Tags",
                GasEditorUtility.RuntimeTagUsage,
                "No blocked source tags.");
            GasEditorUtility.DrawTagEditor(
                targeting.FindPropertyRelative("_blockedTargetTags"),
                catalog,
                ownerAsset,
                "Blocked Target Tags",
                GasEditorUtility.RuntimeTagUsage,
                "No blocked target tags.");
        }

        private static void DrawAbilityCostCooldown(
            SerializedProperty property,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset)
        {
            GasEditorUtility.DrawProperty(property.FindPropertyRelative("_costs"), "Costs", true);
            GasEditorUtility.DrawProperty(property.FindPropertyRelative("_cooldownTicks"), "Cooldown Ticks");
            GasEditorUtility.DrawSingleTagEditor(
                property.FindPropertyRelative("_cooldownTag"),
                catalog,
                ownerAsset,
                "Cooldown Tag",
                CombatTagUsage.Ability,
                "No cooldown tag.");
        }

        private static void DrawEffectTagRules(
            SerializedProperty property,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset)
        {
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative("_effectTags"),
                catalog,
                ownerAsset,
                "Effect Tags",
                CombatTagUsage.Effect,
                "No effect tags.");
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative("_grantedTags"),
                catalog,
                ownerAsset,
                "Granted Tags",
                GasEditorUtility.RuntimeTagUsage,
                "No granted tags.");
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative("_requiredTargetTags"),
                catalog,
                ownerAsset,
                "Required Target Tags",
                GasEditorUtility.RuntimeTagUsage,
                "No required target tags.");
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative("_blockedTargetTags"),
                catalog,
                ownerAsset,
                "Blocked Target Tags",
                GasEditorUtility.RuntimeTagUsage,
                "No blocked target tags.");
        }

        private static void DrawEffectRemoval(
            SerializedProperty property,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset)
        {
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative("_removedTargetEffectTags"),
                catalog,
                ownerAsset,
                "Removed Effect Tags",
                CombatTagUsage.Effect,
                "No removed tags.");
            GasEditorUtility.DrawProperty(property.FindPropertyRelative("_removedTargetEffects"), "Removed Effects", true);
        }

        private static void DrawTriggerTagFilters(
            SerializedProperty property,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset)
        {
            DrawTriggerTagField(property, "_requiredOwnerTags", "Required Owner Tags", catalog, ownerAsset, GasEditorUtility.RuntimeTagUsage);
            DrawTriggerTagField(property, "_blockedOwnerTags", "Blocked Owner Tags", catalog, ownerAsset, GasEditorUtility.RuntimeTagUsage);
            DrawTriggerTagField(property, "_requiredInstigatorTags", "Required Instigator Tags", catalog, ownerAsset, GasEditorUtility.RuntimeTagUsage);
            DrawTriggerTagField(property, "_blockedInstigatorTags", "Blocked Instigator Tags", catalog, ownerAsset, GasEditorUtility.RuntimeTagUsage);
            DrawTriggerTagField(property, "_requiredTargetTags", "Required Target Tags", catalog, ownerAsset, GasEditorUtility.RuntimeTagUsage);
            DrawTriggerTagField(property, "_blockedTargetTags", "Blocked Target Tags", catalog, ownerAsset, GasEditorUtility.RuntimeTagUsage);
            DrawTriggerTagField(property, "_requiredIncomingAbilityTags", "Required Incoming Ability Tags", catalog, ownerAsset, CombatTagUsage.Ability);
            DrawTriggerTagField(property, "_blockedIncomingAbilityTags", "Blocked Incoming Ability Tags", catalog, ownerAsset, CombatTagUsage.Ability);
            DrawTriggerTagField(property, "_requiredIncomingEffectTags", "Required Incoming Effect Tags", catalog, ownerAsset, CombatTagUsage.Effect);
            DrawTriggerTagField(property, "_blockedIncomingEffectTags", "Blocked Incoming Effect Tags", catalog, ownerAsset, CombatTagUsage.Effect);
            DrawTriggerTagField(property, "_requiredImpactTags", "Required Impact Tags", catalog, ownerAsset, CombatTagUsage.Impact);
            DrawTriggerTagField(property, "_blockedImpactTags", "Blocked Impact Tags", catalog, ownerAsset, CombatTagUsage.Impact);
        }

        private static void DrawTriggerTagField(
            SerializedProperty property,
            string childPath,
            string label,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset,
            CombatTagUsage usage)
        {
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative(childPath),
                catalog,
                ownerAsset,
                label,
                usage,
                "Not configured.");
        }

        private static void DrawTriggerThreshold(
            SerializedProperty threshold,
            CombatDefinitionCatalogAsset catalog)
        {
            if (threshold == null)
            {
                return;
            }

            GasResourceEditorUtility.DrawSingleResourceEditor(
                threshold.FindPropertyRelative("_resourceId"),
                catalog,
                "Resource Id",
                "No resource selected.");
            GasEditorUtility.DrawProperty(threshold.FindPropertyRelative("_value"), "Threshold", true);
            GasEditorUtility.DrawProperty(threshold.FindPropertyRelative("_direction"), "Direction");
        }

        private static void DrawTriggerAction(
            SerializedProperty action,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset)
        {
            if (action == null)
            {
                return;
            }

            var kindProperty = action.FindPropertyRelative("_kind");
            GasEditorUtility.DrawProperty(kindProperty, "Action Kind");

            var kind = kindProperty == null ? TriggerActionKind.None : (TriggerActionKind)kindProperty.enumValueIndex;
            var fields = TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor;
            if (CombatTriggerActionDescriptorRegistryHub.TryGetDescriptor(kind, out var descriptor))
            {
                fields = descriptor.InspectorFields;
            }
            else
            {
                EditorGUILayout.HelpBox($"No descriptor found for {kind}. Fallback fields are shown.", MessageType.Info);
            }

            var sourceActor = action.FindPropertyRelative("_sourceActor");
            var targetActor = action.FindPropertyRelative("_targetActor");
            var triggeredAbility = action.FindPropertyRelative("_triggeredAbility");
            var effectAsset = action.FindPropertyRelative("_effectAsset");
            var effectTag = action.FindPropertyRelative("_effectTag");
            var resourceId = action.FindPropertyRelative("_resourceId");
            var resourceAmount = action.FindPropertyRelative("_resourceAmount");
            var tag = action.FindPropertyRelative("_tag");
            var operationTemplate = action.FindPropertyRelative("_operationTemplate");
            var magnitudeMultiplier = action.FindPropertyRelative("_magnitudeMultiplier");
            var cueName = action.FindPropertyRelative("_cueName");
            var abilityToCancel = action.FindPropertyRelative("_abilityToCancel");
            var customActionId = action.FindPropertyRelative("_customActionId");

            if (HasField(fields, TriggerActionInspectorField.SourceActor))
            {
                GasEditorUtility.DrawProperty(sourceActor, "Source Actor");
            }

            if (HasField(fields, TriggerActionInspectorField.TargetActor))
            {
                GasEditorUtility.DrawProperty(targetActor, "Target Actor");
            }

            if (HasField(fields, TriggerActionInspectorField.TriggeredAbility))
            {
                GasEditorUtility.DrawProperty(triggeredAbility, "Triggered Ability");
            }

            if (HasField(fields, TriggerActionInspectorField.EffectAsset))
            {
                GasEditorUtility.DrawProperty(effectAsset, "Effect Asset");
            }

            if (HasField(fields, TriggerActionInspectorField.EffectTag))
            {
                GasEditorUtility.DrawSingleTagEditor(
                    effectTag,
                    catalog,
                    ownerAsset,
                    "Effect Tag",
                    CombatTagUsage.Effect,
                    "No effect tag.");
            }

            if (HasField(fields, TriggerActionInspectorField.ResourceId))
            {
                GasResourceEditorUtility.DrawSingleResourceEditor(resourceId, catalog, "Resource Id", "No resource selected.");
            }

            if (HasField(fields, TriggerActionInspectorField.ResourceAmount))
            {
                GasEditorUtility.DrawProperty(resourceAmount, "Resource Amount", true);
            }

            if (HasField(fields, TriggerActionInspectorField.RuntimeTag))
            {
                GasEditorUtility.DrawSingleTagEditor(
                    tag,
                    catalog,
                    ownerAsset,
                    "Runtime Tag",
                    GasEditorUtility.RuntimeTagUsage,
                    "No runtime tag.");
            }

            if (HasField(fields, TriggerActionInspectorField.OperationTemplate))
            {
                DrawImpactOperation(operationTemplate, catalog, ownerAsset);
            }

            if (HasField(fields, TriggerActionInspectorField.MagnitudeMultiplier))
            {
                GasEditorUtility.DrawProperty(magnitudeMultiplier, "Magnitude Multiplier", true);
            }

            if (HasField(fields, TriggerActionInspectorField.CueName))
            {
                GasEditorUtility.DrawProperty(cueName, "Cue Name");
            }

            if (HasField(fields, TriggerActionInspectorField.AbilityToCancel))
            {
                GasEditorUtility.DrawProperty(abilityToCancel, "Ability To Cancel");
            }

            if (HasField(fields, TriggerActionInspectorField.CustomActionId))
            {
                GasEditorUtility.DrawProperty(customActionId, "Custom Action Id");
            }
        }

        private static void DrawImpactOperation(
            SerializedProperty operationTemplate,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset)
        {
            if (operationTemplate == null)
            {
                return;
            }

            GasEditorUtility.DrawProperty(operationTemplate.FindPropertyRelative("_type"), "Impact Operation Type");
            GasResourceEditorUtility.DrawSingleResourceEditor(
                operationTemplate.FindPropertyRelative("_resourceId"),
                catalog,
                "Resource Id",
                "No resource selected.");
            GasEditorUtility.DrawProperty(operationTemplate.FindPropertyRelative("_effectAsset"), "Effect Asset");
            GasEditorUtility.DrawSingleTagEditor(
                operationTemplate.FindPropertyRelative("_tag"),
                catalog,
                ownerAsset,
                "Tag",
                CombatTagUsage.Impact | GasEditorUtility.RuntimeTagUsage,
                "No tag selected.");
            GasEditorUtility.DrawProperty(operationTemplate.FindPropertyRelative("_amount"), "Amount", true);
            GasEditorUtility.DrawProperty(operationTemplate.FindPropertyRelative("_cueName"), "Cue Name");
        }

        private static bool HasField(TriggerActionInspectorField value, TriggerActionInspectorField field)
        {
            return (value & field) == field;
        }

        private static void DrawChildProperties(SerializedProperty property)
        {
            var iterator = property.Copy();
            var endProperty = iterator.GetEndProperty();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                EditorGUILayout.PropertyField(iterator, true);
                enterChildren = false;
            }
        }

        private static int GetModuleOrder(Type moduleType)
        {
            return moduleType?.GetCustomAttributes(typeof(GasAuthoringModuleAttribute), false)
                .OfType<GasAuthoringModuleAttribute>()
                .FirstOrDefault()?.Order ?? 0;
        }

        private static string GetModuleTitle(SerializedProperty property)
        {
            return property?.managedReferenceValue == null
                ? "Module"
                : GetModuleTitle(property.managedReferenceValue.GetType());
        }

        private static string GetModuleTitle(Type moduleType)
        {
            if (moduleType == null)
            {
                return "Module";
            }

            var attribute = moduleType.GetCustomAttributes(typeof(GasAuthoringModuleAttribute), false)
                .OfType<GasAuthoringModuleAttribute>()
                .FirstOrDefault();
            return attribute?.Title ?? ObjectNames.NicifyVariableName(moduleType.Name);
        }

        private static IEnumerable<SerializedProperty> ToEnumerable(this SerializedProperty arrayProperty)
        {
            if (arrayProperty == null || !arrayProperty.isArray)
            {
                yield break;
            }

            for (var index = 0; index < arrayProperty.arraySize; index++)
            {
                yield return arrayProperty.GetArrayElementAtIndex(index);
            }
        }
    }
}
