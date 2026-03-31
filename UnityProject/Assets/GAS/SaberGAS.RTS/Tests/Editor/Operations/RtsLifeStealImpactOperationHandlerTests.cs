using Herta;
using NUnit.Framework;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Foundation;
using Saber.GAS.Resources;
using Saber.GAS.Runtime;
using Saber.GAS.RTS.Bootstrap;
using Saber.GAS.RTS.Examples;

namespace Saber.GAS.RTS.Tests.Operations
{
    public sealed class RtsLifeStealImpactOperationHandlerTests
    {
        private static readonly ResourceId HealthResourceId = new ResourceId("resource.hp");

        [Test]
        public void LifeStealOperation_ShouldHealSource_WhenHandlerInjected()
        {
            var world = new CombatWorldState();
            var attacker = AddActor(world, "attacker", "red", 0, 0, 0, 80);
            var defender = AddActor(world, "defender", "blue", 2, 0, 0, 100);

            var lifeStealAbility = RtsCustomTriggerActionExamples.CreateLifeStealStrikeAbility(
                new AbilityId("ability.test.lifesteal"),
                HealthResourceId,
                10,
                50 * FP._1 / 100);
            world.AddAbility(lifeStealAbility);
            attacker.GrantAbility(lifeStealAbility.Id);

            var runtime = new CombatRuntime(world, new RtsCombatRuntimeModulePack().CreateRuntimeOptions());
            runtime.Tick();

            var result = runtime.TryActivate(new AbilityActivationRequest
            {
                SourceActorId = attacker.ActorId,
                AbilityId = lifeStealAbility.Id,
                RequestTick = world.CurrentTick,
                TargetData = new AbilityTargetData
                {
                    TargetActorIds = { defender.ActorId },
                },
            });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(GetCurrentHealth(defender), Is.EqualTo((FP)90));
            Assert.That(GetCurrentHealth(attacker), Is.EqualTo((FP)85));
        }

        [Test]
        public void LifeStealOperation_ShouldNotHealSource_WhenHandlerNotInjected()
        {
            var world = new CombatWorldState();
            var attacker = AddActor(world, "attacker", "red", 0, 0, 0, 80);
            var defender = AddActor(world, "defender", "blue", 2, 0, 0, 100);

            var lifeStealAbility = RtsCustomTriggerActionExamples.CreateLifeStealStrikeAbility(
                new AbilityId("ability.test.lifesteal.nohandler"),
                HealthResourceId,
                10,
                50 * FP._1 / 100);
            world.AddAbility(lifeStealAbility);
            attacker.GrantAbility(lifeStealAbility.Id);

            var pack = new RtsCombatRuntimeModulePack
            {
                LifeStealImpactOperationHandler = null,
            };
            var runtime = new CombatRuntime(world, pack.CreateRuntimeOptions());
            runtime.Tick();

            var result = runtime.TryActivate(new AbilityActivationRequest
            {
                SourceActorId = attacker.ActorId,
                AbilityId = lifeStealAbility.Id,
                RequestTick = world.CurrentTick,
                TargetData = new AbilityTargetData
                {
                    TargetActorIds = { defender.ActorId },
                },
            });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(GetCurrentHealth(defender), Is.EqualTo((FP)90));
            Assert.That(GetCurrentHealth(attacker), Is.EqualTo((FP)80));
        }

        private static CombatActorState AddActor(
            CombatWorldState world,
            string actorId,
            string teamId,
            int x,
            int y,
            int z,
            int health)
        {
            var actor = new CombatActorState(new ActorId(actorId))
            {
                TeamId = new TeamId(teamId),
                Position = new WorldPosition(x, y, z),
                IsAlive = true,
            };

            var healthResource = actor.Resources.GetOrCreate(HealthResourceId, 100, 100);
            healthResource.SetCurrent(health);
            world.AddActor(actor);
            return actor;
        }

        private static FP GetCurrentHealth(CombatActorState actor)
        {
            return actor.Resources.GetOrCreate(HealthResourceId).Current;
        }
    }
}
