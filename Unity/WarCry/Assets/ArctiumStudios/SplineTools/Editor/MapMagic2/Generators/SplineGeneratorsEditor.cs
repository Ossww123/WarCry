#if ST_MM_2

using ArctiumStudios.SplineTools.Generators.Spline;
using Den.Tools.GUI;
using MapMagic.Nodes;
using MapMagic.Nodes.GUI;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class SplineGeneratorsEditor : GeneratorsEditor
    {
        [Draw.EditorAttribute(typeof(ConnectionsGeneratorV2))]
        public static void DrawGenerator(ConnectionsGeneratorV2 gen)
        {
            DrawHelpLink(gen);
            if (NotPlacedInRootGraph(gen)) return;

            DrawInOuts(gen,
                ((gen.inputGraph, "Graph"), (gen.outputGraph, "Graph")),
                ((gen.inputHeights, "Heights"), default),
                ((gen.inputRiverGraph, "River Graph"), default)
            );

            using (Cell.LinePx(20)) GeneratorDraw.DrawLayersAddRemove(gen, ref gen.layers, true);
            using (Cell.LinePx(0)) GeneratorDraw.DrawLayersThemselves(gen, gen.layers, true, DrawConnectionsGeneratorV2Layer);
        }

        private static void DrawConnectionsGeneratorV2Layer(MapMagic.Nodes.Generator generator, int num)
        {
            var gen = (ConnectionsGeneratorV2) generator;
            var layer = gen.layers[num];
            layer.Parent = gen;
            
            if (SplineTools.HasEditorInstance())
                Generator.EnsureGuidIsUnique(layer.layerGuid, gen, newGuid => layer.layerGuid = newGuid);

            using (Cell.LineStd)
            {
                using (Cell.Padded(130, 0, 0, 0)) Draw.LayerChevron(num, ref gen.guiExpanded);
            }

            DrawInOuts(gen, ((layer.inputNodeMask, "Node Mask"), (layer.outputEdges, "Edges")));
            
            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            if (num == gen.guiExpanded)
            {
                using (Cell.LineStd) Draw.Field(ref layer.usedGizmoLevel, "Gizmo");

                //params
                if (MapMagicUtil.GetInputGenerator(layer.inputNodeMask) != null)
                    using (Cell.LineStd)
                        DrawClampedField(layer.nodeMaskThreshold, "Mask Threshold");

                if (layer.Types().Length == 0) return;

                using (Cell.LineStd) layer.source = Draw.PopupSelector(layer.source, layer.Types(), "Source");

                if (!layer.IsAroundType(layer.Types()[layer.source]))
                    using (Cell.LineStd)
                        Draw.ToggleLeft(ref layer.useSourcePerimeter, "Start at Perimeter");
                else layer.useSourcePerimeter = false;

                if (!layer.useSourcePerimeter && !layer.IsAroundType(layer.Types()[layer.source]) &&
                    !layer.IsAroundType(layer.Types()[layer.destination]))
                    using (Cell.LineStd)
                        Draw.ToggleLeft(ref layer.skipInsideSourceRadius, "Skip Inside Radius");
                else layer.skipInsideSourceRadius = false;

                using (Cell.LineStd) layer.destination = Draw.PopupSelector(layer.destination, layer.Types(), "Destination");

                if (!layer.IsAroundType(layer.Types()[layer.destination]))
                    using (Cell.LineStd)
                        Draw.ToggleLeft(ref layer.useDestinationPerimeter, "End at Perimeter");
                else layer.useDestinationPerimeter = false;

                if (!layer.useDestinationPerimeter && !layer.IsAroundType(layer.Types()[layer.source]) &&
                    !layer.IsAroundType(layer.Types()[layer.destination]))
                    using (Cell.LineStd)
                        Draw.ToggleLeft(ref layer.skipInsideDestinationRadius, "Skip Inside Radius");
                else layer.skipInsideDestinationRadius = false;

                layer.source = Mathf.Min(layer.source, layer.Types().Length - 1);
                layer.destination = Mathf.Min(layer.destination, layer.Types().Length - 1);

                using (Cell.LineStd) Draw.ToggleLeft(ref layer.showConfig, "Show Ext. Config");

                if (layer.showConfig)
                {
                    using (Cell.LineStd) Draw.Field(ref layer.connectionType, "Type");
                    using (Cell.LineStd) Draw.Field(ref layer.usedAlgorithm, "Algorithm");
                    using (Cell.LineStd) DrawClampedField(layer.bezierSectionLength, "Section Length");
                    using (Cell.LineStd) DrawClampedField(layer.bezierSectionAngleVariance, "Section Angle Variance");
                    using (Cell.LineStd) DrawClampedField(layer.bezierControlPointAngleVariance, "Control Angle Variance");

                    using (Cell.LineStd)
                        DrawClampedFieldsAndSimpleCurve(layer.widthFalloff, layer.widthMin, layer.widthMax, "Width", true);

                    using (Cell.LineStd) DrawClampedField(layer.slopeMax, "Max Slope");
                    using (Cell.LineStd) DrawClampedField(layer.heightMin, "Min Height");

                    DrawFeature(ref layer.angleLimit, "Limit Angle",
                        () =>
                        {
                            using (Cell.LineStd) Draw.ToggleLeft(ref layer.angleLimitSource, "At Source");
                            using (Cell.LineStd) Draw.ToggleLeft(ref layer.angleLimitDestination, "At Destination");
                            using (Cell.LineStd) DrawClampedField(layer.angleMin, "Min Angle");
                        });

                    DrawFeature(ref layer.connectionsLimit, "Limit Connections",
                        () =>
                        {
                            using (Cell.LineStd) DrawClampedField(layer.connectionsMax, "-> Max");
                        });

                    DrawFeature(ref layer.distanceLimit, "Limit Distance", () =>
                    {
                        using (Cell.LineStd) DrawClampedField(layer.distanceMin, "Min");
                        using (Cell.LineStd) DrawClampedField(layer.distanceMax, "Max");
                    });

                    using (Cell.LineStd) DrawClampedField(layer.iterations, "Iterations");
                    using (Cell.LineStd) Draw.ToggleLeft(ref layer.preferDirectConnection, "Prefer Direct");
                    using (Cell.LineStd) Draw.ToggleLeft(ref layer.connectFromUnreachableOnly, "From Unreachable Only");
                    using (Cell.LineStd) Draw.ToggleLeft(ref layer.connectToReachableOnly, "To Reachable Only");

                    if (layer.source == layer.destination) // only allow fully connected graphs when the same types are used
                    {
                        using (Cell.LineStd) Draw.ToggleLeft(ref layer.connectedGraph, "Connected Graph");
                    }

                    // layout.fieldSize = 0.4f;

                    using (Cell.LineStd) DrawClampedField(layer.lookahead, "Lookahead");
                    using (Cell.LineStd) DrawClampedField(layer.pathfindingCandidates, "Candidates");
                    using (Cell.LineStd) DrawClampedField(layer.bias, "Bias");

                    using (Cell.LineStd)
                        DrawFeature(ref layer.discardRedundantConnectionActive, "Discard Redundant",
                            () =>
                            {
                                using (Cell.LineStd) DrawClampedField(layer.discardRedundantConnectionFactor, "Factor");
                            });

                    if (MapMagicUtil.GetInputGenerator(gen.inputRiverGraph) != null)
                    {
                        GroupFields("River Crossings", () =>
                        {
                            DrawFeature(ref layer.riverBridgeEnabled, "River Bridge Points", () =>
                            {
                                using (Cell.LineStd) DrawClampedField(layer.riverBridgeWidthThreshold, "Width Threshold");
                                using (Cell.LineStd) DrawClampedField(layer.riverBridgeSlopeThreshold, "Slope Threshold");
                                using (Cell.LineStd) DrawClampedField(layer.riverBridgeOffset, "Offset");
                            });

                            using (Cell.LineStd) DrawClampedField(layer.riverFordOffset, "Ford Offset");
                        });

                        DrawFeature(ref layer.lakeBridgeEnabled, "Lake Bridge Points", () =>
                        {
                            using (Cell.LineStd) DrawClampedField(layer.lakeBridgeWidthThreshold, "Width Threshold");
                            using (Cell.LineStd) DrawClampedField(layer.lakeBridgeRoutingThreshold, "Routing Threshold");
                            using (Cell.LineStd) DrawClampedField(layer.lakeBridgeOffset, "Offset");
                        });
                        
                        using (Cell.LineStd) DrawClampedField(layer.lakePathOffset, "Lake Path Offset");
                    }
                }

                if (layer.AllowDeferredProcessing())
                    using (Cell.LineStd)
                        Draw.ToggleLeft(ref layer.precache, "Precache");
            }
        }

        [Draw.EditorAttribute(typeof(CombineEdgesGenerator))]
        public static void DrawGenerator(CombineEdgesGenerator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, (default, (gen.outputEdges, "Edges")));

            using (Cell.LinePx(0))
                LayersEditor.DrawLayers(ref gen.inlets,
                    onDraw: num =>
                    {
                        if (num >= gen.inlets.Length) return; //on layer remove
                        int iNum = gen.inlets.Length - 1 - num;

                        Cell.EmptyLinePx(2);
                        using (Cell.LineStd)
                        {
                            using (Cell.RowPx(0))
                                GeneratorDraw.DrawInlet(gen.inlets[iNum], gen);
                            Cell.EmptyRowPx(10);
                            using (Cell.RowPx(15)) Draw.Icon(UI.current.textures.GetTexture("DPUI/Icons/Layer"));
                            using (Cell.Row) Draw.Label("Layer " + iNum);
                        }

                        Cell.EmptyLinePx(2);
                    },
                    onCreate: num => new Inlet<EdgesByOffset>());
        }

        [Draw.EditorAttribute(typeof(CutWaterCrossingEdgesGenerator))]
        public static void DrawGenerator(CutWaterCrossingEdgesGenerator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen,
                ((gen.inputEdges, "Edges"), (gen.outputBridgeEdges, "Bridges")),
                (default, (gen.outputFordEdges, "Fords")),
                (default, (gen.outputOtherEdges, "Other"))
            );
        }

        [Draw.EditorAttribute(typeof(SplitEdgesGenerator))]
        public static void DrawGenerator(SplitEdgesGenerator gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, ((gen.inputEdges, "Edges"), default));

            using (Cell.LineStd) Draw.Field(ref gen.matchType, "Match Type");

            using (Cell.LinePx(20)) GeneratorDraw.DrawLayersAddRemove(gen, ref gen.layers, true);
            using (Cell.LinePx(0)) GeneratorDraw.DrawLayersThemselves(gen, gen.layers, true, DrawSplitEdgesGeneratorLayer);
        }

        private static void DrawSplitEdgesGeneratorLayer(MapMagic.Nodes.Generator generator, int num)
        {
            var gen = (SplitEdgesGenerator) generator;
            var layer = gen.layers[num];
            layer.Parent = gen;

            using (Cell.LineStd)
            {
                using (Cell.Padded(0, 20, 0, 0)) Draw.Field(ref layer.label);
                using (Cell.Padded(130, 0, 0, 0)) Draw.LayerChevron(num, ref gen.guiExpanded);
            }

            DrawInOuts(gen, (default, (layer.outputEdges, "Edges")));

            if (num == gen.guiExpanded)
            {
                if (gen.matchType == SplitEdgesHelper.MatchType.layered)
                {
                    using (Cell.LineStd) Draw.Field(ref layer.crossings, "Crossings");
                    using (Cell.LineStd) Draw.Field(ref layer.length, "Length");
                } else
                    using (Cell.LineStd)
                        DrawClampedField(layer.weight, "Weight");
            }
        }

        [Draw.EditorAttribute(typeof(RiversGenerator))]
        public static void DrawGenerator(RiversGenerator gen)
        {
            DrawHelpLink(gen);
            if (SplineTools.HasEditorInstance())
                Generator.EnsureGuidIsUnique(gen.startingGraphGuid, gen, newGuid => gen.startingGraphGuid = newGuid);

            if (NotPlacedInRootGraph(gen)) return;

            DrawInOuts(gen,
                ((gen.inputGraph, "Graph"), (gen.outputGraph, "Graph")),
                ((gen.inputBounds, "Bounds"), (gen.outputNodes, "Lake Nodes")),
                ((gen.inputHeights, "Heights"), (gen.outputEdges, "Edges")),
                ((gen.inputGrowthMask, "Growth Mask"), (gen.outputLakeMask, "Lake Mask")),
                ((gen.inputDecayMask, "Decay Mask"), (gen.outputSeaMask, "Sea Mask"))
            );
            
            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            using (Cell.LineStd) Draw.Field(ref gen.usedGizmoLevel, "Gizmo");

            var inputGraphGenerator = MapMagicUtil.GetInputGraphGenerator(gen.inputGraph);

            if (inputGraphGenerator != null)
            {
                using (Cell.LineStd) gen.springType = Draw.PopupSelector(gen.springType, gen.Types(), "Spring");
                gen.springType = Mathf.Min(gen.springType, gen.Types().Length - 1);
            }

            GroupFields("Lakes", () =>
            {
                using (Cell.LineStd) Draw.Field(ref gen.lakeMode, "Mode");

                if (gen.lakeMode == RiverPathfinder.LakeMode.EnforceStartWithLakes)
                {
                    using (Cell.LineStd)
                        Draw.ToggleLeft(ref gen.useSpringAsLakeSize, "Spring Radius as Lake Size");

                    if (gen.useSpringAsLakeSize)
                    {
                        DrawClampedField(gen.initialLakeSizeModifier, "Initial Lake Size Modifier");
                    }

                }

                using (Cell.LineStd) DrawClampedField(gen.lakeSizeFactor, "Lake Size");
                using (Cell.LineStd) DrawClampedField(gen.lakeBorder, "Lake Border");

                if (gen.lakeMode != RiverPathfinder.LakeMode.OnlyLakes)
                {
                    using (Cell.LineStd) DrawClampedField(gen.outflowingRiverAngleMin, "Outfl.River Angle Min");

                    GroupFields("Outfl. River Size", () =>
                    {
                        using (Cell.LineStd)
                            DrawCurveSimple(gen.outflowingRiverSizeFalloff);
                    });
                }
            });

            // only river config below
            if (gen.lakeMode == RiverPathfinder.LakeMode.OnlyLakes) return;

            using (Cell.LineStd)
                GroupFields("Rivers", () =>
                {
                    using (Cell.LineStd) DrawClampedField(gen.growthMax, "Max Growth");
                    using (Cell.LineStd) DrawClampedField(gen.decayMax, "Max Decay");
                    using (Cell.LineStd) DrawClampedField(gen.sectionLengthFactor, "Section Length Factor");

                    using (Cell.LineStd)
                        GroupFields("Direction Change Angle", () =>
                        {
                            using (Cell.LineStd)
                                DrawClampedFieldsAndSimpleCurve(gen.riverDirectionChangeAngleFalloff, gen.riverDirectionChangeAngleMin,
                                    gen.riverDirectionChangeAngleMax, "Range", true);
                        });

                    using (Cell.LineStd)
                        GroupFields("Carve Through Height", () =>
                        {
                            using (Cell.LineStd)
                                DrawClampedFieldsAndSimpleCurve(gen.carveThroughHeightFalloff, gen.carveThroughHeightMin, gen.carveThroughHeightMax,
                                    "Range", true);
                        });

                    using (Cell.LineStd) DrawClampedField(gen.widthMax, "Max Width");
                    using (Cell.LineStd) DrawClampedField(gen.widthNoise, "Width Noise");
                    using (Cell.LineStd) DrawClampedField(gen.dryUpWidth, "Dry Up Width");
                });

            using (Cell.LineStd)
                GroupFields("Joining Rivers", () =>
                {
                    using (Cell.LineStd) DrawClampedField(gen.riverJoinAngleMax, "Angle Max");
                    using (Cell.LineStd) DrawClampedField(gen.riverJoinSlopeMax, "Slope Max");
                    using (Cell.LineStd) DrawClampedField(gen.riverJoinSlopeChangeMax, "Slope Change Max");
                });

            using (Cell.LineStd)
                GroupFields("Sea", () =>
                {
                    using (Cell.LineStd) DrawClampedField(gen.heightMin, "Sea Height");
                    using (Cell.LineStd) DrawClampedField(gen.requiredSeaDepth, "Required Sea Depth");
                    using (Cell.LineStd) DrawClampedField(gen.seaJoinWidenFactor, "Widen Factor");
                    using (Cell.LineStd) DrawClampedField(gen.seaBorder, "Sea Border");
                });
        }
    }
}

#endif
