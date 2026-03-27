using Saber.GAS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Editor
{
    [CustomEditor(typeof(EffectDefinitionAsset))]
    public sealed class EffectDefinitionAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var effect = target as EffectDefinitionAsset;
            if (effect == null)
            {
                return;
            }

            if (effect.EnsureModulesInitialized())
            {
                EditorUtility.SetDirty(effect);
            }

            serializedObject.Update();

            var catalog = GasEditorUtility.FindCatalog(effect);
            var modules = serializedObject.FindProperty("_modules");
            var identity = effect.GetModule<EffectIdentityModule>();
            var removalModule = effect.GetModule<EffectRemovalModule>();
            var triggerModule = effect.GetModule<EffectTriggerModule>();
            var resourceModule = effect.GetModule<EffectResourcePayloadModule>();
            var projectileModule = effect.GetModule<EffectProjectilePayloadModule>();
            var shieldModule = effect.GetModule<EffectShieldPayloadModule>();
            var buildIssue = GasAuthoringValidationUtility.GetEffectBuildIssue(effect);

            GasEditorUtility.DrawInspectorHeader(
                "Effect 模块容器",
                "Effect 现在按模块承载标签、移除规则、资源变化、护盾语义和时序，不再把所有字段一次性摊开。");
            GasEditorUtility.DrawCatalogBar(catalog);
            EditorGUILayout.Space();

            if (identity == null || string.IsNullOrWhiteSpace(identity.EffectId))
            {
                EditorGUILayout.HelpBox("当前 Effect 还没有可用的效果 Id。", MessageType.Warning);
                if (GUILayout.Button("自动生成 EffectId"))
                {
                    serializedObject.ApplyModifiedProperties();
                    effect.GetOrAddModule<EffectIdentityModule>().EffectId = GasEditorUtility.SuggestId("effect", effect.name);
                    EditorUtility.SetDirty(effect);
                    serializedObject.Update();
                }
            }

            if (!string.IsNullOrWhiteSpace(buildIssue))
            {
                EditorGUILayout.HelpBox(buildIssue, MessageType.Warning);
            }

            GasEditorUtility.DrawSection("概览", "先看当前 Effect 实际挂载了哪些能力块。", () =>
            {
                EditorGUILayout.LabelField("模块数量", modules == null ? "0" : modules.arraySize.ToString());
                EditorGUILayout.LabelField("局部标签定义", effect.LocalTagDefinitions.Count.ToString());
                EditorGUILayout.LabelField("移除目标效果", removalModule == null ? "0" : removalModule.RemovedTargetEffects.Count.ToString());
                EditorGUILayout.LabelField("即时资源变化", resourceModule == null || resourceModule.InstantResourceDeltas == null ? "0" : resourceModule.InstantResourceDeltas.Length.ToString());
                EditorGUILayout.LabelField("周期资源变化", resourceModule == null || resourceModule.PeriodicResourceDeltas == null ? "0" : resourceModule.PeriodicResourceDeltas.Length.ToString());
                EditorGUILayout.LabelField("护盾语义", shieldModule == null || shieldModule.ShieldSemantics == null ? "0" : shieldModule.ShieldSemantics.Length.ToString());
                EditorGUILayout.LabelField("触发器", triggerModule == null ? "0" : triggerModule.Triggers.Count.ToString());
            });

            GasModuleEditorUtility.DrawModuleList<EffectAuthoringModule>(
                serializedObject,
                modules,
                "Effect 模块列表",
                "当前还是空容器。建议先添加“基础信息”，再按需补“标签”“移除规则”“资源变化”“时序规则”“触发器载荷”等模块。");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
