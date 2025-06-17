#if ST_MM_2

using System;
using System.Collections.Generic;
using ArctiumStudios.SplineTools.Generators.Spline;
using MapMagic.Nodes;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Spline/Unstable",
        name = "Cut Water Crossing Edges",
        disengageable = true,
        colorType = typeof(EdgesByOffset),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/spline/cut_water_crossing_edges")]
    public class CutWaterCrossingEdgesGenerator : Generator, IMultiInlet, IMultiOutlet
    {
        public Inlet<EdgesByOffset> inputEdges = new Inlet<EdgesByOffset>();
        public Outlet<EdgesByOffset> outputBridgeEdges = new Outlet<EdgesByOffset>();
        public Outlet<EdgesByOffset> outputFordEdges = new Outlet<EdgesByOffset>();
        public Outlet<EdgesByOffset> outputOtherEdges = new Outlet<EdgesByOffset>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputEdges;
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return outputBridgeEdges;
            yield return outputFordEdges;
            yield return outputOtherEdges;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            if (!enabled || stop != null && stop.stop || MandatoryInputMissing(inputEdges)) return;

            var connections = tileData.ReadInletProduct(inputEdges);

            var result = CutWaterCrossingEdgesHelper.BridgeEdges(connections, tileData.random.Seed);

            tileData.StoreProduct(outputBridgeEdges, result.bridgeEdges);
            tileData.StoreProduct(outputFordEdges, result.fordEdges);
            tileData.StoreProduct(outputOtherEdges, result.otherEdges);
        }
    }
}

#endif
