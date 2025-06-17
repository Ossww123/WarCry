#if ST_MM_2

using System;
using System.Collections.Generic;
using ArctiumStudios.SplineTools.Generators.Spline;
using MapMagic.Nodes;
using MapMagic.Products;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Spline",
        name = "Split Edges",
        disengageable = true,
        colorType = typeof(EdgesByOffset),
        iconName = "GeneratorIcons/Split",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/spline/split_edges")]
    public class SplitEdgesGenerator : LayeredGenerator<SplitEdgesGenerator.Layer>, IMultiInlet, IMultiOutlet
    {
        public Inlet<EdgesByOffset> inputEdges = new Inlet<EdgesByOffset>();
        public SplitEdgesHelper.MatchType matchType;
        [SerializeField] public int guiExpanded;

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputEdges;
        }
        
        public IEnumerable<IOutlet<object>> Outlets()
        {
            foreach (var layer in layers) yield return layer.outputEdges;
        }

        [Serializable]
        public class Layer : AbstractLayer, SplitEdgesHelper.ISplitEdgesLayer
        {
            public Outlet<EdgesByOffset> outputEdges = new Outlet<EdgesByOffset>();
            public ClampedFloat weight = new ClampedFloat(1f, 0f, float.MaxValue);
            public string label = "Edge Layer";
            public SplitEdgesHelper.CrossingConstraint crossings = SplitEdgesHelper.CrossingConstraint.Ignore;
            public Vector2 length = new Vector2(0, float.MaxValue);

            public float Weight()
            {
                return weight.ClampedValue;
            }

            public Vector2 Length()
            {
                return length;
            }

            public SplitEdgesHelper.CrossingConstraint Crossings()
            {
                return crossings;
            }
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            Init();

            if (!enabled || stop != null && stop.stop || layers.Length == 0 || MandatoryInputMissing(inputEdges)) return;

            var connections = tileData.ReadInletProduct(inputEdges);

            var dst = SplitEdgesHelper.SplitConnections(connections, layers, matchType, tileData.random.Seed);

            for (var i = 0; i < layers.Length; i++) tileData.StoreProduct(layers[i].outputEdges, dst[i]);
        }
    }
}

#endif
