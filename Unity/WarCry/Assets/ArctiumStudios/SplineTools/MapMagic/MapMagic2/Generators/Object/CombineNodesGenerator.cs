#if ST_MM_2

using System.Collections.Generic;
using MapMagic.Nodes;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    [System.Serializable]
    [GeneratorMenu(menu = "SplineTools/Objects",
        name = "Combine Nodes",
        priority = 10,
        iconName = "GeneratorIcons/Combine",
        colorType = typeof(NodesByOffset),
        disengageable = true,
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/object/combine_nodes")]
    public class CombineNodesGenerator : LayeredGenerator<CombineNodesGenerator.CombineLayer>, IMultiInlet, IMultiOutlet
    {
        public class CombineLayer : AbstractLayer
        {
            public Inlet<NodesByOffset> inputNodes = new Inlet<NodesByOffset>();
        }

        public Outlet<NodesByOffset> outputNodes = new Outlet<NodesByOffset>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            foreach (var layer in layers) yield return layer.inputNodes;
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return outputNodes;
        }

        public override void Generate(TileData data, StopToken stop)
        {
            if (stop != null && stop.stop || !enabled)
            {
                data.StoreProduct(outputNodes, new NodesByOffset());
                return;
            }

            var combinedNodes = new NodesByOffset();

            foreach (var layer in layers)
            {
                var layerNodes = data.ReadInletProduct(layer.inputNodes);

                if (layerNodes == null) continue;

                foreach (var pair in layerNodes)
                {
                    if (!combinedNodes.ContainsKey(pair.Key)) combinedNodes.Add(pair.Key, pair.Value);
                    else combinedNodes[pair.Key].AddRange(pair.Value);
                }
            }

            data.StoreProduct(outputNodes, combinedNodes);
        }
    }
}

#endif
