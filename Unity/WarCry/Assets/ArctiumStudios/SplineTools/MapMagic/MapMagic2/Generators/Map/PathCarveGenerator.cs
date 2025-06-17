#if ST_MM_2

using System;
using Den.Tools.Matrices;
using MapMagic.Nodes;
using MapMagic.Products;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Map",
        name = "Carve Path",
        priority = 10,
        disengageable = true,
        colorType = typeof(MatrixWorld),
        iconName = "GeneratorIcons/Flatten",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/map/path_carve")]
    public class PathCarveGenerator : PathFlattenGenerator
    {
        public float depthMax = 2f;
        public ClampedFloat depthRatio = new ClampedFloat(0.1f, 0f, float.MaxValue);

        public PathCarveGenerator()
        {
            mode = Mode.Lower;
            inclineBySlope = true;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            borderType = BorderType.Adaptive;
            borderMax.value = 0;
            base.Generate(tileData, stop);
        }

        protected override ElevatingEdgeWalker NewEdgeWalker(TileData tileData, StopToken stop)
        {
            var options = new CarvingEdgeWalker.Options((int) tileData.area.full.worldSize.x, tileData.area.full.rect.size.x,
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
                depthRatio = depthRatio.ClampedValue,
                depthMax = depthMax,
                inclineBySlope = inclineBySlope,
                crossingTiltSmoothingDistance = crossingTiltSmoothingDistance.ClampedValue
            };

            Func<Vector2, float> maskFunc = v => SecondaryMaskMapped(v.ToMapSpaceCoord(tileData.area.full), inputMask, tileData, stop);
            Func<Vector2, float> varianceFunc = v =>
                SecondaryMaskMapped(v.ToMapSpaceCoord(tileData.area.full), inputVariance, tileData, stop).ToWorldSpaceHeight();

            var edgeWalker = new CarvingEdgeWalker(options,
                v => HeightMapped(v.ToMapSpaceCoord(tileData.area.full), this, tileData, stop).ToWorldSpaceHeight(),
                MapMagicUtil.GetInputGenerator(inputMask) == null ? null : maskFunc,
                MapMagicUtil.GetInputGenerator(inputVariance) == null ? null : varianceFunc,
                () => stop != null && stop.stop);
            return edgeWalker;
        }
    }
}

#endif
