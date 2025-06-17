#if ST_MM_2

using System;
using System.Collections.Generic;
using System.Linq;
using MapMagic.Nodes;
using MapMagic.Products;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Objects",
        name = "Manual Scatter",
        priority = 10,
        disengageable = true,
        iconName = "GeneratorIcons/Scatter",
        colorType = typeof(WorldGraphGuid),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/object/manual_scatter")]
    public class ManualScatterGenerator : LayeredGenerator<ManualScatterGenerator.Layer>, GraphGenerator, IOutlet<Den.Tools.TransitionsList>,
        IMultiInlet, IMultiOutlet
        
    {
        [Serializable]
        public class Layer : AbstractLayer
        {
            public string nodeGuid;
            public Vector3 position;
            public ClampedFloat radius = new ClampedFloat(50f, 2f, float.MaxValue);

            [NonSerialized] public bool placingPoi = false;
            [NonSerialized] public bool hasConflict = false;
        }

        [SerializeField] private bool readOnly = false;
        public string startingGraphGuid = Guid.NewGuid().ToString();
        public string type = "CustomType";
        public bool useRadiusPerLayer = false;
        public bool useCustomIds = false;
        public ClampedFloat radius = new ClampedFloat(50f, 2f, float.MaxValue);

        public Inlet<WorldGraphGuid> inputGraph = new Inlet<WorldGraphGuid>();
        public Outlet<NodesByOffset> outputNodes = new Outlet<NodesByOffset>();
        public Outlet<WorldGraphGuid> outputGraph = new Outlet<WorldGraphGuid>();

        public float Radius(Layer layer)
        {
            return useRadiusPerLayer ? layer.radius.ClampedValue : radius.ClampedValue;
        }

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputGraph;
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return outputNodes;
            yield return outputGraph;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            Init();

            // return on stop/disable
            if (!enabled || stop != null && stop.stop) return;
            
            readOnly = tileData is ReadOnlyTileData;

            var inputGraphGuid = tileData.ReadInletProduct(inputGraph)?.value ?? startingGraphGuid;
            var graph = (InternalWorldGraph) SplineTools.Instance.state.Graph(inputGraphGuid);

            if (!readOnly)
            {
                lock (graph)
                {
                    if (!graph.ProcessedGraphComponentsContains(startingGraphGuid))
                    {
                        graph.ProcessedGraphComponentsAdd(startingGraphGuid);
                        var rnd = new ConsistentRandom(tileData.random.Seed + 454574);

                        foreach (var layer in layers)
                        {
                            layer.Parent = this;

                            var nodeId = useCustomIds && layer.nodeGuid != null && !Equals(layer.nodeGuid, "")
                                ? layer.nodeGuid
                                : rnd.NextGuid(layer.position).ToString();

                            var node = new InternalNode(nodeId, layer.position, Radius(layer),
                                NodeType.Of(NodeBaseType.Custom, type), null, (int) tileData.area.active.worldSize.x);

                            graph.StoreNode(node);
                        }
                    }
                }
            }

            var nodes = graph.NodesInRect(tileData.area.active.ToWorldRect(),
                new[] {NodeType.Of(NodeBaseType.Custom, type)});

            var transitions = new Den.Tools.TransitionsList();

            foreach (var node in nodes)
            {
                var position = node.Position();
                var transition = new Den.Tools.Transition(position.x, position.y.ToMapSpaceHeight(), position.z);
                transitions.Add(transition);
                transition.scale *= node.Radius();
            }

            tileData.StoreProduct(this, transitions);
            tileData.StoreProduct(outputNodes, new NodesByOffset(graph.NodeByOffset(NodeType.Of(NodeBaseType.Custom, type))));
            tileData.StoreProduct(outputGraph, new WorldGraphGuid(graph.guid));
        }

        public bool LayersHaveSameNodeGuid()
        {
            var types = new HashSet<string>();

            foreach (var layer in layers) Den.Tools.Extensions.CheckAdd(types, layer.nodeGuid);

            return types.Count != layers.Length;
        }

        public List<string> GetLocalTypes()
        {
            return new List<string> {type};
        }

        public float GetLocalMinDistanceToSame(string type)
        {
            var minDistance = float.PositiveInfinity;

            var conflicts = new HashSet<Layer>();

            foreach (var first in layers)
            foreach (var second in layers)
            {
                // reset
                first.hasConflict = false;

                if (first == second) continue;
                var distance = (first.position - second.position).magnitude;

                if (distance < 2 * GetLocalMaxRadiusRaw())
                {
                    conflicts.Add(first);
                    conflicts.Add(second);
                }

                minDistance = Mathf.Min(minDistance, distance);
            }

            foreach (var conflict in conflicts) conflict.hasConflict = true;

            return minDistance;
        }

        private float GetLocalMaxRadiusRaw()
        {
            if (!useRadiusPerLayer) return radius.ClampedValue;

            var maxRadius = 0f;

            foreach (var layer in layers)
            {
                maxRadius = Mathf.Max(maxRadius, layer.radius.ClampedValue);
            }

            return maxRadius;
        }

        public float GetLocalMinDistanceToOthers(string type)
        {
            var inputGenerators = new List<GraphGenerator>();
            GraphGeneratorHelper.CollectAllInputGraphGenerators(this, inputGenerators);

            var minDistance = float.PositiveInfinity;

            // no need to reset conflict here since GetLocalMinDistanceToSame already did that before
            var conflicts = new HashSet<Layer>();

            foreach (var gen in inputGenerators.OfType<ManualScatterGenerator>().Where(g => g != this))
            {
                foreach (var layer in layers)
                foreach (var other in gen.layers)
                {
                    var distance = (layer.position - other.position).magnitude;

                    if (distance < GetLocalMaxRadiusRaw() + gen.GetLocalMaxRadiusRaw()) conflicts.Add(layer);

                    minDistance = Mathf.Min(minDistance, distance);
                }
            }

            foreach (var conflict in conflicts) conflict.hasConflict = true;

            return minDistance;
        }

        public Vector2 GetLocalHeightRange(string type)
        {
            if (layers.Length == 0) return new Vector2(0f, ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.globals.height);

            var min = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.globals.height;
            var max = 0f;

            foreach (var layer in layers)
            {
                min = Mathf.Min(min, layer.position.y);
                max = Mathf.Max(max, layer.position.y);
            }

            return new Vector2(min, max);
        }

        public Vector2 GetLocalRadiusRange(string type)
        {
            return new Vector2(radius.ClampedValue, radius.ClampedValue);
        }

        public IInlet<WorldGraphGuid> GetInputGraph()
        {
            return inputGraph;
        }

        public IOutlet<WorldGraphGuid> GetOutputGraph()
        {
            return outputGraph;
        }

    }
}

#endif
