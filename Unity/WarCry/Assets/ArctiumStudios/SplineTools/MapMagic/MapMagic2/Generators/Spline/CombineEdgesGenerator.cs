#if ST_MM_2

using System;
using System.Collections.Generic;
using MapMagic.Nodes;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Spline",
        name = "Combine Edges",
        disengageable = true,
        colorType = typeof(EdgesByOffset),
        iconName = "GeneratorIcons/Combine",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/spline/combine_edges")]
    public class CombineEdgesGenerator : Generator, IMultiInlet, IMultiOutlet
    {
        public Outlet<EdgesByOffset> outputEdges = new Outlet<EdgesByOffset>();
        
        public Inlet<EdgesByOffset>[] inlets = new Inlet<EdgesByOffset>[2]
        {
            new Inlet<EdgesByOffset>(), new Inlet<EdgesByOffset>()
        };

        public IEnumerable<IInlet<object>> Inlets()
        {
            for (int i = 0; i < inlets.Length; i++) yield return inlets[i];
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return outputEdges;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            //preparing output
            var combinedConnections = new EdgesByOffset();
            tileData.StoreProduct(outputEdges, combinedConnections);

            //return on stop/disable
            if (stop != null && stop.stop || inlets.Length == 0) return;

            if (!enabled)
            {
                if (inlets.Length > 1)
                {
                    var inletConnections = tileData.ReadInletProduct(inlets[0]);
                    foreach (var keyValuePair in inletConnections)
                    {
                        combinedConnections.Add(keyValuePair.Key, keyValuePair.Value);
                    }
                }

                return;
            }

            for (var i = 0; i < inlets.Length; i++)
            {
                EdgesByOffset connections = tileData.ReadInletProduct(inlets[i]);
                if (connections == null) continue;

                foreach (var entry in connections)
                {
                    if (!combinedConnections.ContainsKey(entry.Key)) combinedConnections.Add(entry.Key, new List<Connection>());
                    combinedConnections[entry.Key].AddRange(entry.Value);
                }
            }
        }
    }
}

#endif
