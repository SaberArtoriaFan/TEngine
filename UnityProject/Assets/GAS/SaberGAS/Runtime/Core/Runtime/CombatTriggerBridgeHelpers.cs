using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Resources;
using Saber.GAS.Triggers;

namespace Saber.GAS.Runtime
{
    internal static class CombatRuntimeActorLocator
    {
        public static bool TryGetActor(CombatWorldState worldState, ActorId actorId, out CombatActorState actor)
        {
            actor = null;
            return worldState != null &&
                   !actorId.IsEmpty &&
                   worldState.TryGetActor(actorId, out actor);
        }
    }

    internal static class CombatTriggerContextFactory
    {
        public static CombatTriggerContext Create(
            SimulationTick currentTick,
            CombatTriggerEventKind eventKind,
            CombatTriggerTiming timing,
            ActorId ownerActorId,
            ActorId instigatorActorId,
            ActorId targetActorId,
            AbilityDefinition relatedAbility,
            EffectDefinition relatedEffect,
            CombatActionAttempt relatedAttempt,
            CombatImpact relatedImpact,
            CombatImpactOperation relatedOperation,
            ActiveEffect sourceActiveEffect,
            ActiveAbilityInstance sourceAbilityInstance,
            ResourceId resourceId,
            FP previousResourceValue,
            FP currentResourceValue,
            object customPayload)
        {
            return new CombatTriggerContext
            {
                CurrentTick = currentTick,
                EventKind = eventKind,
                Timing = timing,
                OwnerActorId = ownerActorId,
                InstigatorActorId = instigatorActorId,
                TargetActorId = targetActorId,
                RelatedAbility = relatedAbility,
                RelatedEffect = relatedEffect,
                RelatedAttempt = relatedAttempt,
                RelatedImpact = relatedImpact,
                RelatedOperation = relatedOperation,
                SourceActiveEffect = sourceActiveEffect,
                SourceAbilityInstance = sourceAbilityInstance,
                ResourceId = resourceId,
                PreviousResourceValue = previousResourceValue,
                CurrentResourceValue = currentResourceValue,
                CustomPayload = customPayload,
            };
        }
    }
}
