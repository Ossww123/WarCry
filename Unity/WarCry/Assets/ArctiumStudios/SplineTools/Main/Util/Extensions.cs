using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace ArctiumStudios.SplineTools
{
    public static class Vector3Extensions
    {
        public static Vector2 V2(this Vector3 vector)
        {
            return new Vector2(vector.x, vector.z);
        }

        public static Vector3 RotateForSlope(this Vector3 vector, float maxSlope)
        {
            var sectionAngle = vector.AngleToGround();

            // angle already within bounds 
            if (sectionAngle >= 0 && sectionAngle < maxSlope || sectionAngle < 0 && sectionAngle > -maxSlope) return vector;
            
            var axis = Vector3.Cross(vector, Vector3.up);
            var rotationAngle = sectionAngle > 0 ? maxSlope : -maxSlope;

            return Quaternion.AngleAxis(rotationAngle, axis) * vector.V2().V3();
        }

        public static Vector3 RotateAngle(this Vector3 vector, float offsetAngle, Vector3 axis)
        {
            var sectionAngle = vector.AngleToGround();
            var rotatedVector = Quaternion.AngleAxis(offsetAngle, axis) * vector;

            if (Mathf.Abs(rotatedVector.AngleToGround()) > Mathf.Abs(sectionAngle))
                rotatedVector = Quaternion.AngleAxis(offsetAngle, -axis) * vector;

            var lengthModifier = vector.V2().magnitude / rotatedVector.V2().magnitude;

            var projectedRotatedVector = float.IsNaN(lengthModifier) ? rotatedVector : rotatedVector * lengthModifier;
            
            return projectedRotatedVector;
        }

        /// <param name="vector"></param>
        /// <returns>Angle in degrees</returns>
        public static float AngleToGround(this Vector3 vector)
        {
            return vector.AngleToAxis(Vector3.up);
        }

        public static float AngleToAxis(this Vector3 vector, Vector3 normal)
        {
            var projected = Vector3.ProjectOnPlane(vector, normal);
            var angleToGround = Vector3.Angle(vector, projected);
            return vector.y >= 0 ? angleToGround : -angleToGround;
        }

        public static string ToPreciseString(this Vector3 vector3)
        {
            return "(" + vector3.x + ", " + vector3.y + ", " + vector3.z + ")";
        }
        
        public static Vector3 Flattened(this Vector3 vector)
        {
            return new Vector3(vector.x, 0, vector.z);
        }
    }

    public static class Vector2Extensions
    {
        public static Vector3 V3(this Vector2 vector)
        {
            return vector.V3(0);
        }

        public static Vector3 V3(this Vector2 vector, float height)
        {
            return new Vector3(vector.x, height, vector.y);
        }

        public static bool Inside(this Vector2 vector2, Rect rect)
        {
            return rect.Contains(vector2);
        }

        public static bool InsidePolygon(this Vector2 p, List<Vector2> polygon)
        {
            // point is completely inside the polygon
            var result = false;
            var j = polygon.Count - 1;
            for (var i = 0; i < polygon.Count; i++)
            {
                if (polygon[i].y < p.y && polygon[j].y >= p.y || polygon[j].y < p.y && polygon[i].y >= p.y)
                {
                    if (polygon[i].x + (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) * (polygon[j].x - polygon[i].x) < p.x)
                    {
                        result = !result;
                    }
                }

                j = i;
            }

            return result;
        }

        public static bool MatrixCellIntersectsPolygon(this Vector2 p, List<Vector2> polygon)
        {
            // matrix cell intersects with polygon
            var leftUp = Vector2.left + Vector2.up;
            var leftDown = Vector2.left;
            var rightUp = Vector2.up;
            var rightDown = Vector2.zero;

            for (var i = 1; i < polygon.Count; i++)
            {
                if (Util.Intersect(p + leftUp, p + leftDown, polygon[i - 1], polygon[i]).HasValue // LEFT
                    || Util.Intersect(p + rightUp, p + rightDown, polygon[i - 1], polygon[i]).HasValue // RIGHT
                    || Util.Intersect(p + leftUp, p + rightUp, polygon[i - 1], polygon[i]).HasValue // TOP
                    || Util.Intersect(p + leftDown, p + rightDown, polygon[i - 1], polygon[i]).HasValue) // BOTTOM
                    return true;
            }

            return false;
        }

        public static List<Vector2> CircleAround(this Vector2 center, float radius)
        {
            var perimeter = 2 * Mathf.PI * radius;
            var numPoints = (int) (perimeter * 2);

            var step = 2 * Mathf.PI / numPoints;
            var points = new List<Vector2>();

            for (var i = 0; i < numPoints; i++)
            {
                var angle = i * step;
                var dir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                points.Add(center + dir * radius);
            }

            return points;
        }
    }

    public static class Vector2ListExtensions
    {
        public static float SumSectionLengths(this List<Vector2> list)
        {
            var sum = 0f;

            for (var i = 1; i < list.Count; i++) sum += (list[i] - list[i - 1]).magnitude;

            return sum;
        }
    }

    public static class RectExtensions
    {
        public static bool HasBorder(this Rect rect, Vector2 point)
        {
            return point.x - rect.min.x >= 0 && point.x - rect.min.x <= rect.size.x &&
                   point.y - rect.min.y >= 0 && point.y - rect.min.y <= rect.size.y;
        }

        public static Rect Scaled(this Rect rect, float scale)
        {
            return new Rect(
                rect.position * scale,
                rect.size * scale);
        }
    }

    public static class FloatExtensions
    {
        public static bool Between(this float value, float from, float to)
        {
            return value < to && value > from || value > to && value < from;
        }

        public static bool AboveBoth(this float value, float from, float to)
        {
            return value > to && value > from;
        }

        public static bool BelowBoth(this float value, float from, float to)
        {
            return value < to && value < from;
        }

        public static float ClampMin(this float value, float min)
        {
            return Mathf.Max(min, value);
        }

        public static float ClampMax(this float value, float max)
        {
            return Mathf.Min(max, value);
        }

        public static float Clamp(this float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        public static float Clamp01(this float value)
        {
            return Mathf.Clamp01(value);
        }
    }

    public static class IntExtensions
    {
        public static int ClampMin(this int value, int min)
        {
            return Mathf.Max(min, value);
        }

        public static int ClampMax(this int value, int max)
        {
            return Mathf.Min(max, value);
        }

        public static int Clamp(this int value, int min, int max)
        {
            return Mathf.Clamp(value, min, max);
        }
    }

    public static class EdgeListExtensions
    {
        public static Node SourceEndpoint(this List<Edge> section)
        {
            return section.First().Source();
        }

        public static Node DestinationEndpoint(this List<Edge> section)
        {
            return section.Last().Destination();
        }

        public static List<Node> GetCrossingNodes(this List<Edge> edges)
        {
            return edges.SelectMany(e => e.Nodes()).Where(n => n.Type().IsCrossing()).Distinct().ToList();
        }
    }

    public static class Vector4Extensions
    {
        public static Vector3 V3(this Vector4 vector4)
        {
            return new Vector3(vector4.x, vector4.y, vector4.z);
        }

        public static Vector2 V2(this Vector4 vector4)
        {
            return new Vector2(vector4.x, vector4.z);
        }
    }

    public static class OffsetToConnectionListDictExtensions
    {

    }

    public static class SystemRandomExtensions
    {
        public static float NextFloat(this Random rnd)
        {
            return (float) rnd.NextDouble();
        }

        public static float NextFloat(this Random rnd, float min, float max)
        {
            return min + (float) rnd.NextDouble() * (max - min);
        }

        public static Guid NextGuid(this Random rnd, params Vector3[] positions)
        {
            var bytes = new byte[16];
            float positionSalt = 1;

            for (var i = 0; i < positions.Length; i++)
            {
                var position = positions[i];
                positionSalt *= (Mathf.Abs(position.x) + 377 + i) * (Mathf.Abs(position.y) + 377 + i) * (Mathf.Abs(position.z) + 377 + i);
            }

            for (var i = 0; i < 16; i += 3)
            {
                var newBytes = BitConverter.GetBytes(positionSalt * rnd.NextFloat());
                for (var j = 0; j < 3 && i + j < 16; j++) bytes[i + j] = newBytes[j + 1];
            }

            return new Guid(bytes);
        }
    }

    public static class HeightProcessMatrixExtensions
    {
        public static float GetInterpolated(this BorderHelper.HeightProcess[,] array, float x, float z, float height)
        {
            var sizeX = array.GetLength(0);
            var sizeY = array.GetLength(1);

            var px = (int) x;
            var nx = px + 1;
            if (nx >= sizeX) nx = sizeX - 1;

            var py = (int) z;
            var ny = py + 1;
            if (ny >= sizeY) ny = sizeY - 1;

            var percentX = x - px;
            var percentZ = z - py;

            var valFy = array.ProcessedHeight(px, py, height) * (1 - percentX) + array.ProcessedHeight(nx, py, height) * percentX;
            var valCy = array.ProcessedHeight(px, ny, height) * (1 - percentX) + array.ProcessedHeight(nx, ny, height) * percentX;
            var val = valFy * (1 - percentZ) + valCy * percentZ;

            return val;
        }

        public static float ProcessedHeight(this BorderHelper.HeightProcess[,] array, int x, int z, float height)
        {
            var heightProcess = array[x, z];
            if (heightProcess == null) return height;
            return heightProcess.ProcessedHeight(height);
        }
    }

    public static class AnimationCurveExtensions
    {
        public static float EvaluateClamped(this AnimationCurve curve, float t)
        {
            return ClampedCurve.For(curve).Evaluate(t);
        }
    }

    public static class IEnumerableExtensions
    {
        public static T MinBy<T>(this IEnumerable<T> elements, Func<T, IComparable> func)
        {
            return CompareBy(elements, func, (one, two) => one.CompareTo(two) < 0);
        }

        public static T MaxBy<T>(this IEnumerable<T> elements, Func<T, IComparable> func)
        {
            return CompareBy(elements, func, (one, two) => one.CompareTo(two) > 0);
        }

        public static T CompareBy<T>(this IEnumerable<T> elements, Func<T, IComparable> func, Func<IComparable, IComparable, bool> compare)
        {
            IComparable min = null;
            T minElement = default(T);

            foreach (var elem in elements)
            {
                var comparable = func.Invoke(elem);

                if (min == null || compare.Invoke(comparable, min))
                {
                    min = comparable;
                    minElement = elem;
                }
            }

            return minElement;
        }
    }
}
