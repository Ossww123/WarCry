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
        name = "Flatten+ (Legacy)",
        priority = -99,
        disengageable = true,
        colorType = typeof(MatrixWorld),
        iconName = "GeneratorIcons/Flatten",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/map/flatten_plus")]
    public class FlattenPlusGenerator : Generator, IInlet<MatrixWorld>, IMultiInlet, IOutlet<MatrixWorld>
    {
        public int type;
        public ClampedFloat borderMax = new ClampedFloat(20f, 0f, float.MaxValue);
        public ClampedFloat borderSlopeMax = new ClampedFloat(25f, 0f, float.MaxValue);
        public ClampedFloat borderChange = new ClampedFloat(0.2f, 0f, float.MaxValue);
        public float varianceOffset = 0f;
        public BorderType borderType = BorderType.Fixed;

        public AnimationCurve borderFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));

        public Inlet<WorldGraphGuid> inputGraph = new Inlet<WorldGraphGuid>();
        public Inlet<MatrixWorld> inputVariance = new Inlet<MatrixWorld>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputGraph;
            yield return inputVariance;
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
            var inputGraphGuid = tileData.ReadInletProduct(inputGraph)?.value;
            var heights = tileData.ReadInletProduct(this);
            var variance = tileData.ReadInletProduct(inputVariance);

            // return on stop/disable
            if (!enabled || stop != null && stop.stop || MandatoryInputMissing(inputGraph, this) || inputGraphGuid == null || heights == null)
            {
                tileData.StoreProduct(this, heights ?? new MatrixWorld(tileData.area.full.rect, tileData.area.full.worldPos,
                    tileData.area.full.worldSize, tileData.globals.height));
                return;
            }

            // prepare output
            var dstHeights = new MatrixWorld(heights);

            var graph = (InternalWorldGraph) SplineTools.Instance.state.Graph(inputGraphGuid);

            Func<Vector2, float> varianceFunc = v =>
                SecondaryMaskMapped(v.ToMapSpaceCoord(tileData.area.full), inputVariance, tileData, stop).ToWorldSpaceHeight();

            var resolution = tileData.area.full.rect.size.x;
            var options = new BorderHelper.Options((int) tileData.area.full.worldSize.x, resolution, tileData.area.PixelSize.x, Vector2.zero)
            {
                varianceOffset = varianceOffset,
                inclineBySlope = false
            };

            var borderHelper = new BorderHelper(
                options,
                v => HeightMapped(v.ToMapSpaceCoord(tileData.area.full), this, tileData, stop).ToWorldSpaceHeight(),
                null,
                MapMagicUtil.GetInputGenerator(inputVariance) == null ? null : varianceFunc,
                () => stop != null && stop.stop);

            var flattener = new Flattener(borderHelper);

            var inputGraphGenerator = MapMagicUtil.GetInputGraphGenerator(inputGraph);
            var types = Types();

            var maxRadius = (GraphGeneratorHelper.GetRadiusRange(inputGraphGenerator, types[type])).y;

            var worldSpaceRect = tileData.area.full.ToWorldRect();
            var relevantRect = tileData.area.full.rect
                .Expanded(Mathf.CeilToInt((maxRadius + borderMax.ClampedValue / 2).ToMapSpace(tileData.area.active)))
                .ToWorldSpaceRect(tileData.area.full);

            var nodes = graph.NodesInRect(relevantRect, new[] { NodeType.Of(NodeBaseType.Custom, types[type]) });

            var processedHeights = new BorderHelper.HeightProcess[resolution, resolution];
            var sectionHeightMatrix = new float[resolution, resolution];

            foreach (var node in nodes)
            {
                FlattenInsidePerimeter(tileData.area.full.rect, node, dstHeights, variance, tileData);
                flattener.FlattenBorder(node, sectionHeightMatrix, processedHeights, worldSpaceRect, borderMax.ClampedValue,
                    borderSlopeMax.ClampedValue, borderChange.ClampedValue, tileData.globals.height, borderType, borderFalloff);
            }

            for (var x = tileData.area.full.rect.Min.x; x < tileData.area.full.rect.Max.x; x++)
            for (var z = tileData.area.full.rect.Min.z; z < tileData.area.full.rect.Max.z; z++)
            {
                var processedHeight = processedHeights[
                    (int) ((float) x - tileData.area.full.rect.Min.x),
                    (int) ((float) z - tileData.area.full.rect.Min.z)];

                if (processedHeight == null) continue;
                if (stop != null && stop.stop) return;

                dstHeights[x, z] = processedHeight.ProcessedHeight(dstHeights[x, z].ToWorldSpaceHeight()).ToMapSpaceHeight();
            }

            tileData.StoreProduct(this, dstHeights);
        }

        private void FlattenInsidePerimeter(Den.Tools.CoordRect rect, Node node, Matrix dst, Matrix variance, TileData tileData)
        {
            // flatten the area inside the border
            var radiusMapped = Mathf.CeilToInt(node.Radius().ToMapSpace(tileData.area.full));
            var cPos = node.PositionV2().ToMapSpaceCoord(tileData.area.full);
            var heightMapped = node.Position().y.ToMapSpaceHeight();

            for (var x = -radiusMapped - 2; x <= radiusMapped + 2; x++)
            for (var z = -radiusMapped - 2; z <= radiusMapped + 2; z++)
            {
                var p = cPos + new Den.Tools.Coord(x, z);
                if (p.x < rect.Min.x || p.x > rect.Max.x - 1 || p.z < rect.Min.z || p.z > rect.Max.z - 1) continue;
                var distance = (p.vector2 - (cPos.vector2)).magnitude - 3f; // at most 2 diagonal squares error -> 2*sqrt(2) ~ 3
                if (distance > radiusMapped) continue;

                dst[p.x, p.z] = heightMapped + (variance == null ? 0 : variance[p.x, p.z] + varianceOffset.ToMapSpaceHeight());
            }
        }
    }
}

#endif
