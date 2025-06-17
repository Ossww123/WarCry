#if ST_MM_2 && ST_MM_2_BIOMES

using System.Linq;
using Den.Tools.GUI;

namespace ArctiumStudios.SplineTools
{
    public class BiomesGeneratorsEditor : GeneratorsEditor
    {
        [Draw.EditorAttribute(typeof(BiomeEdgesEnter))]
        public static void DrawGenerator(BiomeEdgesEnter gen)
        {
            DrawHelpLink(gen);

            if (SplineTools.HasEditorInstance())
                Generator.EnsureGuidIsUnique(gen.refGuid, gen, newGuid => gen.refGuid = newGuid);

            if (NotPlacedInRootGraph(gen)) return;

            DrawInOuts(gen, ((gen.inputEdges, "Edges"), default));

            Cell.current.fieldWidth = 0.7f;
            using (Cell.LineStd)
                Draw.Field(ref gen.refName, "Name");
        }

        [Draw.EditorAttribute(typeof(BiomeEdgesExit))]
        public static void DrawGenerator(BiomeEdgesExit gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, (default, (gen.outputEdges, "Edges")));

            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            if (PlacedInRootGraph(gen)) return;

            var mapMagicInstance = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance;

            var nodes = mapMagicInstance.graph.GeneratorsOfType<BiomeEdgesEnter>().ToList();

            if (nodes.Any())
            {
                var refGuids = nodes.Select(n => n.refGuid).ToArray();
                var refNames = nodes.Select(n => n.refName).ToArray();

                var selectedRefGuid = gen.linkedRefGuid;

                using (Cell.LineStd)
                {
                    Cell.current.fieldWidth = 0.8f;
                    using (Cell.RowRel(1 - Cell.current.fieldWidth)) Draw.Label("Ref");
                    using (Cell.RowRel(Cell.current.fieldWidth))
                    {
                        var selected = Draw.PopupSelector(selectedRefGuid, refGuids, refNames, "Ref");
                        gen.linkedRefGuid = selected;
                    }
                }
            } else
            {
                DrawNoNodeFoundInRootGraphInfo("BiomeEdgesEnter");
            }
        }

        [Draw.EditorAttribute(typeof(BiomeNodesEnter))]
        public static void DrawGenerator(BiomeNodesEnter gen)
        {
            DrawHelpLink(gen);
            if (SplineTools.HasEditorInstance())
                Generator.EnsureGuidIsUnique(gen.refGuid, gen, newGuid => gen.refGuid = newGuid);

            if (NotPlacedInRootGraph(gen)) return;

            DrawInOuts(gen, ((gen.inputNodes, "Nodes"), default));

            Cell.current.fieldWidth = 0.7f;
            using (Cell.LineStd)
                Draw.Field(ref gen.refName, "Name");
        }

        [Draw.EditorAttribute(typeof(BiomeNodesExit))]
        public static void DrawGenerator(BiomeNodesExit gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, (default, (gen.outputNodes, "Nodes")));

            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            if (PlacedInRootGraph(gen)) return;

            var mapMagicInstance = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance;

            var nodes = mapMagicInstance.graph.GeneratorsOfType<BiomeNodesEnter>().ToList();

            if (nodes.Any())
            {
                var refGuids = nodes.Select(n => n.refGuid).ToArray();
                var refNames = nodes.Select(n => n.refName).ToArray();

                var selectedRefGuid = gen.linkedRefGuid;

                using (Cell.LineStd)
                {
                    Cell.current.fieldWidth = 0.8f;
                    using (Cell.RowRel(1 - Cell.current.fieldWidth)) Draw.Label("Ref");
                    using (Cell.RowRel(Cell.current.fieldWidth))
                    {
                        var selected = Draw.PopupSelector(selectedRefGuid, refGuids, refNames, "Ref");
                        gen.linkedRefGuid = selected;
                    }
                }
            } else
            {
                DrawNoNodeFoundInRootGraphInfo("BiomeNodesEnter");
            }
        }

        [Draw.EditorAttribute(typeof(BiomeObjectsEnter))]
        public static void DrawGenerator(BiomeObjectsEnter gen)
        {
            DrawHelpLink(gen);
            if (SplineTools.HasEditorInstance())
                Generator.EnsureGuidIsUnique(gen.refGuid, gen, newGuid => gen.refGuid = newGuid);

            if (NotPlacedInRootGraph(gen)) return;

            DrawInOuts(gen, ((gen.inputTransitionsList, "Objects"), default));

            Cell.current.fieldWidth = 0.7f;
            using (Cell.LineStd)
                Draw.Field(ref gen.refName, "Name");
        }

        [Draw.EditorAttribute(typeof(BiomeObjectsExit))]
        public static void DrawGenerator(BiomeObjectsExit gen)
        {
            DrawHelpLink(gen);
            DrawInOuts(gen, (default, (gen.outputTransitionsList, "Objects")));

            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            if (PlacedInRootGraph(gen)) return;

            var mapMagicInstance = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance;

            var nodes = mapMagicInstance.graph.GeneratorsOfType<BiomeObjectsEnter>().ToList();

            if (nodes.Any())
            {
                var refGuids = nodes.Select(n => n.refGuid).ToArray();
                var refNames = nodes.Select(n => n.refName).ToArray();

                var selectedRefGuid = gen.linkedRefGuid;

                using (Cell.LineStd)
                {
                    Cell.current.fieldWidth = 0.8f;
                    using (Cell.RowRel(1 - Cell.current.fieldWidth)) Draw.Label("Ref");
                    using (Cell.RowRel(Cell.current.fieldWidth))
                    {
                        var selected = Draw.PopupSelector(selectedRefGuid, refGuids, refNames, "Ref");
                        gen.linkedRefGuid = selected;
                    }
                }
            } else
            {
                DrawNoNodeFoundInRootGraphInfo("BiomeObjectsEnter");
            }
        }

        private static void DrawNoNodeFoundInRootGraphInfo(string requiredGen)
        {
            using (Cell.LineStd) Draw.Label("No " + requiredGen);
            using (Cell.LineStd) Draw.Label("nodes found in");
            using (Cell.LineStd) Draw.Label("root graph!");
        }

        [Draw.EditorAttribute(typeof(BiomeHeightSyncGenerator))]
        public static void DrawGenerator(BiomeHeightSyncGenerator gen)
        {
            DrawHelpLink(gen);
            if (!SplineTools.HasEditorInstance())
            {
                DrawUnassignedWarning();
                return;
            }

            if (PlacedInRootGraph(gen)) return;

            var mapMagicInstance = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance;
            var nodes = mapMagicInstance.graph.GeneratorsOfType<BiomeHeightSyncReferenceGenerator>().ToList();

            if (nodes.Any())
            {
                var refGuids = nodes.Select(n => n.refGuid).ToArray();
                var refNames = nodes.Select(n => n.refName).ToArray();

                var selectedRefGuid = gen.linkedRefGuid;

                using (Cell.LineStd)
                {
                    Cell.current.fieldWidth = 0.8f;
                    using (Cell.RowRel(1 - Cell.current.fieldWidth)) Draw.Label("Ref");
                    using (Cell.RowRel(Cell.current.fieldWidth))
                    {
                        var selected = Draw.PopupSelector(selectedRefGuid, refGuids, refNames, "Ref");
                        gen.linkedRefGuid = selected;
                    }
                }
            } else
            {
                Draw.Label("No BiomeHeightSyncReference nodes found!");
            }
        }

        [Draw.EditorAttribute(typeof(BiomeHeightSyncReferenceGenerator))]
        public static void DrawGenerator(BiomeHeightSyncReferenceGenerator gen)
        {
            DrawHelpLink(gen);
            if (SplineTools.HasEditorInstance())
                Generator.EnsureGuidIsUnique(gen.refGuid, gen, newGuid => gen.refGuid = newGuid);

            if (NotPlacedInRootGraph(gen)) return;

            Cell.current.fieldWidth = 0.7f;
            using (Cell.LineStd)
                Draw.Field(ref gen.refName, "Name");
        }
    }
}

#endif
