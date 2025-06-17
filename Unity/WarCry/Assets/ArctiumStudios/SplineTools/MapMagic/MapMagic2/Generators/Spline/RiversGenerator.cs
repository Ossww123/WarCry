#if ST_MM_2

using System;
using System.Collections.Generic;
using System.Linq;
using MapMagic.Nodes;
using MapMagic.Products;
using Den.Tools.Matrices;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Spline/Unstable",
        name = "Rivers & Lakes",
        disengageable = true,
        colorType = typeof(WorldGraphGuid),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/spline/rivers")]
    public class RiversGenerator : Generator, GraphGenerator, IMultiInlet, IMultiOutlet, ISerializationCallbackReceiver, EdgeGenerator
    {
        public string startingGraphGuid = Guid.NewGuid().ToString();

        public string[] Types()
        {
            return GraphGeneratorHelper.GetTypes(InputGraphGenerator()).ToArray();
        }

        public GraphGenerator InputGraphGenerator()
        {
            return MapMagicUtil.GetInputGraphGenerator(inputGraph);
        }

        public int springType;

        // rivers
        public ClampedFloat carveThroughHeightMin = new ClampedFloat(3f, 0f, float.MaxValue);
        public ClampedFloat carveThroughHeightMax;
        public AnimationCurve carveThroughHeightFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));
        public ClampedFloat widthMax = new ClampedFloat(15f, 0f, float.MaxValue);
        public ClampedFloat widthNoise = new ClampedFloat(0.3f, 0f, float.MaxValue);
        public ClampedFloat dryUpWidth = new ClampedFloat(3f, 0f, float.MaxValue);
        public ClampedFloat growthMax = new ClampedFloat(0.08f, 0f, float.MaxValue);
        public ClampedFloat decayMax = new ClampedFloat(0.04f, 0f, float.MaxValue);
        public ClampedFloat sectionLengthFactor = new ClampedFloat(2f, 0f, float.MaxValue);
        public ClampedFloat riverDirectionChangeAngleMin = new ClampedFloat(15f, 0f, 180f);
        public ClampedFloat riverDirectionChangeAngleMax;
        public AnimationCurve riverDirectionChangeAngleFalloff = new AnimationCurve(new Keyframe(0, 1, 1, 0), new Keyframe(1, 0, 0, 1));

        // joining rivers
        public ClampedFloat riverJoinAngleMax = new ClampedFloat(85f, 0f, 90f);
        public ClampedFloat riverJoinSlopeMax = new ClampedFloat(15f, 0f, 90f);
        public ClampedFloat riverJoinSlopeChangeMax = new ClampedFloat(15f, 0f, 90f);

        // lakes
        public RiverPathfinder.LakeMode lakeMode = RiverPathfinder.LakeMode.AllowStartWithLakes;
        public ClampedFloat lakeSizeFactor = new ClampedFloat(3f, 0f, float.MaxValue);
        public ClampedFloat lakeBorder = new ClampedFloat(10f, 0f, float.MaxValue);
        public ClampedFloat outflowingRiverAngleMin = new ClampedFloat(60f, 0f, 180f);
        public bool useSpringAsLakeSize = false;
        public ClampedFloat initialLakeSizeModifier = new ClampedFloat(1.5f, 0.1f, float.MaxValue);
        public AnimationCurve outflowingRiverSizeFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 1), new Keyframe(1, 1, 1, 1));
        // sea
        public ClampedFloat heightMin = new ClampedFloat(0f, 0f, float.MaxValue);
        public ClampedFloat requiredSeaDepth = new ClampedFloat(10f, 0f, float.MaxValue);
        public ClampedFloat seaJoinWidenFactor = new ClampedFloat(1.5f, 0f, float.MaxValue);
        public ClampedFloat seaBorder = new ClampedFloat(15f, 0f, float.MaxValue);

        public GizmoLevel usedGizmoLevel = GizmoLevel.Off;

        [SerializeField] private bool readOnly = false;

        public Inlet<WorldGraphGuid> inputGraph = new Inlet<WorldGraphGuid>();
        public Inlet<Bounds> inputBounds = new Inlet<Bounds>();
        public Inlet<MatrixWorld> inputHeights = new Inlet<MatrixWorld>();
        public Inlet<MatrixWorld> inputGrowthMask = new Inlet<MatrixWorld>();
        public Inlet<MatrixWorld> inputDecayMask = new Inlet<MatrixWorld>();
        
        public Outlet<WorldGraphGuid> outputGraph = new Outlet<WorldGraphGuid>();
        public Outlet<NodesByOffset> outputNodes = new Outlet<NodesByOffset>();
        public Outlet<EdgesByOffset> outputEdges = new Outlet<EdgesByOffset>();
        public Outlet<MatrixWorld> outputLakeMask = new Outlet<MatrixWorld>();
        public Outlet<MatrixWorld> outputSeaMask = new Outlet<MatrixWorld>();

        public RiversGenerator()
        {
            InitClampedDynamicValues(this);
        }

        public static void InitClampedDynamicValues(RiversGenerator gen)
        {
            gen.carveThroughHeightMax = new ClampedFloatDynamic(gen.carveThroughHeightMax?.value ?? 10, () => gen.carveThroughHeightMin.ClampedValue,
                () => float.MaxValue);
            gen.riverDirectionChangeAngleMax = new ClampedFloatDynamic(gen.riverDirectionChangeAngleMax?.value ?? 100f,
                () => gen.riverDirectionChangeAngleMin.ClampedValue, () => 180f);
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return outputGraph;
            yield return outputEdges;
            yield return outputNodes;
            yield return outputLakeMask;
            yield return outputSeaMask;
        }

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputGraph;
            yield return inputHeights;
            yield return inputBounds;
            yield return inputGrowthMask;
            yield return inputDecayMask;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            // return on stop/disable
            if (!enabled || tileData.isDraft || stop != null && stop.stop || MandatoryInputMissing(inputGraph, inputHeights, inputBounds))
            {
                tileData.StoreProduct(outputGraph, tileData.ReadInletProduct(inputGraph));
                tileData.StoreProduct(outputEdges, new EdgesByOffset());
                tileData.StoreProduct(outputLakeMask, new MatrixWorld(tileData.area.full.rect, tileData.area.full.worldPos,
                    tileData.area.full.worldSize, tileData.globals.height));
                tileData.StoreProduct(outputSeaMask, new MatrixWorld(tileData.area.full.rect, tileData.area.full.worldPos, tileData.area.full.worldSize,
                    tileData.globals.height));
                return;
            }
            
            readOnly = tileData is ReadOnlyTileData;

            var inputGraphGuid = tileData.ReadInletProduct(inputGraph);
            var graph = (InternalWorldGraph) SplineTools.Instance.state.Graph(inputGraphGuid.value);

            var worldBounds = tileData.ReadInletProduct(inputBounds).ToCoordRect(tileData.area);
            var growthMasks = MasksByOffset(inputGrowthMask, tileData, worldBounds, stop);
            var decayMasks = MasksByOffset(inputDecayMask, tileData, worldBounds, stop);

            if (!readOnly)
            {
                lock (graph)
                {
                    if (!graph.ProcessedGraphComponentsContains(startingGraphGuid))
                    {
                        graph.ProcessedGraphComponentsAdd(startingGraphGuid);

                        var relevantNodes = graph.Nodes(new[] {NodeType.Of(NodeBaseType.Custom, Types()[springType])});

                        var pathfinder = NewRiverPathfinder(graph, growthMasks, decayMasks, tileData, worldBounds, stop);

                        foreach (var node in relevantNodes)
                        {
                            // skip nodes that are near an already existing river
                            var nodesInRange = graph
                                .NodesInRange(node.PositionV2(), node.Radius() * 2 * sectionLengthFactor.ClampedValue * 2,
                                    new[] {NodeType.Of(NodeBaseType.RiverSection)})
                                .Where(n => !n.Equals(node))
                                .ToList();

                            if (nodesInRange.Count != 0) continue;

                            pathfinder.GenerateRiver(node);
                        }
                    }
                }
            }

            var connections = graph.ConnectionsByOffset(startingGraphGuid);
            var lakeNodesByOffset = connections.SelectMany(entry => entry.Value)
                .SelectMany(connection => connection.Nodes())
                .Where(node => node.Type().BaseType == NodeBaseType.Lake)
                .GroupBy(n => Offset.For(n.PositionV2(), InternalWorldGraph.OffsetResolution))
                .ToDictionary(e => e.Key, e => e.Distinct().ToList());

#if UNITY_EDITOR
            foreach (var entry in connections)
                entry.Value.SelectMany(c => c.Edges()).ToList().ForEach(edge => ((InternalEdge) edge).DrawGizmos(usedGizmoLevel));
#endif

            var lakeMask = GetBlendingMask(graph, NodeType.Of(NodeBaseType.Lake), lakeBorder.ClampedValue, tileData, stop);
            var seaMask = GetBlendingMask(graph, NodeType.Of(NodeBaseType.Sea), seaBorder.ClampedValue, tileData, stop);

            tileData.StoreProduct(outputGraph, new WorldGraphGuid(graph.guid));
            tileData.StoreProduct(outputEdges, new EdgesByOffset(connections));
            tileData.StoreProduct(outputNodes, new NodesByOffset(lakeNodesByOffset));
            tileData.StoreProduct(outputLakeMask, lakeMask);
            tileData.StoreProduct(outputSeaMask, seaMask);
        }

        private RiverPathfinder NewRiverPathfinder(InternalWorldGraph graph, Dictionary<Den.Tools.Coord, Matrix> growthMasks,
            Dictionary<Den.Tools.Coord, Matrix> decayMasks, TileData tileData, Den.Tools.CoordRect worldBounds, StopToken stop)
        {
            var options = new RiverPathfinder.Options()
            {
                decayMax = decayMax.ClampedValue,
                growthMax = growthMax.ClampedValue,
                widthMax = widthMax.ClampedValue,
                widthNoise = widthNoise.ClampedValue,
                dryUpWidth = dryUpWidth.ClampedValue,
                heightMin = heightMin.ClampedValue,
                heightMax = tileData.globals.height,
                resolution = (int) ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tileSize.x,
                carveThroughHeightMin = carveThroughHeightMin.ClampedValue,
                carveThroughHeightMax = carveThroughHeightMax.ClampedValue,
                carveThroughHeightFalloff = carveThroughHeightFalloff,
                lakeSizeFactor = lakeSizeFactor.ClampedValue,
                slopeMax = 89f,
                bezierControlPointAngleVariance = 90f,
                riverJoinAngleMax = riverJoinAngleMax.ClampedValue,
                riverJoinSlopeMax = riverJoinSlopeMax.ClampedValue,
                riverJoinSlopeChangeMax = riverJoinSlopeChangeMax.ClampedValue,
                outflowingRiverAngleMin = outflowingRiverAngleMin.ClampedValue,
                sectionLengthFactor = sectionLengthFactor.ClampedValue,
                riverDirectionChangeAngleMin = riverDirectionChangeAngleMin.ClampedValue,
                riverDirectionChangeAngleMax = riverDirectionChangeAngleMax.ClampedValue,
                riverDirectionChangeAngleFalloff = riverDirectionChangeAngleFalloff,
                lakeMode = lakeMode,
                useSpringAsLakeSize = useSpringAsLakeSize,
                initialLakeSizeModifier = initialLakeSizeModifier.ClampedValue,
                outflowingRiverSizeFalloff = outflowingRiverSizeFalloff,
                requiredSeaDepth = requiredSeaDepth.ClampedValue,
                seaJoinWidenFactor = seaJoinWidenFactor.ClampedValue
            };

            Func<Vector2, float> growthFunc = v =>
                growthMasks.GetMaskValue(v.ToMapSpaceCoord(tileData.area.active), tileData.area.active).ToWorldSpaceHeight();
            Func<Vector2, float> decayFunc = v =>
                decayMasks.GetMaskValue(v.ToMapSpaceCoord(tileData.area.active), tileData.area.active).ToWorldSpaceHeight();

            var pathfinder = new RiverPathfinder(graph, startingGraphGuid,
                v => HeightMapped(v.ToMapSpaceCoord(tileData.area.active), inputHeights, tileData, stop).ToWorldSpaceHeight(),
                options,
                growthMasks == null ? null : growthFunc,
                decayMasks == null ? null : decayFunc,
                worldBounds.ToWorldSpaceRect(tileData.area.active),
                () => stop != null && stop.stop);
            return pathfinder;
        }

        private Matrix GetBlendingMask(InternalWorldGraph graph, NodeType type, float border, TileData tileData, StopToken stop)
        {
            var mask = new MatrixWorld(tileData.area.full.rect, tileData.area.full.worldPos, tileData.area.full.worldSize, tileData.globals.height);
            var nodes = graph.Nodes(new[] {type});
            if (nodes.Count == 0) return mask;

            var maxLakeRadius = nodes.Max(n => n.Radius());

            var relevantRect = tileData.area.active.rect.Expanded(Mathf.CeilToInt(maxLakeRadius)).ToWorldSpaceRect(tileData.area.active);
            var lakes = graph.NodesInRect(relevantRect, new[] {type});

            foreach (var lake in lakes)
            {
                var lakePositionMapped = lake.PositionV2().ToMapSpace(tileData.area.active);
                var lakeRadiusMapped = lake.Radius().ToMapSpace(tileData.area.active);

                var toCornerMapped = new Vector2(lakeRadiusMapped, lakeRadiusMapped);
                var topLeftMapped = lakePositionMapped - toCornerMapped;
                var bottomRightMapped = lakePositionMapped + toCornerMapped;

                var outlineV2 = lake.GetData<Vector2>(Constants.LakeOutline).ToList();
                var outlineV2Mapped = outlineV2.Select(v => v.ToMapSpace(tileData.area.active)).ToList();

                for (var x = (int) topLeftMapped.x; x < bottomRightMapped.x; x++)
                for (var z = (int) topLeftMapped.y; z < bottomRightMapped.y; z++)
                {
                    // mapping to matrix cells is off by 1
                    var offsetX = x - 1;
                    var offsetZ = z - 1;

                    if (!tileData.area.active.rect.Contains(offsetX, offsetZ)) continue;

                    var v = new Vector2(x, z);

                    if (!v.InsidePolygon(outlineV2Mapped)) continue;

                    Vector2 closestMappedV2;
                    RiverPathfinder.FindClosest(v, outlineV2Mapped, out closestMappedV2);

                    mask[offsetX, offsetZ] = Mathf.Clamp01((v - closestMappedV2).magnitude / border);
                }
            }

            return mask;
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

        public void OnBeforeSerialize()
        {
            // noop
        }

        public void OnAfterDeserialize()
        {
            InitClampedDynamicValues(this);
        }

        public float MaxSectionLength()
        {
            return widthMax.ClampedValue * sectionLengthFactor.ClampedValue;
        }
    }
}

#endif
