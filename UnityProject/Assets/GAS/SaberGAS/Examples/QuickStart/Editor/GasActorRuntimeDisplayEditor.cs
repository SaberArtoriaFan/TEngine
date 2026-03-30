#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Examples.QuickStart.Editor
{
    [CustomEditor(typeof(GasActorRuntimeDisplay))]
    public sealed class GasActorRuntimeDisplayEditor : UnityEditor.Editor
    {
        private static readonly Dictionary<string, UnityEngine.Object> AbilityAssetCache = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        private static readonly Dictionary<string, UnityEngine.Object> EffectAssetCache = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        private static readonly Dictionary<string, UnityEngine.Object> TriggerAssetCache = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);

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
                    DrawAbilityJumpGroups(view);
                    EditorGUILayout.Space(4f);
                    DrawEffectJumpGroups(view);
                    EditorGUILayout.Space(4f);
                    DrawTriggerJumpGroups(view);
                    break;
            }
        }

        private void DrawAbilityJumpGroups(GasActorRuntimeDisplay view)
        {
            EditorGUILayout.LabelField("Ability", EditorStyles.miniBoldLabel);
            DrawIdButtons("Granted", view.GrantedAbilityIds, ResolveAbilityAssetById, "Ability");
            DrawIdButtons("Active", view.ActiveAbilityIds, ResolveAbilityAssetById, "Ability");
        }

        private void DrawEffectJumpGroups(GasActorRuntimeDisplay view)
        {
            EditorGUILayout.LabelField("Effect", EditorStyles.miniBoldLabel);
            DrawIdButtons("Active", view.ActiveEffectIds, ResolveEffectAssetById, "Effect");
        }

        private void DrawTriggerJumpGroups(GasActorRuntimeDisplay view)
        {
            EditorGUILayout.LabelField("Trigger", EditorStyles.miniBoldLabel);
            DrawIdButtons("Actor", view.ActorTriggerIds, ResolveTriggerAssetById, "Trigger");
            DrawIdButtons("Ability", view.AbilityTriggerIds, ResolveTriggerAssetById, "Trigger");
            DrawIdButtons("Effect", view.EffectTriggerIds, ResolveTriggerAssetById, "Trigger");
        }

        private void DrawIdButtons(
            string label,
            IReadOnlyList<string> ids,
            Func<string, UnityEngine.Object> resolver,
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
                    OpenResolvedAsset(target, kindName, id);
                }

                GUILayout.Label(target != null ? "Found" : "Missing", EditorStyles.miniLabel, GUILayout.Width(52f));
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void OpenResolvedAsset(UnityEngine.Object target, string kindName, string id)
        {
            if (target == null)
            {
                EditorUtility.DisplayDialog(
                    "未找到对应定义",
                    $"{kindName} Id `{id}` 没有匹配到 Authoring 资产。",
                    "知道了");
                return;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
            AssetDatabase.OpenAsset(target);
        }

        private static UnityEngine.Object ResolveAbilityAssetById(string abilityId)
        {
            return ResolveById(
                abilityId,
                AbilityAssetCache,
                "AbilityDefinitionAsset",
                "Saber.GAS.Authoring.AbilityIdentityModule",
                "AbilityId");
        }

        private static UnityEngine.Object ResolveEffectAssetById(string effectId)
        {
            return ResolveById(
                effectId,
                EffectAssetCache,
                "EffectDefinitionAsset",
                "Saber.GAS.Authoring.EffectIdentityModule",
                "EffectId");
        }

        private static UnityEngine.Object ResolveTriggerAssetById(string triggerId)
        {
            return ResolveById(
                triggerId,
                TriggerAssetCache,
                "TriggerDefinitionAsset",
                "Saber.GAS.Authoring.TriggerIdentityModule",
                "TriggerId");
        }

        private static UnityEngine.Object ResolveById(
            string id,
            Dictionary<string, UnityEngine.Object> cache,
            string assetTypeName,
            string identityTypeName,
            string identityValuePropertyName)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            if (cache.TryGetValue(id, out var cached))
            {
                if (cached == null || AssetDatabase.Contains(cached))
                {
                    return cached;
                }

                cache.Remove(id);
            }

            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/GAS/SaberGAS" });
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

                    cache[id] = candidate;
                    return candidate;
                }
            }

            cache[id] = null;
            return null;
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
