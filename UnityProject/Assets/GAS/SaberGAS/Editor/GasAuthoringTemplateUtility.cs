using Saber.GAS.Abilities;
using Saber.GAS.Authoring;
using Saber.GAS.Effects;
using Saber.GAS.Triggers;
using UnityEditor;

namespace Saber.GAS.Editor
{
    internal static class GasAuthoringTemplateUtility
    {
        public static AbilityDefinitionAsset CreateInstantDamageAbilityTemplate(
            CombatDefinitionCatalogAsset catalog,
            bool focusAsset = true)
        {
            var effect = GasEditorUtility.CreateEmbeddedSubAsset<EffectDefinitionAsset>(catalog, "Effect");
            if (effect == null)
            {
                return null;
            }

            effect.EnsureModulesInitialized();
            effect.GetOrAddModule<EffectIdentityModule>().EffectId = GasEditorUtility.SuggestId("effect", effect.name);
            effect.GetOrAddModule<EffectResourcePayloadModule>().InstantResourceDeltas = new[]
            {
                new ResourceDeltaAuthoringData("health", new FixedPointValue(-10f)),
            };
            EditorUtility.SetDirty(effect);
            GasEditorUtility.FinalizeCreatedAsset(catalog, effect, false);

            var ability = GasEditorUtility.CreateEmbeddedSubAsset<AbilityDefinitionAsset>(catalog, "Ability");
            if (ability == null)
            {
                return null;
            }

            ability.EnsureModulesInitialized();
            var identity = ability.GetOrAddModule<AbilityIdentityModule>();
            identity.AbilityId = GasEditorUtility.SuggestId("ability", ability.name);
            identity.DisplayName = "Instant Damage";
            ability.GetOrAddModule<AbilityActivationModule>().ActivationMode = AbilityActivationMode.Instant;
            ability.GetOrAddModule<AbilityEffectPayloadModule>().ImmediateEffectsArray = new[] { effect };
            EditorUtility.SetDirty(ability);
            GasEditorUtility.FinalizeCreatedAsset(catalog, ability, focusAsset);
            return ability;
        }

        public static EffectDefinitionAsset CreateBuffEffectTemplate(
            CombatDefinitionCatalogAsset catalog,
            bool focusAsset = true)
        {
            var effect = GasEditorUtility.CreateEmbeddedSubAsset<EffectDefinitionAsset>(catalog, "Effect");
            if (effect == null)
            {
                return null;
            }

            effect.EnsureModulesInitialized();
            effect.GetOrAddModule<EffectIdentityModule>().EffectId = GasEditorUtility.SuggestId("effect", effect.name);
            effect.GetOrAddModule<EffectTimingModule>().DurationPolicy = EffectDurationPolicy.Timed;
            var timing = effect.GetOrAddModule<EffectTimingModule>();
            timing.DurationPolicy = EffectDurationPolicy.Timed;
            timing.StackPolicy = EffectStackPolicy.AddStackAndRefresh;
            timing.DurationTicks = 300;
            timing.MaxStacks = 3;
            effect.GetOrAddModule<EffectTagRulesModule>().GrantedTags = new[] { "state.buff.sample" };
            EditorUtility.SetDirty(effect);
            GasEditorUtility.FinalizeCreatedAsset(catalog, effect, focusAsset);
            return effect;
        }

        public static TriggerDefinitionAsset CreatePassiveTriggerTemplate(
            CombatDefinitionCatalogAsset catalog,
            bool focusAsset = true)
        {
            var trigger = GasEditorUtility.CreateEmbeddedSubAsset<TriggerDefinitionAsset>(catalog, "Trigger");
            if (trigger == null)
            {
                return null;
            }

            trigger.EnsureModulesInitialized();
            var identity = trigger.GetOrAddModule<TriggerIdentityModule>();
            identity.TriggerId = GasEditorUtility.SuggestId("trigger", trigger.name);
            identity.DisplayName = "Passive Trigger";

            var routing = trigger.GetOrAddModule<TriggerRoutingModule>();
            routing.SourceKind = TriggerSourceKind.Actor;
            routing.EventKind = CombatTriggerEventKind.OnTick;
            routing.Timing = CombatTriggerTiming.EndOfStage;
            routing.CollectionMode = TriggerCollectionMode.OwnerOnly;

            trigger.GetOrAddModule<TriggerActionModule>().Action = new TriggerActionAuthoringData
            {
                Kind = TriggerActionKind.AddTag,
                SourceActor = TriggerActorReference.Owner,
                TargetActor = TriggerActorReference.Owner,
                Tag = "state.passive.sample",
            };

            EditorUtility.SetDirty(trigger);
            GasEditorUtility.FinalizeCreatedAsset(catalog, trigger, focusAsset);
            return trigger;
        }

        public static EffectDefinitionAsset CreateBlankEffect(
            CombatDefinitionCatalogAsset catalog,
            bool focusAsset = true)
        {
            var effect = GasEditorUtility.CreateEmbeddedSubAsset<EffectDefinitionAsset>(catalog, "Effect");
            if (effect == null)
            {
                return null;
            }

            effect.EnsureModulesInitialized();
            effect.GetOrAddModule<EffectIdentityModule>().EffectId = GasEditorUtility.SuggestId("effect", effect.name);
            EditorUtility.SetDirty(effect);
            GasEditorUtility.FinalizeCreatedAsset(catalog, effect, focusAsset);
            return effect;
        }

        public static AbilityDefinitionAsset CreateBlankAbility(
            CombatDefinitionCatalogAsset catalog,
            bool focusAsset = true)
        {
            var ability = GasEditorUtility.CreateEmbeddedSubAsset<AbilityDefinitionAsset>(catalog, "Ability");
            if (ability == null)
            {
                return null;
            }

            ability.EnsureModulesInitialized();
            ability.GetOrAddModule<AbilityIdentityModule>().AbilityId = GasEditorUtility.SuggestId("ability", ability.name);
            EditorUtility.SetDirty(ability);
            GasEditorUtility.FinalizeCreatedAsset(catalog, ability, focusAsset);
            return ability;
        }

        public static TriggerDefinitionAsset CreateBlankTrigger(
            CombatDefinitionCatalogAsset catalog,
            bool focusAsset = true)
        {
            var trigger = GasEditorUtility.CreateEmbeddedSubAsset<TriggerDefinitionAsset>(catalog, "Trigger");
            if (trigger == null)
            {
                return null;
            }

            trigger.EnsureModulesInitialized();
            trigger.GetOrAddModule<TriggerIdentityModule>().TriggerId = GasEditorUtility.SuggestId("trigger", trigger.name);
            EditorUtility.SetDirty(trigger);
            GasEditorUtility.FinalizeCreatedAsset(catalog, trigger, focusAsset);
            return trigger;
        }

        public static CombatActorTemplateAsset CreateBlankActorTemplate(
            CombatDefinitionCatalogAsset catalog,
            bool focusAsset = true)
        {
            var actorTemplate = GasEditorUtility.CreateEmbeddedSubAsset<CombatActorTemplateAsset>(catalog, "ActorTemplate");
            if (actorTemplate == null)
            {
                return null;
            }

            var actorSo = new SerializedObject(actorTemplate);
            GasEditorUtility.SetString(actorSo, "_defaultActorId", GasEditorUtility.SuggestId("actor", actorTemplate.name));
            actorSo.ApplyModifiedPropertiesWithoutUndo();
            GasEditorUtility.FinalizeCreatedAsset(catalog, actorTemplate, focusAsset);
            return actorTemplate;
        }
    }
}
