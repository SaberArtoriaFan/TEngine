using System.Collections.Generic;
using System.Linq;
using Herta;
using NUnit.Framework;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Foundation;
using Saber.GAS.RTS.Relations;
using Saber.GAS.RTS.Spatial;
using Saber.GAS.Runtime;

namespace Saber.GAS.RTS.Tests.Spatial
{
    public sealed class RtsOctreeSpatialQueryServiceTests
    {
        [Test]
        public void QueryActorsInRange_ShouldReturnActorsInsideRadius()
        {
            var world = new CombatWorldState();
            var a = AddActor(world, "a", "red", 0, 0, 0);
            var b = AddActor(world, "b", "red", 3, 0, 0);
            AddActor(world, "c", "blue", 8, 0, 0);

            var service = new RtsOctreeSpatialQueryService();
            var results = new List<CombatActorState>();

            service.QueryActorsInRange(world, new WorldPosition(FP._0, FP._0, FP._0), 5, results);

            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results, Contains.Item(a));
            Assert.That(results, Contains.Item(b));
        }

        [Test]
        public void TryFindNearestActor_ShouldExcludeSourceActor()
        {
            var world = new CombatWorldState();
            var source = AddActor(world, "source", "red", 0, 0, 0);
            var near = AddActor(world, "near", "blue", 1, 0, 0);
            AddActor(world, "far", "blue", 5, 0, 0);

            var service = new RtsOctreeSpatialQueryService();

            var found = service.TryFindNearestActor(world, source, 10, out var nearestActor);

            Assert.That(found, Is.True);
            Assert.That(nearestActor, Is.SameAs(near));
        }

        [Test]
        public void AreaTargeting_ShouldFilterByTargetFlagsAndTags()
        {
            var world = new CombatWorldState();
            var source = AddActor(world, "source", "red", 0, 0, 0);
            var enemy = AddActor(world, "enemy", "blue", 4, 0, 0);
            AddActor(world, "ally", "red", 3, 0, 0);
            AddActor(world, "enemy_out", "blue", 12, 0, 0);

            var ability = new AbilityDefinition(new AbilityId("ability.rts.aoe"));
            ability.Targeting.Kind = AbilityTargetKind.Area;
            ability.Targeting.MaxRange = 6;
            ability.Targeting.AllowedFlags = AbilityTargetFlags.Enemy | AbilityTargetFlags.Alive;

            var targetData = new AbilityTargetData
            {
                TargetPoint = new WorldPosition(2, 0, 0),
            };

            var resolver = new RtsSpatialTargetingResolver(
                new RtsOctreeSpatialQueryService(),
                new RtsTeamRelationResolver());
            var results = new List<CombatActorState>();

            resolver.ResolveTargets(world, source, ability, targetData, results);

            Assert.That(
                results.Count,
                Is.EqualTo(1),
                "Resolved targets: " + string.Join(",", results.Select(actor => actor.ActorId.Value)));
            Assert.That(results[0].ActorId, Is.EqualTo(enemy.ActorId));
        }

        [Test]
        public void ActorTargetingWithoutInput_ShouldResolveNearestMatchingActor()
        {
            var world = new CombatWorldState();
            var source = AddActor(world, "source", "red", 0, 0, 0);
            AddActor(world, "ally_close", "red", 1, 0, 0);
            var enemy = AddActor(world, "enemy_mid", "blue", 4, 0, 0);
            AddActor(world, "enemy_far", "blue", 9, 0, 0);

            var ability = new AbilityDefinition(new AbilityId("ability.rts.auto_target"));
            ability.Targeting.Kind = AbilityTargetKind.Actor;
            ability.Targeting.MaxRange = 6;
            ability.Targeting.AllowedFlags = AbilityTargetFlags.Enemy | AbilityTargetFlags.Alive;

            var resolver = new RtsSpatialTargetingResolver(
                new RtsOctreeSpatialQueryService(),
                new RtsTeamRelationResolver());
            var results = new List<CombatActorState>();

            resolver.ResolveTargets(world, source, ability, null, results);

            Assert.That(
                results.Count,
                Is.EqualTo(1),
                "Resolved targets: " + string.Join(",", results.Select(actor => actor.ActorId.Value)));
            Assert.That(results[0].ActorId, Is.EqualTo(enemy.ActorId));
        }

        private static CombatActorState AddActor(
            CombatWorldState world,
            string actorId,
            string teamId,
            int x,
            int y,
            int z)
        {
            var actor = new CombatActorState(new ActorId(actorId))
            {
                TeamId = new TeamId(teamId),
                Position = new WorldPosition(x, y, z),
                IsAlive = true,
            };
            world.AddActor(actor);
            return actor;
        }
    }
}
