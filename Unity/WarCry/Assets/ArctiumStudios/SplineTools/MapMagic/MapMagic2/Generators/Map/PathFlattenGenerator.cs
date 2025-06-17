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
    [GeneratorMenu(menu = "SplineTools/Map",
        name = "Flatten Path",
        priority = 10,
        disengageable = true,
        colorType = typeof(MatrixWorld),
        iconName = "GeneratorIcons/Flatten",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/map/path_flatten")]
    public class PathFlattenGenerator : EdgeWalkingGenerator, IInlet<MatrixWorld>, IMultiInlet, IOutlet<MatrixWorld>, BorderGenerator
    {
        public Inlet<EdgesByOffset> inputEdges = new Inlet<EdgesByOffset>();
        public Inlet<MatrixWorld> inputMask = new Inlet<MatrixWorld>();
        public Inlet<MatrixWorld> inputVariance = new Inlet<MatrixWorld>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputEdges;
            yield return inputMask;
            yield return inputVariance;
        }

        public Mode mode = Mode.LowerAndRaise;
        public ClampedFloat borderSlopeMax = new ClampedFloat(35f, 0.01f, float.MaxValue);
        public float varianceOffset = 0f;
        public ClampedFloat crossingTiltSmoothingDistance = new ClampedFloat(15f, 0f, float.MaxValue);
        public BorderType borderType = BorderType.Fixed;
        public bool inclineBySlope = false;

        public AnimationCurve falloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));

        public enum Mode
        {
            LowerAndRaise,
            Raise,
            Lower
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            // getting inputs
            var connections = tileData.ReadInletProduct(inputEdges);
            var heights = tileData.ReadInletProduct(this);

            // return on stop/disable/null input
            if (stop != null && stop.stop || !enabled || MandatoryInputMissing(inputEdges, this) || heights == null || connections == null ||
                connections.Count == 0)
            {
                tileData.StoreProduct(this,
                    heights ?? new MatrixWorld(tileData.area.full.rect, tileData.area.full.worldPos, tileData.area.full.worldSize,
                        tileData.globals.height));
                return;
            }

            // prepare output
            var dstHeights = new MatrixWorld(heights);

            var margin = Margin(tileData, connections);

            var edgeWalker = NewEdgeWalker(tileData, stop);

            var worldSpaceRect = tileData.area.full.ToWorldRect();
            var processedHeights = new BorderHelper.HeightProcess[tileData.area.full.rect.size.x, tileData.area.full.rect.size.x];
            var processedEdges = edgeWalker.ProcessEdges(connections, processedHeights, worldSpaceRect, margin);

            for (var x = tileData.area.full.rect.Min.x; x < tileData.area.full.rect.Max.x; x++)
            for (var z = tileData.area.full.rect.Min.z; z < tileData.area.full.rect.Max.z; z++)
            {
                if (stop != null && stop.stop) return;

                var initialHeight = dstHeights[x, z];
                var flattenedHeight = processedHeights.GetInterpolated(
                    (int) ((float) x - tileData.area.full.rect.Min.x),
                    (int) ((float) z - tileData.area.full.rect.Min.z),
                    initialHeight.ToWorldSpaceHeight()).ToMapSpaceHeight();

                if (mode == Mode.Raise && flattenedHeight < initialHeight) continue;
                if (mode == Mode.Lower && flattenedHeight > initialHeight) continue;

                dstHeights[x, z] = flattenedHeight;
            }

            // blur/smooth crossings
            BlurCrossings(tileData.area.full.rect, processedEdges, processedHeights, dstHeights, tileData);

            // setting output
            if (stop != null && stop.stop) return;
            tileData.StoreProduct(this, dstHeights);
        }

        private void BlurCrossings(Den.Tools.CoordRect rect, List<Edge> processedEdges, BorderHelper.HeightProcess[,] processedHeights, Matrix dst,
            TileData tileData)
        {
            processedEdges.SelectMany(e => e.Nodes())
                .Where(n => n.Type().IsCrossing())
                .Distinct().ToList()
                .ForEach(crossing =>
                {
                    var cPos = crossing.PositionV2().ToMapSpaceCoord(tileData.area.full);

                    var radiusMapped = (crossing.Radius() + borderMax.ClampedValue).ToMapSpace(tileData.area.active);

                    for (var x = (int) -radiusMapped; x < radiusMapped; x++)
                    for (var z = (int) -radiusMapped; z < radiusMapped; z++)
                    {
                        var p = cPos + new Den.Tools.Coord(x, z);
                        if (p.x < rect.Min.x + 1 || p.x > rect.Max.x - 2 || p.z < rect.Min.z + 1 || p.z > rect.Max.z - 2) continue;
                        var distance = (p.vector2 - cPos.vector2).magnitude;

                        var processedHeight = processedHeights[
                            (int) ((float) p.x - rect.Min.x),
                            (int) ((float) p.z - rect.Min.z)];

                        if (distance > radiusMapped || processedHeight == null) continue;

                        var smoothed = (dst[p.x - 1, p.z] + dst[p.x + 1, p.z] + dst[p.x, p.z - 1] + dst[p.x, p.z + 1]) / 4;
                        var ratio = 1;

                        dst[p.x, p.z] = Mathf.Lerp(dst[p.x, p.z], smoothed, crossingFalloff.EvaluateClamped(ratio));
                    }
                });
        }

        protected virtual ElevatingEdgeWalker NewEdgeWalker(TileData tileData, StopToken stop)
        {
            var options = new ElevatingEdgeWalker.Options((int) tileData.area.full.worldSize.x, tileData.area.full.rect.size.x,
                tileData.area.PixelSize.x, tileData.globals.height, Vector2.zero)
            {
                detail = detail.ClampedValue,
                falloff = falloff,
                additionalWidth = additionalWidth.ClampedValue,
                borderChange = borderChange.ClampedValue,
                crossingDistance = crossingDistance.ClampedValue,
                crossingFade = crossingFade,
                crossingFalloff = crossingFalloff,
                crossingOverflow = crossingOverflow,
                crossingWiden = crossingWiden,
                borderMax = borderMax.ClampedValue,
                crossingOverflowDistance = crossingOverflowDistance.ClampedValue,
                crossingOverflowFalloff = crossingOverflowFalloff,
                crossingWidenDistance = crossingWidenDistance.ClampedValue,
                crossingWidenFalloff = crossingWidenFalloff,
                crossingWidthFalloff = crossingWidthFalloff,
                endFadeDistance = endFadeDistance.ClampedValue,
                endFadeFalloff = endFadeFalloff,
                usedEndpointHandling = usedEndpointHandling,
                usedGizmoLevel = usedGizmoLevel,
                crossingWidenFalloffMax = crossingWidenFalloffMax.ClampedValue,
                crossingWidenFalloffMin = crossingWidenFalloffMin.ClampedValue,
                borderType = borderType,
                varianceOffset = varianceOffset,
                borderSlopeMax = borderSlopeMax.ClampedValue,
                inclineBySlope = inclineBySlope,
                crossingTiltSmoothingDistance = crossingTiltSmoothingDistance.ClampedValue
            };

            Func<Vector2, float> maskFunc = v => SecondaryMaskMapped(v.ToMapSpaceCoord(tileData.area.full), inputMask, tileData, stop);
            Func<Vector2, float> varianceFunc = v =>
                SecondaryMaskMapped(v.ToMapSpaceCoord(tileData.area.full), inputVariance, tileData, stop).ToWorldSpaceHeight();

            var edgeWalker = new ElevatingEdgeWalker(options,
                v => HeightMapped(v.ToMapSpaceCoord(tileData.area.full), this, tileData, stop).ToWorldSpaceHeight(),
                MapMagicUtil.GetInputGenerator(inputMask) == null ? null : maskFunc,
                MapMagicUtil.GetInputGenerator(inputVariance) == null ? null : varianceFunc,
                () => stop != null && stop.stop);
            return edgeWalker;
        }

        protected override float GlobalMaxBorder(TileData tileData)
        {
            var pathFlattenGenerators = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph.generators
                .OfType<BorderGenerator>()
                .ToList();

            return pathFlattenGenerators.Count == 0
                ? 0
                : pathFlattenGenerators.Max(g => g.MaxBorder());
        }

        public float MaxBorder()
        {
            return borderMax.ClampedValue + additionalWidth.ClampedValue;
        }
    }
}

#endif
