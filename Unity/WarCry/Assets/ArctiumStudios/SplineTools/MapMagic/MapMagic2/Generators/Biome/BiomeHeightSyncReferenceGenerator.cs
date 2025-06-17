#if ST_MM_2 && ST_MM_2_BIOMES

using System;
using Den.Tools.Matrices;
using MapMagic.Nodes;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Biomes",
        name = "Biome Height Sync Reference", colorType = typeof(MatrixWorld),
        iconName = "GeneratorIcons/PortalOut",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/biome/biome_height_sync")]
    public class BiomeHeightSyncReferenceGenerator : Generator, IOutlet<MatrixWorld>
    {
        public string refGuid = Guid.NewGuid().ToString();
        public string refName = "sync";

        public override void Generate(TileData tileData, StopToken stop)
        {
            if (stop != null && stop.stop || !enabled)
            {
                tileData.StoreProduct(this, new MatrixWorld(tileData.area.full.rect, tileData.area.full.worldPos,
                    tileData.area.full.worldSize, tileData.globals.height));
                return;
            }

            var matrix = BiomeHeightSyncGenerator.GetOrCacheCombinedBiomesHeightMap(tileData, refGuid, stop);

            tileData.StoreProduct(this, matrix);
        }
    }
}

#endif
