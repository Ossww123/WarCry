#if ST_MM_2

using System;
using System.Collections.Generic;
using System.Threading;
using MapMagic.Nodes;
using MapMagic.Products;
using Den.Tools.Matrices;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Map",
        name = "Bounded Shape",
        priority = 10,
        disengageable = false,
        colorType = typeof(MatrixWorld),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/map/bounded_shape")]
    public class BoundedShapeGenerator : Generator, IInlet<MatrixWorld>, IMultiInlet, IOutlet<MatrixWorld>
    {
        public enum Algorithm
        {
            Curvature1
        }

        public Algorithm usedAlgorithm = Algorithm.Curvature1;
        public ClampedFloat cc = new ClampedFloat(0.7f, 0f, 20f);

        public AnimationCurve curve = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));

        public Inlet<Bounds> inputBounds = new Inlet<Bounds>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputBounds;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            var heights = tileData.ReadInletProduct(this);

            if (!enabled || stop != null && stop.stop || MandatoryInputMissing(inputBounds))
            {
                tileData.StoreProduct(this, heights);
                return;
            }

            var dstHeights = heights != null
                ? new MatrixWorld(heights)
                : new MatrixWorld(tileData.area.full.rect, tileData.area.full.worldPos, tileData.area.full.worldSize, tileData.globals.height);

            var rectMin = dstHeights.rect.Min;
            var rectMax = dstHeights.rect.Max;

            // var productsBefore = new Dictionary<ulong, object>(tileData.products);
            // var inputBoundsLinkedGenId = inputBounds.LinkedGenId;
            // var inputBoundsLinkedOutletId = inputBounds.LinkedOutletId;

            var worldBounds = tileData.ReadInletProduct(inputBounds);

            if (worldBounds == null)
            {
                var maxTries = 10;
                var tries = 1;

                while (worldBounds == null && tries < maxTries)
                {
                    Log.Debug(this, () => "'inputBounds' is null, retry #" + tries + "/" + maxTries + ". This should not happen, " +
                                          "but a retry is performed as a workaround for a race condition presumably in or with MM2.");

                    Thread.Sleep(10); // give MM2 some time
                    worldBounds = tileData.ReadInletProduct(inputBounds);

                    // Log.Warn(this, () => Log.LogCollection(productsBefore));
                    // Log.Warn(this, () => inputBoundsLinkedGenId + " -> " + inputBounds.LinkedGenId);
                    // Log.Warn(this, () => inputBoundsLinkedOutletId + " -> " + inputBounds.LinkedOutletId);

                    tries++;
                }

                if (worldBounds == null)
                {
                    Log.Error(this, () => "Connecting a 'Bounds Exit' Portal to a 'BoundedShape' generator sometimes causes problems at " +
                                          "the moment when multithreading is enabled. As workaround, please connect the Bounds generator directly. " +
                                          "This error can also occur when no portal is used, but less likely. " +
                                          "It seems to be caused by a race condition presumably in or with MM2.");
                }
            }

            var mapBounds = worldBounds.ToCoordRect(tileData.area);

            if (MapMagicUtil.OutOfBounds(tileData.area.full.rect, mapBounds))
            {
                var outOfBounds = new MatrixWorld(dstHeights);
                for (var x = rectMin.x; x < rectMax.x; x++)
                {
                    var value = curve.EvaluateClamped(0);

                    for (var z = rectMin.z; z < rectMax.z; z++) outOfBounds[x, z] = dstHeights[x, z] * value;
                }

                tileData.StoreProduct(this, outOfBounds);
                return;
            }


            for (var x = rectMin.x; x < rectMax.x; x++)
            for (var z = rectMin.z; z < rectMax.z; z++)
            {
                if (stop != null && stop.stop) return;

                float distance;
                switch (usedAlgorithm)
                {
                    case Algorithm.Curvature1:
                        distance = CurvatureDistance1(mapBounds, new Vector2(x, z));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                dstHeights[x, z] *= curve.EvaluateClamped(distance);
            }

            if (stop != null && stop.stop) return; //do not write output if generation was stopped
            tileData.StoreProduct(this, dstHeights);
        }

        private float CurvatureDistance1(Den.Tools.CoordRect worldBounds, Vector2 destination)
        {
            if (!worldBounds.Contains(destination.V3())) return 0f;

            var delta = destination - worldBounds.Center.vector2;

            var cMin = 0f;
            var cMax = 1f;
            var w = (float) worldBounds.size.x;
            var h = (float) worldBounds.size.z;
            var x = delta.x + w / 2;
            var y = delta.y + h / 2;

            var c = cMin + (cMax - cMin) * Mathf.Pow((16 * x * y * (w - x) * (h - y)) / (w * w * h * h), cc.ClampedValue);
            return c;
        }
    }
}

#endif
