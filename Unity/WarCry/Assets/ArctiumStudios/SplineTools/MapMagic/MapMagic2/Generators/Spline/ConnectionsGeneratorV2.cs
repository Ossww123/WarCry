#if ST_MM_2

using System;
using System.Collections.Generic;
using System.Linq;
using ArctiumStudios.SplineTools.Generators.Spline;
using Den.Tools.Matrices;
using Den.Tools.Tasks;
using MapMagic.Nodes;
using MapMagic.Products;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Spline", name = "Connections V2", disengageable = true, colorType = typeof(WorldGraphGuid),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/spline/connections_v2")]
    public class ConnectionsGeneratorV2 : LayeredGenerator<ConnectionsGeneratorV2.Layer>, GraphGenerator, IMultiInlet, IMultiOutlet, 
        IConnectionsGenerator
    {
        [Serializable]
        public class Layer : AbstractLayer, ISerializationCallbackReceiver
        {
            public string layerGuid = Guid.NewGuid().ToString();

            public string[] Types()
            {
                return GraphGeneratorHelper.GetTypes(InputGraphGenerator()).ToArray();
            }

            public GraphGenerator InputGraphGenerator()
            {
                return MapMagicUtil.GetInputGraphGenerator(((ConnectionsGeneratorV2) Parent).inputGraph);
            }

            // types[source]
            public int source;
            public bool useSourcePerimeter = false;
            public bool skipInsideSourceRadius = false;

            // types[destination]
            public int destination;
            public bool useDestinationPerimeter = false;
            public bool skipInsideDestinationRadius = false;

            public string connectionType = "Road";

            public bool showConfig = true;

            // road width
            public ClampedFloat widthMin;
            public ClampedFloat widthMax;
            public AnimationCurve widthFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));

            // used algorithm
            public ConnectionsV2Helper.Algorithm usedAlgorithm = ConnectionsV2Helper.Algorithm.Nearest;

            public ClampedFloat bezierSectionAngleVariance; // angle degrees, 0-180° 
            public ClampedFloat bezierControlPointAngleVariance; // angle degrees, 0-180° 
            public ClampedFloat bias;

            public ClampedVector2 bezierSectionLength = new ClampedVector2(new Vector2(25, 25), 1, float.MaxValue); // min - max length of a section

            // only connect from nodes that are not yet connected to the graph somehow
            public bool connectFromUnreachableOnly = false;

            // only connect to nodes that are connected to the graph somehow
            public bool connectToReachableOnly = false;

            // controls how many connections of the same type to the same node are allowed
            public ClampedInt connectionsMax;
            public bool connectionsLimit = false;

            // the minimum angle between connections, discard when too close
            public bool angleLimit = false;
            public bool angleLimitSource = true;
            public bool angleLimitDestination = true;
            public ClampedFloat angleMin; // 0° - 180°

            public bool preferDirectConnection = false;

            public ClampedInt iterations;

            // min and max distance between 'source' and 'destination' node, discard when out of range 
            public bool distanceLimit = false;
            public ClampedFloat distanceMin;
            public ClampedFloat distanceMax;

            // connect split clusters to get one fully connected graph
            public bool connectedGraph = false;

            // the max slope in degrees 0° - 80°
            public ClampedFloat slopeMax;

            // minimum allowed height for a connection, most likely above sea level
            public ClampedFloat heightMin;

            public ClampedInt lookahead;
            public ClampedInt pathfindingCandidates;

            public bool discardRedundantConnectionActive = true;
            public ClampedFloat discardRedundantConnectionFactor;

            // lake & river routing
            public bool lakeBridgeEnabled = true;
            public ClampedFloat lakeBridgeRoutingThreshold;
            public ClampedFloat lakeBridgeWidthThreshold;
            public ClampedFloat lakeBridgeOffset;
            public ClampedFloat lakePathOffset;
            public bool riverBridgeEnabled = true;
            public ClampedFloat riverBridgeWidthThreshold;
            public ClampedFloat riverBridgeSlopeThreshold;
            public ClampedFloat riverBridgeOffset;
            public ClampedFloat riverFordOffset;

            public GizmoLevel usedGizmoLevel = GizmoLevel.Off;

            public bool precache = false;

            public ClampedFloat nodeMaskThreshold;

            public Inlet<MatrixWorld> inputNodeMask = new Inlet<MatrixWorld>();
            public Outlet<EdgesByOffset> outputEdges = new Outlet<EdgesByOffset>();

            
            [NonSerialized] public Layer[] layersAbove;

            public Layer()
            {
                widthMin = new ClampedFloat(10, 0f, float.MaxValue);
                bezierSectionAngleVariance = new ClampedFloat(45f, 0f, 90f);
                bezierControlPointAngleVariance = new ClampedFloat(45f, 0f, 90f);
                bias = new ClampedFloat(0.2f, -0.99f, 0.99f);
                connectionsMax = new ClampedInt(5, 1, int.MaxValue);
                angleMin = new ClampedFloat(30f, 0f, 180f);
                iterations = new ClampedInt(1, 1, 5);
                distanceMin = new ClampedFloat(0f, 0f, float.MaxValue);
                heightMin = new ClampedFloat(0f, 0f, float.MaxValue);
                lookahead = new ClampedInt(2, 0, 100);
                pathfindingCandidates = new ClampedInt(10, 1, 1000);
                discardRedundantConnectionFactor = new ClampedFloat(10f, 1f, float.MaxValue);
                lakeBridgeRoutingThreshold = new ClampedFloat(1.5f, 0f, float.MaxValue);
                lakeBridgeWidthThreshold = new ClampedFloat(15f, 0f, float.MaxValue);
                lakeBridgeOffset = new ClampedFloat(5f, 0f, float.MaxValue);
                lakePathOffset = new ClampedFloat(10f, 0f, float.MaxValue);
                riverBridgeWidthThreshold = new ClampedFloat(10f, 0f, float.MaxValue);
                riverBridgeSlopeThreshold = new ClampedFloat(15f, 0f, float.MaxValue);
                riverBridgeOffset = new ClampedFloat(5f, 0f, float.MaxValue);
                riverFordOffset = new ClampedFloat(5f, 0f, float.MaxValue);
                nodeMaskThreshold = new ClampedFloat(0.9f, 0f, 1f);
                
                InitClampedDynamicValues(this);
            }

            public static void InitClampedDynamicValues(Layer layer)
            {
                layer.widthMax = new ClampedFloatDynamic(layer.widthMax?.value ?? 15, () => layer.widthMin.ClampedValue, () => float.MaxValue);
                layer.distanceMax =
                    new ClampedFloatDynamic(layer.distanceMax?.value ?? 100f, () => layer.distanceMin.ClampedValue, () => float.MaxValue);
                layer.slopeMax = new ClampedFloatDynamic(layer.slopeMax?.value ?? 45f,
                    () => ConnectionsV2Helper.MinSlope(layer.source, layer.destination, layer.InputGraphGenerator(), layer.Types()), () => 85f);
            }

            public bool Deferred()
            {
                return AllowDeferredProcessing() && !precache;
            }

            public Vector2 BezierSectionLength()
            {
                var max = ConnectionsV2Helper.MaxSectionLength(source, destination, InputGraphGenerator(), Types());
                var sectionLength = bezierSectionLength.ClampedValue;
                var clamped = new Vector2(Mathf.Clamp(sectionLength.x, 1, max), Mathf.Clamp(sectionLength.y, 1, max));
                return new Vector2(clamped.x, clamped.y);
            }

            public bool IsAroundType(string type)
            {
                return ConnectionsV2Helper.IsAroundType(type, InputGraphGenerator());
            }

            public bool AllowDeferredProcessing()
            {
                if (layersAbove != null && layersAbove.Any(l => !l.Deferred())) return false;

                return ConnectionsV2Helper.NodesBelongTogether(source, destination, InputGraphGenerator(), usedAlgorithm, Types());
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

        [SerializeField] private bool readOnly = false;
        [SerializeField] public int guiExpanded;
        
        public Inlet<WorldGraphGuid> inputGraph = new Inlet<WorldGraphGuid>();
        public Inlet<MatrixWorld> inputHeights = new Inlet<MatrixWorld>();
        public Inlet<WorldGraphGuid> inputRiverGraph = new Inlet<WorldGraphGuid>();
        public Outlet<WorldGraphGuid> outputGraph = new Outlet<WorldGraphGuid>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputGraph;
            yield return inputHeights;
            yield return inputRiverGraph;
            foreach (var layer in layers) yield return layer.inputNodeMask;
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return outputGraph;
            foreach (var layer in layers) yield return layer.outputEdges;
        }

        public override LayeredGenerator Init()
        {
            for (var index = 0; index < layers.Length; index++)
            {
                var layer = layers[index];
                layer.layersAbove = layers.Where(l => Den.Tools.ArrayTools.Find(Layers, l) > index).ToArray();
            }

            return base.Init();
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            Init();

            if (!enabled || tileData.isDraft || stop != null && stop.stop || layers.Length == 0 || MandatoryInputMissing(inputGraph, inputHeights))
            {
                if (tileData.ReadInletProduct(inputGraph) != null) tileData.StoreProduct(outputGraph, tileData.ReadInletProduct(inputGraph));
                foreach (var layer in layers) tileData.StoreProduct(layer.outputEdges, new EdgesByOffset());
                return;
            }
            
            readOnly = tileData is ReadOnlyTileData;

            var inputGraphGuid = tileData.ReadInletProduct(inputGraph)?.value;
            var riverGraphGuid = tileData.ReadInletProduct(inputRiverGraph)?.value;
            var state = SplineTools.Instance.state;

            var graph = (InternalWorldGraph) state.Graph(inputGraphGuid);
            var riverGraph = riverGraphGuid == null ? null : (InternalWorldGraph) state.Graph(riverGraphGuid);

            if (!readOnly)
            {
                lock (graph)
                {
                    // generate edges
                    foreach (var layer in layers)
                    {
                        layer.Parent = this;

                        var connectionFactory = NewConnectionFactory(layer, graph, riverGraph, tileData, stop);

                        ConnectionsV2Helper.GenerateConnections(tileData.area.active.rect, graph, layer.layerGuid, 
                            tileData.area.active.worldPos.ToVector2(), layer.Types(),
                            layer.source, layer.destination, layer.Deferred(), MapMagicUtil.GetInputGraphGenerator(inputGraph),
                            layer.useSourcePerimeter, layer.useDestinationPerimeter, layer.connectionType, layer.usedAlgorithm,
                            connectionFactory, layer.iterations.ClampedValue, layer.connectedGraph, f => f.ToMapSpace(tileData.area.active),
                            v2 => v2.ToMapSpace(tileData.area.active));
                    }
                }
            }

            foreach (var layer in layers)
            {
                var connections = graph.ConnectionsByOffset(layer.layerGuid);

#if UNITY_EDITOR
                foreach (var entry in connections)
                    entry.Value.SelectMany(c => c.Edges()).ToList().ForEach(edge => ((InternalEdge) edge).DrawGizmos(layer.usedGizmoLevel));
#endif

                tileData.StoreProduct(layer.outputEdges, new EdgesByOffset(connections));
            }

            tileData.StoreProduct(outputGraph, new WorldGraphGuid(graph.guid));
        }

        

        private ConnectionFactory NewConnectionFactory(Layer layer, InternalWorldGraph graph, InternalWorldGraph riverGraph,
            TileData data, StopToken stop)
        {
            var options = new ConnectionFactory.Options()
            {
                connectionsLimit = layer.connectionsLimit,
                distanceLimit = layer.distanceLimit,
                connectionsMax = layer.connectionsMax.ClampedValue,
                distanceMax = layer.distanceMax.ClampedValue,
                distanceMin = layer.distanceMin.ClampedValue,
                connectFromUnreachableOnly = layer.connectFromUnreachableOnly,
                connectToReachableOnly = layer.connectToReachableOnly,
                angleLimit = layer.angleLimit,
                angleLimitSource = layer.angleLimitSource,
                angleLimitDestination = layer.angleLimitDestination,
                angleMin = layer.angleMin.ClampedValue,
                nodesMustBelongTogether = ConnectionsV2Helper.NodesBelongTogether(layer.source, layer.destination,
                    layer.InputGraphGenerator(), layer.usedAlgorithm, layer.Types())
            };

            var pathFinderOptions = new ConnectionPathfinderV2.Options()
            {
                bias = layer.bias.ClampedValue,
                lookahead = layer.lookahead.ClampedValue,
                widthMin = layer.widthMin.ClampedValue,
                widthMax = layer.widthMax.ClampedValue,
                widthFalloff = layer.widthFalloff,
                slopeMax = layer.slopeMax.ClampedValue,
                heightMin = layer.heightMin.ClampedValue,
                heightMax = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.globals.height,
                resolution = (int) ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tileSize.x,
                pathfindingCandidates = layer.pathfindingCandidates.ClampedValue,
                bezierSectionLength = layer.BezierSectionLength(),
                nodeMaskThreshold = layer.nodeMaskThreshold.ClampedValue,
                bezierSectionAngleVariance = layer.bezierSectionAngleVariance.ClampedValue,
                bezierControlPointAngleVariance = layer.bezierControlPointAngleVariance.ClampedValue,
                skipInsideSourceRadius = layer.skipInsideSourceRadius,
                skipInsideDestinationRadius = layer.skipInsideDestinationRadius,
                preferDirectConnection = layer.preferDirectConnection,
                discardRedundantConnectionActive = layer.discardRedundantConnectionActive,
                discardRedundantConnectionFactor = layer.discardRedundantConnectionFactor.ClampedValue,
                lakeBridgeEnabled = layer.lakeBridgeEnabled,
                lakeBridgeRoutingThreshold = layer.lakeBridgeRoutingThreshold.ClampedValue,
                lakeBridgeWidthThreshold = layer.lakeBridgeWidthThreshold.ClampedValue,
                lakeBridgeOffset = layer.lakeBridgeOffset.ClampedValue,
                riverBridgeEnabled = layer.riverBridgeEnabled,
                riverBridgeWidthThreshold = layer.riverBridgeWidthThreshold.ClampedValue,
                riverBridgeSlopeThreshold = layer.riverBridgeSlopeThreshold.ClampedValue,
                riverBridgeOffset = layer.riverBridgeOffset.ClampedValue,
                riverFordOffset = layer.riverFordOffset.ClampedValue,
                lakePathOffset = layer.lakePathOffset.ClampedValue
            };

            Func<Vector2, float> nodeMaskFunc = v =>
                SecondaryMaskMapped(v.ToMapSpaceCoord(data.area.active), layer.inputNodeMask, data, stop);

            var pathfinder = new ConnectionPathfinderV2(graph, layer.layerGuid,
                v => HeightMapped(v.ToMapSpaceCoord(data.area.active), inputHeights, data, stop).ToWorldSpaceHeight(),
                riverGraph,
                (source, destination) => PrecacheHeightMapsBetween(source.PositionV2(), destination.PositionV2(), data, stop),
                pathFinderOptions,
                MapMagicUtil.GetInputGenerator(layer.inputNodeMask) == null ? null : nodeMaskFunc,
                () => stop != null && stop.stop);

            return new ConnectionFactory(options, pathfinder, () => stop != null && stop.stop);
        }

        private void PrecacheHeightMapsBetween(Vector2 sourcePosition, Vector2 destinationPosition, TileData data, StopToken stop)
        {
            // precache all height maps that we most presumably will need on the way
            if (!ThreadManager.useMultithreading || ThreadManager.maxThreads <= 1) return;

            var touchedRects = MapMagicUtil.GetTouchedCoordRects(sourcePosition.ToMapSpaceCoord(data.area.active),
                destinationPosition.ToMapSpaceCoord(data.area.active), LowestPossibleTileResolution(data));

            if (touchedRects.Count <= 1) return;

            CacheHeightMaps(touchedRects, inputHeights, data, false, stop);
        }

        public List<string> GetLocalTypes()
        {
            return new List<string>();
        }

        public float GetLocalMinDistanceToSame(string type)
        {
            return 0;
        }

        public float GetLocalMinDistanceToOthers(string type)
        {
            return 0;
        }

        public Vector2 GetLocalHeightRange(string type)
        {
            return Vector2.up;
        }

        public Vector2 GetLocalRadiusRange(string type)
        {
            return Vector2.zero;
        }

        public IInlet<WorldGraphGuid> GetInputGraph()
        {
            return inputGraph;
        }

        public IOutlet<WorldGraphGuid> GetOutputGraph()
        {
            return outputGraph;
        }

        public float MaxSectionLength()
        {
            return layers.Max(l => l.BezierSectionLength().y);
        }
    }
}

#endif
