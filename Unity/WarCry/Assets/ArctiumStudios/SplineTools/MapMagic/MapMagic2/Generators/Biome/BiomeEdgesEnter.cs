#if ST_MM_2 && ST_MM_2_BIOMES

using System;
using System.Collections.Generic;
using MapMagic.Nodes;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Biomes", name = "Biome Edges Enter", disengageable = false, colorType = typeof(EdgesByOffset),
        iconName = "GeneratorIcons/PortalIn",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/biome/biome_edges")]
    public class BiomeEdgesEnter : Generator, IMultiInlet, BiomePortalEnter
    {
        public string refGuid = Guid.NewGuid().ToString();
        public string refName = "edges";
        public Inlet<EdgesByOffset> inputEdges = new Inlet<EdgesByOffset>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputEdges;
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
