#if ST_MM_2 && ST_MM_2_BIOMES

using System;
using System.Collections.Generic;
using System.Linq;
using MapMagic.Nodes;

namespace ArctiumStudios.SplineTools
{
    public abstract class BiomePortalExit<T> : Generator, IBiomePortalExit<T>, ICustomDependence where T : Generator, BiomePortalEnter
    {
        [NonSerialized] private T enter;

        public abstract string LinkedRefGuid();

        public T Enter()
        {
            if (enter != null && enter.RefGuid() == LinkedRefGuid()) return enter;

            var rootGraph = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph;
            enter = rootGraph.GeneratorsOfType<T>().FirstOrDefault(g => g.RefGuid() == LinkedRefGuid());

            return enter;
        }

        public IEnumerable<MapMagic.Nodes.Generator> PriorGens()
        {
            if (enter != null)
                yield return enter;
        }
    }
}

#endif
