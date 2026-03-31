using System;
using System.Collections.Generic;
using Saber.GAS.Authoring;
using Saber.GAS.Triggers;

namespace Saber.GAS.Editor
{
    internal static class GasAuthoringValidationUtility
    {
        public static string GetEffectBuildIssue(EffectDefinitionAsset effectAsset)
        {
            if (effectAsset == null)
            {
                return null;
            }

            try
            {
                effectAsset.EnsureModulesInitialized();
                effectAsset.BuildDefinition(new CombatAuthoringBuildContext());
                return null;
            }
            catch (Exception exception)
            {
                return exception.Message;
            }
        }

        public static string GetTriggerActionIssue(TriggerDefinitionAsset triggerAsset)
        {
            if (triggerAsset == null)
            {
                return null;
            }

            triggerAsset.EnsureModulesInitialized();
            var actionModule = triggerAsset.GetModule<TriggerActionModule>();
            var action = actionModule?.Action;
            if (action == null)
            {
                return null;
            }

            TriggerActionDefinition builtAction;
            try
            {
                builtAction = new TriggerActionDefinition();
                action.ApplyTo(builtAction, new CombatAuthoringBuildContext(), triggerAsset.name);
            }
            catch (Exception exception)
            {
                return exception.Message;
            }

            if (!CombatTriggerActionDescriptorRegistryHub.TryGetDescriptor(builtAction.Kind, out var descriptor))
            {
                return string.Format("TriggerActionKind '{0}' 未注册到描述符系统。", builtAction.Kind);
            }

            return descriptor.Validate(builtAction);
        }

        public static List<string> CollectCatalogIssues(CombatDefinitionCatalogAsset catalog)
        {
            var issues = new List<string>();
            if (catalog == null)
            {
                return issues;
            }

            var buildContext = new CombatAuthoringBuildContext();

            var abilities = GasEditorUtility.GetEmbeddedAssets<AbilityDefinitionAsset>(catalog);
            for (var i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i];
                if (ability == null)
                {
                    continue;
                }

                ability.EnsureModulesInitialized();
                var identity = ability.GetModule<AbilityIdentityModule>();
                if (identity == null || string.IsNullOrWhiteSpace(identity.AbilityId))
                {
                    issues.Add(string.Format("{0}: AbilityId 不能为空。", ability.name));
                    continue;
                }

                TryBuild(() => buildContext.BuildAbility(ability), ability.name, issues);
            }

            var effects = GasEditorUtility.GetEmbeddedAssets<EffectDefinitionAsset>(catalog);
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null)
                {
                    continue;
                }

                effect.EnsureModulesInitialized();
                var identity = effect.GetModule<EffectIdentityModule>();
                if (identity == null || string.IsNullOrWhiteSpace(identity.EffectId))
                {
                    issues.Add(string.Format("{0}: EffectId 不能为空。", effect.name));
                    continue;
                }

                TryBuild(() => buildContext.BuildEffect(effect), effect.name, issues);
            }

            var actorTemplates = GasEditorUtility.GetEmbeddedAssets<CombatActorTemplateAsset>(catalog);
            for (var i = 0; i < actorTemplates.Length; i++)
            {
                if (actorTemplates[i] == null)
                {
                    continue;
                }

                var serializedObject = new UnityEditor.SerializedObject(actorTemplates[i]);
                var issue = GasEditorUtility.GetRequiredStringIssue(serializedObject.FindProperty("_defaultActorId"), "DefaultActorId");
                if (!string.IsNullOrWhiteSpace(issue))
                {
                    issues.Add(string.Format("{0}: {1}", actorTemplates[i].name, issue));
                }
            }

            var triggers = GasEditorUtility.GetEmbeddedAssets<TriggerDefinitionAsset>(catalog);
            for (var i = 0; i < triggers.Length; i++)
            {
                var trigger = triggers[i];
                if (trigger == null)
                {
                    continue;
                }

                trigger.EnsureModulesInitialized();
                var identity = trigger.GetModule<TriggerIdentityModule>();
                if (identity == null || string.IsNullOrWhiteSpace(identity.TriggerId))
                {
                    issues.Add(string.Format("{0}: TriggerId 不能为空。", trigger.name));
                    continue;
                }

                var actionIssue = GetTriggerActionIssue(trigger);
                if (!string.IsNullOrWhiteSpace(actionIssue))
                {
                    issues.Add(string.Format("{0}: {1}", trigger.name, actionIssue));
                }

                TryBuild(() => buildContext.BuildTrigger(trigger), trigger.name, issues);
            }

            return issues;
        }

        private static void TryBuild(Action buildAction, string assetName, ICollection<string> issues)
        {
            if (buildAction == null)
            {
                return;
            }

            try
            {
                buildAction();
            }
            catch (Exception exception)
            {
                issues.Add(string.Format("{0}: {1}", assetName, exception.Message));
            }
        }
    }
}
