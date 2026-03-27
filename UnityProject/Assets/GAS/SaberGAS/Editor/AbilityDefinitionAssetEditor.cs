using Saber.GAS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Editor
{
    [CustomEditor(typeof(AbilityDefinitionAsset))]
    public sealed class AbilityDefinitionAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var ability = target as AbilityDefinitionAsset;
            if (ability == null)
            {
                return;
            }

            if (ability.EnsureModulesInitialized())
            {
                EditorUtility.SetDirty(ability);
            }

            serializedObject.Update();

            var catalog = GasEditorUtility.FindCatalog(ability);
            var modules = serializedObject.FindProperty("_modules");
            var identity = ability.GetModule<AbilityIdentityModule>();
            var effectModule = ability.GetModule<AbilityEffectPayloadModule>();
            var triggerModule = ability.GetModule<AbilityTriggerModule>();

            GasEditorUtility.DrawInspectorHeader(
                "Ability 模块容器",
                "Ability 现在只负责承载模块。需要什么能力，就按需添加什么模块。");
            GasEditorUtility.DrawCatalogBar(catalog);
            EditorGUILayout.Space();

            if (identity == null || string.IsNullOrWhiteSpace(identity.AbilityId))
            {
                EditorGUILayout.HelpBox("当前 Ability 还没有可用的能力 Id。", MessageType.Warning);
                if (GUILayout.Button("自动生成 AbilityId"))
                {
                    serializedObject.ApplyModifiedProperties();
                    ability.GetOrAddModule<AbilityIdentityModule>().AbilityId = GasEditorUtility.SuggestId("ability", ability.name);
                    EditorUtility.SetDirty(ability);
                    serializedObject.Update();
                }
            }

            GasEditorUtility.DrawSection("概览", "先看当前 Ability 只挂了哪些模块和关系。", () =>
            {
                EditorGUILayout.LabelField("模块数量", modules == null ? "0" : modules.arraySize.ToString());
                EditorGUILayout.LabelField("激活时效果", effectModule == null ? "0" : effectModule.Effects.Count.ToString());
                EditorGUILayout.LabelField("周期效果", effectModule == null ? "0" : effectModule.PeriodicEffects.Count.ToString());
                EditorGUILayout.LabelField("结束效果", effectModule == null ? "0" : effectModule.EndEffects.Count.ToString());
                EditorGUILayout.LabelField("触发器", triggerModule == null ? "0" : triggerModule.Triggers.Count.ToString());
                EditorGUILayout.LabelField("局部标签定义", ability.LocalTagDefinitions.Count.ToString());
            });

            GasModuleEditorUtility.DrawModuleList<AbilityAuthoringModule>(
                serializedObject,
                modules,
                "Ability 模块列表",
                "当前还是空容器。建议先添加“基础信息”，再按需补“标签与过滤”“目标规则”“消耗与冷却”“效果载荷”“触发器载荷”。");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
