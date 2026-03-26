using System;
using Saber.GAS.Actors;
using Saber.GAS.Runtime;

namespace Saber.GAS.Authoring
{
    /// <summary>
    /// 把配置资产装配到 CombatRuntime / CombatWorldState 的辅助入口。
    /// </summary>
    public static class CombatAuthoringInstaller
    {
        public static CombatSystemInstance Initialize(CombatDefinitionCatalogAsset catalog, CombatRuntimeOptions runtimeOptions = null)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            return catalog.Initialize(runtimeOptions);
        }

        public static void RegisterCatalog(CombatWorldState worldState, CombatDefinitionCatalogAsset catalog, CombatAuthoringBuildContext context = null)
        {
            if (worldState == null)
            {
                throw new ArgumentNullException(nameof(worldState));
            }

            if (catalog == null)
            {
                return;
            }

            catalog.RegisterInto(worldState, context);
        }

        public static CombatActorState CreateActor(CombatRuntime runtime, CombatActorTemplateAsset template, CombatAuthoringBuildContext context = null)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            var actor = runtime.AddActor(template.GetDefaultActorId());
            ApplyTemplate(runtime, actor, template, context);
            return actor;
        }

        public static void ApplyTemplate(CombatRuntime runtime, CombatActorState actor, CombatActorTemplateAsset template, CombatAuthoringBuildContext context = null)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            var buildContext = context ?? new CombatAuthoringBuildContext();
            template.WarmupDefinitions(buildContext);

            foreach (var ability in buildContext.BuiltAbilities)
            {
                runtime.WorldState.AddAbility(ability);
            }

            template.ApplyTo(actor, buildContext);

            var triggerDefinitions = template.BuildActorTriggers(buildContext);
            for (var i = 0; i < triggerDefinitions.Count; i++)
            {
                runtime.AddActorTrigger(actor.ActorId, triggerDefinitions[i]);
            }
        }
    }
}
