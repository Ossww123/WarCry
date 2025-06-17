#if ST_MM_2

using System;
using System.Collections.Generic;
using System.Linq;
using MapMagic.Nodes;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Objects",
        name = "Edges > Nodes",
        priority = 10,
        disengageable = true,
        colorType = typeof(NodesByOffset),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/object/edges_to_nodes")]
    public class EdgesToNodesGenerator : Generator, IMultiInlet, IMultiOutlet
    {
        public Inlet<EdgesByOffset> inputEdges = new Inlet<EdgesByOffset>();
        public Outlet<NodesByOffset> outputNodes = new Outlet<NodesByOffset>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputEdges;
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return outputNodes;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            var edgesByOffset = tileData.ReadInletProduct(inputEdges);

            // return on stop/disable
            if (!enabled || stop != null && stop.stop || edgesByOffset == null)
            {
                tileData.StoreProduct(outputNodes, new NodesByOffset());
                return;
            }

            var nodesByOffset = edgesByOffset.Values
                .SelectMany(cl => cl.SelectMany(c => c.Nodes()))
                .GroupBy(n => Offset.For(n.PositionV2(), InternalWorldGraph.OffsetResolution))
                .ToDictionary(e => e.Key, e => e.Distinct().ToList());

            tileData.StoreProduct(outputNodes, new NodesByOffset(nodesByOffset));
        }
    }
}

#endif
