using Saber.GAS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Editor
{
    [CustomEditor(typeof(TriggerDefinitionAsset))]
    public sealed class TriggerDefinitionAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var trigger = target as TriggerDefinitionAsset;
            if (trigger == null)
            {
                return;
            }

            if (trigger.EnsureModulesInitialized())
            {
                EditorUtility.SetDirty(trigger);
            }

            serializedObject.Update();

            var catalog = GasEditorUtility.FindCatalog(trigger);
            var modules = serializedObject.FindProperty("_modules");
            var identity = trigger.GetModule<TriggerIdentityModule>();
            var routing = trigger.GetModule<TriggerRoutingModule>();
            var actionModule = trigger.GetModule<TriggerActionModule>();

            GasEditorUtility.DrawInspectorHeader(
                "Trigger 模块容器",
                "把路由、标签过滤、关系限制、阈值和动作拆成独立模块后，只会看到当前这条 Trigger 真正在用的部分。");
            GasEditorUtility.DrawCatalogBar(catalog);
            EditorGUILayout.Space();

            if (identity == null || string.IsNullOrWhiteSpace(identity.TriggerId))
            {
                EditorGUILayout.HelpBox("当前 Trigger 还没有可用的触发器 Id。", MessageType.Warning);
                if (GUILayout.Button("自动生成 TriggerId"))
                {
                    serializedObject.ApplyModifiedProperties();
                    trigger.GetOrAddModule<TriggerIdentityModule>().TriggerId = GasEditorUtility.SuggestId("trigger", trigger.name);
                    EditorUtility.SetDirty(trigger);
                    serializedObject.Update();
                }
            }

            var actionIssue = GasAuthoringValidationUtility.GetTriggerActionIssue(trigger);
            if (!string.IsNullOrWhiteSpace(actionIssue))
            {
                EditorGUILayout.HelpBox(actionIssue, MessageType.Warning);
            }

            GasEditorUtility.DrawSection("概览", "先确认这条 Trigger 当前由哪些模块组成。", () =>
            {
                EditorGUILayout.LabelField("模块数量", modules == null ? "0" : modules.arraySize.ToString());
                EditorGUILayout.LabelField("局部标签定义", trigger.LocalTagDefinitions.Count.ToString());
                EditorGUILayout.LabelField("当前事件", routing == null ? "Manual" : routing.EventKind.ToString());
                EditorGUILayout.LabelField("当前动作", actionModule == null ? "None" : actionModule.Kind.ToString());
            });

            GasModuleEditorUtility.DrawModuleList<TriggerAuthoringModule>(
                serializedObject,
                modules,
                "Trigger 模块列表",
                "当前还是空容器。建议先添加“基础信息”，再按需补“路由”“标签过滤”“关系限制”“阈值”“动作”。");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
