#if ST_MM_2

using System;
using System.Collections.Generic;
using Den.Tools.Matrices;
using MapMagic.Nodes;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Map",
        name = "Flatten Stamp Objects",
        priority = 10,
        disengageable = true,
        colorType = typeof(MatrixWorld),
        iconName = "GeneratorIcons/Flatten",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/map/flatten_stamp_objects")]
    public class StampFlattenObjectsGenerator : Generator, IInlet<MatrixWorld>, IMultiInlet, IOutlet<MatrixWorld>, IMultiOutlet
    {
        public float varianceOffset = 0f;

        public Inlet<Den.Tools.TransitionsList> inputSpatialHash = new Inlet<Den.Tools.TransitionsList>();
        public Inlet<MatrixWorld> inputStamp = new Inlet<MatrixWorld>();
        public Inlet<MatrixWorld> inputStampHeights = new Inlet<MatrixWorld>();
        public Inlet<MatrixWorld> inputVariance = new Inlet<MatrixWorld>();
        public Outlet<MatrixWorld> outputMask = new Outlet<MatrixWorld>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputSpatialHash;
            yield return inputVariance;
            yield return inputStamp;
            yield return inputStampHeights;
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return outputMask;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            var transitionsList = tileData.ReadInletProduct(inputSpatialHash);
            var heights = tileData.ReadInletProduct(this);
            var variance = tileData.ReadInletProduct(inputVariance);

            // return on stop/disable
            if (!enabled || stop != null && stop.stop || MandatoryInputMissing(inputSpatialHash, this))
            {
                tileData.StoreProduct(this, heights ?? new MatrixWorld(tileData.area.full.rect, tileData.area.full.worldPos,
                    tileData.area.full.worldSize, tileData.globals.height));
                return;
            }

            // prepare output
            var dstHeights = new MatrixWorld(heights);

            var mask = new MatrixWorld(heights);
            var processedHeights = new Den.Tools.Matrix2D<BorderHelper.HeightProcess>(tileData.area.full.rect);

            foreach (var transition in transitionsList.arr)
            {
                StampFlattenV2Generator.FlattenStampMapped(processedHeights, inputStamp, inputStampHeights, transition.scale.x,
                    transition.rotation.eulerAngles.y, transition.pos, variance, varianceOffset, mask, 1, this, tileData, stop);
            }

            for (var x = tileData.area.full.rect.Min.x; x < tileData.area.full.rect.Max.x; x++)
            {
                for (var z = tileData.area.full.rect.Min.z; z < tileData.area.full.rect.Max.z; z++)
                {
                    var processedHeight = processedHeights[x, z];
                    if (processedHeight == null) continue;
                    if (stop != null && stop.stop) return;

                    dstHeights[x, z] = processedHeight.ProcessedHeight(dstHeights[x, z]);
                }
            }

            tileData.StoreProduct(outputMask, mask);
            tileData.StoreProduct(this, dstHeights);
        }
    }
}

#endif
