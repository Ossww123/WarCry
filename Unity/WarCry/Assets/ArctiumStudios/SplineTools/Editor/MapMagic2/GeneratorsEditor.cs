#if ST_MM_2

using System;
using System.Collections.Generic;
using Den.Tools.GUI;
using MapMagic.Nodes;
using MapMagic.Nodes.GUI;
using UnityEditor;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class GeneratorsEditor
    {
        public static void DrawClampedField(ClampedInt value, string label = null)
        {
            using (Cell.LineStd) Draw.Field(ref value.value, label);

            if (value.value < value.ClampedValue) DrawClampError(value.ClampedValue, "Min");
            if (value.value > value.ClampedValue) DrawClampError(value.ClampedValue, "Max");
        }

        public static void DrawClampedField(ClampedFloat value, string label = null)
        {
            using (Cell.LineStd) Draw.Field(ref value.value, label);

            if (value.value < value.ClampedValue) DrawClampError(value.ClampedValue, "Min");
            if (value.value > value.ClampedValue) DrawClampError(value.ClampedValue, "Max");
        }

        public static void DrawClampedField(ClampedVector2 value, string label = null)
        {
            using (Cell.LineStd) Draw.Field(ref value.value, label);

            if (value.value.x < value.ClampedValue.x || value.value.y < value.ClampedValue.y) DrawClampError(value.ClampedValue, "Min");
            if (value.value.x > value.ClampedValue.x || value.value.y > value.ClampedValue.y) DrawClampError(value.ClampedValue, "Max");
        }

        private static void DrawClampError(object value, string type)
        {
            using (Cell.LineStd) Draw.Helpbox("@" + type + " " + value, MessageType.Warning);
        }

        public static void DrawFeature(ref bool featureToggle, string label, Action fields)
        {
            using (Cell.LineStd) Draw.ToggleLeft(ref featureToggle, label);

            if (featureToggle) GroupFields(null, fields);
        }

        public static void DrawClampedFieldsAndSimpleCurve(AnimationCurve curve, ClampedFloat min, ClampedFloat max,
            string name, bool inLayer = false)
        {
            DrawClampedFieldsAndSimpleCurve(curve, min, max, name, inLayer, false);
        }

        private static void DrawClampedFieldsAndSimpleCurve(AnimationCurve curve, ClampedFloat min, ClampedFloat max,
            string name, bool inLayer, bool autoMappedHeightFields)
        {
            var savedFieldWidth = Cell.current.fieldWidth;

            Cell.current.fieldWidth = 0.5f;

            if (autoMappedHeightFields)
                DrawClampedField(min, name);
            else
                DrawClampedField(min, name);

            if (autoMappedHeightFields)
                DrawClampedField(max);
            else
                DrawClampedField(max);

            if (Math.Abs(min.ClampedValue - max.ClampedValue) > 0.0001f) DrawCurveSimple(curve, inLayer);

            Cell.current.fieldWidth = savedFieldWidth;
        }

        public static void DrawCurveSimple(AnimationCurve curve, bool inLayer = false)
        {
            using (Cell.LinePx(GeneratorDraw.nodeWidth + 4)) //don't really know why 4
            using (Cell.Padded(5))
                Draw.AnimationCurve(curve);
        }

        public static void GroupFields(string title, Action fields)
        {
            if (title != null)
                using (Cell.LineStd)
                    Draw.Label(title);

            using (Cell.LineStd)
            using (Cell.Padded(10, 0, 0, 0))
            {
                fields.Invoke();
            }
        }

        public static void DrawInOuts(MapMagic.Nodes.Generator gen,
            // params KeyValuePair<KeyValuePair<Inlet<object>, string>, KeyValuePair<Outlet<object>, string>>[] inouts)
            params ((IInlet<object>, string), (IOutlet<object>, string))[] inouts)
        {
            foreach (var inOutPair in inouts)
            {
                using (Cell.LineStd)
                {
                    if (!inOutPair.Item1.Equals(default))
                    {
                        using (Cell.RowPx(0)) GeneratorDraw.DrawInlet(inOutPair.Item1.Item1, gen);
                        using (Cell.Padded(10, 0, 0, 0)) Draw.Label(inOutPair.Item1.Item2);
                    }

                    if (!inOutPair.Item2.Equals(default))
                    {
                        Cell.EmptyRowPx(150);
                        using (Cell.Padded(95, 10, 0, 0)) Draw.Label(inOutPair.Item2.Item2);
                        using (Cell.RowPx(0)) GeneratorDraw.DrawOutlet(inOutPair.Item2.Item1);
                    }
                }
            }
        }

        public static void DrawUnassignedWarning()
        {
            using (Cell.LinePx(16 + 16)) Draw.Helpbox("Not assigned to current \nSplineTools object");
        }

        public static void DrawHelpLink(MapMagic.Nodes.Generator gen)
        {
            using (Cell.LineStd)
            {
                if (Draw.Button("Open Documentation"))
                {
                    var genType = gen.GetType();
                    var menuAtt = GeneratorDraw.GetMenuAttribute(genType);
                    Application.OpenURL(menuAtt.helpLink);
                }
            }
        }

        public static bool NotPlacedInRootGraph(Generator gen)
        {
            if (GraphWindow.current.mapMagic != null && SplineTools.HasEditorInstance())
            {
                var mapMagicInstance = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance;
                if (mapMagicInstance.graph != MapMagicUtil.GetContainingGraph(gen))
                {
                    using (Cell.LineStd) Draw.Label("This node must be placed");
                    using (Cell.LineStd) Draw.Label("in the root graph!");
                    return true;
                }
            }

            return false;
        }

        public static bool PlacedInRootGraph(Generator gen)
        {
            if (GraphWindow.current.mapMagic != null && SplineTools.HasEditorInstance())
            {
                var mapMagicInstance = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance;
                if (mapMagicInstance.graph == MapMagicUtil.GetContainingGraph(gen))
                {
                    using (Cell.LineStd) Draw.Label("This node must be placed");
                    using (Cell.LineStd) Draw.Label("in a Biome subGraph!");
                    return true;
                }
            }

            return false;
        }
    }
}

#endif
