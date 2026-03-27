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

            switch (action.Kind)
            {
                case TriggerActionKind.ActivateAbility:
                    return action.TriggeredAbility == null
                        ? "当前 Trigger 动作是 ActivateAbility，但没有指定要触发的 Ability。"
                        : null;
                case TriggerActionKind.ApplyEffect:
                    return action.EffectAsset == null
                        ? "当前 Trigger 动作是 ApplyEffect，但没有指定目标 Effect。"
                        : null;
                case TriggerActionKind.RemoveEffectById:
                    return action.EffectAsset == null
                        ? "当前 Trigger 动作是 RemoveEffectById，但没有指定要移除的 Effect。"
                        : null;
                case TriggerActionKind.CancelAbility:
                    return action.AbilityToCancel == null
                        ? "当前 Trigger 动作是 CancelAbility，但没有指定要取消的 Ability。"
                        : null;
                case TriggerActionKind.RemoveEffectsByTag:
                case TriggerActionKind.CleanseByTag:
                    return string.IsNullOrWhiteSpace(action.EffectTag)
                        ? "当前 Trigger 动作依赖 EffectTag，但 EffectTag 为空。"
                        : null;
                case TriggerActionKind.AddResource:
                case TriggerActionKind.RemoveResource:
                case TriggerActionKind.AddResourceFromImpact:
                    return string.IsNullOrWhiteSpace(action.ResourceId)
                        ? "当前 Trigger 动作需要 ResourceId，但 ResourceId 为空。"
                        : null;
                case TriggerActionKind.AddTag:
                case TriggerActionKind.RemoveTag:
                    return string.IsNullOrWhiteSpace(action.Tag)
                        ? "当前 Trigger 动作需要 Tag，但 Tag 为空。"
                        : null;
                case TriggerActionKind.EmitCue:
                    return string.IsNullOrWhiteSpace(action.CueName)
                        ? "当前 Trigger 动作需要 CueName，但 CueName 为空。"
                        : null;
                default:
                    return null;
            }
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
