using System;
using System.Collections.Generic;
using Saber.GAS.Actors;
using Saber.GAS.Authoring;
using UnityEngine;

namespace Saber.GAS.Editor
{
    internal sealed class GasPreviewReport
    {
        public bool Success { get; set; }

        public string ErrorMessage { get; set; }

        public UnityEngine.Object ErrorAsset { get; set; }

        public List<string> Abilities { get; } = new List<string>();

        public List<string> Effects { get; } = new List<string>();

        public List<string> Triggers { get; } = new List<string>();

        public List<string> InitialActors { get; } = new List<string>();
    }

    internal static class GasPreviewService
    {
        public static GasPreviewReport Build(CombatDefinitionCatalogAsset catalog)
        {
            var report = new GasPreviewReport();
            if (catalog == null)
            {
                report.Success = false;
                report.ErrorMessage = "未指定 CombatDefinitionCatalogAsset。";
                return report;
            }

            CombatSystemInstance instance = null;
            try
            {
                instance = catalog.Initialize();
                foreach (var ability in instance.BuildContext.BuiltAbilities)
                {
                    report.Abilities.Add(string.Format("{0} ({1})", ability.Name, ability.Id));
                }

                foreach (var effect in instance.BuildContext.BuiltEffects)
                {
                    report.Effects.Add(effect.Id.ToString());
                }

                foreach (var trigger in instance.BuildContext.BuiltTriggers)
                {
                    report.Triggers.Add(string.Format("{0} ({1})", trigger.Name, trigger.Id));
                }

                foreach (var actor in instance.WorldState.Actors)
                {
                    report.InitialActors.Add(BuildActorSummary(actor));
                }

                report.Success = true;
                return report;
            }
            catch (Exception exception)
            {
                report.Success = false;
                report.ErrorMessage = exception.Message;
                report.ErrorAsset = TryLocateAsset(catalog, exception.Message);
                return report;
            }
            finally
            {
                instance?.Dispose();
            }
        }

        private static string BuildActorSummary(CombatActorState actor)
        {
            return string.Format(
                "{0} | Team={1} | Pos={2} | GrantedAbilities={3} | Triggers={4}",
                actor.ActorId,
                actor.TeamId,
                actor.Position,
                actor.GrantedAbilities.Count,
                actor.ActiveTriggers.Count);
        }

        private static UnityEngine.Object TryLocateAsset(CombatDefinitionCatalogAsset catalog, string message)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            var assets = GasGraphAdapter.CollectGraphAssets(catalog);
            for (var i = 0; i < assets.Count; i++)
            {
                if (message.IndexOf(assets[i].name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return assets[i];
                }
            }

            return catalog;
        }
    }
}
