using System;
using System.Collections.Generic;
using System.Text;
using Saber.GAS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Editor
{
    internal static class GasEditorUtility
    {
        private sealed class GasTagCandidate
        {
            public string Tag;
            public string Group;
            public string Note;
            public string SourceLabel;
            public CombatTagUsage Usage;
            public int Priority;
        }

        private static readonly Dictionary<string, bool> ExpandStates = new Dictionary<string, bool>();
        private static readonly Dictionary<string, string> TextStates = new Dictionary<string, string>();
        private static readonly GUIStyle ChipStyle = new GUIStyle(EditorStyles.miniButton)
        {
            margin = new RectOffset(0, 4, 0, 4),
            padding = new RectOffset(8, 8, 4, 4),
        };

        public static readonly CombatTagUsage RuntimeTagUsage =
            CombatTagUsage.Shared |
            CombatTagUsage.Actor |
            CombatTagUsage.Ability |
            CombatTagUsage.Effect |
            CombatTagUsage.Trigger;

        public static CombatDefinitionCatalogAsset FindCatalog(UnityEngine.Object value)
        {
            if (value == null)
            {
                return null;
            }

            var assetPath = AssetDatabase.GetAssetPath(value);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i] is CombatDefinitionCatalogAsset catalog)
                {
                    return catalog;
                }
            }

            return null;
        }

        public static bool BelongsToCatalog(CombatDefinitionCatalogAsset catalog, UnityEngine.Object value)
        {
            if (catalog == null || value == null)
            {
                return false;
            }

            return FindCatalog(value) == catalog;
        }

        public static T[] GetEmbeddedAssets<T>(CombatDefinitionCatalogAsset catalog) where T : UnityEngine.Object
        {
            if (catalog == null)
            {
                return Array.Empty<T>();
            }

            var assetPath = AssetDatabase.GetAssetPath(catalog);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return Array.Empty<T>();
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var results = new List<T>();
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i] is T typed && assets[i] != catalog)
                {
                    results.Add(typed);
                }
            }

            results.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return results.ToArray();
        }

        public static void SyncEmbeddedDefinitions(CombatDefinitionCatalogAsset catalog)
        {
            if (catalog == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(catalog);
            SetObjectArray(serializedObject, "_abilities", GetEmbeddedAssets<AbilityDefinitionAsset>(catalog));
            SetObjectArray(serializedObject, "_effects", GetEmbeddedAssets<EffectDefinitionAsset>(catalog));
            SetObjectArray(serializedObject, "_triggers", GetEmbeddedAssets<TriggerDefinitionAsset>(catalog));
            SetObjectArray(serializedObject, "_actorTemplates", GetEmbeddedAssets<CombatActorTemplateAsset>(catalog));
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            var assetPath = AssetDatabase.GetAssetPath(catalog);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                AssetDatabase.ImportAsset(assetPath);
            }
        }

        public static T CreateEmbeddedSubAsset<T>(CombatDefinitionCatalogAsset catalog, string prefix) where T : ScriptableObject
        {
            if (catalog == null)
            {
                return null;
            }

            var assetPath = AssetDatabase.GetAssetPath(catalog);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = GenerateUniqueSubAssetName(assetPath, prefix);
            AssetDatabase.AddObjectToAsset(asset, catalog);
            return asset;
        }

        public static void FinalizeCreatedAsset(
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object asset,
            bool focusAsset = true)
        {
            if (catalog == null || asset == null)
            {
                return;
            }

            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            SyncEmbeddedDefinitions(catalog);

            if (focusAsset)
            {
                FocusAsset(asset);
            }
        }

        public static void FocusAsset(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        public static void OpenAssetAtPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
            {
                return;
            }

            AssetDatabase.OpenAsset(asset);
            FocusAsset(asset);
        }

        public static string SuggestId(string prefix, string assetName)
        {
            var normalized = Slugify(assetName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized = "sample";
            }

            return string.Format("{0}.{1}", prefix, normalized);
        }

        public static void SetString(SerializedObject serializedObject, string propertyPath, string value)
        {
            var property = serializedObject.FindProperty(propertyPath);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        public static void SetEnumValue<TEnum>(SerializedObject serializedObject, string propertyPath, TEnum value)
            where TEnum : struct, Enum
        {
            var property = serializedObject.FindProperty(propertyPath);
            if (property != null)
            {
                property.enumValueIndex = Convert.ToInt32(value);
            }
        }

        public static void SetObjectReference(SerializedObject serializedObject, string propertyPath, UnityEngine.Object value)
        {
            var property = serializedObject.FindProperty(propertyPath);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        public static void SetStringArray(SerializedObject serializedObject, string propertyPath, params string[] values)
        {
            var property = serializedObject.FindProperty(propertyPath);
            if (property == null || !property.isArray)
            {
                return;
            }

            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        public static void SetObjectArray(SerializedObject serializedObject, string propertyPath, params UnityEngine.Object[] values)
        {
            var property = serializedObject.FindProperty(propertyPath);
            if (property == null || !property.isArray)
            {
                return;
            }

            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        public static void AppendObjectReference(SerializedObject serializedObject, string propertyPath, UnityEngine.Object value)
        {
            var property = serializedObject.FindProperty(propertyPath);
            if (property == null || !property.isArray)
            {
                return;
            }

            var nextIndex = property.arraySize;
            property.InsertArrayElementAtIndex(nextIndex);
            property.GetArrayElementAtIndex(nextIndex).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(serializedObject.targetObject);
        }

        public static bool HasNullObjectReferences(SerializedProperty property)
        {
            if (property == null || !property.isArray)
            {
                return false;
            }

            for (var i = 0; i < property.arraySize; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                if (element.propertyType == SerializedPropertyType.ObjectReference && element.objectReferenceValue == null)
                {
                    return true;
                }
            }

            return false;
        }

        public static void RemoveNullObjectReferences(SerializedProperty property)
        {
            if (property == null || !property.isArray)
            {
                return;
            }

            for (var i = property.arraySize - 1; i >= 0; i--)
            {
                var element = property.GetArrayElementAtIndex(i);
                if (element.propertyType != SerializedPropertyType.ObjectReference || element.objectReferenceValue != null)
                {
                    continue;
                }

                property.DeleteArrayElementAtIndex(i);
            }
        }

        public static void RemoveEmptyStringEntries(SerializedProperty property)
        {
            if (property == null || !property.isArray)
            {
                return;
            }

            for (var i = property.arraySize - 1; i >= 0; i--)
            {
                var element = property.GetArrayElementAtIndex(i);
                if (element.propertyType == SerializedPropertyType.String &&
                    string.IsNullOrWhiteSpace(element.stringValue))
                {
                    property.DeleteArrayElementAtIndex(i);
                }
            }
        }

        public static string GetRequiredStringIssue(SerializedProperty property, string label)
        {
            if (property == null || !string.IsNullOrWhiteSpace(property.stringValue))
            {
                return null;
            }

            return string.Format("{0} 不能为空。", label);
        }





        public static void DrawInspectorHeader(string title, string description)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrWhiteSpace(description))
            {
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            }
        }

        public static void DrawSection(string title, string description, Action body)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrWhiteSpace(description))
            {
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(2f);
            }

            body?.Invoke();
            EditorGUILayout.EndVertical();
        }

        public static void DrawCatalogBar(CombatDefinitionCatalogAsset catalog)
        {
            if (catalog == null)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField(string.Format("当前 Catalog: {0}", catalog.name), EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("打开 Workbench", EditorStyles.toolbarButton, GUILayout.Width(100f)))
            {
                GasWorkbenchWindow.Open(catalog);
            }

            if (GUILayout.Button("定位资源", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                FocusAsset(catalog);
            }

            EditorGUILayout.EndHorizontal();
        }

        public static void DrawProperty(
            SerializedProperty property,
            string label,
            bool includeChildren = false,
            string tooltip = null)
        {
            if (property == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), includeChildren);
        }

        public static void DrawSingleTagEditor(
            SerializedProperty property,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset,
            string label,
            CombatTagUsage usageMask,
            string emptySummary = "未配置标签")
        {
            if (property == null || property.propertyType != SerializedPropertyType.String)
            {
                DrawProperty(property, label);
                return;
            }

            var stateKey = GetStateKey(property) + ":single-tag";
            var expanded = GetExpandState(stateKey);
            var currentValue = NormalizeTag(property.stringValue);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(expanded ? "收起编辑" : "编辑标签", GUILayout.Width(84f)))
            {
                expanded = !expanded;
                SetExpandState(stateKey, expanded);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                string.IsNullOrWhiteSpace(currentValue) ? emptySummary : currentValue,
                EditorStyles.wordWrappedMiniLabel);

            if (expanded)
            {
                DrawSingleTagSelectionPanel(property, catalog, ownerAsset, usageMask, stateKey);
            }

            EditorGUILayout.EndVertical();
        }

        public static void DrawTagEditor(
            SerializedProperty property,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset,
            string label,
            CombatTagUsage usageMask,
            string emptySummary = "未配置标签")
        {
            if (property == null || !property.isArray)
            {
                return;
            }

            var stateKey = GetStateKey(property) + ":multi-tag";
            var expanded = GetExpandState(stateKey);
            var tags = CollectStringArrayValues(property);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(expanded ? "收起编辑" : "编辑标签", GUILayout.Width(84f)))
            {
                expanded = !expanded;
                SetExpandState(stateKey, expanded);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                tags.Count == 0 ? emptySummary : string.Join("  |  ", tags.ToArray()),
                EditorStyles.wordWrappedMiniLabel);

            if (expanded)
            {
                DrawMultiTagSelectionPanel(property, catalog, ownerAsset, usageMask, stateKey);
            }

            EditorGUILayout.EndVertical();
        }

        public static void DrawTagDefinitionLibrary(
            SerializedProperty property,
            string label,
            string description,
            CombatTagUsage defaultUsage)
        {
            DrawSection(label, description, () =>
            {
                DrawTagDefinitionCollection(property, defaultUsage);
            });
        }

        public static void DrawReadOnlyObjectList<T>(string title, T[] values) where T : UnityEngine.Object
        {
            DrawSection(title, null, () =>
            {
                if (values == null || values.Length == 0)
                {
                    EditorGUILayout.LabelField("暂无。", EditorStyles.wordWrappedMiniLabel);
                    return;
                }

                using (new EditorGUI.DisabledScope(true))
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                        EditorGUILayout.ObjectField(values[i], typeof(T), false);
                    }
                }
            });
        }

        private static void DrawTagDefinitionCollection(SerializedProperty property, CombatTagUsage defaultUsage)
        {
            if (property == null || !property.isArray)
            {
                return;
            }

            var stateKey = GetStateKey(property) + ":definitions";
            var expanded = GetExpandState(stateKey);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("标签定义", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(expanded ? "收起列表" : "编辑词库", GUILayout.Width(84f)))
            {
                expanded = !expanded;
                SetExpandState(stateKey, expanded);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(FormatTagDefinitionSummary(property), EditorStyles.wordWrappedMiniLabel);
            if (!expanded)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.Space(4f);
            for (var i = 0; i < property.arraySize; i++)
            {
                var definition = property.GetArrayElementAtIndex(i);
                var tagProperty = definition.FindPropertyRelative("_tag");
                var groupProperty = definition.FindPropertyRelative("_group");
                var usageProperty = definition.FindPropertyRelative("_usage");
                var noteProperty = definition.FindPropertyRelative("_note");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(string.Format("标签定义 {0}", i + 1), EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("删除", GUILayout.Width(52f)))
                {
                    property.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                tagProperty.stringValue = EditorGUILayout.TextField("标签名", tagProperty.stringValue);
                groupProperty.stringValue = EditorGUILayout.TextField("分组", groupProperty.stringValue);

                var usageValue = (CombatTagUsage)usageProperty.intValue;
                if (usageValue == CombatTagUsage.None)
                {
                    usageValue = defaultUsage;
                }

                usageValue = (CombatTagUsage)EditorGUILayout.EnumFlagsField("适用范围", usageValue);
                usageProperty.intValue = (int)usageValue;

                EditorGUILayout.LabelField("说明", EditorStyles.miniLabel);
                noteProperty.stringValue = EditorGUILayout.TextArea(noteProperty.stringValue, GUILayout.MinHeight(36f));
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("新增标签定义", GUILayout.Width(100f)))
            {
                var nextIndex = property.arraySize;
                property.InsertArrayElementAtIndex(nextIndex);
                var definition = property.GetArrayElementAtIndex(nextIndex);
                definition.FindPropertyRelative("_tag").stringValue = string.Empty;
                definition.FindPropertyRelative("_group").stringValue = string.Empty;
                definition.FindPropertyRelative("_usage").intValue = (int)defaultUsage;
                definition.FindPropertyRelative("_note").stringValue = string.Empty;
            }

            using (new EditorGUI.DisabledScope(property.arraySize == 0))
            {
                if (GUILayout.Button("清理空定义", GUILayout.Width(84f)))
                {
                    RemoveEmptyTagDefinitions(property);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private static void DrawSingleTagSelectionPanel(
            SerializedProperty property,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset,
            CombatTagUsage usageMask,
            string stateKey)
        {
            EditorGUILayout.Space(3f);

            var currentValue = NormalizeTag(property.stringValue);
            if (!string.IsNullOrWhiteSpace(currentValue))
            {
                EditorGUILayout.LabelField("当前标签", EditorStyles.miniBoldLabel);
                DrawTagChipFlow(
                    new[] { new GUIContent(string.Format("{0}  ×", currentValue), "点击清空当前标签") },
                    content => { property.stringValue = string.Empty; });
                EditorGUILayout.Space(4f);
            }

            DrawTagSearchBar(stateKey);
            DrawManualSingleTagField(property, stateKey);
            DrawCandidateGroups(
                CollectTagCandidates(catalog, ownerAsset, usageMask, GetTextState(stateKey + ":search"), currentValue),
                candidate => property.stringValue = candidate.Tag);
        }

        private static void DrawMultiTagSelectionPanel(
            SerializedProperty property,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset,
            CombatTagUsage usageMask,
            string stateKey)
        {
            EditorGUILayout.Space(3f);

            var selectedTags = CollectStringArrayValues(property);
            if (selectedTags.Count > 0)
            {
                EditorGUILayout.LabelField("已选标签", EditorStyles.miniBoldLabel);
                var selectedContents = new List<GUIContent>(selectedTags.Count);
                for (var i = 0; i < selectedTags.Count; i++)
                {
                    selectedContents.Add(new GUIContent(
                        string.Format("{0}  ×", selectedTags[i]),
                        "点击移除这个标签"));
                }

                DrawTagChipFlow(selectedContents, content =>
                {
                    RemoveStringValue(property, content.text.Replace("  ×", string.Empty));
                });

                EditorGUILayout.Space(4f);
            }

            DrawTagSearchBar(stateKey);
            DrawManualMultiTagField(property, stateKey);

            using (new EditorGUI.DisabledScope(property.arraySize == 0))
            {
                if (GUILayout.Button("清理空标签", GUILayout.Width(84f)))
                {
                    RemoveEmptyStringEntries(property);
                }
            }

            EditorGUILayout.Space(6f);

            DrawCandidateGroups(
                CollectTagCandidates(catalog, ownerAsset, usageMask, GetTextState(stateKey + ":search"), selectedTags),
                candidate => AddStringValue(property, candidate.Tag));
        }

        private static void DrawTagSearchBar(string stateKey)
        {
            var searchKey = stateKey + ":search";
            var search = GetTextState(searchKey);

            EditorGUILayout.BeginHorizontal();
            search = EditorGUILayout.TextField("检索词库", search);
            if (GUILayout.Button("清空", GUILayout.Width(48f)))
            {
                search = string.Empty;
            }
            EditorGUILayout.EndHorizontal();

            SetTextState(searchKey, search);
            EditorGUILayout.LabelField(
                "候选会同时来自根配置的全局标签词库，以及当前 Catalog 内 Ability/Effect/Trigger 的局部标签库。",
                EditorStyles.wordWrappedMiniLabel);
        }

        private static void DrawManualSingleTagField(SerializedProperty property, string stateKey)
        {
            var manualKey = stateKey + ":manual";
            var manualValue = GetTextState(manualKey);

            EditorGUILayout.BeginHorizontal();
            manualValue = EditorGUILayout.TextField("手动输入", manualValue);
            if (GUILayout.Button("设置", GUILayout.Width(56f)))
            {
                property.stringValue = NormalizeTag(manualValue);
                manualValue = string.Empty;
            }
            EditorGUILayout.EndHorizontal();

            SetTextState(manualKey, manualValue);
        }

        private static void DrawManualMultiTagField(SerializedProperty property, string stateKey)
        {
            var manualKey = stateKey + ":manual";
            var manualValue = GetTextState(manualKey);

            EditorGUILayout.BeginHorizontal();
            manualValue = EditorGUILayout.TextField("手动新增", manualValue);
            if (GUILayout.Button("加入", GUILayout.Width(56f)))
            {
                AddStringValue(property, manualValue);
                manualValue = string.Empty;
            }
            EditorGUILayout.EndHorizontal();

            SetTextState(manualKey, manualValue);
        }

        private static void DrawCandidateGroups(
            List<GasTagCandidate> candidates,
            Action<GasTagCandidate> onSelect)
        {
            if (candidates.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有匹配的标签候选。你可以继续调整检索词，或者直接手动输入。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("词库候选", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("悬停标签按钮可以查看来源、分组与说明。", EditorStyles.wordWrappedMiniLabel);

            var groupedCandidates = new Dictionary<string, List<GasTagCandidate>>(StringComparer.OrdinalIgnoreCase);
            var orderedGroups = new List<string>();
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var groupName = string.IsNullOrWhiteSpace(candidate.Group) ? "未分组" : candidate.Group;
                List<GasTagCandidate> group;
                if (!groupedCandidates.TryGetValue(groupName, out group))
                {
                    group = new List<GasTagCandidate>();
                    groupedCandidates[groupName] = group;
                    orderedGroups.Add(groupName);
                }

                group.Add(candidate);
            }

            for (var i = 0; i < orderedGroups.Count; i++)
            {
                var groupName = orderedGroups[i];
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(groupName, EditorStyles.miniBoldLabel);

                var group = groupedCandidates[groupName];
                var contents = new List<GUIContent>(group.Count);
                for (var j = 0; j < group.Count; j++)
                {
                    contents.Add(new GUIContent(group[j].Tag, BuildCandidateTooltip(group[j])));
                }

                DrawTagChipFlow(contents, content =>
                {
                    for (var j = 0; j < group.Count; j++)
                    {
                        if (group[j].Tag == content.text)
                        {
                            onSelect?.Invoke(group[j]);
                            return;
                        }
                    }
                });
            }
        }

        private static void DrawTagChipFlow(IReadOnlyList<GUIContent> contents, Action<GUIContent> onClick)
        {
            if (contents == null || contents.Count == 0)
            {
                return;
            }

            var maxWidth = Mathf.Max(120f, EditorGUIUtility.currentViewWidth - 60f);
            var currentRowWidth = 0f;
            var currentRow = new List<GUIContent>();

            void FlushRow()
            {
                if (currentRow.Count == 0)
                {
                    return;
                }

                EditorGUILayout.BeginHorizontal();
                for (var i = 0; i < currentRow.Count; i++)
                {
                    var content = currentRow[i];
                    var buttonWidth = Mathf.Clamp(ChipStyle.CalcSize(content).x + 12f, 56f, maxWidth);
                    if (GUILayout.Button(content, ChipStyle, GUILayout.Width(buttonWidth)))
                    {
                        onClick?.Invoke(content);
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                currentRow.Clear();
                currentRowWidth = 0f;
            }

            for (var i = 0; i < contents.Count; i++)
            {
                var content = contents[i];
                var buttonWidth = Mathf.Clamp(ChipStyle.CalcSize(content).x + 16f, 56f, maxWidth);
                if (currentRow.Count > 0 && currentRowWidth + buttonWidth > maxWidth)
                {
                    FlushRow();
                }

                currentRow.Add(content);
                currentRowWidth += buttonWidth + 4f;
            }

            FlushRow();
        }

        private static List<GasTagCandidate> CollectTagCandidates(
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset,
            CombatTagUsage usageMask,
            string search,
            string selectedValue)
        {
            return CollectTagCandidates(
                catalog,
                ownerAsset,
                usageMask,
                search,
                string.IsNullOrWhiteSpace(selectedValue)
                    ? Array.Empty<string>()
                    : new[] { selectedValue });
        }

        private static List<GasTagCandidate> CollectTagCandidates(
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset,
            CombatTagUsage usageMask,
            string search,
            IReadOnlyCollection<string> selectedValues)
        {
            var results = new List<GasTagCandidate>();
            if (catalog == null)
            {
                return results;
            }

            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (selectedValues != null)
            {
                foreach (var value in selectedValues)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        selected.Add(value.Trim());
                    }
                }
            }

            AppendTagCandidates(results, catalog.GlobalTagDefinitions, "全局词库", false, false, usageMask);
            AppendLocalTagCandidates(results, GetEmbeddedAssets<AbilityDefinitionAsset>(catalog), ownerAsset, "Ability", usageMask);
            AppendLocalTagCandidates(results, GetEmbeddedAssets<EffectDefinitionAsset>(catalog), ownerAsset, "Effect", usageMask);
            AppendLocalTagCandidates(results, GetEmbeddedAssets<TriggerDefinitionAsset>(catalog), ownerAsset, "Trigger", usageMask);

            results.Sort(CompareTagCandidates);

            var filtered = new List<GasTagCandidate>(results.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var searchValue = NormalizeTag(search);
            for (var i = 0; i < results.Count; i++)
            {
                var candidate = results[i];
                if (selected.Contains(candidate.Tag))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(searchValue) && !MatchesSearch(candidate, searchValue))
                {
                    continue;
                }

                if (!seen.Add(candidate.Tag))
                {
                    continue;
                }

                filtered.Add(candidate);
            }

            return filtered;
        }

        private static void AppendLocalTagCandidates<TAsset>(
            List<GasTagCandidate> results,
            TAsset[] assets,
            UnityEngine.Object ownerAsset,
            string typeLabel,
            CombatTagUsage usageMask)
            where TAsset : UnityEngine.Object
        {
            if (assets == null)
            {
                return;
            }

            for (var i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                if (asset == null)
                {
                    continue;
                }

                IReadOnlyList<CombatTagDefinitionAuthoringData> localDefinitions = null;
                if (asset is AbilityDefinitionAsset ability)
                {
                    localDefinitions = ability.LocalTagDefinitions;
                }
                else if (asset is EffectDefinitionAsset effect)
                {
                    localDefinitions = effect.LocalTagDefinitions;
                }
                else if (asset is TriggerDefinitionAsset trigger)
                {
                    localDefinitions = trigger.LocalTagDefinitions;
                }

                if (localDefinitions == null)
                {
                    continue;
                }

                AppendTagCandidates(
                    results,
                    localDefinitions,
                    string.Format("{0}/{1}", typeLabel, asset.name),
                    true,
                    asset == ownerAsset,
                    usageMask);
            }
        }

        private static void AppendTagCandidates(
            List<GasTagCandidate> results,
            IReadOnlyList<CombatTagDefinitionAuthoringData> definitions,
            string sourceLabel,
            bool isLocal,
            bool isOwnerLocal,
            CombatTagUsage usageMask)
        {
            if (definitions == null)
            {
                return;
            }

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Tag))
                {
                    continue;
                }

                var definitionUsage = definition.Usage;
                if (!MatchesUsage(definitionUsage, usageMask))
                {
                    continue;
                }

                results.Add(new GasTagCandidate
                {
                    Tag = definition.Tag,
                    Group = definition.Group,
                    Note = definition.Note,
                    SourceLabel = sourceLabel,
                    Usage = definitionUsage,
                    Priority = isOwnerLocal ? 0 : (isLocal ? 2 : 1),
                });
            }
        }

        private static bool MatchesUsage(CombatTagUsage definitionUsage, CombatTagUsage requestedMask)
        {
            if (requestedMask == CombatTagUsage.None)
            {
                return true;
            }

            var normalizedUsage = definitionUsage == CombatTagUsage.None
                ? CombatTagUsage.Shared
                : definitionUsage;

            if ((normalizedUsage & CombatTagUsage.Shared) != 0)
            {
                return true;
            }

            return (normalizedUsage & requestedMask) != 0;
        }

        private static bool MatchesSearch(GasTagCandidate candidate, string search)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            return candidate.Tag.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   candidate.Group.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   candidate.SourceLabel.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   candidate.Note.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CompareTagCandidates(GasTagCandidate left, GasTagCandidate right)
        {
            var priorityCompare = left.Priority.CompareTo(right.Priority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            var groupCompare = string.Compare(left.Group, right.Group, StringComparison.OrdinalIgnoreCase);
            if (groupCompare != 0)
            {
                return groupCompare;
            }

            return string.Compare(left.Tag, right.Tag, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildCandidateTooltip(GasTagCandidate candidate)
        {
            var builder = new StringBuilder();
            builder.Append("来源: ").Append(candidate.SourceLabel);
            builder.AppendLine();
            builder.Append("适用范围: ").Append(FormatUsage(candidate.Usage));

            if (!string.IsNullOrWhiteSpace(candidate.Group))
            {
                builder.AppendLine();
                builder.Append("分组: ").Append(candidate.Group);
            }

            if (!string.IsNullOrWhiteSpace(candidate.Note))
            {
                builder.AppendLine();
                builder.Append("说明: ").Append(candidate.Note);
            }

            return builder.ToString();
        }

        private static string FormatUsage(CombatTagUsage usage)
        {
            var normalizedUsage = usage == CombatTagUsage.None ? CombatTagUsage.Shared : usage;
            var labels = new List<string>();

            AppendUsageLabel(labels, normalizedUsage, CombatTagUsage.Shared, "共享");
            AppendUsageLabel(labels, normalizedUsage, CombatTagUsage.Actor, "Actor");
            AppendUsageLabel(labels, normalizedUsage, CombatTagUsage.Ability, "Ability");
            AppendUsageLabel(labels, normalizedUsage, CombatTagUsage.Effect, "Effect");
            AppendUsageLabel(labels, normalizedUsage, CombatTagUsage.Trigger, "Trigger");
            AppendUsageLabel(labels, normalizedUsage, CombatTagUsage.Impact, "Impact");

            return labels.Count == 0 ? "未指定" : string.Join(" / ", labels.ToArray());
        }

        private static void AppendUsageLabel(
            List<string> labels,
            CombatTagUsage currentUsage,
            CombatTagUsage expectedUsage,
            string label)
        {
            if ((currentUsage & expectedUsage) != 0)
            {
                labels.Add(label);
            }
        }

        private static void RemoveEmptyTagDefinitions(SerializedProperty property)
        {
            if (property == null || !property.isArray)
            {
                return;
            }

            for (var i = property.arraySize - 1; i >= 0; i--)
            {
                var definition = property.GetArrayElementAtIndex(i);
                var tagProperty = definition.FindPropertyRelative("_tag");
                if (tagProperty == null || string.IsNullOrWhiteSpace(tagProperty.stringValue))
                {
                    property.DeleteArrayElementAtIndex(i);
                }
            }
        }

        private static void AddStringValue(SerializedProperty property, string value)
        {
            if (property == null || !property.isArray)
            {
                return;
            }

            var normalizedValue = NormalizeTag(value);
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return;
            }

            for (var i = 0; i < property.arraySize; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                if (element.propertyType == SerializedPropertyType.String &&
                    string.Equals(NormalizeTag(element.stringValue), normalizedValue, StringComparison.OrdinalIgnoreCase))
                {
                    element.stringValue = normalizedValue;
                    return;
                }
            }

            var nextIndex = property.arraySize;
            property.arraySize += 1;
            property.GetArrayElementAtIndex(nextIndex).stringValue = normalizedValue;
        }

        private static void RemoveStringValue(SerializedProperty property, string value)
        {
            if (property == null || !property.isArray)
            {
                return;
            }

            var normalizedValue = NormalizeTag(value);
            for (var i = property.arraySize - 1; i >= 0; i--)
            {
                var element = property.GetArrayElementAtIndex(i);
                if (element.propertyType == SerializedPropertyType.String &&
                    string.Equals(NormalizeTag(element.stringValue), normalizedValue, StringComparison.OrdinalIgnoreCase))
                {
                    property.DeleteArrayElementAtIndex(i);
                    return;
                }
            }
        }

        private static string GetStateKey(SerializedProperty property)
        {
            return string.Format(
                "{0}:{1}",
                property.serializedObject.targetObject.GetInstanceID(),
                property.propertyPath);
        }

        private static bool GetExpandState(string key)
        {
            bool expanded;
            if (ExpandStates.TryGetValue(key, out expanded))
            {
                return expanded;
            }

            return false;
        }

        private static void SetExpandState(string key, bool expanded)
        {
            ExpandStates[key] = expanded;
        }

        private static string GetTextState(string key)
        {
            string value;
            return TextStates.TryGetValue(key, out value) ? value : string.Empty;
        }

        private static void SetTextState(string key, string value)
        {
            TextStates[key] = value ?? string.Empty;
        }

        private static List<string> CollectStringArrayValues(SerializedProperty property)
        {
            var results = new List<string>();
            if (property == null || !property.isArray)
            {
                return results;
            }

            for (var i = 0; i < property.arraySize; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                if (element.propertyType != SerializedPropertyType.String)
                {
                    continue;
                }

                var value = NormalizeTag(element.stringValue);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    results.Add(value);
                }
            }

            return results;
        }

        private static string FormatTagDefinitionSummary(SerializedProperty property)
        {
            if (property == null || !property.isArray || property.arraySize == 0)
            {
                return "当前还没有定义标签词库。";
            }

            var tags = new List<string>();
            for (var i = 0; i < property.arraySize; i++)
            {
                var definition = property.GetArrayElementAtIndex(i);
                var tagProperty = definition.FindPropertyRelative("_tag");
                var groupProperty = definition.FindPropertyRelative("_group");
                var usageProperty = definition.FindPropertyRelative("_usage");

                var tag = NormalizeTag(tagProperty?.stringValue);
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                var group = NormalizeTag(groupProperty?.stringValue);
                var usage = usageProperty == null
                    ? CombatTagUsage.Shared
                    : (CombatTagUsage)usageProperty.intValue;

                if (string.IsNullOrWhiteSpace(group))
                {
                    tags.Add(string.Format("{0} ({1})", tag, FormatUsage(usage)));
                }
                else
                {
                    tags.Add(string.Format("{0} [{1}] ({2})", tag, group, FormatUsage(usage)));
                }
            }

            if (tags.Count == 0)
            {
                return "当前词库里只有空条目，建议先补齐标签名。";
            }

            var previewCount = Mathf.Min(4, tags.Count);
            return string.Format(
                "已定义 {0} 个标签：{1}{2}",
                tags.Count,
                string.Join("  |  ", tags.GetRange(0, previewCount).ToArray()),
                tags.Count > previewCount ? "  |  ..." : string.Empty);
        }

        private static string GenerateUniqueSubAssetName(string assetPath, string prefix)
        {
            var existingAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var index = 1;
            while (true)
            {
                var candidate = string.Format("{0}_{1:D2}", prefix, index);
                var exists = false;
                for (var i = 0; i < existingAssets.Length; i++)
                {
                    if (existingAssets[i] != null &&
                        string.Equals(existingAssets[i].name, candidate, StringComparison.Ordinal))
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

        private static string NormalizeTag(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            var lastWasDash = false;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                    lastWasDash = false;
                    continue;
                }

                if (lastWasDash)
                {
                    continue;
                }

                builder.Append('-');
                lastWasDash = true;
            }

            return builder.ToString().Trim('-');
        }
    }
}
