using Saber.GAS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Editor
{
    [CustomEditor(typeof(CombatActorTemplateAsset))]
    public sealed class CombatActorTemplateAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var actorTemplate = target as CombatActorTemplateAsset;
            if (actorTemplate == null)
            {
                return;
            }

            var catalog = GasEditorUtility.FindCatalog(actorTemplate);
            var defaultActorId = serializedObject.FindProperty("_defaultActorId");
            var defaultTeamId = serializedObject.FindProperty("_defaultTeamId");
            var defaultPosition = serializedObject.FindProperty("_defaultPosition");
            var initialTags = serializedObject.FindProperty("_initialTags");
            var attributes = serializedObject.FindProperty("_attributes");
            var resources = serializedObject.FindProperty("_resources");
            var grantedAbilities = serializedObject.FindProperty("_grantedAbilities");
            var actorTriggers = serializedObject.FindProperty("_actorTriggers");

            GasEditorUtility.DrawInspectorHeader(
                "ActorTemplate 配置",
                "围绕出生身份、初始状态和运行时授予内容来编辑 ActorTemplate。");
            GasEditorUtility.DrawCatalogBar(catalog);
            EditorGUILayout.Space();

            var actorIdIssue = GasEditorUtility.GetRequiredStringIssue(defaultActorId, "DefaultActorId");
            if (!string.IsNullOrWhiteSpace(actorIdIssue))
            {
                EditorGUILayout.HelpBox(actorIdIssue, MessageType.Warning);
                if (GUILayout.Button("用资源名自动生成 DefaultActorId"))
                {
                    defaultActorId.stringValue = GasEditorUtility.SuggestId("actor", actorTemplate.name);
                }
            }

            if (GasEditorUtility.HasNullObjectReferences(grantedAbilities) ||
                GasEditorUtility.HasNullObjectReferences(actorTriggers))
            {
                EditorGUILayout.HelpBox("授予列表中存在空引用，建议先清理。", MessageType.Warning);
                if (GUILayout.Button("移除空引用"))
                {
                    GasEditorUtility.RemoveNullObjectReferences(grantedAbilities);
                    GasEditorUtility.RemoveNullObjectReferences(actorTriggers);
                }
            }

            GasEditorUtility.DrawSection("关系摘要", "先确认这份模板出生时会带什么。", () =>
            {
                EditorGUILayout.LabelField("授予 Ability", grantedAbilities.arraySize.ToString());
                EditorGUILayout.LabelField("授予 Trigger", actorTriggers.arraySize.ToString());
            });

            GasEditorUtility.DrawSection("基础信息", null, () =>
            {
                GasEditorUtility.DrawProperty(defaultActorId, "默认 ActorId");
                GasEditorUtility.DrawProperty(defaultTeamId, "默认 TeamId");
                GasEditorUtility.DrawProperty(defaultPosition, "默认位置", true);
            });

            GasEditorUtility.DrawSection("初始状态", null, () =>
            {
                GasEditorUtility.DrawTagEditor(
                    initialTags,
                    catalog,
                    actorTemplate,
                    "初始标签",
                    GasEditorUtility.RuntimeTagUsage,
                    "当前没有初始标签。");
                GasEditorUtility.DrawProperty(attributes, "初始属性", true);
                GasEditorUtility.DrawProperty(resources, "初始资源", true);
            });

            GasEditorUtility.DrawSection("运行时授予", null, () =>
            {
                GasEditorUtility.DrawProperty(grantedAbilities, "授予 Ability", true);
                GasEditorUtility.DrawProperty(actorTriggers, "授予 Trigger", true);
            });

            if (catalog != null)
            {
                GasEditorUtility.DrawSection("快速补链", "直接给当前 ActorTemplate 新建并挂接 Ability 或 Trigger。", () =>
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("新建并授予 Ability"))
                    {
                        var ability = GasAuthoringTemplateUtility.CreateBlankAbility(catalog, false);
                        GasEditorUtility.AppendObjectReference(serializedObject, "_grantedAbilities", ability);
                    }

                    if (GUILayout.Button("新建并授予 Trigger"))
                    {
                        var trigger = GasAuthoringTemplateUtility.CreateBlankTrigger(catalog, false);
                        GasEditorUtility.AppendObjectReference(serializedObject, "_actorTriggers", trigger);
                    }
                    EditorGUILayout.EndHorizontal();
                });
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
