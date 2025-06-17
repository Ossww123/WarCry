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
    [GeneratorMenu(
        menu = "SplineTools/Objects",
        name = "Bounded Scatter V2",
        priority = 10,
        disengageable = true,
        iconName = "GeneratorIcons/Scatter",
        colorType = typeof(WorldGraphGuid),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/object/bounded_scatter_v2")]
    public class BoundedScatterGeneratorV2 : LayeredGenerator<BoundedScatterGeneratorV2.Layer>, GraphGenerator, IMultiInlet,
        IMultiOutlet, IBoundedScatterGenerator
    {
        [Serializable]
        public class Layer : AbstractLayer, ISerializationCallbackReceiver
        {
            public int seed;
            
            public ClampedInt countMax;
            public ClampedInt iterations;

            public ClampedFloat safeBorders;

            public AnimationCurve radiusFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));
            public ClampedFloat radiusMin;
            public ClampedFloat radiusMax;

            public Placement placement = Placement.Random;

            public bool distanceLimit = true;
            public ClampedFloat distanceToSameMin;
            public ClampedFloat distanceToOthersMin;

            public bool heightLimit = false;
            public ClampedFloat heightMin;
            public ClampedFloat heightMax;
            public string type = "Layer";

            public bool deviationLimit = false;
            public ClampedFloat deviationUpMax;
            public ClampedFloat deviationDownMax;
            public ClampedInt deviationResolution;

            public ClampedFloat distanceToLakePerimeterMin;
            public ClampedFloat distanceToRiverMin;

            [NonSerialized] public Layer[] layersBelow;
            
            public Inlet<MatrixWorld> inputMask = new Inlet<MatrixWorld>();
            public Outlet<NodesByOffset> outputNodes = new Outlet<NodesByOffset>();
            public Outlet<Den.Tools.TransitionsList> outputTransitionsList = new Outlet<Den.Tools.TransitionsList>();

            public Layer() : this(12345)
            {
            }

            public Layer(int seed)
            {
                this.seed = seed;
                countMax = new ClampedInt(10, 1, int.MaxValue);
                iterations = new ClampedInt(10, 10, 100);
                safeBorders = new ClampedFloat(50f, 0f, float.MaxValue);
                radiusMin = new ClampedFloat(40f, 2f, float.MaxValue);
                deviationUpMax = new ClampedFloat(5f, 0f, float.MaxValue);
                deviationDownMax = new ClampedFloat(5f, 0f, float.MaxValue);
                deviationResolution = new ClampedInt(2, 0, int.MaxValue);

                InitClampedDynamicValues(this);
            }

            public static void InitClampedDynamicValues(Layer layer)
            {
                layer.radiusMax = new ClampedFloatDynamic(layer.radiusMax?.value ?? 50f, () => layer.radiusMin.ClampedValue, () => float.MaxValue);
                layer.distanceToSameMin =
                    new ClampedFloatDynamic(layer.distanceToSameMin?.value ?? 400, layer.GetMinDistanceToSame, () => float.MaxValue);
                layer.distanceToOthersMin =
                    new ClampedFloatDynamic(layer.distanceToOthersMin?.value ?? 400, layer.GetMinDistanceToOthers, () => float.MaxValue);
                layer.heightMin = new ClampedFloatDynamic(layer.heightMin?.value ?? 0, () => 0,
                    () => ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.globals.height);
                layer.heightMax = new ClampedFloatDynamic(layer.heightMax?.value ?? 300, () => layer.heightMin.ClampedValue,
                    () => ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.globals.height);
                layer.distanceToLakePerimeterMin =
                    new ClampedFloatDynamic(layer.distanceToLakePerimeterMin?.value ?? 50f, () => 0, () => float.MaxValue);
                layer.distanceToRiverMin = new ClampedFloatDynamic(layer.distanceToRiverMin?.value ?? 50f, () => 0, () => float.MaxValue);
            }

            public float HeightMin()
            {
                return heightLimit ? heightMin.ClampedValue : 0f;
            }

            public float HeightMax()
            {
                return heightLimit ? heightMax.ClampedValue : ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.globals.height;
            }

            public float GetMinDistanceToSame()
            {
                var minDistanceForSlope = Mathf.Tan(5 * Mathf.Deg2Rad) * (HeightMax() - HeightMin());

                return Mathf.Ceil(2 * radiusMax.ClampedValue + minDistanceForSlope);
            }

            public float GetMinDistanceToOthers()
            {
                var localMin = layersBelow == null || layersBelow.Length == 0
                    ? -1
                    : layersBelow.Max(l =>
                    {
                        var maxHeightDifference = Mathf.Max(HeightMax() - l.HeightMin(), l.HeightMax() - HeightMin());
                        var minDistanceForSlope = Mathf.Tan(5 * Mathf.Deg2Rad) * maxHeightDifference;

                        return l.radiusMax.ClampedValue + radiusMax.ClampedValue + minDistanceForSlope;
                    });

                var inputGenerators = new List<GraphGenerator>();
                GraphGeneratorHelper.CollectAllInputGraphGenerators((GraphGenerator) Parent, inputGenerators);

                if (inputGenerators.Count <= 1 && localMin < 1) return -1;

                var inputMin = inputGenerators.Count <= 1
                    ? 0
                    : inputGenerators.Where(g => g != Parent).Max(g => g.GetLocalTypes().Max(t =>
                    {
                        var maxHeightDifference = Mathf.Max(HeightMax() - g.GetLocalHeightRange(t).x, g.GetLocalHeightRange(t).y - HeightMin());
                        var minDistanceForSlope = Mathf.Tan(5 * Mathf.Deg2Rad) * maxHeightDifference;

                        return g.GetLocalRadiusRange(t).y + radiusMax.ClampedValue + minDistanceForSlope;
                    }));

                return Mathf.Ceil(Mathf.Max(localMin, inputMin));
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

        public string startingGraphGuid = Guid.NewGuid().ToString();
        [SerializeField] private bool readOnly = false;
        [SerializeField] public int guiExpanded;

        public Inlet<WorldGraphGuid> inputGraph = new Inlet<WorldGraphGuid>();
        public Inlet<Bounds> inputBounds = new Inlet<Bounds>();
        public Inlet<MatrixWorld> inputHeights = new Inlet<MatrixWorld>();
        public Inlet<WorldGraphGuid> inputRiverGraph = new Inlet<WorldGraphGuid>();

        public Outlet<WorldGraphGuid> outputGraph = new Outlet<WorldGraphGuid>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputGraph;
            yield return inputBounds;
            yield return inputHeights;
            yield return inputRiverGraph;
            foreach (var layer in layers) yield return layer.inputMask;
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return outputGraph;
            foreach (var layer in layers)
            {
                yield return layer.outputNodes;
                yield return layer.outputTransitionsList;
            }
        }

        public override LayeredGenerator Init()
        {
            for (var index = 0; index < Layers.Length; index++)
            {
                var layer = Layers[index];
                layer.layersBelow = Layers.Where(l => Den.Tools.ArrayTools.Find(Layers, l) < index).ToArray();
            }

            return base.Init();
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            Init();
            
            if (!enabled || tileData.isDraft || stop != null && stop.stop || Layers.Length == 0 
                || MandatoryInputMissing(inputBounds, inputHeights)) return;

            readOnly = tileData is ReadOnlyTileData;

            var worldBounds = tileData.ReadInletProduct(inputBounds);

            var inputGraphGuid = tileData.ReadInletProduct(inputGraph) != null ? tileData.ReadInletProduct(inputGraph).value : startingGraphGuid;
            var graph = (InternalWorldGraph) SplineTools.Instance.state.Graph(inputGraphGuid);

            var riverGraphGuid = tileData.ReadInletProduct(inputRiverGraph);
            var riverGraph = riverGraphGuid == null ? null : (InternalWorldGraph) SplineTools.Instance.state.Graph(riverGraphGuid.value);

            // create graph nodes
            if (!readOnly)
            {
                lock (graph)
                {
                    if (!graph.ProcessedGraphComponentsContains(startingGraphGuid))
                    {
                        graph.ProcessedGraphComponentsAdd(startingGraphGuid);

                        foreach (var layer in Layers)
                        {
                            layer.Parent = this;

                            var scatterer = NewScatterer(tileData, stop, layer, graph, riverGraph);

                            var type = NodeType.Of(NodeBaseType.Custom, layer.type);
                            scatterer.GlobalRandomScatter(worldBounds.rect, type, null);
                        }
                    }
                }
            }

            // processing
            foreach (var layer in Layers)
            {
                var layerCopy = layer;

                // get nodes for current rect
                var nodes = graph.NodesInRect(tileData.area.active.ToWorldRect(),
                    new[] {NodeType.Of(NodeBaseType.Custom, layerCopy.type)});

                var transitions = new Den.Tools.TransitionsList();

                foreach (var node in nodes)
                {
                    var p = node.Position();
                    var transition = new Den.Tools.Transition(p.x, p.y.ToMapSpaceHeight(), p.z);
                    transition.scale *= node.Radius();
                    transitions.Add(transition);
                }

                tileData.StoreProduct(layer.outputTransitionsList, transitions);
                tileData.StoreProduct(layer.outputNodes, new NodesByOffset(graph.NodeByOffset(NodeType.Of(NodeBaseType.Custom, layerCopy.type))));
            }

            tileData.StoreProduct(outputGraph, new WorldGraphGuid(graph.guid));
        }

        private ScattererV2 NewScatterer(TileData data, StopToken stop, Layer layer, InternalWorldGraph graph, InternalWorldGraph riverGraph)
        {
            var options = new ScattererV2.Options((int) ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tileSize.x)
            {
                iterations = layer.iterations.ClampedValue,
                placement = layer.placement,
                distanceLimit = layer.distanceLimit,
                countMax = layer.countMax.ClampedValue,
                heightMin = layer.HeightMin(),
                heightMax = layer.HeightMax(),
                radiusMin = layer.radiusMin.ClampedValue,
                radiusMax = layer.radiusMax.ClampedValue,
                radiusFalloff = layer.radiusFalloff,
                safeBorders = layer.safeBorders.ClampedValue,
                distanceToOthersMin = layer.distanceToOthersMin.ClampedValue,
                distanceToSameMin = layer.distanceToSameMin.ClampedValue,
                deviationLimit = layer.deviationLimit,
                deviationDownMax = layer.deviationDownMax.ClampedValue,
                deviationUpMax = layer.deviationUpMax.ClampedValue,
                deviationResolution = layer.deviationResolution.ClampedValue,
                distanceToRiverMin = layer.distanceToRiverMin.ClampedValue,
                distanceToLakePerimeterMin = layer.distanceToLakePerimeterMin.ClampedValue
            };

            Func<Vector2, float> maskFunc = v => SecondaryMaskMapped(v.ToMapSpaceCoord(data.area.active), layer.inputMask, data, stop);

            var scatterer = new ScattererV2(options, graph, riverGraph,
                v => HeightMapped(v.ToMapSpaceCoord(data.area.active), inputHeights, data, stop).ToWorldSpaceHeight(),
                MapMagicUtil.GetInputGenerator(layer.inputMask) == null ? null : maskFunc,
                data.random.Seed + layer.seed, // globally unique -> don't include seed from current rect
                () => stop != null && stop.stop);
            return scatterer;
        }

        private bool LayersHaveSameType()
        {
            var types = new HashSet<string>();

            foreach (var layer in Layers) Den.Tools.Extensions.CheckAdd(types, layer.type);

            return types.Count != Layers.Length;
        }

        public List<string> GetLocalTypes()
        {
            return Layers.Select(layer => layer.type).ToList();
        }

        public float GetLocalMinDistanceToSame(string type)
        {
            return Layers.First(layer => layer.type.Equals(type)).distanceToSameMin.ClampedValue;
        }

        public float GetLocalMinDistanceToOthers(string type)
        {
            return Layers.First(layer => layer.type.Equals(type)).distanceToOthersMin.ClampedValue;
        }

        public Vector2 GetLocalHeightRange(string type)
        {
            var l = Layers.First(layer => layer.type.Equals(type));
            return new Vector2(l.HeightMin(), l.HeightMax());
        }

        public Vector2 GetLocalRadiusRange(string type)
        {
            var layer = Layers.First(l => l.type.Equals(type));
            return new Vector2(layer.radiusMin.ClampedValue, layer.radiusMax.ClampedValue);
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
