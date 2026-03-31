using System.Collections.Generic;
using Herta;
using Saber.GAS.Actors;
using Saber.GAS.Foundation;

namespace Saber.GAS.RTS.Spatial
{
    /// <summary>
    /// 基于 FixedPoint 的轻量八叉树空间索引。
    /// 专注于 RTS 技能目标检索，不处理刚体或碰撞响应。
    /// </summary>
    internal sealed class RtsOctreeSpatialIndex
    {
        private readonly RtsSpatialQueryOptions _options;
        private OctreeNode _root;
        private int _actorCount;

        public RtsOctreeSpatialIndex(RtsSpatialQueryOptions options)
        {
            _options = options ?? new RtsSpatialQueryOptions();
            _options.Sanitize();
            _root = CreateEmptyRoot();
        }

        public int ActorCount => _actorCount;

        public void Rebuild(IEnumerable<CombatActorState> actors)
        {
            _options.Sanitize();
            var entries = BuildEntries(actors);
            _root = BuildRoot(entries);
            _actorCount = entries.Count;

            for (var i = 0; i < entries.Count; i++)
            {
                Insert(_root, entries[i]);
            }
        }

        public void QuerySphere(WorldPosition center, FP radius, IList<CombatActorState> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();

            if (radius < FP._0 || _actorCount == 0)
            {
                return;
            }

            var centerVector = ToVector(center);
            var radiusSquared = radius * radius;
            QuerySphereRecursive(_root, centerVector, radiusSquared, results);
        }

        public bool TryFindNearest(WorldPosition center, FP maxDistance, ActorId excludedActorId, out CombatActorState nearestActor)
        {
            nearestActor = null;
            if (_actorCount == 0)
            {
                return false;
            }

            var centerVector = ToVector(center);
            var bestDistanceSquared = maxDistance > FP._0 ? maxDistance * maxDistance : FP.MaxValue;
            SearchNearestRecursive(_root, centerVector, excludedActorId, ref bestDistanceSquared, ref nearestActor);
            return nearestActor != null;
        }

        private static FPVector3 ToVector(WorldPosition position)
        {
            return new FPVector3(position.X, position.Y, position.Z);
        }

        private static FPVector3 MakeVector(FP value)
        {
            return new FPVector3(value, value, value);
        }

        private List<ActorEntry> BuildEntries(IEnumerable<CombatActorState> actors)
        {
            var entries = new List<ActorEntry>();
            if (actors == null)
            {
                return entries;
            }

            foreach (var actor in actors)
            {
                if (actor == null)
                {
                    continue;
                }

                entries.Add(new ActorEntry(actor, ToVector(actor.Position)));
            }

            return entries;
        }

        private OctreeNode BuildRoot(IList<ActorEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return CreateEmptyRoot();
            }

            var min = entries[0].Position;
            var max = entries[0].Position;

            for (var i = 1; i < entries.Count; i++)
            {
                min = FPVector3.Min(min, entries[i].Position);
                max = FPVector3.Max(max, entries[i].Position);
            }

            var extents = (max - min) * FP._0_50;
            extents += MakeVector(_options.BoundsPadding);
            extents = EnsureMinimumExtent(extents, _options.MinimumNodeExtent);
            var center = min + ((max - min) * FP._0_50);
            return new OctreeNode(new FPBounds3(center, extents), 0, _options.NodeCapacity);
        }

        private OctreeNode CreateEmptyRoot()
        {
            var extents = MakeVector(_options.MinimumNodeExtent);
            return new OctreeNode(new FPBounds3(FPVector3.Zero, extents), 0, _options.NodeCapacity);
        }

        private static FPVector3 EnsureMinimumExtent(FPVector3 extents, FP minimumExtent)
        {
            if (extents.X < minimumExtent)
            {
                extents.X = minimumExtent;
            }

            if (extents.Y < minimumExtent)
            {
                extents.Y = minimumExtent;
            }

            if (extents.Z < minimumExtent)
            {
                extents.Z = minimumExtent;
            }

            return extents;
        }

        private void Insert(OctreeNode node, ActorEntry entry)
        {
            if (node.Depth >= _options.MaxDepth || IsAtMinimumExtent(node.Bounds.Extents))
            {
                node.Entries.Add(entry);
                return;
            }

            if (node.Children == null && node.Entries.Count < _options.NodeCapacity)
            {
                node.Entries.Add(entry);
                return;
            }

            if (node.Children == null)
            {
                if (!TrySplit(node))
                {
                    node.Entries.Add(entry);
                    return;
                }

                RedistributeEntries(node);
            }

            InsertIntoChild(node, entry);
        }

        private bool IsAtMinimumExtent(FPVector3 extents)
        {
            return extents.X <= _options.MinimumNodeExtent &&
                   extents.Y <= _options.MinimumNodeExtent &&
                   extents.Z <= _options.MinimumNodeExtent;
        }

        private bool TrySplit(OctreeNode node)
        {
            var childExtents = node.Bounds.Extents * FP._0_50;
            if (IsAtMinimumExtent(childExtents))
            {
                return false;
            }

            var center = node.Bounds.Center;
            node.Children = new OctreeNode[8];

            for (var i = 0; i < 8; i++)
            {
                var x = (i & 1) == 0 ? -childExtents.X : childExtents.X;
                var y = (i & 2) == 0 ? -childExtents.Y : childExtents.Y;
                var z = (i & 4) == 0 ? -childExtents.Z : childExtents.Z;
                var childCenter = center + new FPVector3(x, y, z);
                node.Children[i] = new OctreeNode(new FPBounds3(childCenter, childExtents), node.Depth + 1, _options.NodeCapacity);
            }

            return true;
        }

        private void RedistributeEntries(OctreeNode node)
        {
            for (var i = node.Entries.Count - 1; i >= 0; i--)
            {
                var entry = node.Entries[i];
                node.Entries.RemoveAt(i);
                InsertIntoChild(node, entry);
            }
        }

        private void InsertIntoChild(OctreeNode node, ActorEntry entry)
        {
            if (node.Children == null)
            {
                node.Entries.Add(entry);
                return;
            }

            var childIndex = GetChildIndex(node.Bounds.Center, entry.Position);
            var child = node.Children[childIndex];
            if (child == null || !child.Bounds.Contains(entry.Position))
            {
                node.Entries.Add(entry);
                return;
            }

            Insert(child, entry);
        }

        private static int GetChildIndex(FPVector3 center, FPVector3 point)
        {
            var index = 0;
            if (point.X >= center.X)
            {
                index |= 1;
            }

            if (point.Y >= center.Y)
            {
                index |= 2;
            }

            if (point.Z >= center.Z)
            {
                index |= 4;
            }

            return index;
        }

        private static void QuerySphereRecursive(OctreeNode node, FPVector3 center, FP radiusSquared, IList<CombatActorState> results)
        {
            if (node == null || !IntersectsSphere(node.Bounds, center, radiusSquared))
            {
                return;
            }

            for (var i = 0; i < node.Entries.Count; i++)
            {
                var entry = node.Entries[i];
                if (FPVector3.DistanceSquared(entry.Position, center) <= radiusSquared)
                {
                    results.Add(entry.Actor);
                }
            }

            if (node.Children == null)
            {
                return;
            }

            for (var i = 0; i < node.Children.Length; i++)
            {
                QuerySphereRecursive(node.Children[i], center, radiusSquared, results);
            }
        }

        private static void SearchNearestRecursive(
            OctreeNode node,
            FPVector3 center,
            ActorId excludedActorId,
            ref FP bestDistanceSquared,
            ref CombatActorState bestActor)
        {
            if (node == null)
            {
                return;
            }

            var minimumDistance = DistanceSquaredToBounds(center, node.Bounds);
            if (minimumDistance > bestDistanceSquared)
            {
                return;
            }

            for (var i = 0; i < node.Entries.Count; i++)
            {
                var entry = node.Entries[i];
                if (!excludedActorId.IsEmpty && entry.Actor.ActorId == excludedActorId)
                {
                    continue;
                }

                var distanceSquared = FPVector3.DistanceSquared(center, entry.Position);
                if (distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                bestActor = entry.Actor;
            }

            if (node.Children == null)
            {
                return;
            }

            for (var i = 0; i < node.Children.Length; i++)
            {
                SearchNearestRecursive(node.Children[i], center, excludedActorId, ref bestDistanceSquared, ref bestActor);
            }
        }

        private static bool IntersectsSphere(FPBounds3 bounds, FPVector3 center, FP radiusSquared)
        {
            return DistanceSquaredToBounds(center, bounds) <= radiusSquared;
        }

        private static FP DistanceSquaredToBounds(FPVector3 point, FPBounds3 bounds)
        {
            var min = bounds.Min;
            var max = bounds.Max;
            var distanceSquared = FP._0;

            if (point.X < min.X)
            {
                var delta = min.X - point.X;
                distanceSquared += delta * delta;
            }
            else if (point.X > max.X)
            {
                var delta = point.X - max.X;
                distanceSquared += delta * delta;
            }

            if (point.Y < min.Y)
            {
                var delta = min.Y - point.Y;
                distanceSquared += delta * delta;
            }
            else if (point.Y > max.Y)
            {
                var delta = point.Y - max.Y;
                distanceSquared += delta * delta;
            }

            if (point.Z < min.Z)
            {
                var delta = min.Z - point.Z;
                distanceSquared += delta * delta;
            }
            else if (point.Z > max.Z)
            {
                var delta = point.Z - max.Z;
                distanceSquared += delta * delta;
            }

            return distanceSquared;
        }

        private sealed class OctreeNode
        {
            public OctreeNode(FPBounds3 bounds, int depth, int capacity)
            {
                Bounds = bounds;
                Depth = depth;
                Entries = new List<ActorEntry>(capacity);
            }

            public FPBounds3 Bounds { get; }

            public int Depth { get; }

            public List<ActorEntry> Entries { get; }

            public OctreeNode[] Children { get; set; }
        }

        private struct ActorEntry
        {
            public ActorEntry(CombatActorState actor, FPVector3 position)
            {
                Actor = actor;
                Position = position;
            }

            public CombatActorState Actor { get; }

            public FPVector3 Position { get; }
        }
    }
}
