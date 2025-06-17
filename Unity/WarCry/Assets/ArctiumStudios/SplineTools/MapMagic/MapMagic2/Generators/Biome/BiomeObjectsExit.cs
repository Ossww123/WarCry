#if ST_MM_2 && ST_MM_2_BIOMES

using System;
using System.Collections.Generic;
using MapMagic.Nodes;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Biomes", name = "Biome Objects Exit", disengageable = false, colorType = typeof(Den.Tools.TransitionsList),
        iconName = "GeneratorIcons/PortalOut",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/biome/biome_objects")]
    public class BiomeObjectsExit : BiomePortalExit<BiomeObjectsEnter>, IMultiOutlet
    {
        public string linkedRefGuid = "";
        public Outlet<Den.Tools.TransitionsList> outputTransitionsList = new Outlet<Den.Tools.TransitionsList>();

        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return outputTransitionsList;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            var rootGraph = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph;
            var enterNode = Enter();

            if (enterNode == null)
            {
                tileData.StoreProduct(outputTransitionsList, new Den.Tools.TransitionsList());
                return;
            }

            var newTileData = MapMagicUtil.NewTileData(tileData);
            rootGraph.GenerateRecursive(enterNode, newTileData, rootGraph.defaults, stop);

            var edges = newTileData.ReadInletProduct(enterNode.inputTransitionsList);

            tileData.StoreProduct(outputTransitionsList, edges);
        }

        public override string LinkedRefGuid()
        {
            return linkedRefGuid;
        }
    }
}

#endif
