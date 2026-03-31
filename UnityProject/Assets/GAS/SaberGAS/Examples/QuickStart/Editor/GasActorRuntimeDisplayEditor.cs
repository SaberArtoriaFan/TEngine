#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Examples.QuickStart.Editor
{
    [CustomEditor(typeof(GasActorRuntimeDisplay))]
    public sealed class GasActorRuntimeDisplayEditor : UnityEditor.Editor
    {
        private enum ResolvedTargetSource
        {
            Missing = 0,
            Asset = 1,
            Code = 2,
        }

        private readonly struct ResolvedTarget
        {
            public static ResolvedTarget Missing => new ResolvedTarget(ResolvedTargetSource.Missing, null, 0, string.Empty);

            public ResolvedTarget(ResolvedTargetSource source, UnityEngine.Object targetObject, int line, string path)
            {
                Source = source;
                TargetObject = targetObject;
                Line = line;
                Path = path ?? string.Empty;
            }

            public ResolvedTargetSource Source { get; }

            public UnityEngine.Object TargetObject { get; }

            public int Line { get; }

            public string Path { get; }

            public bool IsValid => TargetObject != null;

            public string SourceLabel
            {
                get
                {
                    switch (Source)
                    {
                        case ResolvedTargetSource.Asset:
                            return "Asset";
                        case ResolvedTargetSource.Code:
                            return "Code";
                        default:
                            return "Missing";
                    }
                }
            }
        }

        private static readonly string[] AuthoringSearchFolders = { "Assets/GAS/SaberGAS" };
        private static readonly string[] QuickStartCodeSearchFolders = { "Assets/GAS/SaberGAS/Examples/QuickStart" };

        private static readonly Dictionary<string, ResolvedTarget> AbilityTargetCache = new Dictionary<string, ResolvedTarget>(StringComparer.Ordinal);
        private static readonly Dictionary<string, ResolvedTarget> EffectTargetCache = new Dictionary<string, ResolvedTarget>(StringComparer.Ordinal);
        private static readonly Dictionary<string, ResolvedTarget> TriggerTargetCache = new Dictionary<string, ResolvedTarget>(StringComparer.Ordinal);

        private SerializedProperty _runtimeProvider;
        private SerializedProperty _actorId;
        private SerializedProperty _title;
        private SerializedProperty _autoFindProviderInScene;
        private SerializedProperty _autoRefreshInPlayMode;
        private SerializedProperty _refreshInterval;
        private SerializedProperty _maxEntriesPerSection;
        private SerializedProperty _section;

        private void OnEnable()
        {
            _runtimeProvider = serializedObject.FindProperty("_runtimeProvider");
            _actorId = serializedObject.FindProperty("_actorId");
            _title = serializedObject.FindProperty("_title");
            _autoFindProviderInScene = serializedObject.FindProperty("_autoFindProviderInScene");
            _autoRefreshInPlayMode = serializedObject.FindProperty("_autoRefreshInPlayMode");
            _refreshInterval = serializedObject.FindProperty("_refreshInterval");
            _maxEntriesPerSection = serializedObject.FindProperty("_maxEntriesPerSection");
            _section = serializedObject.FindProperty("_section");
        }

        public override void OnInspectorGUI()
        {
            var view = (GasActorRuntimeDisplay)target;

            serializedObject.Update();

            EditorGUILayout.LabelField("Binding", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_runtimeProvider);
            EditorGUILayout.PropertyField(_actorId);
            EditorGUILayout.PropertyField(_title);
            EditorGUILayout.PropertyField(_autoFindProviderInScene);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Refresh", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_autoRefreshInPlayMode);
            EditorGUILayout.PropertyField(_refreshInterval);
            EditorGUILayout.PropertyField(_maxEntriesPerSection);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Section Switch", EditorStyles.boldLabel);
            DrawSectionButtons(view);

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Refresh Now"))
            {
                Undo.RecordObject(view, "Refresh GAS Actor Runtime");
                view.RefreshInspectorContent();
                EditorUtility.SetDirty(view);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Jump To Definition", EditorStyles.boldLabel);
            DrawJumpPanel(view);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(view.InspectorStatus);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Inspector Output", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextArea(view.InspectorText, GUILayout.MinHeight(260f));
            }

            if (Application.isPlaying && view.AutoRefreshInPlayMode)
            {
                Repaint();
            }
        }

        private void DrawSectionButtons(GasActorRuntimeDisplay view)
        {
            var labels = new[]
            {
                "Overview",
                "AttributeSet",
                "Ability",
                "Effect",
                "Trigger",
            };

            var selected = GUILayout.Toolbar((int)view.Section, labels);
            if (selected != (int)view.Section)
            {
                Undo.RecordObject(view, "Switch GAS Runtime Section");
                view.SetSection((GasActorRuntimeDisplay.InspectorSection)selected);
                _section.intValue = selected;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(view);
            }
        }

        private void DrawJumpPanel(GasActorRuntimeDisplay view)
        {
            switch (view.Section)
            {
                case GasActorRuntimeDisplay.InspectorSection.Ability:
                    DrawAbilityJumpGroups(view);
                    break;
                case GasActorRuntimeDisplay.InspectorSection.Effect:
                    DrawEffectJumpGroups(view);
                    break;
                case GasActorRuntimeDisplay.InspectorSection.Trigger:
                    DrawTriggerJumpGroups(view);
                    break;
                default:
                    EditorGUILayout.HelpBox(
                        "当前分区仅显示本区运行时数据。切到 Ability / Effect / Trigger 可跳转定义。",
                        MessageType.None);
                    break;
            }
        }

        private void DrawAbilityJumpGroups(GasActorRuntimeDisplay view)
        {
            EditorGUILayout.LabelField("Ability", EditorStyles.miniBoldLabel);
            DrawIdButtons("Granted", view.GrantedAbilityIds, ResolveAbilityTargetById, "Ability");
            DrawIdButtons("Active", view.ActiveAbilityIds, ResolveAbilityTargetById, "Ability");
        }

        private void DrawEffectJumpGroups(GasActorRuntimeDisplay view)
        {
            EditorGUILayout.LabelField("Effect", EditorStyles.miniBoldLabel);
            DrawIdButtons("Active", view.ActiveEffectIds, ResolveEffectTargetById, "Effect");
        }

        private void DrawTriggerJumpGroups(GasActorRuntimeDisplay view)
        {
            EditorGUILayout.LabelField("Trigger", EditorStyles.miniBoldLabel);
            DrawIdButtons("Actor", view.ActorTriggerIds, ResolveTriggerTargetById, "Trigger");
            DrawIdButtons("Ability", view.AbilityTriggerIds, ResolveTriggerTargetById, "Trigger");
            DrawIdButtons("Effect", view.EffectTriggerIds, ResolveTriggerTargetById, "Trigger");
        }

        private void DrawIdButtons(
            string label,
            IReadOnlyList<string> ids,
            Func<string, ResolvedTarget> resolver,
            string kindName)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);

            if (ids == null || ids.Count == 0)
            {
                EditorGUILayout.LabelField("  (none)", EditorStyles.miniLabel);
                return;
            }

            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var target = resolver(id);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(id, EditorStyles.miniButtonLeft))
                {
                    OpenResolvedTarget(target, kindName, id);
                }

                GUILayout.Label(target.SourceLabel, EditorStyles.miniLabel, GUILayout.Width(56f));
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void OpenResolvedTarget(ResolvedTarget target, string kindName, string id)
        {
            if (!target.IsValid)
            {
                EditorUtility.DisplayDialog(
                    "未找到可跳转定义",
                    $"{kindName} Id `{id}` 未匹配到 Authoring 资产，也未在 QuickStart 示例代码中找到定义。",
                    "知道了");
                return;
            }

            Selection.activeObject = target.TargetObject;
            EditorGUIUtility.PingObject(target.TargetObject);
            if (target.Source == ResolvedTargetSource.Code && target.Line > 0)
            {
                AssetDatabase.OpenAsset(target.TargetObject, target.Line);
                return;
            }

            AssetDatabase.OpenAsset(target.TargetObject);
        }

        private static ResolvedTarget ResolveAbilityTargetById(string abilityId)
        {
            return ResolveById(
                abilityId,
                AbilityTargetCache,
                "AbilityDefinitionAsset",
                "Saber.GAS.Authoring.AbilityIdentityModule",
                "AbilityId");
        }

        private static ResolvedTarget ResolveEffectTargetById(string effectId)
        {
            return ResolveById(
                effectId,
                EffectTargetCache,
                "EffectDefinitionAsset",
                "Saber.GAS.Authoring.EffectIdentityModule",
                "EffectId");
        }

        private static ResolvedTarget ResolveTriggerTargetById(string triggerId)
        {
            return ResolveById(
                triggerId,
                TriggerTargetCache,
                "TriggerDefinitionAsset",
                "Saber.GAS.Authoring.TriggerIdentityModule",
                "TriggerId");
        }

        private static ResolvedTarget ResolveById(
            string id,
            Dictionary<string, ResolvedTarget> cache,
            string assetTypeName,
            string identityTypeName,
            string identityValuePropertyName)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return ResolvedTarget.Missing;
            }

            if (cache.TryGetValue(id, out var cached))
            {
                if (IsCacheValid(cached))
                {
                    return cached;
                }

                cache.Remove(id);
            }

            var authoringAsset = ResolveAuthoringAssetById(id, assetTypeName, identityTypeName, identityValuePropertyName);
            if (authoringAsset != null)
            {
                var assetTarget = new ResolvedTarget(ResolvedTargetSource.Asset, authoringAsset, 0, string.Empty);
                cache[id] = assetTarget;
                return assetTarget;
            }

            var codeTarget = ResolveCodeTargetById(id);
            cache[id] = codeTarget;
            return codeTarget;
        }

        private static bool IsCacheValid(ResolvedTarget cached)
        {
            if (!cached.IsValid)
            {
                return true;
            }

            return AssetDatabase.Contains(cached.TargetObject);
        }

        private static UnityEngine.Object ResolveAuthoringAssetById(
            string id,
            string assetTypeName,
            string identityTypeName,
            string identityValuePropertyName)
        {
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", AuthoringSearchFolders);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                for (var j = 0; j < assets.Length; j++)
                {
                    var candidate = assets[j];
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (!string.Equals(candidate.GetType().Name, assetTypeName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!MatchByIdentity(candidate, identityTypeName, identityValuePropertyName, id) &&
                        !MatchByBuildDefinition(candidate, id))
                    {
                        continue;
                    }

                    return candidate;
                }
            }

            return null;
        }

        private static ResolvedTarget ResolveCodeTargetById(string id)
        {
            var guids = AssetDatabase.FindAssets("t:MonoScript", QuickStartCodeSearchFolders);
            var exactToken = "\"" + id + "\"";
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path) ||
                    !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                string[] lines;
                try
                {
                    lines = File.ReadAllLines(fullPath);
                }
                catch
                {
                    continue;
                }

                var line = FindLine(lines, exactToken, id);
                if (line <= 0)
                {
                    continue;
                }

                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null)
                {
                    continue;
                }

                return new ResolvedTarget(ResolvedTargetSource.Code, script, line, path);
            }

            return ResolvedTarget.Missing;
        }

        private static int FindLine(string[] lines, string exactToken, string fallbackToken)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!string.IsNullOrEmpty(exactToken) &&
                    line.IndexOf(exactToken, StringComparison.Ordinal) >= 0)
                {
                    return i + 1;
                }
            }

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!string.IsNullOrEmpty(fallbackToken) &&
                    line.IndexOf(fallbackToken, StringComparison.Ordinal) >= 0)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        private static bool MatchByIdentity(
            UnityEngine.Object asset,
            string identityTypeName,
            string identityValuePropertyName,
            string expectedId)
        {
            if (asset == null)
            {
                return false;
            }

            try
            {
                var identityType = FindType(identityTypeName);
                if (identityType == null)
                {
                    return false;
                }

                var getModule = asset.GetType().GetMethod("GetModule", BindingFlags.Instance | BindingFlags.Public);
                if (getModule == null || !getModule.IsGenericMethodDefinition)
                {
                    return false;
                }

                var genericGetModule = getModule.MakeGenericMethod(identityType);
                var identity = genericGetModule.Invoke(asset, null);
                if (identity == null)
                {
                    return false;
                }

                var idProperty = identity.GetType().GetProperty(identityValuePropertyName, BindingFlags.Instance | BindingFlags.Public);
                if (idProperty == null)
                {
                    return false;
                }

                var value = idProperty.GetValue(identity) as string;
                return string.Equals(value, expectedId, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static bool MatchByBuildDefinition(UnityEngine.Object asset, string expectedId)
        {
            if (asset == null)
            {
                return false;
            }

            try
            {
                var buildDefinition = asset.GetType().GetMethod("BuildDefinition", BindingFlags.Instance | BindingFlags.Public);
                if (buildDefinition == null)
                {
                    return false;
                }

                object definition;
                var parameters = buildDefinition.GetParameters();
                if (parameters.Length == 0)
                {
                    definition = buildDefinition.Invoke(asset, null);
                }
                else if (parameters.Length == 1)
                {
                    definition = buildDefinition.Invoke(asset, new object[] { null });
                }
                else
                {
                    return false;
                }

                if (definition == null)
                {
                    return false;
                }

                var definitionId = definition.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);
                if (definitionId == null)
                {
                    return false;
                }

                var idObject = definitionId.GetValue(definition);
                if (idObject == null)
                {
                    return false;
                }

                var valueProperty = idObject.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                if (valueProperty == null)
                {
                    return false;
                }

                var actualId = valueProperty.GetValue(idObject) as string;
                return string.Equals(actualId, expectedId, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static Type FindType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                Type resolvedType;
                try
                {
                    resolvedType = assemblies[i].GetType(fullName, false);
                }
                catch
                {
                    continue;
                }

                if (resolvedType != null)
                {
                    return resolvedType;
                }
            }

            return null;
        }
    }
}
#endif
