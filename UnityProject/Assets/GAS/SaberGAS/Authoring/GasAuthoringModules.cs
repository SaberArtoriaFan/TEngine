using System;
using System.Collections.Generic;
using Saber.GAS.Abilities;
using Saber.GAS.Effects;
using Saber.GAS.Triggers;

namespace Saber.GAS.Authoring
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GasAuthoringModuleAttribute : Attribute
    {
        public GasAuthoringModuleAttribute(string title, int order = 0)
        {
            Title = title;
            Order = order;
        }

        public string Title { get; }

        public int Order { get; }
    }

    public interface ICombatLocalTagModule
    {
        IReadOnlyList<CombatTagDefinitionAuthoringData> LocalTagDefinitions { get; }
    }

    [Serializable]
    public abstract class CombatAuthoringModule
    {
    }

    [Serializable]
    public abstract class AbilityAuthoringModule : CombatAuthoringModule
    {
        public abstract void ApplyTo(
            AbilityDefinition definition,
            AbilityDefinitionAsset owner,
            CombatAuthoringBuildContext context);
    }

    [Serializable]
    public abstract class EffectAuthoringModule : CombatAuthoringModule
    {
        public abstract void ApplyTo(
            EffectDefinition definition,
            EffectDefinitionAsset owner,
            CombatAuthoringBuildContext context);
    }

    [Serializable]
    public abstract class TriggerAuthoringModule : CombatAuthoringModule
    {
        public abstract void ApplyTo(
            TriggerDefinition definition,
            TriggerDefinitionAsset owner,
            CombatAuthoringBuildContext context);
    }

    internal static class GasAuthoringModuleUtility
    {
        public static IReadOnlyList<CombatTagDefinitionAuthoringData> CollectLocalTagDefinitions<TModule>(
            List<TModule> modules)
            where TModule : CombatAuthoringModule
        {
            if (modules == null || modules.Count == 0)
            {
                return Array.Empty<CombatTagDefinitionAuthoringData>();
            }

            var results = new List<CombatTagDefinitionAuthoringData>();
            for (var i = 0; i < modules.Count; i++)
            {
                if (modules[i] is not ICombatLocalTagModule localTagModule ||
                    localTagModule.LocalTagDefinitions == null)
                {
                    continue;
                }

                for (var j = 0; j < localTagModule.LocalTagDefinitions.Count; j++)
                {
                    var definition = localTagModule.LocalTagDefinitions[j];
                    if (definition != null)
                    {
                        results.Add(definition);
                    }
                }
            }

            return results;
        }
    }
}
