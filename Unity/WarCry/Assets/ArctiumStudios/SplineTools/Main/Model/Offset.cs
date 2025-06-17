using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    public struct Offset
    {
        public readonly int x;
        public readonly int z;

        public Offset(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public static Offset For(Vector3 vector3, int resolution)
        {
            return For(vector3.V2(), resolution);
        }

        public static Offset For(Vector2 vector2, int resolution)
        {
            return new Offset(Util.OffsetValue(vector2.x, resolution), Util.OffsetValue(vector2.y, resolution));
        }

        public override string ToString()
        {
            return "Offset(" + x + ", " + z + ")";
        }

        public bool Equals(Offset other)
        {
            return x == other.x && z == other.z;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Offset && Equals((Offset) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (x * 397) ^ z;
            }
        }
    }

    public static class Offsets
    {
        public static HashSet<Offset> ForRect(Rect rect, int resolution)
        {
            return For(rect.min, rect.max, resolution);
        }

        private static HashSet<Offset> For(Vector2 lowerLeft, Vector2 upperRight, int resolution)
        {
            var offsets = new HashSet<Offset>();

            var startOffset = Offset.For(lowerLeft, resolution);

            for (var x = startOffset.x; x < upperRight.x; x += resolution)
            for (var z = startOffset.z; z < upperRight.y; z += resolution)
                offsets.Add(new Offset(x, z));

            return offsets;
        }

        public static HashSet<Offset> ForRange(Vector2 vector2, float radius, int resolution)
        {
            var lowerLeft = vector2 - new Vector2(radius, radius);
            var upperRight = vector2 + new Vector2(radius, radius);

            return For(lowerLeft, upperRight, resolution);
        }

        public static HashSet<Offset> TouchedBetween(Vector2 source, Vector2 destination, int resolution)
        {
            var startOffset = Offset.For(source, resolution);
            var endOffset = Offset.For(destination, resolution);

            if (startOffset.Equals(endOffset)) return new HashSet<Offset> {startOffset};

            var minX = Mathf.Min(startOffset.x, endOffset.x);
            var minZ = Mathf.Min(startOffset.z, endOffset.z);
            var maxX = Mathf.Max(startOffset.x + resolution, endOffset.x + resolution);
            var maxZ = Mathf.Max(startOffset.z + resolution, endOffset.z + resolution);

            var bounds = new Rect(minX, minZ, maxX - minX, maxZ - minZ);

            var allOffsets = ForRect(bounds, resolution);

            var offsets = allOffsets.Where(offset => offset.Equals(startOffset)
                                                     || offset.Equals(endOffset)
                                                     || Util.BorderIntersection(source, destination,
                                                         new Rect(offset.x, offset.z, resolution, resolution)).HasValue).ToArray();

            return new HashSet<Offset>(offsets);
        }

        public static HashSet<Offset> TouchedBetween(Vector2 source, Vector2 destination, int resolution, float padding)
        {
            var startOffsets = TouchedBetween(source, destination, resolution);

            var offsets = new HashSet<Offset>();
            offsets.UnionWith(startOffsets);

            foreach (var offset in startOffsets) offsets.UnionWith(ForRange(new Vector2(offset.x, offset.z), padding, resolution));

            return offsets;
        }
    }
}