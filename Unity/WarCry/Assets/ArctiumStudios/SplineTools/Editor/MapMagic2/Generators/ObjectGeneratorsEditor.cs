#if ST_MM_2

using System;
using System.Collections.Generic;
using System.Linq;
using Den.Tools.GUI;
using MapMagic.Nodes.GUI;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class ObjectGeneratorsEditor : GeneratorsEditor
    {
        [Draw.EditorAttribute(typeof(BoundedScatterGeneratorV2))]
        public static void DrawGenerator(BoundedScatterGeneratorV2 gen)
        {
            DrawHelpLink(gen);
            if (SplineTools.HasEditorInstance())
                Generator.EnsureGuidIsUnique(gen.startingGraphGuid, gen, newGuid => gen.startingGraphGuid = newGuid);

            if (NotPlacedInRootGraph(gen)) return;

            DrawInOuts(gen,
                ((gen.inputGraph, "Graph"), (gen.outputGraph, "Graph")),
                ((gen.inputBounds, "Bounds"), default),
                ((gen.inputHeights, "Heights"), default),
                ((gen.inputRiverGraph, "River Graph"), default)
            );

            using (Cell.LinePx(20)) GeneratorDraw.DrawLayersAddRemove(gen, ref gen.layers, true);
            using (Cell.LinePx(0)) GeneratorDraw.DrawLayersThemselves(gen, gen.layers, true, DrawBoundedScatterGeneratorV2Layer);
        }


        private static void DrawBoundedScatterGeneratorV2Layer(MapMagic.Nodes.Generator generator, int num)
        {
            var gen = (BoundedScatterGeneratorV2) generator;
            var layer = gen.layers[num];
            layer.Parent = gen;

            if (layer == null) return;

            using (Cell.LineStd)
            {
                using (Cell.Padded(0, 20, 0, 0)) Draw.Field(ref layer.type);
                using (Cell.Padded(130, 0, 0, 0)) Draw.LayerChevron(num, ref gen.guiExpanded);
            }

            DrawInOuts(gen, ((layer.inputMask, "Mask"), (layer.outputNodes, "Nodes")));
            DrawInOuts(gen, (default, (layer.outputTransitionsList, "Objects")));

            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            if (num == gen.guiExpanded)
            {
                //params
                Cell.current.fieldWidth = 0.4f;
                using (Cell.LineStd) Draw.Field(ref layer.seed, "Seed");
                using (Cell.LineStd) DrawClampedField(layer.countMax, "Max Count");
                using (Cell.LineStd) DrawClampedField(layer.iterations, "Iterations");
                using (Cell.LineStd) Draw.Field(ref layer.placement, "Placement");

                DrawFeature(ref layer.distanceLimit, "Limit Distance", () =>
                    {
                        DrawClampedField(layer.distanceToSameMin, "To Same Type");

                        if (layer.GetMinDistanceToOthers() >= 0)
                            DrawClampedField(layer.distanceToOthersMin, "To Other Types");

                        if (Den.Tools.Extensions.CheckGet(GraphWindow.current.graph.links, gen.inputRiverGraph) != null)
                        {
                            DrawClampedField(layer.distanceToLakePerimeterMin, "To Lake Perimeter");
                            DrawClampedField(layer.distanceToRiverMin, "To River");
                        }
                    }
                );

                DrawFeature(ref layer.heightLimit, "Limit Height Range", () =>
                {
                    DrawClampedField(layer.heightMin, "Min");
                    DrawClampedField(layer.heightMax, "Max");
                });

                DrawFeature(ref layer.deviationLimit, "Limit Allowed Deviation", () =>
                {
                    DrawClampedField(layer.deviationUpMax, "Up");
                    DrawClampedField(layer.deviationDownMax, "Down");
                    DrawClampedField(layer.deviationResolution, "Resolution");
                });

                DrawClampedFieldsAndSimpleCurve(layer.radiusFalloff, layer.radiusMin, layer.radiusMax, "Radius", true);

                DrawClampedField(layer.safeBorders, "Safe Borders");
            }
        }

        [Draw.EditorAttribute(typeof(ManualScatterGenerator))]
        public static void DrawGenerator(ManualScatterGenerator gen)
        {
            DrawHelpLink(gen);
            if (SplineTools.HasEditorInstance())
                Generator.EnsureGuidIsUnique(gen.startingGraphGuid, gen, newGuid => gen.startingGraphGuid = newGuid);

            if (NotPlacedInRootGraph(gen)) return;

            DrawInOuts(gen, ((gen.inputGraph, "Graph"), (gen.outputGraph, "Graph")));
            DrawInOuts(gen, (default, (gen.outputNodes, "Nodes")));

            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            var inputGenerators = new List<GraphGenerator>();
            GraphGeneratorHelper.CollectAllInputGraphGenerators(gen, inputGenerators);

            if (inputGenerators.Any(g => g is BoundedScatterGeneratorV2))
            {
                using (Cell.LineStd) Draw.Label("WARNING: Manual Scatter");
                using (Cell.LineStd) Draw.Label("should be placed before");
                using (Cell.LineStd) Draw.Label("Bounded Scatter nodes!");
            }

            using (Cell.LineStd) Draw.Field(ref gen.type, "Type");

            if (GraphGeneratorHelper.GetAllConnectedTypes(gen).Contains(gen.type))
            {
                using (Cell.LineStd) Draw.Label("WARNING: Type already");
                using (Cell.LineStd) Draw.Label("present in graph");
            }

            using (Cell.LineStd) Draw.ToggleLeft(ref gen.useCustomIds, "Custom Ids");

            if (gen.useCustomIds && gen.LayersHaveSameNodeGuid())
            {
                using (Cell.LineStd) Draw.Label("WARNING: Some layers");
                using (Cell.LineStd) Draw.Label("have the same Id");
            }

            using (Cell.LineStd) Draw.ToggleLeft(ref gen.useRadiusPerLayer, "Radius Per Layer");

            if (!gen.useRadiusPerLayer)
                using (Cell.LineStd)
                    DrawClampedField(gen.radius, "Radius");

            using (Cell.LineStd) Draw.Label("Min Distance");
            using (Cell.LineStd) Draw.Label("To Same: " + gen.GetLocalMinDistanceToSame(gen.type));
            using (Cell.LineStd) Draw.Label("To Others: " + gen.GetLocalMinDistanceToOthers(gen.type));

            using (Cell.LinePx(20)) GeneratorDraw.DrawLayersAddRemove(gen, ref gen.layers, true);
            using (Cell.LinePx(0)) GeneratorDraw.DrawLayersThemselves(gen, gen.layers, true, DrawManualScatterGeneratorLayer);
        }

        private static void DrawManualScatterGeneratorLayer(MapMagic.Nodes.Generator generator, int num)
        {
            var gen = (ManualScatterGenerator) generator;
            var layer = gen.layers[num];
            layer.Parent = gen;

            if (layer == null) return;

            var selected = true;

            if (gen.useCustomIds)
                using (Cell.LineStd)
                    Draw.Field(ref layer.nodeGuid, "Id");

            if (selected || !gen.useCustomIds)
                using (Cell.LineStd)
                    Draw.Field(ref layer.position);

            var placing = DrawPlaceButton(layer, layer.placingPoi);

            if (placing != layer.placingPoi)
            {
                layer.placingPoi = placing;

                if (placing)
                {
                    var radius = gen.useRadiusPerLayer ? layer.radius : gen.radius;

                    SplineTools.Instance.PlacePoi(
                        vector3 =>
                        {
                            foreach (var g in ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph.generators
                                     .OfType<ManualScatterGenerator>())
                            foreach (var l in g.layers)
                            {
                                if (l == layer) continue;
                                if ((vector3 - l.position).magnitude <
                                    (gen.Radius(layer) + g.Radius(layer)) + 1) return false;
                            }

                            layer.position = vector3;
                            layer.placingPoi = false;
                            return true;
                        }, () => layer.placingPoi = false,
                        radius.ClampedValue);
                } else SplineTools.Instance.CancelPlacePoi();
            }

            if (selected)
            {
                if (gen.useRadiusPerLayer)
                {
                    using (Cell.LineStd) DrawClampedField(layer.radius, "Radius");
                }
            }
        }

        private static bool DrawPlaceButton(ManualScatterGenerator.Layer layer, bool value)
        {
            var tooltip = layer.hasConflict
                ? "POI is too close to others. Please move it or any connection to it will behave strangely."
                : "Place POI directly at mouse position with Left-Click on the terrain in the scene view.";

            bool newValue;

            // using (Cell.LineStd) 

            using (Cell.LineStd)
            {
                var texture2D = layer.hasConflict
                    ? UI.current.textures.GetTexture("MapMagic/Icons/Unpin")
                    : UI.current.textures.GetTexture("MapMagic/Icons/Pin");
                newValue = Draw.CheckButton(value, texture2D);
                // Draw.Icon(texture2D);
            }

            return newValue;
        }

        [Draw.EditorAttribute(typeof(PathScatterGenerator))]
        public static void DrawGenerator(PathScatterGenerator gen)
        {
            DrawHelpLink(gen);
            //inouts
            DrawInOuts(gen, ((gen.inputEdges, "Edges"), default));

            using (Cell.LineStd) Draw.Field(ref gen.seed, "Seed");
            using (Cell.LineStd) Draw.Field(ref gen.usedSide, "Side");

            using (Cell.LineStd) Draw.Field(ref gen.countMax, "Max Count");

            using (Cell.LineStd) Draw.ToggleLeft(ref gen.startAtSource, "Start at Source");
            using (Cell.LineStd) Draw.ToggleLeft(ref gen.alignHeight, "Align Height");
            using (Cell.LineStd) Draw.ToggleLeft(ref gen.alignRotation, "Align Rotation");

            if (gen.usedSide == Side.Both || gen.usedSide == Side.Random)
                using (Cell.LineStd)
                    Draw.ToggleLeft(ref gen.reverseRotation, "Reverse Other Rotation");

            DrawClampedFieldsAndSimpleCurve(gen.stepLengthFalloff, gen.stepLengthMin, gen.stepLengthMax, "Step");
            if (gen.usedSide == Side.Both)
                using (Cell.LineStd)
                    Draw.ToggleLeft(ref gen.mirrorSteps, "Mirror Step");

            DrawClampedFieldsAndSimpleCurve(gen.distanceFalloff, gen.distanceMin, gen.distanceMax, "Offset");
            if (gen.usedSide == Side.Both && gen.mirrorSteps)
                using (Cell.LineStd)
                    Draw.ToggleLeft(ref gen.mirrorDistance, "Mirror Offset");
        }

        [Draw.EditorAttribute(typeof(RadiusScatterGenerator))]
        public static void DrawGenerator(RadiusScatterGenerator gen)
        {
            if (SplineTools.HasEditorInstance())
                Generator.EnsureGuidIsUnique(gen.startingGraphGuid, gen, newGuid => gen.startingGraphGuid = newGuid);

            if (NotPlacedInRootGraph(gen)) return;

            DrawInOuts(gen,
                ((gen.inputGraph, "Graph"), (gen.outputGraph, "Graph")),
                ((gen.inputHeights, "Heights"), (gen.outputNodes, "Nodes")),
                ((gen.inputMask, "Mask"), default)
            );

            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            var inputGraphGenerator = MapMagicUtil.GetInputGraphGenerator(gen.inputGraph);

            using (Cell.LineStd) Draw.Field(ref gen.seed, "Seed");
            using (Cell.LineStd) Draw.Field(ref gen.type, "Type");

            if (inputGraphGenerator != null)
            {
                var types = gen.Types();

                if (Enumerable.Contains(types, gen.type) || GraphGeneratorHelper.GetAllConnectedTypes(gen).Contains(gen.type))
                {
                    using (Cell.LineStd) Draw.Label("WARNING: Type already");
                    using (Cell.LineStd) Draw.Label("present in graph");
                }
            }

            DrawClampedFieldsAndSimpleCurve(gen.radiusFalloff, gen.radiusMin, gen.radiusMax, "Radius");

            if (inputGraphGenerator != null)
            {
                var types = gen.Types();

                //params
                using (Cell.LineStd) Draw.Label("Around");

                using (Cell.LineStd) gen.aroundType = Draw.PopupSelector(gen.aroundType, types);
                gen.aroundType = Mathf.Min(gen.aroundType, types.Length - 1);
            }

            using (Cell.LineStd) DrawClampedField(gen.outerMargin, "Outer Margin");

            DrawFeature(ref gen.distanceLimit, "Limit Distance", () =>
            {
                using (Cell.LineStd) DrawClampedField(gen.distanceToSameMin, "To Same Type");
                using (Cell.LineStd) DrawClampedField(gen.distanceToOthersMin, "To Other Types");
            });

            using (Cell.LineStd) DrawClampedField(gen.countMax, "Max Count");

            if (gen.AllowDeferredProcessing())
                using (Cell.LineStd)
                    Draw.ToggleLeft(ref gen.precache, "Precache");
        }

        [Draw.EditorAttribute(typeof(ExtractNodesGenerator))]
        public static void DrawGenerator(ExtractNodesGenerator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, ((gen.inputEdges, "Edges"), default));

            using (Cell.LineStd) Draw.ToggleLeft(ref gen.includeEndpoint, "Endpoint");
            using (Cell.LineStd) Draw.ToggleLeft(ref gen.includeSection, "Section");
            using (Cell.LineStd) Draw.ToggleLeft(ref gen.includeCrossing, "Crossing");
            using (Cell.LineStd) Draw.ToggleLeft(ref gen.includeCrossingPerimeter, "Crossing Perimeter");
            using (Cell.LineStd) Draw.ToggleLeft(ref gen.includePerimeter, "Perimeter");
        }

        [Draw.EditorAttribute(typeof(CombineNodesGenerator))]
        public static void DrawGenerator(CombineNodesGenerator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, (default, (gen.outputNodes, "Nodes")));

            using (Cell.LinePx(0)) GeneratorDraw.DrawLayersThemselves(gen, gen.layers, true, DrawCombineNodesGeneratorLayer);
            using (Cell.LinePx(20)) GeneratorDraw.DrawLayersAddRemove(gen, ref gen.layers, true);
        }

        private static void DrawCombineNodesGeneratorLayer(MapMagic.Nodes.Generator generator, int num)
        {
            var gen = (CombineNodesGenerator) generator;
            var layer = gen.layers[num];
            layer.Parent = gen;

            if (layer == null) return;

            Cell.EmptyLinePx(2);
            using (Cell.LineStd)
            {
                using (Cell.RowPx(0))
                    GeneratorDraw.DrawInlet(layer.inputNodes, gen);
                Cell.EmptyRowPx(10);
                using (Cell.RowPx(15)) Draw.Icon(UI.current.textures.GetTexture("DPUI/Icons/Layer"));
                using (Cell.Row) Draw.Label("Layer " + num);
            }

            Cell.EmptyLinePx(2);
        }

        [Draw.EditorAttribute(typeof(SplitNodesGenerator))]
        public static void DrawGenerator(SplitNodesGenerator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, ((gen.inputNodes, "Nodes"), default));

            using (Cell.LineStd) Draw.Field(ref gen.matchType, "Match Type");

            using (Cell.LinePx(20)) GeneratorDraw.DrawLayersAddRemove(gen, ref gen.layers, true);
            using (Cell.LinePx(0)) GeneratorDraw.DrawLayersThemselves(gen, gen.layers, true, DrawSplitNodesGeneratorLayer);
        }

        private static void DrawSplitNodesGeneratorLayer(MapMagic.Nodes.Generator generator, int num)
        {
            var gen = (SplitNodesGenerator) generator;
            var layer = gen.layers[num];
            layer.Parent = gen;

            using (Cell.LineStd)
            {
                using (Cell.Padded(0, 20, 0, 0)) Draw.Field(ref layer.label);
                using (Cell.Padded(130, 0, 0, 0)) Draw.LayerChevron(num, ref gen.guiExpanded);
            }

            DrawInOuts(gen, (default, (layer.outputNodes, "Nodes")));

            if (num == gen.guiExpanded)
            {
                if (gen.matchType == SplitNodesHelper.MatchType.layered)
                {
                    DrawFeature(ref layer.heightFilter, "Filter Height", () =>
                    {
                        DrawClampedField(layer.heightMin, "Min");
                        DrawClampedField(layer.heightMax, "Max");
                    });

                    DrawFeature(ref layer.radiusFilter, "Filter Radius", () =>
                    {
                        DrawClampedField(layer.radiusMin, "Min");
                        DrawClampedField(layer.radiusMax, "Max");
                    });

                    DrawFeature(ref layer.typeFilter, "Filter Type", () =>
                    {
                        var baseTypeNames = Enum.GetNames(typeof(NodeBaseType));
                        var baseTypes = (NodeBaseType[]) Enum.GetValues(typeof(NodeBaseType));
                        Draw.PopupSelector(ref layer.nodeBaseType, baseTypes, baseTypeNames, "BaseType");
                    });
                } else
                    using (Cell.LineStd)
                        DrawClampedField(layer.weight, "Weight");
            }
        }

        [Draw.EditorAttribute(typeof(NodesToObjectsGenerator))]
        public static void DrawGenerator(NodesToObjectsGenerator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, ((gen.inputNodes, "Nodes"), default));

            using (Cell.LineStd) Draw.Toggle(ref gen.alignRotation, "Align Rotation");
        }

        [Draw.EditorAttribute(typeof(EdgesToNodesGenerator))]
        public static void DrawGenerator(EdgesToNodesGenerator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, ((gen.inputEdges, "Edges"), (gen.outputNodes, "Nodes")));
        }
    }
}

#endif
