#if ST_MM_2

using System;
using System.Collections.Generic;
using System.Linq;
using Den.Tools.Matrices;
using MapMagic.Nodes;
using MapMagic.Products;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Objects",
        name = "Radius Scatter",
        priority = 10,
        disengageable = true,
        iconName = "GeneratorIcons/Scatter",
        colorType = typeof(WorldGraphGuid),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/object/radius_scatter")]
    public class RadiusScatterGenerator : Generator, IOutlet<Den.Tools.TransitionsList>, IMultiInlet, IMultiOutlet,
        ISerializationCallbackReceiver, IRadiusScatterGenerator
    {
        public string startingGraphGuid = Guid.NewGuid().ToString();
        public int seed = 12345;
        [SerializeField] private bool readOnly = false;
        public string type = "Around";
        public int aroundType;

        public ClampedFloat outerMargin;

        public AnimationCurve radiusFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));
        public ClampedFloat radiusMin;
        public ClampedFloat radiusMax;

        public ClampedInt countMax;

        public bool distanceLimit = true;
        public ClampedFloat distanceToSameMin;
        public ClampedFloat distanceToOthersMin;

        public bool precache = false;

        private bool Deferred()
        {
            return AllowDeferredProcessing() && !precache;
        }

        public Inlet<WorldGraphGuid> inputGraph = new Inlet<WorldGraphGuid>();
        public Inlet<MatrixWorld> inputHeights = new Inlet<MatrixWorld>();
        public Inlet<MatrixWorld> inputMask = new Inlet<MatrixWorld>();
        public Outlet<WorldGraphGuid> outputGraph = new Outlet<WorldGraphGuid>();
        public Outlet<NodesByOffset> outputNodes = new Outlet<NodesByOffset>();

        public RadiusScatterGenerator()
        {
            radiusMin = new ClampedFloat(10f, 2f, float.MaxValue);
            countMax = new ClampedInt(10, 0, int.MaxValue);

            InitClampedDynamicValues(this);
        }

        public static void InitClampedDynamicValues(RadiusScatterGenerator gen)
        {
            gen.outerMargin = new ClampedFloatDynamic(gen.outerMargin?.value ?? 10f, () => 0, () => FindRadiusRange(gen, gen.aroundType).x);
            gen.radiusMax = new ClampedFloatDynamic(gen.radiusMax?.value ?? 10f, () => gen.radiusMin.ClampedValue, () => float.MaxValue);
            gen.distanceToSameMin =
                new ClampedFloatDynamic(gen.distanceToSameMin?.value ?? 20f, () => gen.radiusMax.ClampedValue * 2, () => float.MaxValue);
            gen.distanceToOthersMin = new ClampedFloatDynamic(gen.distanceToOthersMin?.value ?? 20f, () => gen.radiusMax.ClampedValue * 2,
                () => float.MaxValue);
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return outputGraph;
            yield return outputNodes;
        }

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputGraph;
            yield return inputHeights;
            yield return inputMask;
        }

        public string[] Types()
        {
            return GraphGeneratorHelper.GetTypes(InputGraphGenerator()).ToArray();
        }

        public GraphGenerator InputGraphGenerator()
        {
            return MapMagicUtil.GetInputGraphGenerator(inputGraph);
        }

        private float MaxPossibleScatterRadius(GraphGenerator inputGraphGenerator)
        {
            return FindRadiusRange(inputGraphGenerator, aroundType).y - outerMargin.ClampedValue;
        }

        private float Radius(ConsistentRandom rnd)
        {
            return rnd.NextFloat(radiusMin.ClampedValue, radiusMax.ClampedValue);
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            var transitions = new Den.Tools.TransitionsList();
            tileData.StoreProduct(this, transitions);
            var inputGraphGuid = tileData.ReadInletProduct(inputGraph);

            // return on stop/disable
            if (!enabled || tileData.isDraft || stop != null && stop.stop || MandatoryInputMissing(inputGraph, inputHeights)
                || inputGraphGuid == null)
            {
                if (MapMagicUtil.GetInputGenerator(inputGraph) != null) tileData.StoreProduct(outputGraph, tileData.ReadInletProduct(inputGraph));
                return;
            }

            readOnly = tileData is ReadOnlyTileData;

            var graph = (InternalWorldGraph) SplineTools.Instance.state.Graph(inputGraphGuid.value);
            var inputGraphGenerator = MapMagicUtil.GetInputGraphGenerator(inputGraph);
            var types = Types();

            if (!readOnly)
            {
                lock (graph)
                {
                    if (!graph.ProcessedGraphComponentsContains(startingGraphGuid + tileData.area.active.rect))
                    {
                        graph.ProcessedGraphComponentsAdd(startingGraphGuid + tileData.area.active.rect);

                        HashSet<Node> relevantNodes;

                        if (Deferred())
                        {
                            var relevantRect = tileData.area.active.rect.Expanded(Mathf.CeilToInt(MaxPossibleScatterRadius(inputGraphGenerator)
                                    .ToMapSpace(tileData.area.active)))
                                .ToWorldSpaceRect(tileData.area.active);
                            relevantNodes = graph.NodesInRect(relevantRect, new[] {NodeType.Of(NodeBaseType.Custom, types[aroundType])});
                        } else
                        {
                            relevantNodes = graph.Nodes(new[] {NodeType.Of(NodeBaseType.Custom, types[aroundType])});
                        }

                        Log.Debug(this, () => types[aroundType] + " -> " + Log.LogCollection(relevantNodes));

                        var nodeTypes = Enumerable.ToArray(graph.NodeTypes());

                        foreach (var node in relevantNodes)
                        {
                            var minScatterRadius = distanceToOthersMin.ClampedValue;
                            var maxScatterRadius = node.Radius() - outerMargin.ClampedValue;

                            Log.Debug(this, () => "Scatter around " + node);

                            // node can be relevant for multiple rects, so they could already be processed -> track processed nodes
                            if (graph.ProcessedGraphComponentsContains(startingGraphGuid + node.Guid())) continue;
                            graph.ProcessedGraphComponentsAdd(startingGraphGuid + node.Guid());

                            // to ensure deterministic generation the random must be set per node
                            var scatterer = NewScatterer(tileData, stop, (InternalNode) node, minScatterRadius, maxScatterRadius, graph, null);

                            var radiusVector = new Vector2(maxScatterRadius, maxScatterRadius);
                            var offset = node.PositionV2() - radiusVector;
                            var rect = new Rect(offset, radiusVector * 2);

                            scatterer.GlobalRandomScatter(rect, NodeType.Of(NodeBaseType.Custom, type), node);
                        }
                    }
                }
            }

            var nodes = graph.NodesInRect(tileData.area.active.ToWorldRect(), new[] {NodeType.Of(NodeBaseType.Custom, type)});

            foreach (var node in nodes)

            {
                var p = node.Position();
                var transition = new Den.Tools.Transition(p.x, p.y.ToMapSpaceHeight(), p.z);
                transition.scale *= node.Radius();
                transitions.Add(transition);
            }

            tileData.StoreProduct(this, transitions);
            tileData.StoreProduct(outputGraph, new WorldGraphGuid(graph.guid));
            tileData.StoreProduct(outputNodes, new NodesByOffset(graph.NodeByOffset(NodeType.Of(NodeBaseType.Custom, type))));
        }

        private ScattererV2 NewScatterer(TileData data, StopToken stop, InternalNode node, float minScatterRadius, float maxScatterRadius,
            InternalWorldGraph graph, InternalWorldGraph riverGraph)
        {
            var options = new ScattererV2.Options((int) ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tileSize.x)
            {
                iterations = 10,
                placement = Placement.Random,
                distanceLimit = distanceLimit,
                countMax = countMax.ClampedValue,
                heightMin = 0,
                heightMax = data.globals.height,
                radiusMin = radiusMin.ClampedValue,
                radiusMax = radiusMax.ClampedValue,
                radiusFalloff = radiusFalloff,
                safeBorders = 0f,
                distanceToOthersMin = distanceToOthersMin.ClampedValue,
                distanceToSameMin = distanceToSameMin.ClampedValue,
                deviationLimit = false,
                deviationDownMax = float.MaxValue,
                deviationUpMax = float.MaxValue,
                deviationResolution = 2,
                distanceToRiverMin = 0f,
                distanceToLakePerimeterMin = 0f
            };

            Func<Vector2, float> maskFunc = v =>
            {
                // discard points out of specified range
                var distance = (v - node.PositionV2()).magnitude;

                if (distance < minScatterRadius || distance > maxScatterRadius)
                    return 0f;

                return SecondaryMaskMapped(v.ToMapSpaceCoord(data.area.active), inputMask, data, stop);
            };

            var scatterer = new ScattererV2(options, graph, riverGraph,
                v => HeightMapped(v.ToMapSpaceCoord(data.area.active), inputHeights, data, stop).ToWorldSpaceHeight(),
                MapMagicUtil.GetInputGenerator(inputMask) == null ? null : maskFunc,
                data.random.Seed + seed + node.Seed(), // globally unique -> don't include seed from current rect
                () => stop != null && stop.stop);
            return scatterer;
        }

        public bool AllowDeferredProcessing()
        {
            var connectionsGenerators = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph.generators
                .OfType<ConnectionsGeneratorV2>()
                .Where(gen =>
                {
                    var gens = new List<GraphGenerator>();
                    GraphGeneratorHelper.CollectAllInputGraphGenerators(gen, gens);
                    return gens.Contains(this);
                })
                .ToList();

            if (connectionsGenerators.Count == 0) return true;

            // check if any of the connected Connections generators uses precaching
            return !connectionsGenerators
                .SelectMany(gen => gen.layers)
                .Any(layer => !layer.Deferred() && (layer.Types()[layer.source].Equals(type) || layer.Types()[layer.destination].Equals(type)));
        }

        public static Vector2 FindRadiusRange(GraphGenerator inputGraphGenerator, int aroundType)
        {
            if (inputGraphGenerator == null) return new Vector2(float.MaxValue, float.MaxValue);
            var types = GraphGeneratorHelper.GetTypes(inputGraphGenerator).ToArray();
            return GraphGeneratorHelper.GetRadiusRange(inputGraphGenerator, types[aroundType]);
        }

        public float OuterMargin()
        {
            return outerMargin.ClampedValue;
        }

        public string GetAroundType()
        {
            var types = Types();
            return types[Mathf.Max(aroundType, types.Length - 1)];
        }


        public List<string> GetLocalTypes()
        {
            return new List<string> {type};
        }

        public float GetLocalMinDistanceToSame(string type)
        {
            return distanceToSameMin.ClampedValue;
        }

        public float GetLocalMinDistanceToOthers(string type)
        {
            return distanceToOthersMin.ClampedValue;
        }

        public Vector2 GetLocalHeightRange(string type)
        {
            var types = Types();
            return types != null
                ? GraphGeneratorHelper.GetHeightRange(MapMagicUtil.GetInputGraphGenerator(inputGraph), types[aroundType])
                : Vector2.zero;
        }

        public Vector2 GetLocalRadiusRange(string type)
        {
            return new Vector2(radiusMin.ClampedValue, radiusMax.ClampedValue);
        }

        public IInlet<WorldGraphGuid> GetInputGraph()
        {
            return inputGraph;
        }

        public IOutlet<WorldGraphGuid> GetOutputGraph()
        {
            return outputGraph;
        }

        public void OnBeforeSerialize()
        {
            // noop
        }

        public void OnAfterDeserialize()
        {
            InitClampedDynamicValues(this);
        }
    }
}

#endif
