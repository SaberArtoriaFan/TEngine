using System;
using System.Collections.Generic;
using Saber.GAS.Authoring;
using Saber.GAS.Triggers;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Editor
{
    internal enum GasGraphRelationKind
    {
        None = 0,
        AbilityEffects = 1,
        AbilityPeriodicEffects = 2,
        AbilityEndEffects = 3,
        AbilityTriggers = 4,
        EffectRemovedTargetEffects = 5,
        EffectTriggers = 6,
        ActorGrantedAbilities = 7,
        ActorTriggers = 8,
        TriggerApplyEffect = 9,
        TriggerActivateAbility = 10,
        TriggerCancelAbility = 11,
    }

    internal sealed class GasGraphPortData
    {
        public GasGraphPortData(UnityEngine.Object owner, GasGraphRelationKind relationKind, Type targetType, bool isInput)
        {
            Owner = owner;
            RelationKind = relationKind;
            TargetType = targetType;
            IsInput = isInput;
        }

        public UnityEngine.Object Owner { get; }

        public GasGraphRelationKind RelationKind { get; }

        public Type TargetType { get; }

        public bool IsInput { get; }
    }

    internal readonly struct GasGraphRelation
    {
        public GasGraphRelation(UnityEngine.Object source, GasGraphRelationKind kind, UnityEngine.Object target)
        {
            Source = source;
            Kind = kind;
            Target = target;
        }

        public UnityEngine.Object Source { get; }

        public GasGraphRelationKind Kind { get; }

        public UnityEngine.Object Target { get; }
    }

    internal static class GasGraphAdapter
    {
        public static List<UnityEngine.Object> CollectGraphAssets(CombatDefinitionCatalogAsset catalog)
        {
            var results = new List<UnityEngine.Object>();
            if (catalog == null)
            {
                return results;
            }

            results.AddRange(GasEditorUtility.GetEmbeddedAssets<AbilityDefinitionAsset>(catalog));
            results.AddRange(GasEditorUtility.GetEmbeddedAssets<EffectDefinitionAsset>(catalog));
            results.AddRange(GasEditorUtility.GetEmbeddedAssets<TriggerDefinitionAsset>(catalog));
            results.AddRange(GasEditorUtility.GetEmbeddedAssets<CombatActorTemplateAsset>(catalog));
            return results;
        }

        public static List<GasGraphRelation> BuildRelations(CombatDefinitionCatalogAsset catalog)
        {
            var results = new List<GasGraphRelation>();
            if (catalog == null)
            {
                return results;
            }

            var abilities = GasEditorUtility.GetEmbeddedAssets<AbilityDefinitionAsset>(catalog);
            for (var i = 0; i < abilities.Length; i++)
            {
                abilities[i].EnsureModulesInitialized();
                var effectModule = abilities[i].GetModule<AbilityEffectPayloadModule>();
                if (effectModule != null)
                {
                    AddRelations(results, abilities[i], GasGraphRelationKind.AbilityEffects, effectModule.Effects);
                    AddRelations(results, abilities[i], GasGraphRelationKind.AbilityPeriodicEffects, effectModule.PeriodicEffects);
                    AddRelations(results, abilities[i], GasGraphRelationKind.AbilityEndEffects, effectModule.EndEffects);
                }

                var triggerModule = abilities[i].GetModule<AbilityTriggerModule>();
                if (triggerModule != null)
                {
                    AddRelations(results, abilities[i], GasGraphRelationKind.AbilityTriggers, triggerModule.Triggers);
                }
            }

            var effects = GasEditorUtility.GetEmbeddedAssets<EffectDefinitionAsset>(catalog);
            for (var i = 0; i < effects.Length; i++)
            {
                effects[i].EnsureModulesInitialized();
                var removalModule = effects[i].GetModule<EffectRemovalModule>();
                if (removalModule != null)
                {
                    AddRelations(results, effects[i], GasGraphRelationKind.EffectRemovedTargetEffects, removalModule.RemovedTargetEffects);
                }

                var triggerModule = effects[i].GetModule<EffectTriggerModule>();
                if (triggerModule != null)
                {
                    AddRelations(results, effects[i], GasGraphRelationKind.EffectTriggers, triggerModule.Triggers);
                }
            }

            var actorTemplates = GasEditorUtility.GetEmbeddedAssets<CombatActorTemplateAsset>(catalog);
            for (var i = 0; i < actorTemplates.Length; i++)
            {
                var serializedObject = new SerializedObject(actorTemplates[i]);
                AddArrayRelations(results, actorTemplates[i], GasGraphRelationKind.ActorGrantedAbilities, serializedObject.FindProperty("_grantedAbilities"));
                AddArrayRelations(results, actorTemplates[i], GasGraphRelationKind.ActorTriggers, serializedObject.FindProperty("_actorTriggers"));
            }

            var triggers = GasEditorUtility.GetEmbeddedAssets<TriggerDefinitionAsset>(catalog);
            for (var i = 0; i < triggers.Length; i++)
            {
                triggers[i].EnsureModulesInitialized();
                var actionModule = triggers[i].GetModule<TriggerActionModule>();
                if (actionModule == null)
                {
                    continue;
                }

                AddTriggerActionGraphRelation(results, triggers[i], actionModule);
            }

            return results;
        }

        public static bool AddRelation(UnityEngine.Object source, GasGraphRelationKind kind, UnityEngine.Object target)
        {
            if (source == null || target == null)
            {
                return false;
            }

            var changed = kind switch
            {
                GasGraphRelationKind.AbilityEffects => AddAbilityEffect(source as AbilityDefinitionAsset, target as EffectDefinitionAsset, AbilityEffectModuleSlot.Execute),
                GasGraphRelationKind.AbilityPeriodicEffects => AddAbilityEffect(source as AbilityDefinitionAsset, target as EffectDefinitionAsset, AbilityEffectModuleSlot.Periodic),
                GasGraphRelationKind.AbilityEndEffects => AddAbilityEffect(source as AbilityDefinitionAsset, target as EffectDefinitionAsset, AbilityEffectModuleSlot.End),
                GasGraphRelationKind.AbilityTriggers => AddAbilityTrigger(source as AbilityDefinitionAsset, target as TriggerDefinitionAsset),
                GasGraphRelationKind.EffectRemovedTargetEffects => AddEffectRemoval(source as EffectDefinitionAsset, target as EffectDefinitionAsset),
                GasGraphRelationKind.EffectTriggers => AddEffectTrigger(source as EffectDefinitionAsset, target as TriggerDefinitionAsset),
                GasGraphRelationKind.ActorGrantedAbilities => AddActorArrayRelation(source, "_grantedAbilities", target),
                GasGraphRelationKind.ActorTriggers => AddActorArrayRelation(source, "_actorTriggers", target),
                GasGraphRelationKind.TriggerApplyEffect => SetTriggerReference(source as TriggerDefinitionAsset, target as EffectDefinitionAsset, TriggerActionKind.ApplyEffect),
                GasGraphRelationKind.TriggerActivateAbility => SetTriggerReference(source as TriggerDefinitionAsset, target as AbilityDefinitionAsset, TriggerActionKind.ActivateAbility),
                GasGraphRelationKind.TriggerCancelAbility => SetTriggerReference(source as TriggerDefinitionAsset, target as AbilityDefinitionAsset, TriggerActionKind.CancelAbility),
                _ => false,
            };

            if (changed)
            {
                SaveSource(source);
            }

            return changed;
        }

        public static bool RemoveRelation(UnityEngine.Object source, GasGraphRelationKind kind, UnityEngine.Object target)
        {
            if (source == null || target == null)
            {
                return false;
            }

            var changed = kind switch
            {
                GasGraphRelationKind.AbilityEffects => RemoveAbilityEffect(source as AbilityDefinitionAsset, target as EffectDefinitionAsset, AbilityEffectModuleSlot.Execute),
                GasGraphRelationKind.AbilityPeriodicEffects => RemoveAbilityEffect(source as AbilityDefinitionAsset, target as EffectDefinitionAsset, AbilityEffectModuleSlot.Periodic),
                GasGraphRelationKind.AbilityEndEffects => RemoveAbilityEffect(source as AbilityDefinitionAsset, target as EffectDefinitionAsset, AbilityEffectModuleSlot.End),
                GasGraphRelationKind.AbilityTriggers => RemoveAbilityTrigger(source as AbilityDefinitionAsset, target as TriggerDefinitionAsset),
                GasGraphRelationKind.EffectRemovedTargetEffects => RemoveEffectRemoval(source as EffectDefinitionAsset, target as EffectDefinitionAsset),
                GasGraphRelationKind.EffectTriggers => RemoveEffectTrigger(source as EffectDefinitionAsset, target as TriggerDefinitionAsset),
                GasGraphRelationKind.ActorGrantedAbilities => RemoveActorArrayRelation(source, "_grantedAbilities", target),
                GasGraphRelationKind.ActorTriggers => RemoveActorArrayRelation(source, "_actorTriggers", target),
                GasGraphRelationKind.TriggerApplyEffect => ClearTriggerReference(source as TriggerDefinitionAsset, target as EffectDefinitionAsset, TriggerActionKind.ApplyEffect),
                GasGraphRelationKind.TriggerActivateAbility => ClearTriggerReference(source as TriggerDefinitionAsset, target as AbilityDefinitionAsset, TriggerActionKind.ActivateAbility),
                GasGraphRelationKind.TriggerCancelAbility => ClearTriggerReference(source as TriggerDefinitionAsset, target as AbilityDefinitionAsset, TriggerActionKind.CancelAbility),
                _ => false,
            };

            if (changed)
            {
                SaveSource(source);
            }

            return changed;
        }

        private static bool AddAbilityEffect(
            AbilityDefinitionAsset ability,
            EffectDefinitionAsset effect,
            AbilityEffectModuleSlot slot)
        {
            if (ability == null || effect == null)
            {
                return false;
            }

            ability.EnsureModulesInitialized();
            return ability.GetOrAddModule<AbilityEffectPayloadModule>().AddEffect(slot, effect);
        }

        private static bool RemoveAbilityEffect(
            AbilityDefinitionAsset ability,
            EffectDefinitionAsset effect,
            AbilityEffectModuleSlot slot)
        {
            if (ability == null || effect == null)
            {
                return false;
            }

            ability.EnsureModulesInitialized();
            var module = ability.GetModule<AbilityEffectPayloadModule>();
            return module != null && module.RemoveEffect(slot, effect);
        }

        private static bool AddAbilityTrigger(AbilityDefinitionAsset ability, TriggerDefinitionAsset trigger)
        {
            if (ability == null || trigger == null)
            {
                return false;
            }

            ability.EnsureModulesInitialized();
            return ability.GetOrAddModule<AbilityTriggerModule>().AddTrigger(trigger);
        }

        private static bool RemoveAbilityTrigger(AbilityDefinitionAsset ability, TriggerDefinitionAsset trigger)
        {
            if (ability == null || trigger == null)
            {
                return false;
            }

            ability.EnsureModulesInitialized();
            var module = ability.GetModule<AbilityTriggerModule>();
            return module != null && module.RemoveTrigger(trigger);
        }

        private static bool AddEffectRemoval(EffectDefinitionAsset effect, EffectDefinitionAsset removedEffect)
        {
            if (effect == null || removedEffect == null)
            {
                return false;
            }

            effect.EnsureModulesInitialized();
            return effect.GetOrAddModule<EffectRemovalModule>().AddRemovedEffect(removedEffect);
        }

        private static bool RemoveEffectRemoval(EffectDefinitionAsset effect, EffectDefinitionAsset removedEffect)
        {
            if (effect == null || removedEffect == null)
            {
                return false;
            }

            effect.EnsureModulesInitialized();
            var module = effect.GetModule<EffectRemovalModule>();
            return module != null && module.RemoveRemovedEffect(removedEffect);
        }

        private static bool AddEffectTrigger(EffectDefinitionAsset effect, TriggerDefinitionAsset trigger)
        {
            if (effect == null || trigger == null)
            {
                return false;
            }

            effect.EnsureModulesInitialized();
            return effect.GetOrAddModule<EffectTriggerModule>().AddTrigger(trigger);
        }

        private static bool RemoveEffectTrigger(EffectDefinitionAsset effect, TriggerDefinitionAsset trigger)
        {
            if (effect == null || trigger == null)
            {
                return false;
            }

            effect.EnsureModulesInitialized();
            var module = effect.GetModule<EffectTriggerModule>();
            return module != null && module.RemoveTrigger(trigger);
        }

        private static bool SetTriggerReference(
            TriggerDefinitionAsset trigger,
            EffectDefinitionAsset effect,
            TriggerActionKind kind)
        {
            if (trigger == null || effect == null)
            {
                return false;
            }

            trigger.EnsureModulesInitialized();
            var module = trigger.GetOrAddModule<TriggerActionModule>();
            var changed = module.Kind != kind || module.EffectAsset != effect;
            module.Kind = kind;
            module.EffectAsset = effect;
            return changed;
        }

        private static bool SetTriggerReference(
            TriggerDefinitionAsset trigger,
            AbilityDefinitionAsset ability,
            TriggerActionKind kind)
        {
            if (trigger == null || ability == null)
            {
                return false;
            }

            trigger.EnsureModulesInitialized();
            var module = trigger.GetOrAddModule<TriggerActionModule>();
            var changed = module.Kind != kind;
            module.Kind = kind;
            switch (kind)
            {
                case TriggerActionKind.ActivateAbility:
                    changed |= module.TriggeredAbility != ability;
                    module.TriggeredAbility = ability;
                    break;
                case TriggerActionKind.CancelAbility:
                    changed |= module.AbilityToCancel != ability;
                    module.AbilityToCancel = ability;
                    break;
                default:
                    return false;
            }

            return changed;
        }

        private static bool ClearTriggerReference(
            TriggerDefinitionAsset trigger,
            EffectDefinitionAsset effect,
            TriggerActionKind kind)
        {
            if (trigger == null || effect == null)
            {
                return false;
            }

            trigger.EnsureModulesInitialized();
            var module = trigger.GetModule<TriggerActionModule>();
            if (module == null || module.Kind != kind || module.EffectAsset != effect)
            {
                return false;
            }

            module.EffectAsset = null;
            module.Kind = TriggerActionKind.None;
            return true;
        }

        private static bool ClearTriggerReference(
            TriggerDefinitionAsset trigger,
            AbilityDefinitionAsset ability,
            TriggerActionKind kind)
        {
            if (trigger == null || ability == null)
            {
                return false;
            }

            trigger.EnsureModulesInitialized();
            var module = trigger.GetModule<TriggerActionModule>();
            if (module == null || module.Kind != kind)
            {
                return false;
            }

            var matches = kind switch
            {
                TriggerActionKind.ActivateAbility => module.TriggeredAbility == ability,
                TriggerActionKind.CancelAbility => module.AbilityToCancel == ability,
                _ => false,
            };

            if (!matches)
            {
                return false;
            }

            if (kind == TriggerActionKind.ActivateAbility)
            {
                module.TriggeredAbility = null;
            }
            else if (kind == TriggerActionKind.CancelAbility)
            {
                module.AbilityToCancel = null;
            }

            module.Kind = TriggerActionKind.None;
            return true;
        }

        private static bool AddActorArrayRelation(UnityEngine.Object source, string propertyName, UnityEngine.Object target)
        {
            var serializedObject = new SerializedObject(source);
            var changed = AddUniqueObjectReference(serializedObject.FindProperty(propertyName), target);
            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            return changed;
        }

        private static bool RemoveActorArrayRelation(UnityEngine.Object source, string propertyName, UnityEngine.Object target)
        {
            var serializedObject = new SerializedObject(source);
            var changed = RemoveObjectReference(serializedObject.FindProperty(propertyName), target);
            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            return changed;
        }

        private static void AddRelations<TTarget>(
            List<GasGraphRelation> results,
            UnityEngine.Object source,
            GasGraphRelationKind kind,
            IReadOnlyList<TTarget> targets)
            where TTarget : UnityEngine.Object
        {
            if (targets == null)
            {
                return;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null)
                {
                    results.Add(new GasGraphRelation(source, kind, targets[i]));
                }
            }
        }

        private static void AddArrayRelations(List<GasGraphRelation> results, UnityEngine.Object source, GasGraphRelationKind kind, SerializedProperty arrayProperty)
        {
            if (arrayProperty == null || !arrayProperty.isArray)
            {
                return;
            }

            for (var i = 0; i < arrayProperty.arraySize; i++)
            {
                var target = arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue;
                if (target != null)
                {
                    results.Add(new GasGraphRelation(source, kind, target));
                }
            }
        }

        private static void AddSingleRelation(List<GasGraphRelation> results, UnityEngine.Object source, GasGraphRelationKind kind, UnityEngine.Object target)
        {
            if (target != null)
            {
                results.Add(new GasGraphRelation(source, kind, target));
            }
        }

        private static void AddTriggerActionGraphRelation(
            ICollection<GasGraphRelation> results,
            TriggerDefinitionAsset triggerAsset,
            TriggerActionModule actionModule)
        {
            if (results == null || triggerAsset == null || actionModule == null)
            {
                return;
            }

            if (!CombatTriggerActionDescriptorRegistryHub.TryGetDescriptor(actionModule.Kind, out var descriptor))
            {
                return;
            }

            if (!TryMapGraphTargetToRelationKind(descriptor.GraphTargetKind, out var relationKind))
            {
                return;
            }

            switch (descriptor.GraphTargetKind)
            {
                case TriggerActionGraphTargetKind.TriggeredAbility:
                    if (actionModule.TriggeredAbility != null)
                    {
                        results.Add(new GasGraphRelation(triggerAsset, relationKind, actionModule.TriggeredAbility));
                    }
                    break;
                case TriggerActionGraphTargetKind.EffectAsset:
                    if (actionModule.EffectAsset != null)
                    {
                        results.Add(new GasGraphRelation(triggerAsset, relationKind, actionModule.EffectAsset));
                    }
                    break;
                case TriggerActionGraphTargetKind.AbilityToCancel:
                    if (actionModule.AbilityToCancel != null)
                    {
                        results.Add(new GasGraphRelation(triggerAsset, relationKind, actionModule.AbilityToCancel));
                    }
                    break;
            }
        }

        private static bool TryMapGraphTargetToRelationKind(
            TriggerActionGraphTargetKind graphTargetKind,
            out GasGraphRelationKind relationKind)
        {
            switch (graphTargetKind)
            {
                case TriggerActionGraphTargetKind.TriggeredAbility:
                    relationKind = GasGraphRelationKind.TriggerActivateAbility;
                    return true;
                case TriggerActionGraphTargetKind.EffectAsset:
                    relationKind = GasGraphRelationKind.TriggerApplyEffect;
                    return true;
                case TriggerActionGraphTargetKind.AbilityToCancel:
                    relationKind = GasGraphRelationKind.TriggerCancelAbility;
                    return true;
                default:
                    relationKind = GasGraphRelationKind.None;
                    return false;
            }
        }

        private static bool AddUniqueObjectReference(SerializedProperty arrayProperty, UnityEngine.Object target)
        {
            if (arrayProperty == null || !arrayProperty.isArray)
            {
                return false;
            }

            for (var i = 0; i < arrayProperty.arraySize; i++)
            {
                if (arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue == target)
                {
                    return false;
                }
            }

            var nextIndex = arrayProperty.arraySize;
            arrayProperty.InsertArrayElementAtIndex(nextIndex);
            arrayProperty.GetArrayElementAtIndex(nextIndex).objectReferenceValue = target;
            return true;
        }

        private static bool RemoveObjectReference(SerializedProperty arrayProperty, UnityEngine.Object target)
        {
            if (arrayProperty == null || !arrayProperty.isArray)
            {
                return false;
            }

            for (var i = arrayProperty.arraySize - 1; i >= 0; i--)
            {
                var element = arrayProperty.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != target)
                {
                    continue;
                }

                arrayProperty.DeleteArrayElementAtIndex(i);
                return true;
            }

            return false;
        }

        private static void SaveSource(UnityEngine.Object source)
        {
            if (source == null)
            {
                return;
            }

            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssets();
        }
    }
}
