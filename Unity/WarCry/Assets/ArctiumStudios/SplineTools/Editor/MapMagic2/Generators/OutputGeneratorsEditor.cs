#if ST_MM_2

using Den.Tools.GUI;

namespace ArctiumStudios.SplineTools
{
    public class OutputGeneratorsEditor : GeneratorsEditor
    {
#if ST_RAM_2019 || ST_RAM
        [Draw.EditorAttribute(typeof(RamLakeOutput))]
        public static void DrawGenerator(RamLakeOutput gen)
        {
            DrawHelpLink(gen);
            if (SplineTools.HasEditorInstance())
                Generator.EnsureGuidIsUnique(gen.guid, gen, newGuid => gen.guid = newGuid);

            DrawInOuts(gen, ((gen.inputNodes, "Nodes"), default));

            using (Cell.LineStd) Draw.ObjectField(ref gen.profile, "Profile");
            using (Cell.LineStd) Draw.Field(ref gen.heightOffset, "Height Offset");
        }

        [Draw.EditorAttribute(typeof(RamRiverOutput))]
        public static void DrawGenerator(RamRiverOutput gen)
        {
            DrawHelpLink(gen);
            if (SplineTools.HasEditorInstance())
                Generator.EnsureGuidIsUnique(gen.guid, gen, newGuid => gen.guid = newGuid);

            DrawInOuts(gen,
                ((gen.inputEdges, "Edges"), default),
                ((gen.inputLakeMask, "Lake Mask"), default),
                ((gen.inputSeaMask, "Sea Mask"), default)
            );

            using (Cell.LineStd) Draw.ObjectField(ref gen.profile, "Profile");
            using (Cell.LineStd) Draw.Field(ref gen.heightOffset, "Height Offset");
            using (Cell.LineStd) DrawClampedField(gen.markerDistance, "Marker Distance");
            using (Cell.LineStd) DrawClampedField(gen.widthFactor, "Width Factor");
            using (Cell.LineStd) DrawClampedField(gen.ramSplineLengthMax, "Max Spline Length");
            using (Cell.LineStd) DrawClampedField(gen.crossingOffset, "Crossing Offset");
            using (Cell.LineStd) Draw.ToggleLeft(ref gen.fadeIn, "Fade In");
            using (Cell.LineStd) Draw.ToggleLeft(ref gen.fadeOut, "Fade Out (dried up)");
        }

        [Draw.EditorAttribute(typeof(RamRoadOutput))]
        public static void DrawGenerator(RamRoadOutput gen)
        {
            DrawHelpLink(gen);
            if (SplineTools.HasEditorInstance())
                Generator.EnsureGuidIsUnique(gen.guid, gen, newGuid => gen.guid = newGuid);

            DrawInOuts(gen,
                ((gen.inputEdges, "Edges"), default)
            );

            using (Cell.LineStd) Draw.ObjectField(ref gen.profile, "Profile");
            using (Cell.LineStd) Draw.Field(ref gen.heightOffset, "Height Offset");
            using (Cell.LineStd) DrawClampedField(gen.markerDistance, "Marker Distance");
            using (Cell.LineStd) DrawClampedField(gen.widthFactor, "Width Factor");
            using (Cell.LineStd) DrawClampedField(gen.ramSplineLengthMax, "Max Spline Length");
            using (Cell.LineStd) DrawClampedField(gen.crossingOffset, "Crossing Offset");
        }
#endif
    }
}

#endif
