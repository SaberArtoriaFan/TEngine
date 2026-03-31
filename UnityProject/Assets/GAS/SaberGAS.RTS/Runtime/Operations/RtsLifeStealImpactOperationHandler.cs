using System;
using System.Collections.Generic;
using Herta;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;

namespace Saber.GAS.RTS.Operations
{
    /// <summary>
    /// Payload for the lifesteal operation extension.
    /// </summary>
    public sealed class RtsLifeStealImpactOperationPayload
    {
        public ResourceId ResourceId { get; set; } = ResourceId.Empty;

        public FP Ratio { get; set; } = FP._0;
    }

    /// <summary>
    /// Example impact operation handler that converts dealt damage into source healing.
    /// </summary>
    public sealed class RtsLifeStealImpactOperationHandler : IImpactOperationHandler
    {
        public const string CueName = "rts.operation.lifesteal";

        public bool CanHandle(CombatImpactOperation operation)
        {
            if (operation == null || operation.Type != CombatImpactOperationType.Cue)
            {
                return false;
            }

            if (!string.Equals(operation.CueName, CueName, StringComparison.Ordinal))
            {
                return false;
            }

            return operation.Payload is RtsLifeStealImpactOperationPayload;
        }

        public bool Execute(CombatImpactOperationExecutionContext context)
        {
            var runtime = context.Runtime;
            var operation = context.Operation;
            var impact = context.Impact;
            if (runtime == null || impact == null || operation == null)
            {
                return false;
            }

            var payload = operation.Payload as RtsLifeStealImpactOperationPayload;
            if (payload == null)
            {
                return false;
            }

            if (payload.ResourceId.IsEmpty || payload.Ratio <= FP._0 || impact.SourceActorId.IsEmpty)
            {
                return true;
            }

            FP totalDamage = FP._0;
            for (var index = 0; index < impact.Operations.Count; index++)
            {
                var candidate = impact.Operations[index];
                if (candidate == null ||
                    candidate.Type != CombatImpactOperationType.ResourceDelta ||
                    candidate.ResourceId != payload.ResourceId ||
                    candidate.Amount >= FP._0)
                {
                    continue;
                }

                totalDamage -= candidate.Amount;
            }

            if (totalDamage <= FP._0)
            {
                return true;
            }

            var healAmount = totalDamage * payload.Ratio;
            if (healAmount <= FP._0)
            {
                return true;
            }

            if (context.Attempt == null)
            {
                return true;
            }

            var healImpact = new CombatImpact(
                impact.SourceActorId,
                impact.SourceActorId,
                impact.AbilityId,
                runtime.WorldState.CurrentTick,
                impact.Stage)
            {
                EffectId = impact.EffectId,
            };
            healImpact.Operations.Add(new CombatImpactOperation
            {
                Type = CombatImpactOperationType.ResourceDelta,
                ResourceId = payload.ResourceId,
                Amount = healAmount,
            });

            context.Attempt.Impacts.Add(healImpact);
            return true;
        }
    }

    /// <summary>
    /// Registry wrapper for extension handler onboarding.
    /// </summary>
    public sealed class RtsLifeStealImpactOperationHandlerRegistry : IImpactOperationHandlerRegistry
    {
        private static readonly IImpactOperationHandler[] HandlerList =
        {
            new RtsLifeStealImpactOperationHandler(),
        };

        public IReadOnlyList<IImpactOperationHandler> Handlers => HandlerList;
    }
}

