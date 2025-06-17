#if ST_MM_2 && ST_MM_2_BIOMES

using System;
using System.Collections.Generic;
using MapMagic.Nodes;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Biomes", name = "Biome Nodes Exit", disengageable = false, colorType = typeof(NodesByOffset),
        iconName = "GeneratorIcons/PortalOut",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/biome/biome_nodes")]
    public class BiomeNodesExit : BiomePortalExit<BiomeNodesEnter>, IMultiOutlet
    {
        public string linkedRefGuid = "";
        public Outlet<NodesByOffset> outputNodes = new Outlet<NodesByOffset>();

        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return outputNodes;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            var enterNode = Enter();

            if (enterNode == null)
            {
                tileData.StoreProduct(outputNodes, new NodesByOffset());
                return;
            }

            var newTileData = MapMagicUtil.NewTileData(tileData);
            var rootGraph = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph;
            rootGraph.GenerateRecursive(enterNode, newTileData, rootGraph.defaults, stop);

            var edges = newTileData.ReadInletProduct(enterNode.inputNodes);

            tileData.StoreProduct(outputNodes, edges);
        }

        public override string LinkedRefGuid()
        {
            return linkedRefGuid;
        }
    }
}

#endif
