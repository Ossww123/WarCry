#if ST_MM_2

using System;
using System.Collections.Generic;
using System.Linq;
using MapMagic.Nodes;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Objects/Legacy",
        name = "Extract Nodes (Legacy)",
        priority = -99,
        disengageable = true,
        colorType = typeof(Den.Tools.TransitionsList),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/object/extract_nodes")]
    public class ExtractNodesGenerator : Generator, IMultiInlet, IOutlet<Den.Tools.TransitionsList>
    {
        public bool includeEndpoint = false;
        public bool includeSection = true;
        public bool includeCrossing = true;
        public bool includeCrossingPerimeter = true;
        public bool includePerimeter = true;
        
        public Inlet<EdgesByOffset> inputEdges = new Inlet<EdgesByOffset>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputEdges;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            var transitions = new Den.Tools.TransitionsList();
            tileData.StoreProduct(this, transitions);
            var connections = tileData.ReadInletProduct(inputEdges);

            // return on stop/disable
            if (!enabled || stop != null && stop.stop || connections == null)
                return;

            var worldSpaceRect = tileData.area.active.ToWorldRect();

            var nodes = connections.FilterForRect(worldSpaceRect).SelectMany(c => c.Nodes())
                .Where(n => worldSpaceRect.Contains(n.PositionV2()))
                .Where(n => !n.Type().IsBorder())
                .Where(n => includeEndpoint || n.Type().BaseType != NodeBaseType.Custom)
                .Where(n => includeCrossing || !n.Type().IsCrossing())
                .Where(n => includeSection || !n.Type().IsSection() && !n.Type().IsBorder())
                .Where(n => includeCrossingPerimeter || !n.Type().IsCrossingPerimeter())
                .Where(n => includePerimeter || !n.Type().IsPerimeter());


            foreach (var node in nodes)
            {
                var position = node.Position();
                var transition = new Den.Tools.Transition(position.x, position.y.ToMapSpaceHeight(), position.z);
                transitions.Add(transition);
                transition.scale *= node.Radius();
            }
        }
    }
}

#endif
