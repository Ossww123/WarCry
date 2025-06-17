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
    [GeneratorMenu(menu = "SplineTools/Map",
        name = "Flatten Stamp V2",
        priority = 10,
        disengageable = true,
        colorType = typeof(MatrixWorld),
        iconName = "GeneratorIcons/Flatten",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/map/flatten_stamp_v2")]
    public class StampFlattenV2Generator : Generator, IInlet<MatrixWorld>, IMultiInlet, IOutlet<MatrixWorld>, IMultiOutlet
    {
        public float varianceOffset = 0f;
        public Vector2 rotationRange = new Vector2(0, 360);

        public Inlet<NodesByOffset> inputNodes = new Inlet<NodesByOffset>();
        public Inlet<MatrixWorld> inputVariance = new Inlet<MatrixWorld>();
        public Inlet<MatrixWorld> inputStamp = new Inlet<MatrixWorld>();
        public Inlet<MatrixWorld> inputStampHeights = new Inlet<MatrixWorld>();
        public Outlet<MatrixWorld> outputMask = new Outlet<MatrixWorld>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputNodes;
            yield return inputStamp;
            yield return inputVariance;
            yield return inputStampHeights;
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return outputMask;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            var nodesByOffset = tileData.ReadInletProduct(inputNodes);
            var heights = tileData.ReadInletProduct(this);

            // return on stop/disable
            if (!enabled || stop != null && stop.stop || MandatoryInputMissing(inputNodes, this) || nodesByOffset == null)
            {
                tileData.StoreProduct(this, heights ?? new MatrixWorld(tileData.area.full.rect, tileData.area.full.worldPos,
                    tileData.area.full.worldSize, tileData.globals.height));
                return;
            }

            // prepare output
            var dstHeights = new MatrixWorld(heights);

            var maxRadiusMapped = SplineTools.Instance.GlobalMaxRadius().ToMapSpace(tileData.area.active);

            // maximum upscale for rotation is at 45°
            var relevantRect = tileData.area.full.rect
                .Expanded(Mathf.CeilToInt(Mathf.Sqrt(2 * Mathf.Pow(maxRadiusMapped, 2))))
                .ToWorldSpaceRect(tileData.area.full);

            var nodes = nodesByOffset.FilterForRect(relevantRect);

            var mask = new Matrix(tileData.area.full.rect);
            tileData.StoreProduct(outputMask, mask);

            var processedHeights = new Den.Tools.Matrix2D<BorderHelper.HeightProcess>(tileData.area.full.rect);

            foreach (var node in nodes)
            {
                FlattenStamp((InternalNode) node, processedHeights, mask, tileData, stop);
                if (stop != null && stop.stop) return;
            }

            for (var x = tileData.area.full.rect.Min.x; x < tileData.area.full.rect.Max.x; x++)
            for (var z = tileData.area.full.rect.Min.z; z < tileData.area.full.rect.Max.z; z++)
            {
                var processedHeight = processedHeights[x, z];
                if (processedHeight == null) continue;

                dstHeights[x, z] = processedHeight.ProcessedHeight(dstHeights[x, z]);
            }

            tileData.StoreProduct(this, dstHeights);
        }

        private void FlattenStamp(InternalNode internalNode, Den.Tools.Matrix2D<BorderHelper.HeightProcess> processedHeights, Matrix mask, TileData tileData,
            StopToken stop)
        {
            var radiusMapped = internalNode.Radius().ToMapSpace(tileData.area.active);
            var positionMapped = internalNode.PositionV2().ToMapSpace(tileData.area.full);

            var rnd = new ConsistentRandom(tileData.random.Seed * internalNode.Seed());
            var rotation = rnd.NextFloat(rotationRange.x, rotationRange.y);

            var variance = tileData.ReadInletProduct(inputVariance);

            FlattenStampMapped(processedHeights, inputStamp, inputStampHeights, radiusMapped, rotation, positionMapped, variance,
                varianceOffset, mask, 1, this, tileData, stop);
        }

        public static void FlattenStampMapped(Den.Tools.Matrix2D<BorderHelper.HeightProcess> processedHeights, IInlet<MatrixWorld> inputStamp,
            IInlet<MatrixWorld> inputStampHeights, float radius, float rotation, Vector3 position, Matrix variance, float varianceOffset,
            Matrix mask, float scale, Generator g, TileData tileData, StopToken stop)
        {
            if (MapMagicUtil.GetInputGenerator(inputStamp) == null && MapMagicUtil.GetInputGenerator(inputStampHeights) == null) return;

            var stampScale = (float) Mathf.Max(32, Mathf.ClosestPowerOfTwo((int) radius)) / (int) tileData.area.full.worldSize.x;
            stampScale *= scale;

            var scaledSize = (int) tileData.area.full.worldSize.x * stampScale;
            var rect00 = new Den.Tools.CoordRect(0, 0, scaledSize, scaledSize);

            var refMatrix = MapMagicUtil.GetInputGenerator(inputStamp) == null
                ? new Matrix(rect00)
                : GetOrCacheHeightMap(inputStamp, rect00, tileData, stop);

            var rotated = refMatrix.Rotate(rotation);

            var addedHeights = MapMagicUtil.GetInputGenerator(inputStampHeights) == null
                ? null
                : GetOrCacheHeightMap(inputStampHeights, rect00, tileData, stop).Rotate(rotation);

            var rotationExpandFactor = (float) rotated.rect.size.x / refMatrix.rect.size.x;

            var r = Mathf.CeilToInt(radius * rotationExpandFactor);
            var cPos = new Den.Tools.Coord((int) position.x, (int) position.z);
            var height = position.y;

            var mappingFactor = (float) rotated.rect.size.x / (r * 2);

            for (var x = -r; x < r; x++)
            for (var z = -r; z < r; z++)
            {
                var p = cPos + new Den.Tools.Coord(x, z);
                if (p.x < tileData.area.full.rect.Min.x
                    || p.x > tileData.area.full.rect.Max.x - 1
                    || p.z < tileData.area.full.rect.Min.z
                    || p.z > tileData.area.full.rect.Max.z - 1) continue;

                var sx = (x + r) * mappingFactor;
                var sz = (z + r) * mappingFactor;

                var amount = rotated.GetInterpolated(sx, sz);

                var baseHeight = height + (variance?[p.x, p.z] + varianceOffset ?? 0);
                var rawAddedHeight = addedHeights?.GetInterpolated(sx, sz) ?? 0;
                var addedHeight = addedHeights == null ? 0 : rawAddedHeight;

                var newHeight = baseHeight + addedHeight;

                if (processedHeights[p.x, p.z] == null)
                {
                    var processedHeight = new BorderHelper.HeightProcess();
                    processedHeight.AddStep(newHeight, amount);
                    processedHeights[p.x, p.z] = processedHeight;
                } else
                {
                    processedHeights[p.x, p.z].AddStep(newHeight, amount);
                }

                mask[p.x, p.z] = Mathf.Max(mask[p.x, p.z], MapMagicUtil.GetInputGenerator(inputStamp) != null ? amount : rawAddedHeight);
            }
        }
    }
}

#endif
