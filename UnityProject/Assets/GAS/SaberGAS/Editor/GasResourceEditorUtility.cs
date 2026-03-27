using System;
using System.Collections.Generic;
using Saber.GAS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Editor
{
    internal static class GasResourceEditorUtility
    {
        public static void DrawResourceDefinitionLibrary(
            SerializedProperty property,
            string label,
            string description)
        {
            GasEditorUtility.DrawSection(label, description, () =>
            {
                DrawResourceDefinitionCollection(property);
            });
        }

        public static void DrawSingleResourceEditor(
            SerializedProperty property,
            CombatDefinitionCatalogAsset catalog,
            string label,
            string emptySummary = "当前未配置 ResourceId。")
        {
            if (property == null || property.propertyType != SerializedPropertyType.String)
            {
                GasEditorUtility.DrawProperty(property, label);
                return;
            }

            var resourceDefinitions = CollectDefinitions(catalog);
            if (resourceDefinitions.Count == 0)
            {
                GasEditorUtility.DrawProperty(property, label);
                EditorGUILayout.HelpBox("当前 Catalog 还没有定义全局 ResourceId 词库，所以这里只能先手动输入。建议先回到根配置补资源定义。", MessageType.Info);
                return;
            }

            var currentValue = Normalize(property.stringValue);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                string.IsNullOrWhiteSpace(currentValue) ? emptySummary : currentValue,
                EditorStyles.wordWrappedMiniLabel);
            DrawResourcePopupLayout(property, catalog, label);
            EditorGUILayout.EndVertical();
        }

        public static void DrawResourceIdPopup(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property == null || property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var targetObject = property.serializedObject.targetObject;
            var catalog = GasEditorUtility.FindCatalog(targetObject);
            var resourceDefinitions = CollectDefinitions(catalog);
            if (resourceDefinitions.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            DrawResourcePopup(position, property, label, resourceDefinitions);
        }

        private static void DrawResourceDefinitionCollection(SerializedProperty property)
        {
            if (property == null || !property.isArray)
            {
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("资源词库", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(FormatSummary(property), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);

            for (var i = 0; i < property.arraySize; i++)
            {
                var definition = property.GetArrayElementAtIndex(i);
                var resourceId = definition.FindPropertyRelative("_resourceId");
                var group = definition.FindPropertyRelative("_group");
                var note = definition.FindPropertyRelative("_note");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(string.Format("资源定义 {0}", i + 1), EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("删除", GUILayout.Width(52f)))
                {
                    property.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                resourceId.stringValue = EditorGUILayout.TextField("ResourceId", resourceId.stringValue);
                group.stringValue = EditorGUILayout.TextField("分组", group.stringValue);
                EditorGUILayout.LabelField("说明", EditorStyles.miniLabel);
                note.stringValue = EditorGUILayout.TextArea(note.stringValue, GUILayout.MinHeight(36f));
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("新增 ResourceId", GUILayout.Width(120f)))
            {
                var index = property.arraySize;
                property.InsertArrayElementAtIndex(index);
                var definition = property.GetArrayElementAtIndex(index);
                definition.FindPropertyRelative("_resourceId").stringValue = string.Empty;
                definition.FindPropertyRelative("_group").stringValue = string.Empty;
                definition.FindPropertyRelative("_note").stringValue = string.Empty;
            }

            using (new EditorGUI.DisabledScope(property.arraySize == 0))
            {
                if (GUILayout.Button("清理空定义", GUILayout.Width(84f)))
                {
                    RemoveEmptyDefinitions(property);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private static void DrawResourcePopupLayout(
            SerializedProperty property,
            CombatDefinitionCatalogAsset catalog,
            string label)
        {
            var resourceDefinitions = CollectDefinitions(catalog);
            if (resourceDefinitions.Count == 0)
            {
                GasEditorUtility.DrawProperty(property, label);
                return;
            }

            var rect = EditorGUILayout.GetControlRect();
            DrawResourcePopup(rect, property, new GUIContent(label), resourceDefinitions);

            var currentValue = Normalize(property.stringValue);
            if (!string.IsNullOrWhiteSpace(currentValue) && !ContainsResource(resourceDefinitions, currentValue))
            {
                EditorGUILayout.HelpBox("当前值不在全局 ResourceId 词库中。建议先把它补到根配置的资源词库，再回这里选择。", MessageType.Warning);
            }
        }

        private static void DrawResourcePopup(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            List<CombatResourceDefinitionAuthoringData> resourceDefinitions)
        {
            var currentValue = Normalize(property.stringValue);
            var options = BuildOptions(resourceDefinitions, currentValue, out var optionValues, out var selectedIndex);
            var newIndex = EditorGUI.Popup(position, label, selectedIndex, options);
            if (newIndex < 0 || newIndex >= optionValues.Count)
            {
                return;
            }

            var newValue = optionValues[newIndex];
            if (!string.Equals(currentValue, newValue, StringComparison.Ordinal))
            {
                property.stringValue = newValue;
            }
        }

        private static GUIContent[] BuildOptions(
            List<CombatResourceDefinitionAuthoringData> resourceDefinitions,
            string currentValue,
            out List<string> optionValues,
            out int selectedIndex)
        {
            optionValues = new List<string>();
            var options = new List<GUIContent>();

            optionValues.Add(string.Empty);
            options.Add(new GUIContent("<未设置>", "不配置 ResourceId"));

            if (!string.IsNullOrWhiteSpace(currentValue) && !ContainsResource(resourceDefinitions, currentValue))
            {
                optionValues.Add(currentValue);
                options.Add(new GUIContent(
                    string.Format("<未定义> {0}", currentValue),
                    "当前值不在全局资源词库中，建议回到根配置补定义。"));
            }

            for (var i = 0; i < resourceDefinitions.Count; i++)
            {
                var definition = resourceDefinitions[i];
                optionValues.Add(definition.ResourceId);
                options.Add(new GUIContent(BuildDisplayName(definition), BuildTooltip(definition)));
            }

            selectedIndex = 0;
            for (var i = 0; i < optionValues.Count; i++)
            {
                if (string.Equals(optionValues[i], currentValue, StringComparison.Ordinal))
                {
                    selectedIndex = i;
                    break;
                }
            }

            return options.ToArray();
        }

        private static List<CombatResourceDefinitionAuthoringData> CollectDefinitions(CombatDefinitionCatalogAsset catalog)
        {
            var results = new List<CombatResourceDefinitionAuthoringData>();
            if (catalog == null || catalog.GlobalResourceDefinitions == null)
            {
                return results;
            }

            for (var i = 0; i < catalog.GlobalResourceDefinitions.Count; i++)
            {
                var definition = catalog.GlobalResourceDefinitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.ResourceId))
                {
                    continue;
                }

                results.Add(definition);
            }

            results.Sort((left, right) =>
            {
                var groupCompare = string.Compare(left.Group, right.Group, StringComparison.OrdinalIgnoreCase);
                return groupCompare != 0
                    ? groupCompare
                    : string.Compare(left.ResourceId, right.ResourceId, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        private static bool ContainsResource(List<CombatResourceDefinitionAuthoringData> definitions, string resourceId)
        {
            for (var i = 0; i < definitions.Count; i++)
            {
                if (string.Equals(definitions[i].ResourceId, resourceId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildDisplayName(CombatResourceDefinitionAuthoringData definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(definition.Group)
                ? definition.ResourceId
                : string.Format("[{0}] {1}", definition.Group, definition.ResourceId);
        }

        private static string BuildTooltip(CombatResourceDefinitionAuthoringData definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(definition.Note)
                ? definition.ResourceId
                : string.Format("{0}\n{1}", definition.ResourceId, definition.Note);
        }

        private static string FormatSummary(SerializedProperty property)
        {
            if (property == null || !property.isArray || property.arraySize == 0)
            {
                return "当前还没有定义全局 ResourceId。";
            }

            var preview = new List<string>();
            var count = 0;
            for (var i = 0; i < property.arraySize; i++)
            {
                var definition = property.GetArrayElementAtIndex(i);
                var resourceId = Normalize(definition.FindPropertyRelative("_resourceId")?.stringValue);
                if (string.IsNullOrWhiteSpace(resourceId))
                {
                    continue;
                }

                count += 1;
                if (preview.Count < 4)
                {
                    var group = Normalize(definition.FindPropertyRelative("_group")?.stringValue);
                    preview.Add(string.IsNullOrWhiteSpace(group)
                        ? resourceId
                        : string.Format("{0} [{1}]", resourceId, group));
                }
            }

            if (count == 0)
            {
                return "当前资源词库里只有空条目，建议先补齐 ResourceId。";
            }

            return string.Format(
                "已定义 {0} 个 ResourceId：{1}{2}",
                count,
                string.Join("  |  ", preview.ToArray()),
                count > preview.Count ? "  |  ..." : string.Empty);
        }

        private static void RemoveEmptyDefinitions(SerializedProperty property)
        {
            if (property == null || !property.isArray)
            {
                return;
            }

            for (var i = property.arraySize - 1; i >= 0; i--)
            {
                var definition = property.GetArrayElementAtIndex(i);
                var resourceId = definition.FindPropertyRelative("_resourceId");
                if (resourceId == null || string.IsNullOrWhiteSpace(resourceId.stringValue))
                {
                    property.DeleteArrayElementAtIndex(i);
                }
            }
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
