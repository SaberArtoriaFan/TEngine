using Saber.GAS.Actors;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;

namespace Saber.GAS.Examples.QuickStart
{
    /// <summary>
    /// Exposes runtime actor state so actor-side Mono behaviours can render debug information.
    /// </summary>
    public interface IGasActorRuntimeStateProvider
    {
        CombatWorldState WorldState { get; }

        bool TryGetActorState(ActorId actorId, out CombatActorState actor);
    }
}
