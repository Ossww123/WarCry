#if ST_MM_2 && ST_MM_2_BIOMES

using System;
using System.Collections.Generic;
using MapMagic.Nodes;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Biomes", name = "Biome Objects Enter", disengageable = false, colorType = typeof(Den.Tools.TransitionsList),
        iconName = "GeneratorIcons/PortalIn",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/biome/biome_objects")]
    public class BiomeObjectsEnter : Generator, IMultiInlet, BiomePortalEnter
    {
        public string refGuid = Guid.NewGuid().ToString();
        public string refName = "objects";
        public Inlet<Den.Tools.TransitionsList> inputTransitionsList = new Inlet<Den.Tools.TransitionsList>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputTransitionsList;
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
