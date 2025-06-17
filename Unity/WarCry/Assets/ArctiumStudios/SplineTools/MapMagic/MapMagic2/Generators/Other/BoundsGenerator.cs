#if ST_MM_2

using System;
using System.Collections.Generic;
using MapMagic.Nodes;
using MapMagic.Products;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Other", name = "Bounds", disengageable = false, colorType = typeof(Bounds),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/other/bounds")]
    public class BoundsGenerator : Generator, IMultiOutlet
    {
        public enum Space
        {
            MapMagic,
            World,
            Chunk
        }

        public Space usedSpace = Space.Chunk;
        public ClampedVector2 size = new ClampedVector2(new Vector2(3f, 3f), 0f, float.MaxValue);
        public Vector2 offset;

        public Outlet<Bounds> outputBounds = new Outlet<Bounds>();

        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return outputBounds;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            Bounds bounds;

            var clampedSize = size.ClampedValue;
            var tileSize = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tileSize;
            var tileResolution = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tileResolution;

            switch (usedSpace)
            {
                case Space.MapMagic:
                    bounds = new Bounds(
                        ToWorldSpace(offset.x, tileSize.x, (int) tileResolution),
                        ToWorldSpace(offset.y, tileSize.x, (int) tileResolution),
                        ToWorldSpace(clampedSize.x, tileSize.x, (int) tileResolution),
                        ToWorldSpace(clampedSize.y, tileSize.x, (int) tileResolution)
                    );
                    break;
                case Space.World:
                    bounds = new Bounds(offset.x, offset.y, clampedSize.x, clampedSize.y);
                    break;
                case Space.Chunk:
                    bounds = new Bounds(
                        offset.x * tileSize.x,
                        offset.y * tileSize.z,
                        clampedSize.x * tileSize.x,
                        clampedSize.y * tileSize.z
                    );
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            tileData.StoreProduct(outputBounds, bounds);
        }

        private float ToWorldSpace(float value, float tileSize, int tileResolution)
        {
            return value * (tileSize / (tileResolution - 1));
        }
    }
}

#endif
