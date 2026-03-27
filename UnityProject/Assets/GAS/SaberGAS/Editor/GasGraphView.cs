using System;
using System.Collections.Generic;
using System.Linq;
using Saber.GAS.Authoring;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Saber.GAS.Editor
{
    internal sealed class GasGraphView : GraphView
    {
        private const float FullGraphColumnWidth = 320f;
        private const float FocusGraphColumnWidth = 320f;
        private const float NodeWidth = 250f;
        private const float NodeHeight = 150f;
        private const float NodeVerticalGap = 190f;
        private const float GraphStartX = 40f;
        private const float GraphStartY = 40f;

        private readonly GasWorkbenchState _state;
        private readonly Dictionary<UnityEngine.Object, GasGraphNodeView> _nodes = new Dictionary<UnityEngine.Object, GasGraphNodeView>();
        private bool _suspendGraphEvents;

        public GasGraphView(GasWorkbenchState state)
        {
            _state = state;

            style.flexGrow = 1f;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;
        }

        public void Rebuild()
        {
            _suspendGraphEvents = true;
            DeleteElements(graphElements.ToList());
            _nodes.Clear();

            var catalog = _state.Catalog;
            if (catalog == null)
            {
                _suspendGraphEvents = false;
                return;
            }

            var relations = GasGraphAdapter.BuildRelations(catalog);
            var allAssets = GasGraphAdapter.CollectGraphAssets(catalog);
            var focusAsset = ResolveFocusAsset(allAssets);

            if (focusAsset == null)
            {
                RebuildFullGraph(catalog, relations);
            }
            else
            {
                RebuildFocusedGraph(focusAsset, allAssets, relations);
            }

            if (_state.SelectedObject != null && _nodes.TryGetValue(_state.SelectedObject, out var selectedNode))
            {
                AddToSelection(selectedNode);
                if (_nodes.Count > 1)
                {
                    FrameAll();
                }
                else
                {
                    FrameSelection();
                }
            }
            else if (_nodes.Count > 0)
            {
                FrameAll();
            }

            _suspendGraphEvents = false;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            if (_state.Catalog == null)
            {
                evt.menu.AppendAction("新建/Ability", _ => { }, _ => DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("新建/Effect", _ => { }, _ => DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("新建/Trigger", _ => { }, _ => DropdownMenuAction.Status.Disabled);
                return;
            }

            evt.menu.AppendSeparator();
            evt.menu.AppendAction("新建/Ability", _ => CreateGraphAsset(() => GasAuthoringTemplateUtility.CreateBlankAbility(_state.Catalog, false)));
            evt.menu.AppendAction("新建/Effect", _ => CreateGraphAsset(() => GasAuthoringTemplateUtility.CreateBlankEffect(_state.Catalog, false)));
            evt.menu.AppendAction("新建/Trigger", _ => CreateGraphAsset(() => GasAuthoringTemplateUtility.CreateBlankTrigger(_state.Catalog, false)));
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            var startData = startPort.userData as GasGraphPortData;
            if (startData == null)
            {
                return compatible;
            }

            ports.ForEach(port =>
            {
                if (port == startPort || port.node == startPort.node)
                {
                    return;
                }

                var otherData = port.userData as GasGraphPortData;
                if (otherData == null || otherData.IsInput == startData.IsInput)
                {
                    return;
                }

                var output = startData.IsInput ? otherData : startData;
                var input = startData.IsInput ? startData : otherData;
                if (output.TargetType == null || input.Owner == null)
                {
                    return;
                }

                if (output.TargetType.IsAssignableFrom(input.Owner.GetType()))
                {
                    compatible.Add(port);
                }
            });

            return compatible;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_suspendGraphEvents)
            {
                return change;
            }

            var hasChanges = false;

            if (change.edgesToCreate != null)
            {
                for (var i = 0; i < change.edgesToCreate.Count; i++)
                {
                    var edge = change.edgesToCreate[i];
                    if (TryApplyEdgeCreate(edge))
                    {
                        hasChanges = true;
                    }
                }
            }

            if (change.elementsToRemove != null)
            {
                for (var i = 0; i < change.elementsToRemove.Count; i++)
                {
                    if (change.elementsToRemove[i] is Edge edge && TryApplyEdgeRemove(edge))
                    {
                        hasChanges = true;
                    }
                }
            }

            if (hasChanges)
            {
                _state.Refresh();
                EditorApplication.delayCall += Rebuild;
            }

            return change;
        }

        private void RebuildFullGraph(CombatDefinitionCatalogAsset catalog, List<GasGraphRelation> relations)
        {
            var abilities = GasEditorUtility.GetEmbeddedAssets<AbilityDefinitionAsset>(catalog);
            var effects = GasEditorUtility.GetEmbeddedAssets<EffectDefinitionAsset>(catalog);
            var triggers = GasEditorUtility.GetEmbeddedAssets<TriggerDefinitionAsset>(catalog);
            var actorTemplates = GasEditorUtility.GetEmbeddedAssets<CombatActorTemplateAsset>(catalog);

            AddColumn(abilities, GraphStartX + 0 * FullGraphColumnWidth);
            AddColumn(effects, GraphStartX + 1 * FullGraphColumnWidth);
            AddColumn(triggers, GraphStartX + 2 * FullGraphColumnWidth);
            AddColumn(actorTemplates, GraphStartX + 3 * FullGraphColumnWidth);

            for (var i = 0; i < relations.Count; i++)
            {
                CreateEdge(relations[i]);
            }
        }

        private void RebuildFocusedGraph(
            UnityEngine.Object focusAsset,
            List<UnityEngine.Object> allAssets,
            List<GasGraphRelation> relations)
        {
            var outgoingMap = BuildOutgoingMap(relations);
            var incomingMap = BuildIncomingMap(relations);
            var upstreamDistances = ComputeDistances(focusAsset, incomingMap);
            var downstreamDistances = ComputeDistances(focusAsset, outgoingMap);
            var visibleAssets = CollectVisibleAssets(focusAsset, upstreamDistances, downstreamDistances);
            var orderedLayers = BuildFocusedLayers(focusAsset, visibleAssets, upstreamDistances, downstreamDistances);

            if (orderedLayers.Count == 0)
            {
                orderedLayers[0] = new List<UnityEngine.Object> { focusAsset };
            }

            var minLevel = orderedLayers.Keys.Min();
            foreach (var layer in orderedLayers)
            {
                var x = GraphStartX + (layer.Key - minLevel) * FocusGraphColumnWidth;
                var assets = layer.Value;
                for (var i = 0; i < assets.Count; i++)
                {
                    var node = new GasGraphNodeView(assets[i], HandleNodeSelected, HandleNodeDoubleClicked);
                    AddElement(node);
                    node.SetPosition(new Rect(x, GraphStartY + i * NodeVerticalGap, NodeWidth, NodeHeight));
                    _nodes[assets[i]] = node;
                }
            }

            for (var i = 0; i < relations.Count; i++)
            {
                var relation = relations[i];
                if (!visibleAssets.Contains(relation.Source) || !visibleAssets.Contains(relation.Target))
                {
                    continue;
                }

                CreateEdge(relation);
            }
        }

        private void AddColumn<T>(T[] assets, float x) where T : UnityEngine.Object
        {
            for (var i = 0; i < assets.Length; i++)
            {
                var node = new GasGraphNodeView(assets[i], HandleNodeSelected, HandleNodeDoubleClicked);
                AddElement(node);
                node.SetPosition(new Rect(x, GraphStartY + i * NodeVerticalGap, NodeWidth, NodeHeight));
                _nodes[assets[i]] = node;
            }
        }

        private void CreateEdge(GasGraphRelation relation)
        {
            if (!_nodes.TryGetValue(relation.Source, out var sourceNode) ||
                !_nodes.TryGetValue(relation.Target, out var targetNode))
            {
                return;
            }

            var outputPort = sourceNode.GetOutputPort(relation.Kind);
            var inputPort = targetNode.inputContainer.ElementAt(0) as Port;
            if (outputPort == null || inputPort == null)
            {
                return;
            }

            var edge = outputPort.ConnectTo(inputPort);
            AddElement(edge);
        }

        private bool TryApplyEdgeCreate(Edge edge)
        {
            var outputData = edge.output?.userData as GasGraphPortData;
            var inputData = edge.input?.userData as GasGraphPortData;
            if (outputData == null || inputData == null)
            {
                return false;
            }

            return GasGraphAdapter.AddRelation(outputData.Owner, outputData.RelationKind, inputData.Owner);
        }

        private bool TryApplyEdgeRemove(Edge edge)
        {
            var outputData = edge.output?.userData as GasGraphPortData;
            var inputData = edge.input?.userData as GasGraphPortData;
            if (outputData == null || inputData == null)
            {
                return false;
            }

            return GasGraphAdapter.RemoveRelation(outputData.Owner, outputData.RelationKind, inputData.Owner);
        }

        private void HandleNodeSelected(UnityEngine.Object asset)
        {
            _state.Select(asset);
            Selection.activeObject = asset;
        }

        private void HandleNodeDoubleClicked(UnityEngine.Object asset)
        {
            _state.FocusGraph(_state.FocusedGraphObject == asset ? null : asset);
            _state.Select(asset);
            Selection.activeObject = asset;
        }

        private void CreateGraphAsset(Func<UnityEngine.Object> createFunc)
        {
            if (createFunc == null)
            {
                return;
            }

            var createdAsset = createFunc();
            if (createdAsset == null)
            {
                return;
            }

            GasEditorUtility.FocusAsset(createdAsset);
            _state.Select(createdAsset);
            _state.Refresh();
            EditorApplication.delayCall += Rebuild;
        }

        private UnityEngine.Object ResolveFocusAsset(List<UnityEngine.Object> allAssets)
        {
            var focusedObject = _state.FocusedGraphObject;
            if (focusedObject == null || focusedObject == _state.Catalog)
            {
                return null;
            }

            for (var i = 0; i < allAssets.Count; i++)
            {
                if (allAssets[i] == focusedObject)
                {
                    return focusedObject;
                }
            }

            return null;
        }

        private static Dictionary<UnityEngine.Object, List<UnityEngine.Object>> BuildOutgoingMap(List<GasGraphRelation> relations)
        {
            var map = new Dictionary<UnityEngine.Object, List<UnityEngine.Object>>();
            for (var i = 0; i < relations.Count; i++)
            {
                var relation = relations[i];
                if (!map.TryGetValue(relation.Source, out var list))
                {
                    list = new List<UnityEngine.Object>();
                    map[relation.Source] = list;
                }

                if (!list.Contains(relation.Target))
                {
                    list.Add(relation.Target);
                }
            }

            return map;
        }

        private static Dictionary<UnityEngine.Object, List<UnityEngine.Object>> BuildIncomingMap(List<GasGraphRelation> relations)
        {
            var map = new Dictionary<UnityEngine.Object, List<UnityEngine.Object>>();
            for (var i = 0; i < relations.Count; i++)
            {
                var relation = relations[i];
                if (!map.TryGetValue(relation.Target, out var list))
                {
                    list = new List<UnityEngine.Object>();
                    map[relation.Target] = list;
                }

                if (!list.Contains(relation.Source))
                {
                    list.Add(relation.Source);
                }
            }

            return map;
        }

        private static Dictionary<UnityEngine.Object, int> ComputeDistances(
            UnityEngine.Object origin,
            Dictionary<UnityEngine.Object, List<UnityEngine.Object>> adjacency)
        {
            var distances = new Dictionary<UnityEngine.Object, int>();
            if (origin == null)
            {
                return distances;
            }

            var queue = new Queue<UnityEngine.Object>();
            distances[origin] = 0;
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentDistance = distances[current];
                if (!adjacency.TryGetValue(current, out var neighbors))
                {
                    continue;
                }

                for (var i = 0; i < neighbors.Count; i++)
                {
                    var neighbor = neighbors[i];
                    if (neighbor == null || distances.ContainsKey(neighbor))
                    {
                        continue;
                    }

                    distances[neighbor] = currentDistance + 1;
                    queue.Enqueue(neighbor);
                }
            }

            return distances;
        }

        private static HashSet<UnityEngine.Object> CollectVisibleAssets(
            UnityEngine.Object focusAsset,
            Dictionary<UnityEngine.Object, int> upstreamDistances,
            Dictionary<UnityEngine.Object, int> downstreamDistances)
        {
            var visibleAssets = new HashSet<UnityEngine.Object>();
            if (focusAsset != null)
            {
                visibleAssets.Add(focusAsset);
            }

            foreach (var pair in upstreamDistances)
            {
                visibleAssets.Add(pair.Key);
            }

            foreach (var pair in downstreamDistances)
            {
                visibleAssets.Add(pair.Key);
            }

            return visibleAssets;
        }

        private static SortedDictionary<int, List<UnityEngine.Object>> BuildFocusedLayers(
            UnityEngine.Object focusAsset,
            HashSet<UnityEngine.Object> visibleAssets,
            Dictionary<UnityEngine.Object, int> upstreamDistances,
            Dictionary<UnityEngine.Object, int> downstreamDistances)
        {
            var layers = new SortedDictionary<int, List<UnityEngine.Object>>();
            foreach (var asset in visibleAssets)
            {
                var level = ResolveLevel(asset, focusAsset, upstreamDistances, downstreamDistances);
                if (!layers.TryGetValue(level, out var list))
                {
                    list = new List<UnityEngine.Object>();
                    layers[level] = list;
                }

                list.Add(asset);
            }

            foreach (var pair in layers)
            {
                pair.Value.Sort(CompareAssets);
            }

            return layers;
        }

        private static int ResolveLevel(
            UnityEngine.Object asset,
            UnityEngine.Object focusAsset,
            Dictionary<UnityEngine.Object, int> upstreamDistances,
            Dictionary<UnityEngine.Object, int> downstreamDistances)
        {
            if (asset == null || asset == focusAsset)
            {
                return 0;
            }

            var hasUpstream = upstreamDistances.TryGetValue(asset, out var upstreamDistance) && upstreamDistance > 0;
            var hasDownstream = downstreamDistances.TryGetValue(asset, out var downstreamDistance) && downstreamDistance > 0;

            if (hasUpstream && hasDownstream)
            {
                return downstreamDistance <= upstreamDistance ? downstreamDistance : -upstreamDistance;
            }

            if (hasUpstream)
            {
                return -upstreamDistance;
            }

            if (hasDownstream)
            {
                return downstreamDistance;
            }

            return 0;
        }

        private static int CompareAssets(UnityEngine.Object left, UnityEngine.Object right)
        {
            var typeCompare = GetTypeSortOrder(left).CompareTo(GetTypeSortOrder(right));
            if (typeCompare != 0)
            {
                return typeCompare;
            }

            var leftName = left == null ? string.Empty : left.name;
            var rightName = right == null ? string.Empty : right.name;
            return string.CompareOrdinal(leftName, rightName);
        }

        private static int GetTypeSortOrder(UnityEngine.Object asset)
        {
            return asset switch
            {
                CombatActorTemplateAsset => 0,
                AbilityDefinitionAsset => 1,
                EffectDefinitionAsset => 2,
                TriggerDefinitionAsset => 3,
                _ => 10,
            };
        }
    }
}
