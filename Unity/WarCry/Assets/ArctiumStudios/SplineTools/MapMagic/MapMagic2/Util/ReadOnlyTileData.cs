#if ST_MM_2
using Den.Tools.Matrices;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    public class ReadOnlyTileData : TileData
    {
        public ReadOnlyTileData(TileData src)
        {
            area = src.area;
            globals = src.globals;
            heights = src.heights == null ? null : new MatrixWorld(src.heights);
            random = new Den.Tools.Noise(src.random);
            isDraft = src.isDraft;
            isPreview = src.isPreview;
            // don't copy cached subDatas to allow functions with subGraphs to work

            Clear();
        }
    }
}
#endif
