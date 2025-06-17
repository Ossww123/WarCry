#if ST_MM_2

using System;
using System.Collections.Generic;
using Den.Tools.Matrices;
using MapMagic.Nodes;
using MapMagic.Products;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Map/Legacy",
        name = "Flatten Stamp (Legacy)",
        priority = -99,
        disengageable = true,
        colorType = typeof(MatrixWorld),
        iconName = "GeneratorIcons/Flatten",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/map/flatten_stamp")]
    public class StampFlattenGenerator : LayeredGenerator<StampFlattenGenerator.Layer>, IInlet<MatrixWorld>, IMultiInlet, IOutlet<MatrixWorld>,
        IMultiOutlet
    {
        [Serializable]
        public class Layer : AbstractLayer
        {
            public float varianceOffset = 0f;
            public Vector2 rotationRange = new Vector2(0, 360);
            public ClampedFloat chance = new ClampedFloat(1f, 0f, 1f);

            public Inlet<MatrixWorld> inputVariance = new Inlet<MatrixWorld>();
            public Inlet<MatrixWorld> inputStamp = new Inlet<MatrixWorld>();
            public Inlet<MatrixWorld> inputStampHeights = new Inlet<MatrixWorld>();
            public Outlet<MatrixWorld> outputMask = new Outlet<MatrixWorld>();
        }

        public int type;
        [SerializeField] public int guiExpanded;

        public Inlet<WorldGraphGuid> inputGraph = new Inlet<WorldGraphGuid>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputGraph;
            foreach (var layer in layers)
            {
                yield return layer.inputStamp;
                yield return layer.inputVariance;
                yield return layer.inputStampHeights;
            }
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            foreach (var layer in layers) yield return layer.outputMask;
        }

        public string[] Types()
        {
            return GraphGeneratorHelper.GetTypes(InputGraphGenerator()).ToArray();
        }

        public GraphGenerator InputGraphGenerator()
        {
            return MapMagicUtil.GetInputGraphGenerator(inputGraph);
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            Init();

            var inputGraphGuid = tileData.ReadInletProduct(inputGraph);
            var heights = tileData.ReadInletProduct(this);

            // return on stop/disable
            if (!enabled || stop != null && stop.stop || MandatoryInputMissing(inputGraph, this))
            {
                tileData.StoreProduct(this, heights ?? new MatrixWorld(tileData.area.full.rect, tileData.area.full.worldPos,
                    tileData.area.full.worldSize, tileData.globals.height));
                return;
            }

            // prepare output
            var dstHeights = new MatrixWorld(heights);

            var graph = (InternalWorldGraph) SplineTools.Instance.state.Graph(inputGraphGuid.value);

            var inputGraphGenerator = MapMagicUtil.GetInputGraphGenerator(inputGraph);
            var types = Types();

            var maxRadiusMapped =
                (GraphGeneratorHelper.GetRadiusRange(inputGraphGenerator, types[type]) * 1).y.ToMapSpace(tileData.area.active);

            // maximum upscale for rotation is at 45°
            var relevantRect = tileData.area.full.rect.Expanded(Mathf.CeilToInt(Mathf.Sqrt(2 * Mathf.Pow(maxRadiusMapped, 2))))
                .ToWorldSpaceRect(tileData.area.full);

            var nodes = graph.NodesInRect(relevantRect, new[] { NodeType.Of(NodeBaseType.Custom, types[type]) });

            var masks = new Matrix[layers.Length];

            for (var i = 0; i < layers.Length; i++)
            {
                masks[i] = new Matrix(tileData.area.full.rect);
                tileData.StoreProduct(layers[i].outputMask, masks[i]);
            }

            var processedHeights = new Den.Tools.Matrix2D<BorderHelper.HeightProcess>(tileData.area.full.rect);

            foreach (var node in nodes) FlattenStamp((InternalNode) node, processedHeights, masks, tileData, stop);

            for (var x = tileData.area.full.rect.Min.x; x < tileData.area.full.rect.Max.x; x++)
            for (var z = tileData.area.full.rect.Min.z; z < tileData.area.full.rect.Max.z; z++)
            {
                var processedHeight = processedHeights[x, z];
                if (processedHeight == null) continue;
                if (stop != null && stop.stop) return;

                dstHeights[x, z] = processedHeight.ProcessedHeight(dstHeights[x, z]);
            }

            tileData.StoreProduct(this, dstHeights);
        }

        private void FlattenStamp(InternalNode internalNode, Den.Tools.Matrix2D<BorderHelper.HeightProcess> processedHeights, Matrix[] masks, TileData tileData,
            StopToken stop)
        {
            var radiusMapped = internalNode.Radius().ToMapSpace(tileData.area.active);
            var positionMapped = internalNode.PositionV2().ToMapSpace(tileData.area.full);

            var rnd = new ConsistentRandom(tileData.random.Seed * internalNode.Seed());
            var rndLayer = GetRandomLayer(rnd);

            var layer = layers[rndLayer];
            var mask = masks[rndLayer];
            var rotation = rnd.NextFloat(layer.rotationRange.x, layer.rotationRange.y);

            var variance = tileData.ReadInletProduct(layer.inputVariance);

            StampFlattenV2Generator.FlattenStampMapped(processedHeights, layer.inputStamp, layer.inputStampHeights, radiusMapped, rotation,
                positionMapped, variance, layer.varianceOffset, mask, 1, this, tileData, stop);
        }

        private int GetRandomLayer(ConsistentRandom rnd)
        {
            var rndLayer = 0;

            for (var i = layers.Length - 1; i > 0; i--)
            {
                if (rnd.NextFloat() > layers[i].chance.ClampedValue) continue;
                rndLayer = i;
                break;
            }

            return rndLayer;
        }
    }
}

#endif
