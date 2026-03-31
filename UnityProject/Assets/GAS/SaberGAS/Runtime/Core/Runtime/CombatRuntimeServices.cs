using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Resources;
using Saber.GAS.Triggers;

namespace Saber.GAS.Runtime
{
    /// <summary>
    /// 能力激活服务：负责 Ability 请求入口。
    /// </summary>
    public interface ICombatActivationService
    {
        AbilityActivationResult TryActivate(CombatRuntime runtime, AbilityActivationRequest request);
    }

    /// <summary>
    /// Impact 结算服务：负责 Impact mutate/resolve 链路。
    /// </summary>
    public interface ICombatImpactService
    {
        void ResolveAttemptImpacts(CombatRuntime runtime, CombatActionAttempt attempt, AbilityExecutionRecord record);
    }

    /// <summary>
    /// Effect 生命周期服务：负责每 Tick 的 Effect 推进。
    /// </summary>
    public interface ICombatEffectLifecycleService
    {
        void ProcessEffects(CombatRuntime runtime, CombatActorState actor);
    }

    /// <summary>
    /// TriggerBridge 服务：负责 Trigger 上下文桥接与抛出。
    /// </summary>
    public interface ICombatTriggerBridgeService
    {
        void RaiseTriggerEvent(CombatRuntime runtime, CombatTriggerContext context);

        void ProcessOwnedTriggerEvent(
            CombatRuntime runtime,
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
            ResourceId resourceId = default(ResourceId),
            FP previousResourceValue = default(FP),
            FP currentResourceValue = default(FP),
            object customPayload = null);
    }

    public sealed class DefaultCombatActivationService : ICombatActivationService
    {
        public AbilityActivationResult TryActivate(CombatRuntime runtime, AbilityActivationRequest request)
        {
            return runtime == null
                ? AbilityActivationResult.Fail("CombatRuntime is null.")
                : runtime.TryActivateCore(request);
        }
    }

    public sealed class DefaultCombatImpactService : ICombatImpactService
    {
        public void ResolveAttemptImpacts(CombatRuntime runtime, CombatActionAttempt attempt, AbilityExecutionRecord record)
        {
            runtime?.ResolveAttemptImpactsCore(attempt, record);
        }
    }

    public sealed class DefaultCombatEffectLifecycleService : ICombatEffectLifecycleService
    {
        public void ProcessEffects(CombatRuntime runtime, CombatActorState actor)
        {
            runtime?.ProcessEffectsCore(actor);
        }
    }

    public sealed class DefaultCombatTriggerBridgeService : ICombatTriggerBridgeService
    {
        public void RaiseTriggerEvent(CombatRuntime runtime, CombatTriggerContext context)
        {
            runtime?.RaiseTriggerEventCore(context);
        }

        public void ProcessOwnedTriggerEvent(
            CombatRuntime runtime,
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
            ResourceId resourceId = default(ResourceId),
            FP previousResourceValue = default(FP),
            FP currentResourceValue = default(FP),
            object customPayload = null)
        {
            runtime?.ProcessOwnedTriggerEventCore(
                eventKind,
                timing,
                ownerActorId,
                instigatorActorId,
                targetActorId,
                relatedAbility,
                relatedEffect,
                relatedAttempt,
                relatedImpact,
                relatedOperation,
                sourceActiveEffect,
                sourceAbilityInstance,
                resourceId,
                previousResourceValue,
                currentResourceValue,
                customPayload);
        }
    }
}
