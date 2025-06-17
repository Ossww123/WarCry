#if ST_MM_2 && ST_MM_2_BIOMES

using System;
using System.Collections.Generic;
using MapMagic.Nodes;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Biomes", name = "Biome Nodes Enter", disengageable = false, colorType = typeof(NodesByOffset),
        iconName = "GeneratorIcons/PortalIn",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/biome/biome_nodes")]
    public class BiomeNodesEnter : Generator, IMultiInlet, BiomePortalEnter
    {
        public string refGuid = Guid.NewGuid().ToString();
        public string refName = "nodes";
        public Inlet<NodesByOffset> inputNodes = new Inlet<NodesByOffset>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputNodes;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            // noop
        }

        public string RefGuid()
        {
            return refGuid;
        }
    }
}

#endif
