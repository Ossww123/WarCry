#if ST_MM_2

using System;
using System.Collections.Generic;
using MapMagic.Nodes;
using MapMagic.Products;
using Den.Tools.Matrices;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Map",
        name = "Path",
        priority = 10,
        disengageable = true,
        colorType = typeof(MatrixWorld),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/map/path")]
    public class PathGenerator : EdgeWalkingGenerator, IMultiInlet, IOutlet<MatrixWorld>
    {
        public Inlet<EdgesByOffset> inputEdges = new Inlet<EdgesByOffset>();

        public AnimationCurve falloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputEdges;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            var connections = tileData.ReadInletProduct(inputEdges);

            if (stop != null && stop.stop || !enabled || MandatoryInputMissing(inputEdges) || connections == null || connections.Count == 0)
            {
                tileData.StoreProduct(this, new MatrixWorld(tileData.area.full.rect, tileData.area.full.worldPos,
                    tileData.area.full.worldSize, tileData.globals.height));
                return;
            }

            var margin = Margin(tileData, connections);

            var options = new EdgeWalker<float[,]>.Options((int) tileData.area.full.worldSize.x, tileData.area.full.rect.size.x,
                tileData.area.PixelSize.x, Vector2.zero)
            {
                detail = 1f,
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
                crossingWidenFalloffMin = crossingWidenFalloffMin.ClampedValue
            };

            var edgeWalker = new FlatEdgeWalker(options, () => stop != null && stop.stop);

            var worldSpaceRect = tileData.area.full.ToWorldRect();
            var result = new float[tileData.area.full.rect.size.x, tileData.area.full.rect.size.z];
            edgeWalker.ProcessEdges(connections, result, worldSpaceRect, margin);

            // draw
            var matrix = new MatrixWorld(tileData.area.full.rect, tileData.area.full.worldPos, tileData.area.full.worldSize, tileData.globals.height);

            for (var x = tileData.area.full.rect.Min.x; x < tileData.area.full.rect.Max.x; x++)
            for (var z = tileData.area.full.rect.Min.z; z < tileData.area.full.rect.Max.z; z++)
            {
                matrix[x, z] = result[x - tileData.area.full.rect.Min.x, z - tileData.area.full.rect.Min.z];
            }

            tileData.StoreProduct(this, matrix);
        }
    }
}

#endif
