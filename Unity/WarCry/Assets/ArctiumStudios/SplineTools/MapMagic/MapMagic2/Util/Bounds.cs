#if ST_MM_2

using System;
using MapMagic.Terrains;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class Bounds : ICloneable
    {
        public readonly Rect rect;

        public Bounds(float offsetX, float offsetZ, float sizeX, float sizeZ)
        {
            this.rect = new Rect(offsetX, offsetZ, sizeX, sizeZ);
        }

        public Den.Tools.CoordRect ToCoordRect(Area area)
        {
            var factor = area.active.rect.size.x / area.active.worldSize.x;

            return new Den.Tools.CoordRect(
                rect.x * factor,
                rect.y * factor,
                rect.width * factor,
                rect.height * factor
            );
        }

        public object Clone()
        {
            return new Bounds(rect.min.x, rect.min.y, rect.size.x, rect.size.y);
        }

        public override string ToString()
        {
            return rect.ToString();
        }
    }
}

#endif
