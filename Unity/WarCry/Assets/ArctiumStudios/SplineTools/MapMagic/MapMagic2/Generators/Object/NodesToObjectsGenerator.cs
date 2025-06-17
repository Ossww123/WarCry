#if ST_MM_2

using System;
using System.Collections.Generic;
using MapMagic.Nodes;
using MapMagic.Products;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Objects",
        name = "Nodes > Objects",
        priority = 10,
        disengageable = true,
        colorType = typeof(Den.Tools.TransitionsList),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/object/nodes_to_objects")]
    public class NodesToObjectsGenerator : Generator, IMultiInlet, IOutlet<Den.Tools.TransitionsList>
    {

        public bool alignRotation = false;

        public Inlet<NodesByOffset> inputNodes = new Inlet<NodesByOffset>();


        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputNodes;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            var transitions = new Den.Tools.TransitionsList();
            tileData.StoreProduct(this, transitions);
            var nodesByOffset = tileData.ReadInletProduct(inputNodes);

            // return on stop/disable
            if (!enabled || stop != null && stop.stop || nodesByOffset == null) return;

            var worldSpaceRect = tileData.area.active.ToWorldRect();

            var nodes = nodesByOffset.FilterForRect(worldSpaceRect);

            foreach (var node in nodes)
            {
                var position = node.Position();
                var transition = new Den.Tools.Transition(position.x, position.y.ToMapSpaceHeight(), position.z);
                transition.scale *= node.Radius();

                if (alignRotation)
                {
                    if (node.Edges().Count > 0)
                    {
                        var edge = node.Edges()[0];
                        // ReSharper disable once PossibleUnintendedReferenceComparison
                        transition.rotation = edge.Source() == node
                            ? Quaternion.LookRotation(edge.BezierCurve().Source().ControlV3())
                            : Quaternion.LookRotation(-edge.BezierCurve().Destination().ControlV3());
                    }
                }

                transitions.Add(transition);
            }

            tileData.StoreProduct(this, transitions);
        }
    }
}

#endif
