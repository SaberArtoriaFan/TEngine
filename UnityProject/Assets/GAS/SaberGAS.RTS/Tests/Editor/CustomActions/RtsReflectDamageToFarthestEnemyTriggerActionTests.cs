using Herta;
using NUnit.Framework;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Foundation;
using Saber.GAS.Resources;
using Saber.GAS.Runtime;
using Saber.GAS.RTS.Bootstrap;
using Saber.GAS.RTS.Examples;
using Saber.GAS.Triggers;

namespace Saber.GAS.RTS.Tests.CustomActions
{
    public sealed class RtsReflectDamageToFarthestEnemyTriggerActionTests
    {
        private static readonly ResourceId HealthResourceId = new ResourceId("resource.hp");

        [Test]
        public void ReflectDamage_ShouldHitFarthestEnemyWhenDamaged()
        {
            var world = new CombatWorldState();
            var defender = AddActor(world, "defender", "blue", 0, 0, 0);
            var attacker = AddActor(world, "attacker", "red", 1, 0, 0);
            var enemyNear = AddActor(world, "enemy_near", "red", 3, 0, 0);
            var enemyFar = AddActor(world, "enemy_far", "red", 10, 0, 0);
            AddActor(world, "ally_blue", "blue", 8, 0, 0);

            var attackAbility = RtsCustomTriggerActionExamples.CreateSimpleDamageAbility(
                new AbilityId("ability.test.attack"),
                HealthResourceId,
                10);
            var reflectPassive = RtsCustomTriggerActionExamples.CreateReflectDamageToFarthestEnemyPassiveAbility(
                new AbilityId("ability.test.reflect"),
                new TriggerId("trigger.test.reflect"),
                FP._1);

            world.AddAbility(attackAbility);
            world.AddAbility(reflectPassive);

            attacker.GrantAbility(attackAbility.Id);
            defender.GrantAbility(reflectPassive.Id);

            var runtime = new CombatRuntime(world, new RtsCombatRuntimeModulePack().CreateRuntimeOptions());
            runtime.Tick();

            var result = runtime.TryActivate(new AbilityActivationRequest
            {
                SourceActorId = attacker.ActorId,
                AbilityId = attackAbility.Id,
                RequestTick = world.CurrentTick,
                TargetData = new AbilityTargetData
                {
                    TargetActorIds = { defender.ActorId },
                },
            });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(GetCurrentHealth(defender), Is.EqualTo((FP)90));
            Assert.That(GetCurrentHealth(enemyFar), Is.EqualTo((FP)90));
            Assert.That(GetCurrentHealth(enemyNear), Is.EqualTo((FP)100));
            Assert.That(GetCurrentHealth(attacker), Is.EqualTo((FP)100));
        }

        [Test]
        public void ReflectDamage_WithMaxDistance_ShouldIgnoreOutOfRangeEnemies()
        {
            var world = new CombatWorldState();
            var defender = AddActor(world, "defender", "blue", 0, 0, 0);
            var attacker = AddActor(world, "attacker", "red", 1, 0, 0);
            var enemyNear = AddActor(world, "enemy_near", "red", 3, 0, 0);
            var enemyFar = AddActor(world, "enemy_far", "red", 9, 0, 0);

            var attackAbility = RtsCustomTriggerActionExamples.CreateSimpleDamageAbility(
                new AbilityId("ability.test.attack.max_distance"),
                HealthResourceId,
                10);
            var reflectPassive = RtsCustomTriggerActionExamples.CreateReflectDamageToFarthestEnemyPassiveAbility(
                new AbilityId("ability.test.reflect.max_distance"),
                new TriggerId("trigger.test.reflect.max_distance"),
                FP._1,
                4);

            world.AddAbility(attackAbility);
            world.AddAbility(reflectPassive);

            attacker.GrantAbility(attackAbility.Id);
            defender.GrantAbility(reflectPassive.Id);

            var runtime = new CombatRuntime(world, new RtsCombatRuntimeModulePack().CreateRuntimeOptions());
            runtime.Tick();

            var result = runtime.TryActivate(new AbilityActivationRequest
            {
                SourceActorId = attacker.ActorId,
                AbilityId = attackAbility.Id,
                RequestTick = world.CurrentTick,
                TargetData = new AbilityTargetData
                {
                    TargetActorIds = { defender.ActorId },
                },
            });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(GetCurrentHealth(defender), Is.EqualTo((FP)90));
            Assert.That(GetCurrentHealth(enemyNear), Is.EqualTo((FP)90));
            Assert.That(GetCurrentHealth(enemyFar), Is.EqualTo((FP)100));
        }

        private static CombatActorState AddActor(CombatWorldState world, string actorId, string teamId, int x, int y, int z)
        {
            var actor = new CombatActorState(new ActorId(actorId))
            {
                TeamId = new TeamId(teamId),
                Position = new WorldPosition(x, y, z),
                IsAlive = true,
            };

            var health = actor.Resources.GetOrCreate(HealthResourceId, 100, 100);
            health.SetCurrent(100);
            world.AddActor(actor);
            return actor;
        }

        private static FP GetCurrentHealth(CombatActorState actor)
        {
            return actor.Resources.GetOrCreate(HealthResourceId).Current;
        }
    }
}
