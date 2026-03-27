using Saber.GAS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Editor
{
    [CustomEditor(typeof(CombatDefinitionCatalogAsset))]
    public sealed class CombatDefinitionCatalogAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var catalog = target as CombatDefinitionCatalogAsset;
            if (catalog == null)
            {
                return;
            }

            var randomSeed = serializedObject.FindProperty("_randomSeed");
            var worldTags = serializedObject.FindProperty("_worldTags");
            var globalTagDefinitions = serializedObject.FindProperty("_globalTagDefinitions");
            var globalResourceDefinitions = serializedObject.FindProperty("_globalResourceDefinitions");
            var initialActors = serializedObject.FindProperty("_initialActors");

            var abilities = GasEditorUtility.GetEmbeddedAssets<AbilityDefinitionAsset>(catalog);
            var effects = GasEditorUtility.GetEmbeddedAssets<EffectDefinitionAsset>(catalog);
            var triggers = GasEditorUtility.GetEmbeddedAssets<TriggerDefinitionAsset>(catalog);
            var actorTemplates = GasEditorUtility.GetEmbeddedAssets<CombatActorTemplateAsset>(catalog);
            var issues = GasAuthoringValidationUtility.CollectCatalogIssues(catalog);

            GasEditorUtility.DrawInspectorHeader(
                "GAS 根配置",
                "以 CombatDefinitionCatalogAsset 作为唯一入口，集中管理初始化配置、全局词库、模板、源码生成和 Workbench。");

            GasEditorUtility.DrawCatalogBar(catalog);
            EditorGUILayout.Space();

            if (issues.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    string.Format("当前 Catalog 中发现 {0} 个配置问题，建议先处理后再继续连线、生成源码或运行预览。", issues.Count),
                    MessageType.Warning);

                for (var i = 0; i < issues.Count && i < 8; i++)
                {
                    EditorGUILayout.LabelField(string.Format("- {0}", issues[i]), EditorStyles.wordWrappedMiniLabel);
                }

                if (issues.Count > 8)
                {
                    EditorGUILayout.LabelField("更多问题可以到 Workbench 的预览与校验面板继续查看。", EditorStyles.wordWrappedMiniLabel);
                }

                EditorGUILayout.Space();
            }

            GasEditorUtility.DrawSection("概览摘要", "先快速确认这份根配置里挂了多少内容。", () =>
            {
                EditorGUILayout.LabelField("Ability 数量", abilities.Length.ToString());
                EditorGUILayout.LabelField("Effect 数量", effects.Length.ToString());
                EditorGUILayout.LabelField("Trigger 数量", triggers.Length.ToString());
                EditorGUILayout.LabelField("ActorTemplate 数量", actorTemplates.Length.ToString());
                EditorGUILayout.LabelField("全局标签定义", globalTagDefinitions == null ? "0" : globalTagDefinitions.arraySize.ToString());
                EditorGUILayout.LabelField("全局 ResourceId", globalResourceDefinitions == null ? "0" : globalResourceDefinitions.arraySize.ToString());
                EditorGUILayout.LabelField("Initial Actor 数量", initialActors == null ? "0" : initialActors.arraySize.ToString());
            });

            GasEditorUtility.DrawSection("Workbench 与同步", "从这里进入可视化编辑器，或手动同步子资源收集结果。", () =>
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("打开 GAS Workbench"))
                {
                    GasWorkbenchWindow.Open(catalog);
                }

                if (GUILayout.Button("同步内嵌配置"))
                {
                    serializedObject.ApplyModifiedProperties();
                    GasEditorUtility.SyncEmbeddedDefinitions(catalog);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("打开模块扩展文档"))
                {
                    GasEditorUtility.OpenAssetAtPath(GasSampleCatalogUtility.ModuleExtensionGuideDocumentPath);
                }
                EditorGUILayout.EndHorizontal();
            });

            GasEditorUtility.DrawSection("Example 与入门", "生成一份完整示例，并直接打开快速开始文档。", () =>
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("生成 Example Catalog"))
                {
                    serializedObject.ApplyModifiedProperties();
                    GasSampleCatalogUtility.CreateWorkbenchSampleCatalog();
                }

                if (GUILayout.Button("打开快速开始文档"))
                {
                    GasEditorUtility.OpenAssetAtPath(GasSampleCatalogUtility.QuickStartDocumentPath);
                }
                EditorGUILayout.EndHorizontal();
            });

            GasEditorUtility.DrawSection("起手模板", "先生成一批常用配置，后续再在 Workbench 里继续细化。", () =>
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("瞬发伤害 Ability"))
                {
                    serializedObject.ApplyModifiedProperties();
                    GasAuthoringTemplateUtility.CreateInstantDamageAbilityTemplate(catalog);
                }

                if (GUILayout.Button("Buff Effect"))
                {
                    serializedObject.ApplyModifiedProperties();
                    GasAuthoringTemplateUtility.CreateBuffEffectTemplate(catalog);
                }

                if (GUILayout.Button("被动 Trigger"))
                {
                    serializedObject.ApplyModifiedProperties();
                    GasAuthoringTemplateUtility.CreatePassiveTriggerTemplate(catalog);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4f);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("空 Ability"))
                {
                    serializedObject.ApplyModifiedProperties();
                    GasAuthoringTemplateUtility.CreateBlankAbility(catalog);
                }

                if (GUILayout.Button("空 Effect"))
                {
                    serializedObject.ApplyModifiedProperties();
                    GasAuthoringTemplateUtility.CreateBlankEffect(catalog);
                }

                if (GUILayout.Button("空 Trigger"))
                {
                    serializedObject.ApplyModifiedProperties();
                    GasAuthoringTemplateUtility.CreateBlankTrigger(catalog);
                }
                EditorGUILayout.EndHorizontal();
            });

            GasEditorUtility.DrawTagDefinitionLibrary(
                globalTagDefinitions,
                "全局标签词库",
                "把跨 Ability、Effect、Trigger 反复复用的标签放在这里。后续编辑标签时会自动作为候选出现。",
                CombatTagUsage.Shared);

            GasResourceEditorUtility.DrawResourceDefinitionLibrary(
                globalResourceDefinitions,
                "全局 ResourceId 词库",
                "把所有运行时资源 Id 集中定义在这里，例如 health、mana、stamina。之后在 Cost、Delta、Trigger 等位置会直接以下拉方式选择，避免手填出错。");

            GasEditorUtility.DrawSection("全局 Id 源码", "根据全局 Tag 和 ResourceId 词库生成静态常量类，避免代码侧手写字符串。", () =>
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("生成当前 Catalog"))
                {
                    serializedObject.ApplyModifiedProperties();
                    GasGlobalCodeGenerator.GenerateForCatalog(catalog, true);
                }

                if (GUILayout.Button("生成全部 Catalog"))
                {
                    serializedObject.ApplyModifiedProperties();
                    GasGlobalCodeGenerator.GenerateForAllCatalogs(true);
                }
                EditorGUILayout.EndHorizontal();
            });

            GasEditorUtility.DrawSection("初始化配置", "这里决定一行初始化时世界状态的默认内容。", () =>
            {
                GasEditorUtility.DrawProperty(randomSeed, "随机种子");
                GasEditorUtility.DrawTagEditor(
                    worldTags,
                    catalog,
                    catalog,
                    "世界标签",
                    GasEditorUtility.RuntimeTagUsage,
                    "当前没有世界标签。");
                GasEditorUtility.DrawProperty(initialActors, "初始 Actor", true);
            });

            GasEditorUtility.DrawReadOnlyObjectList("Abilities", abilities);
            GasEditorUtility.DrawReadOnlyObjectList("Effects", effects);
            GasEditorUtility.DrawReadOnlyObjectList("Triggers", triggers);
            GasEditorUtility.DrawReadOnlyObjectList("Actor Templates", actorTemplates);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
