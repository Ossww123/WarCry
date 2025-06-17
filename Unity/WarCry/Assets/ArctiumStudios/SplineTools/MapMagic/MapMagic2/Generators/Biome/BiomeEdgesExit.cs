#if ST_MM_2 && ST_MM_2_BIOMES

using System;
using System.Collections.Generic;
using MapMagic.Nodes;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Biomes", name = "Biome Edges Exit", disengageable = false, colorType = typeof(EdgesByOffset),
        iconName = "GeneratorIcons/PortalOut",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/biome/biome_edges")]
    public class BiomeEdgesExit : BiomePortalExit<BiomeEdgesEnter>, IMultiOutlet
    {
        public string linkedRefGuid = "";
        public Outlet<EdgesByOffset> outputEdges = new Outlet<EdgesByOffset>();

        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return outputEdges;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            var enterNode = Enter();

            if (enterNode == null)
            {
                tileData.StoreProduct(outputEdges, new EdgesByOffset());
                return;
            }

            var newTileData = MapMagicUtil.NewTileData(tileData);
            var rootGraph = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph;
            rootGraph.GenerateRecursive(enterNode, newTileData, rootGraph.defaults, stop);

            var edges = newTileData.ReadInletProduct(enterNode.inputEdges);

            tileData.StoreProduct(outputEdges, edges);
        }

        public override string LinkedRefGuid()
        {
            return linkedRefGuid;
        }
    }
}

#endif
