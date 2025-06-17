using System;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    public struct HeightMapOffset
    {
        public readonly int x;
        public readonly int z;

        public HeightMapOffset(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public static HeightMapOffset For(Vector3 vector3, int tileResolution, int heightMapResolution)
        {
            return For(vector3.V2(), tileResolution, heightMapResolution);
        }

        public static HeightMapOffset For(Vector2 vector2, int tileResolution, int heightMapResolution)
        {
            return For(vector2.x, vector2.y, tileResolution, heightMapResolution);
        }

        public static HeightMapOffset For(float x, float z, int tileResolution, int heightMapResolution)
        {
            var mappingFactor = (float) heightMapResolution / tileResolution;
            return new HeightMapOffset(Util.OffsetValue(x * mappingFactor, heightMapResolution),
                Util.OffsetValue(z * mappingFactor, heightMapResolution));
        }

        public override string ToString()
        {
            return "HeightMapOffset(" + x + ", " + z + ")";
        }

        public bool Equals(HeightMapOffset other)
        {
            return x == other.x && z == other.z;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is HeightMapOffset && Equals((HeightMapOffset) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (x * 397) ^ z;
            }
        }
    }
}