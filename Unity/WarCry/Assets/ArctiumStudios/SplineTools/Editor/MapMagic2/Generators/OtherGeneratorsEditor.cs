#if ST_MM_2

using Den.Tools.GUI;

namespace ArctiumStudios.SplineTools
{
    public class OtherGeneratorsEditor : GeneratorsEditor
    {
        [Draw.EditorAttribute(typeof(BoundsGenerator))]
        public static void DrawGenerator(BoundsGenerator gen)
        {
            DrawHelpLink(gen);
            if (NotPlacedInRootGraph(gen)) return;
            DrawInOuts(gen, (default, (gen.outputBounds, "Bounds")));

            using (Cell.LineStd) Draw.Field(ref gen.usedSpace, "Space");
            using (Cell.LineStd) DrawClampedField(gen.size, "Size");
            using (Cell.LineStd) Draw.Field(ref gen.offset, "Offset");
        }
    }
}

#endif
