#if ST_MM_2

using Den.Tools.GUI;
using MapMagic.Nodes.GUI;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class MapGeneratorsEditor : GeneratorsEditor
    {
        [Draw.EditorAttribute(typeof(PathGenerator))]
        public static void DrawGenerator(PathGenerator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, ((gen.inputEdges, "Edges"), default));

            using (Cell.LineStd) Draw.Field(ref gen.usedGizmoLevel, "Gizmo");

            using (Cell.LineStd) DrawClampedField(gen.additionalWidth, "Border");
            using (Cell.LineStd) DrawCurveSimple(gen.falloff);

            using (Cell.LineStd) Draw.Field(ref gen.usedEndpointHandling, "Endpoint Handling");

            switch (gen.usedEndpointHandling)
            {
                case EndpointHandling.Fade:
                    using (Cell.LineStd) DrawClampedField(gen.endFadeDistance, "Distance");
                    using (Cell.LineStd) DrawCurveSimple(gen.endFadeFalloff);
                    break;
            }

            // crossing handling
            DrawFeature(ref gen.crossingFade, "Fade Crossings", () =>
            {
                using (Cell.LineStd) DrawClampedField(gen.crossingDistance, "Distance");
                using (Cell.LineStd) DrawCurveSimple(gen.crossingFalloff);
            });
            DrawFeature(ref gen.crossingOverflow, "Overflow Crossings", () =>
            {
                using (Cell.LineStd) DrawClampedField(gen.crossingOverflowDistance, "Distance");
                using (Cell.LineStd) DrawCurveSimple(gen.crossingOverflowFalloff);
            });
            DrawFeature(ref gen.crossingWiden, "Widen Crossings", () =>
            {
                using (Cell.LineStd) DrawClampedField(gen.crossingWidenDistance, "Distance");
                using (Cell.LineStd) DrawClampedField(gen.crossingWidenFalloffMax, "Amount");
                using (Cell.LineStd) DrawCurveSimple(gen.crossingWidenFalloff);
            });
        }

        [Draw.EditorAttribute(typeof(BoundedShapeGenerator))]
        public static void DrawGenerator(BoundedShapeGenerator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, ((gen.inputBounds, "Bounds"), default));

            //params
            using (Cell.LineStd) Draw.Field(ref gen.usedAlgorithm, "Algorithm");
            using (Cell.LineStd) DrawClampedField(gen.cc, "CC");

            using (Cell.LineStd) DrawCurveSimple(gen.curve);
        }

        [Draw.EditorAttribute(typeof(FlattenPlusGenerator))]
        public static void DrawGenerator(FlattenPlusGenerator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, ((gen.inputGraph, "Graph"), default),
                ((gen.inputVariance, "Variance"), default));

            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            if (NotPlacedInRootGraph(gen)) return;

            if (MapMagicUtil.GetInputGenerator(gen.inputVariance) != null)
            {
                using (Cell.LineStd) Draw.Field(ref gen.varianceOffset, "Variance Offset");
            }

            var inputGraphGenerator = MapMagicUtil.GetInputGraphGenerator(gen.inputGraph);

            if (inputGraphGenerator != null)
            {
                var types = gen.Types();

                using (Cell.LineStd) gen.type = Draw.PopupSelector(gen.type, types, "Type");
                gen.type = Mathf.Min(gen.type, types.Length - 1);
            }

            //params
            using (Cell.LineStd) Draw.Field(ref gen.borderType, "Border Type");

            using (Cell.LineStd) DrawClampedField(gen.borderMax, "Max Border");

            using (Cell.LineStd) DrawCurveSimple(gen.borderFalloff);

            if (gen.borderType == BorderType.Adaptive)
            {
                using (Cell.LineStd) DrawClampedField(gen.borderSlopeMax, "Max Border Slope");

                using (Cell.LineStd) DrawClampedField(gen.borderChange, "Border Change");
            }
        }

        [Draw.EditorAttribute(typeof(FlattenPlusV2Generator))]
        public static void DrawGenerator(FlattenPlusV2Generator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, ((gen.inputNodes, "Nodes"), default), ((gen.inputVariance, "Variance"), default));

            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            if (MapMagicUtil.GetInputGenerator(gen.inputVariance) != null)
            {
                using (Cell.LineStd) Draw.Field(ref gen.varianceOffset, "Variance Offset");
            }

            //params
            using (Cell.LineStd) Draw.Field(ref gen.borderType, "Border Type");
            using (Cell.LineStd) DrawClampedField(gen.borderMax, "Max Border");

            using (Cell.LineStd) DrawCurveSimple(gen.borderFalloff);

            if (gen.borderType == BorderType.Adaptive)
            {
                using (Cell.LineStd) DrawClampedField(gen.borderSlopeMax, "Max Border Slope");
                using (Cell.LineStd) DrawClampedField(gen.borderChange, "Border Change");
            }
        }

        [Draw.EditorAttribute(typeof(PathFlattenGenerator))]
        public static void DrawGenerator(PathFlattenGenerator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen,
                ((gen.inputEdges, "Edges"), default),
                ((gen.inputMask, "Mask"), default),
                ((gen.inputVariance, "Variance"), default)
            );

            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            if (MapMagicUtil.GetInputGenerator(gen.inputVariance) != null)
                using (Cell.LineStd)
                    Draw.Field(ref gen.varianceOffset, "Variance Offset");

            using (Cell.LineStd) Draw.Field(ref gen.usedGizmoLevel, "Gizmo");

            //params
            using (Cell.LineStd) Draw.Field(ref gen.borderType, "Border Type");
            using (Cell.LineStd) DrawClampedField(gen.borderMax, "Max Border");

            using (Cell.LineStd) DrawCurveSimple(gen.falloff);

            using (Cell.LineStd) DrawClampedField(gen.additionalWidth, "Additional Width");
            using (Cell.LineStd) DrawClampedField(gen.crossingTiltSmoothingDistance, "Crossing Smooth Distance");

            if (gen.borderType == BorderType.Adaptive)
            {
                using (Cell.LineStd) DrawClampedField(gen.borderSlopeMax, "Max Border Slope");
                using (Cell.LineStd) DrawClampedField(gen.borderChange, "Border Change");
            }

            using (Cell.LineStd) Draw.Field(ref gen.usedEndpointHandling, "Endpoint Handling");
            switch (gen.usedEndpointHandling)
            {
                case EndpointHandling.Fade:
                    using (Cell.LineStd) DrawClampedField(gen.endFadeDistance, "Fading Distance");
                    using (Cell.LineStd) DrawCurveSimple(gen.endFadeFalloff);
                    break;
            }

            using (Cell.LineStd) DrawClampedField(gen.detail, "Detail");
            using (Cell.LineStd) Draw.Field(ref gen.mode, "Mode");
            using (Cell.LineStd) Draw.ToggleLeft(ref gen.inclineBySlope, "Incline By Slope");
        }

        [Draw.EditorAttribute(typeof(PathCarveGenerator))]
        public static void DrawGenerator(PathCarveGenerator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen,
                ((gen.inputEdges, "Edges"), default),
                ((gen.inputMask, "Mask"), default),
                ((gen.inputVariance, "Variance"), default)
            );

            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            if (MapMagicUtil.GetInputGenerator(gen.inputVariance) != null)
            {
                using (Cell.LineStd) Draw.Field(ref gen.varianceOffset, "Variance Offset");
            }

            using (Cell.LineStd) Draw.Field(ref gen.usedGizmoLevel, "Gizmo");

            //params
            using (Cell.LineStd) DrawClampedField(gen.depthRatio, "Depth Ratio");
            using (Cell.LineStd) Draw.Field(ref gen.depthMax, "Max Depth");

            using (Cell.LineStd) DrawCurveSimple(gen.falloff);

            using (Cell.LineStd) DrawClampedField(gen.crossingTiltSmoothingDistance, "Crossing Smooth Distance");

            using (Cell.LineStd) Draw.Field(ref gen.usedEndpointHandling, "Endpoint Handling");
            switch (gen.usedEndpointHandling)
            {
                case EndpointHandling.Fade:
                    using (Cell.LineStd) DrawClampedField(gen.endFadeDistance, "Fading Distance");
                    using (Cell.LineStd) DrawCurveSimple(gen.endFadeFalloff);
                    break;
            }

            using (Cell.LineStd) DrawClampedField(gen.detail, "Detail");
            using (Cell.LineStd) Draw.Field(ref gen.mode, "Mode");
            using (Cell.LineStd) Draw.ToggleLeft(ref gen.inclineBySlope, "Incline By Slope");
        }

        [Draw.EditorAttribute(typeof(StampFlattenGenerator))]
        public static void DrawGenerator(StampFlattenGenerator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, ((gen.inputGraph, "Graph"), default));

            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            if (NotPlacedInRootGraph(gen)) return;

            var inputGraphGenerator = MapMagicUtil.GetInputGraphGenerator(gen.inputGraph);

            if (inputGraphGenerator != null)
            {
                var types = gen.Types();

                //params
                using (Cell.LineStd) gen.type = Draw.PopupSelector(gen.type, types, "Type");
                gen.type = Mathf.Min(gen.type, types.Length - 1);
            }

            using (Cell.LinePx(20)) GeneratorDraw.DrawLayersAddRemove(gen, ref gen.layers, true);
            using (Cell.LinePx(0)) GeneratorDraw.DrawLayersThemselves(gen, gen.layers, true, DrawStampFlattenGeneratorLayer);
        }

        private static void DrawStampFlattenGeneratorLayer(MapMagic.Nodes.Generator generator, int num)
        {
            var gen = (StampFlattenGenerator) generator;
            var layer = gen.layers[num];
            layer.Parent = gen;

            if (layer == null) return;


            using (Cell.LineStd)
            {
                using (Cell.Padded(130, 0, 0, 0)) Draw.LayerChevron(num, ref gen.guiExpanded);
            }

            DrawInOuts(gen,
                ((layer.inputStamp, "Flatten Stamp"), (layer.outputMask, "Mask")),
                ((layer.inputStampHeights, "Heights Stamp"), default)
            );

            var expanded = num == gen.guiExpanded;

            if (expanded)
            {
                if (MapMagicUtil.GetInputGenerator(layer.inputVariance) != null)
                {
                    using (Cell.LineStd) Draw.Field(ref layer.varianceOffset, "Variance Offset");
                }

                // layout.fieldSize = 0.6f;
                using (Cell.LineStd) Draw.Field(ref layer.rotationRange, "Rotation");
                // if (layer.num != 0) 
                using (Cell.LineStd) DrawClampedField(layer.chance, "Chance");
            }
        }

        [Draw.EditorAttribute(typeof(StampFlattenV2Generator))]
        public static void DrawGenerator(StampFlattenV2Generator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, ((gen.inputNodes, "Nodes"), (gen.outputMask, "Mask")),
                ((gen.inputStamp, "Flatten Stamp"), default),
                ((gen.inputStampHeights, "Heights Stamp"), default));

            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            if (MapMagicUtil.GetInputGenerator(gen.inputVariance) != null)
            {
                using (Cell.LineStd) Draw.Field(ref gen.varianceOffset, "Variance Offset");
            }

            using (Cell.LineStd) Draw.Field(ref gen.rotationRange, "Rotation");
        }

        [Draw.EditorAttribute(typeof(StampFlattenObjectsGenerator))]
        public static void DrawGenerator(StampFlattenObjectsGenerator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen,
                ((gen.inputSpatialHash, "Objects"), (gen.outputMask, "Mask")),
                ((gen.inputStamp, "Flatten Stamp"), default),
                ((gen.inputStampHeights, "Heights Stamp"), default),
                ((gen.inputVariance, "Variance"), default)
            );

            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            if (MapMagicUtil.GetInputGenerator(gen.inputVariance) != null)
            {
                using (Cell.LineStd) Draw.Field(ref gen.varianceOffset, "Variance Offset");
            }
        }
    }
}

#endif
