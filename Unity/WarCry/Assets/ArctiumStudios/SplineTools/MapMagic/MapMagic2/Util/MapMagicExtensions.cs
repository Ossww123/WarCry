#if ST_MM_2

using System;
using System.Collections.Generic;
using Den.Tools.Matrices;
using MapMagic.Terrains;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public static class Vector2MapMagicExtensions
    {
        public static Vector2 ToWorldSpace(this Vector2 vector, Area.Dimensions dimensions)
        {
            return dimensions.CoordToWorld(Mathf.FloorToInt(vector.x), Mathf.FloorToInt(vector.y)).V2();
        }

        public static Vector2 ToMapSpace(this Vector2 vector, Area.Dimensions dimensions)
        {
            var pixelSize = dimensions.worldSize.x / dimensions.rect.size.x;
            
            return new Vector2(
                vector.x.ToMapSpace(pixelSize),
                vector.y.ToMapSpace(pixelSize)
            );
        }

        public static Den.Tools.Coord ToMapSpaceCoord(this Vector2 vector, Area.Dimensions dimensions)
        {
            var vectorMapped = vector.ToMapSpace(dimensions);
            return new Den.Tools.Coord((int) vectorMapped.x, (int) vectorMapped.y);
        }
    }

    public static class Vector2DMapMagicExtensions
    {
        public static Vector2 ToVector2(this Den.Tools.Vector2D vector2D)
        {
            return new Vector2(vector2D.x, vector2D.z);
        }
    }

    public static class CoordRectExtensions
    {
        public static bool Contains(this Den.Tools.CoordRect rect, Vector3 point)
        {
            var posX = (int) (point.x + 0.5f);
            if (point.x < 0) posX--;

            var posZ = (int) (point.z + 0.5f);
            if (point.z < 0) posZ--;

            return rect.Contains(posX, posZ);
        }

        public static Rect ToWorldSpaceRect(this Den.Tools.CoordRect coordRect, Area.Dimensions dimensions)
        {
            return new Rect(
                coordRect.Min.vector2 * dimensions.PixelSize.V2(),
                new Vector2(coordRect.size.x, coordRect.size.z) * dimensions.PixelSize.V2()
                );
        }
    }

    public static class DimensionsExtensions
    {
        public static Rect ToWorldRect(this Area.Dimensions dimensions)
        {
            return new Rect(
                dimensions.worldPos.ToVector2(),
                dimensions.worldSize.ToVector2());
        }
    }

    public static class FloatMapMagicExtensions
    {
        public static float ToMapSpace(this float value, float pixelSize)
        {
            return Mathf.FloorToInt(value / pixelSize);
        }   
        
        public static float ToMapSpace(this float value, Area.Dimensions dimensions)
        {
            var pixelSize = dimensions.worldSize.x / dimensions.rect.size.x;
            return Mathf.FloorToInt(value / pixelSize);
        }

        public static float ToMapSpaceHeight(this float value)
        {
            return ToMapSpaceHeight(value, ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.globals.height);
        }

        public static float ToMapSpaceHeight(this float value, float terrainHeight)
        {
            return value / terrainHeight;
        }

        public static float ToWorldSpaceHeight(this float value)
        {
            return ToWorldSpaceHeight(value, ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.globals.height);
        }

        public static float ToWorldSpaceHeight(this float value, float terrainHeight)
        {
            return value * terrainHeight;
        }
    }

    public static class MatrixExtensions
    {
        public static Matrix Rotate(this Matrix m, float angle)
        {
            if (Math.Abs(angle) < 0.1f) return m;

            var rotation = Den.Tools.CoordinatesExtensions.EulerToQuat(angle);
            var center = m.rect.size.vector3 / 2;

            // expand matrix
            var rotatedFromCenter = rotation * -center;
            var upscaledSize = Mathf.CeilToInt(Mathf.Max((int) Mathf.Abs(rotatedFromCenter.x), (int) Mathf.Abs(rotatedFromCenter.z)) * 2);
            var upscaledRect = new Den.Tools.CoordRect(0, 0, upscaledSize, upscaledSize);

            var rotCenter = upscaledRect.size.vector3 / 2;
            var rotated = new Matrix(upscaledRect);
            var reverseRotation = Den.Tools.CoordinatesExtensions.EulerToQuat((-angle));

            for (var x = 0; x < rotated.rect.size.x; x++)
            for (var z = 0; z < rotated.rect.size.z; z++)
            {
                var srcCoord = center + reverseRotation * (new Vector3(x, 0, z) - rotCenter);
                rotated[x, z] = srcCoord.x < 0 || srcCoord.y < 0 || srcCoord.x > m.rect.size.x || srcCoord.z > m.rect.size.z
                    ? 0
                    : m.GetInterpolated(srcCoord.x, srcCoord.z);
            }

            return rotated;
        }

        public static float[,] ToFloatArray2D(this Matrix matrix)
        {
            var array = new float[matrix.rect.size.x, matrix.rect.size.z];

            for (var x = 0; x < matrix.rect.size.x; x++)
            for (var z = 0; z < matrix.rect.size.z; z++)
            {
                array[x, z] = matrix[matrix.rect.offset.x + x, matrix.rect.offset.z + z];
            }

            return array;
        }
    }

    public static class CoordToMatrixDictExtensions
    {
        public static float GetMaskValue(this Dictionary<Den.Tools.Coord, Matrix> masksByOffset, Den.Tools.Coord pos, Area.Dimensions dimensions)
        {
            var candidateOffset = new Den.Tools.Coord(
                Util.OffsetValue(pos.x, dimensions.rect.size.x),
                Util.OffsetValue(pos.z, dimensions.rect.size.x)
            );

            return masksByOffset[candidateOffset][pos];
        }
    }
}

#endif
