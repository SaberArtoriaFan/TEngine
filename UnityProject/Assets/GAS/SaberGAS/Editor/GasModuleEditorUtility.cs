using System;
using System.Linq;
using Saber.GAS.Authoring;
using Saber.GAS.Triggers;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Editor
{
    internal static class GasModuleEditorUtility
    {
        public static void DrawModuleList<TModule>(
            SerializedObject serializedObject,
            SerializedProperty modulesProperty,
            string title,
            string emptyMessage)
            where TModule : CombatAuthoringModule
        {
            GasEditorUtility.DrawSection(title, "只添加当前需要的模块，面板就会保持轻量。", () =>
            {
                if (modulesProperty == null || !modulesProperty.isArray)
                {
                    EditorGUILayout.HelpBox("模块列表当前不可用。", MessageType.Warning);
                    return;
                }

                if (modulesProperty.arraySize == 0)
                {
                    EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
                }

                for (var i = 0; i < modulesProperty.arraySize; i++)
                {
                    DrawModuleElement(serializedObject, modulesProperty, i);
                }

                if (GUILayout.Button("添加模块"))
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
                if (GUILayout.Button("上移", GUILayout.Width(48f)))
                {
                    modulesProperty.MoveArrayElement(index, index - 1);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUI.DisabledScope(index >= modulesProperty.arraySize - 1))
            {
                if (GUILayout.Button("下移", GUILayout.Width(48f)))
                {
                    modulesProperty.MoveArrayElement(index, index + 1);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button("移除", GUILayout.Width(48f)))
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

            for (var i = 0; i < moduleTypes.Length; i++)
            {
                var moduleType = moduleTypes[i];
                var title = GetModuleTitle(moduleType);
                var alreadyAdded = existingTypes.Any(type => type == moduleType);
                if (alreadyAdded)
                {
                    menu.AddDisabledItem(new GUIContent(title + "（已添加）"));
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
                EditorGUILayout.HelpBox("模块实例当前为空。", MessageType.Warning);
                return;
            }

            var ownerAsset = property.serializedObject.targetObject;
            var catalog = GasEditorUtility.FindCatalog(ownerAsset);

            switch (property.managedReferenceValue)
            {
                case AbilityLocalTagLibraryModule:
                    GasEditorUtility.DrawTagDefinitionLibrary(
                        property.FindPropertyRelative("_localTagDefinitions"),
                        "局部标签词库",
                        "只给当前 Ability 链路内部使用的标签放这里。",
                        CombatTagUsage.Ability);
                    return;
                case AbilityTagRulesModule:
                    DrawAbilityTagRules(property, catalog, ownerAsset);
                    return;
                case AbilityTargetingModule:
                    DrawAbilityTargeting(property.FindPropertyRelative("_targeting"), catalog, ownerAsset);
                    return;
                case AbilityCostCooldownModule:
                    DrawAbilityCostCooldown(property, catalog, ownerAsset);
                    return;
                case EffectLocalTagLibraryModule:
                    GasEditorUtility.DrawTagDefinitionLibrary(
                        property.FindPropertyRelative("_localTagDefinitions"),
                        "局部标签词库",
                        "只给当前 Effect 链路内部使用的标签放这里。",
                        CombatTagUsage.Effect);
                    return;
                case EffectTagRulesModule:
                    DrawEffectTagRules(property, catalog, ownerAsset);
                    return;
                case EffectRemovalModule:
                    DrawEffectRemoval(property, catalog, ownerAsset);
                    return;
                case TriggerLocalTagLibraryModule:
                    GasEditorUtility.DrawTagDefinitionLibrary(
                        property.FindPropertyRelative("_localTagDefinitions"),
                        "局部标签词库",
                        "只给当前 Trigger 链路内部使用的标签放这里。",
                        CombatTagUsage.Trigger);
                    return;
                case TriggerTagFilterModule:
                    DrawTriggerTagFilters(property, catalog, ownerAsset);
                    return;
                case TriggerThresholdModule:
                    DrawTriggerThreshold(property.FindPropertyRelative("_threshold"), catalog);
                    return;
                case TriggerActionModule:
                    DrawTriggerAction(property.FindPropertyRelative("_action"), catalog, ownerAsset);
                    return;
                default:
                    DrawChildProperties(property);
                    return;
            }
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
                "能力标签",
                CombatTagUsage.Ability,
                "当前没有能力标签。");
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative("_grantedTagsWhileActive"),
                catalog,
                ownerAsset,
                "激活期间授予标签",
                GasEditorUtility.RuntimeTagUsage,
                "当前不会授予额外标签。");
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative("_activationRequiredTags"),
                catalog,
                ownerAsset,
                "激活所需标签",
                GasEditorUtility.RuntimeTagUsage,
                "当前没有激活前置标签。");
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative("_activationBlockedTags"),
                catalog,
                ownerAsset,
                "激活阻断标签",
                GasEditorUtility.RuntimeTagUsage,
                "当前没有阻断标签。");
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

            GasEditorUtility.DrawProperty(targeting.FindPropertyRelative("_kind"), "目标类型");
            GasEditorUtility.DrawProperty(targeting.FindPropertyRelative("_allowedFlags"), "允许目标标记");
            GasEditorUtility.DrawProperty(targeting.FindPropertyRelative("_maxRange"), "最大距离", true);
            GasEditorUtility.DrawTagEditor(
                targeting.FindPropertyRelative("_requiredSourceTags"),
                catalog,
                ownerAsset,
                "施法者所需标签",
                GasEditorUtility.RuntimeTagUsage,
                "当前不要求施法者标签。");
            GasEditorUtility.DrawTagEditor(
                targeting.FindPropertyRelative("_requiredTargetTags"),
                catalog,
                ownerAsset,
                "目标所需标签",
                GasEditorUtility.RuntimeTagUsage,
                "当前不要求目标标签。");
            GasEditorUtility.DrawTagEditor(
                targeting.FindPropertyRelative("_blockedSourceTags"),
                catalog,
                ownerAsset,
                "施法者阻断标签",
                GasEditorUtility.RuntimeTagUsage,
                "当前没有施法者阻断标签。");
            GasEditorUtility.DrawTagEditor(
                targeting.FindPropertyRelative("_blockedTargetTags"),
                catalog,
                ownerAsset,
                "目标阻断标签",
                GasEditorUtility.RuntimeTagUsage,
                "当前没有目标阻断标签。");
        }

        private static void DrawAbilityCostCooldown(
            SerializedProperty property,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset)
        {
            GasEditorUtility.DrawProperty(property.FindPropertyRelative("_costs"), "资源消耗", true);
            GasEditorUtility.DrawProperty(property.FindPropertyRelative("_cooldownTicks"), "冷却 Tick");
            GasEditorUtility.DrawSingleTagEditor(
                property.FindPropertyRelative("_cooldownTag"),
                catalog,
                ownerAsset,
                "冷却标签",
                CombatTagUsage.Ability,
                "当前没有冷却标签。");
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
                "效果标签",
                CombatTagUsage.Effect,
                "当前没有效果标签。");
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative("_grantedTags"),
                catalog,
                ownerAsset,
                "授予标签",
                GasEditorUtility.RuntimeTagUsage,
                "当前不会授予标签。");
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative("_requiredTargetTags"),
                catalog,
                ownerAsset,
                "目标所需标签",
                GasEditorUtility.RuntimeTagUsage,
                "当前不要求目标标签。");
            GasEditorUtility.DrawTagEditor(
                property.FindPropertyRelative("_blockedTargetTags"),
                catalog,
                ownerAsset,
                "目标阻断标签",
                GasEditorUtility.RuntimeTagUsage,
                "当前没有目标阻断标签。");
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
                "移除目标效果标签",
                CombatTagUsage.Effect,
                "当前不会按标签移除效果。");
            GasEditorUtility.DrawProperty(property.FindPropertyRelative("_removedTargetEffects"), "移除目标效果资产", true);
        }

        private static void DrawTriggerTagFilters(
            SerializedProperty property,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset)
        {
            DrawTriggerTagField(property, "_requiredOwnerTags", "拥有者所需标签", catalog, ownerAsset, GasEditorUtility.RuntimeTagUsage);
            DrawTriggerTagField(property, "_blockedOwnerTags", "拥有者阻断标签", catalog, ownerAsset, GasEditorUtility.RuntimeTagUsage);
            DrawTriggerTagField(property, "_requiredInstigatorTags", "施加者所需标签", catalog, ownerAsset, GasEditorUtility.RuntimeTagUsage);
            DrawTriggerTagField(property, "_blockedInstigatorTags", "施加者阻断标签", catalog, ownerAsset, GasEditorUtility.RuntimeTagUsage);
            DrawTriggerTagField(property, "_requiredTargetTags", "目标所需标签", catalog, ownerAsset, GasEditorUtility.RuntimeTagUsage);
            DrawTriggerTagField(property, "_blockedTargetTags", "目标阻断标签", catalog, ownerAsset, GasEditorUtility.RuntimeTagUsage);
            DrawTriggerTagField(property, "_requiredIncomingAbilityTags", "传入 Ability 所需标签", catalog, ownerAsset, CombatTagUsage.Ability);
            DrawTriggerTagField(property, "_blockedIncomingAbilityTags", "传入 Ability 阻断标签", catalog, ownerAsset, CombatTagUsage.Ability);
            DrawTriggerTagField(property, "_requiredIncomingEffectTags", "传入 Effect 所需标签", catalog, ownerAsset, CombatTagUsage.Effect);
            DrawTriggerTagField(property, "_blockedIncomingEffectTags", "传入 Effect 阻断标签", catalog, ownerAsset, CombatTagUsage.Effect);
            DrawTriggerTagField(property, "_requiredImpactTags", "Impact 所需标签", catalog, ownerAsset, CombatTagUsage.Impact);
            DrawTriggerTagField(property, "_blockedImpactTags", "Impact 阻断标签", catalog, ownerAsset, CombatTagUsage.Impact);
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
                "当前未配置。");
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
                "资源 Id",
                "当前还没有选择阈值资源。");
            GasEditorUtility.DrawProperty(threshold.FindPropertyRelative("_value"), "阈值", true);
            GasEditorUtility.DrawProperty(threshold.FindPropertyRelative("_direction"), "比较方向");
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

            GasEditorUtility.DrawProperty(kindProperty, "动作类型");
            GasEditorUtility.DrawProperty(sourceActor, "来源 Actor");
            GasEditorUtility.DrawProperty(targetActor, "目标 Actor");

            var kind = kindProperty == null ? TriggerActionKind.None : (TriggerActionKind)kindProperty.enumValueIndex;
            switch (kind)
            {
                case TriggerActionKind.ActivateAbility:
                    GasEditorUtility.DrawProperty(triggeredAbility, "要触发的 Ability");
                    break;
                case TriggerActionKind.ApplyEffect:
                case TriggerActionKind.RemoveEffectById:
                    GasEditorUtility.DrawProperty(effectAsset, "目标 Effect");
                    break;
                case TriggerActionKind.RemoveEffectsByTag:
                case TriggerActionKind.CleanseByTag:
                    GasEditorUtility.DrawSingleTagEditor(effectTag, catalog, ownerAsset, "EffectTag", CombatTagUsage.Effect, "当前还没有选择 EffectTag。");
                    break;
                case TriggerActionKind.AddImpactOperation:
                    DrawImpactOperation(operationTemplate, catalog, ownerAsset);
                    break;
                case TriggerActionKind.ModifyImpactMagnitude:
                    GasEditorUtility.DrawProperty(magnitudeMultiplier, "强度倍率", true);
                    break;
                case TriggerActionKind.AddResource:
                case TriggerActionKind.RemoveResource:
                    GasResourceEditorUtility.DrawSingleResourceEditor(resourceId, catalog, "资源 Id", "当前还没有选择资源。");
                    GasEditorUtility.DrawProperty(resourceAmount, "资源变化量", true);
                    break;
                case TriggerActionKind.AddTag:
                case TriggerActionKind.RemoveTag:
                    GasEditorUtility.DrawSingleTagEditor(tag, catalog, ownerAsset, "运行时标签", GasEditorUtility.RuntimeTagUsage, "当前还没有选择运行时标签。");
                    break;
                case TriggerActionKind.CancelAbility:
                    GasEditorUtility.DrawProperty(abilityToCancel, "要取消的 Ability");
                    break;
                case TriggerActionKind.EmitCue:
                    GasEditorUtility.DrawProperty(cueName, "Cue 名称");
                    break;
                case TriggerActionKind.SplitImpactToActor:
                    GasEditorUtility.DrawProperty(magnitudeMultiplier, "拆分倍率", true);
                    break;
                case TriggerActionKind.AddResourceFromImpact:
                    GasResourceEditorUtility.DrawSingleResourceEditor(resourceId, catalog, "资源 Id", "当前还没有选择资源。");
                    GasEditorUtility.DrawProperty(magnitudeMultiplier, "Impact 倍率", true);
                    break;
                case TriggerActionKind.Custom:
                    GasEditorUtility.DrawProperty(customActionId, "自定义动作 Id");
                    break;
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

            GasEditorUtility.DrawProperty(operationTemplate.FindPropertyRelative("_type"), "Impact 操作类型");
            GasResourceEditorUtility.DrawSingleResourceEditor(
                operationTemplate.FindPropertyRelative("_resourceId"),
                catalog,
                "资源 Id",
                "当前还没有选择资源。");
            GasEditorUtility.DrawProperty(operationTemplate.FindPropertyRelative("_effectAsset"), "Effect 资产");
            GasEditorUtility.DrawSingleTagEditor(
                operationTemplate.FindPropertyRelative("_tag"),
                catalog,
                ownerAsset,
                "Tag",
                CombatTagUsage.Impact | GasEditorUtility.RuntimeTagUsage,
                "当前还没有选择 Tag。");
            GasEditorUtility.DrawProperty(operationTemplate.FindPropertyRelative("_amount"), "数值", true);
            GasEditorUtility.DrawProperty(operationTemplate.FindPropertyRelative("_cueName"), "Cue 名称");
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
                ? "模块"
                : GetModuleTitle(property.managedReferenceValue.GetType());
        }

        private static string GetModuleTitle(Type moduleType)
        {
            if (moduleType == null)
            {
                return "模块";
            }

            var attribute = moduleType.GetCustomAttributes(typeof(GasAuthoringModuleAttribute), false)
                .OfType<GasAuthoringModuleAttribute>()
                .FirstOrDefault();
            return attribute?.Title ?? ObjectNames.NicifyVariableName(moduleType.Name);
        }

        private static System.Collections.Generic.IEnumerable<SerializedProperty> ToEnumerable(this SerializedProperty arrayProperty)
        {
            if (arrayProperty == null || !arrayProperty.isArray)
            {
                yield break;
            }

            for (var i = 0; i < arrayProperty.arraySize; i++)
            {
                yield return arrayProperty.GetArrayElementAtIndex(i);
            }
        }
    }
}
