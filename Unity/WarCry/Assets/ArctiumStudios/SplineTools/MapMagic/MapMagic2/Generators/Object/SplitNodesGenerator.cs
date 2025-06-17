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
        name = "Split Nodes",
        priority = 10,
        iconName = "GeneratorIcons/Split",
        colorType = typeof(NodesByOffset),
        disengageable = true,
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/object/split_nodes")]
    public class SplitNodesGenerator : LayeredGenerator<SplitNodesGenerator.FilterLayer>, IMultiInlet, IMultiOutlet
    {
        public class FilterLayer : AbstractLayer, SplitNodesHelper.ISplitNodesLayer
        {
            public string label = "Node Layer";

            public ClampedFloat weight = new ClampedFloat(1f, 0f, float.MaxValue);

            public bool heightFilter = false;
            public ClampedFloat heightMin = new ClampedFloat(0f, 0f, 1f);
            public ClampedFloat heightMax = new ClampedFloat(1f, 0f, 1f);

            public bool radiusFilter = false;
            public ClampedFloat radiusMin = new ClampedFloat(0f, 0f, float.MaxValue);
            public ClampedFloat radiusMax = new ClampedFloat(100f, 0f, float.MaxValue);

            public bool typeFilter = false;
            public NodeBaseType nodeBaseType = NodeBaseType.Perimeter;

            public Outlet<NodesByOffset> outputNodes = new Outlet<NodesByOffset>();

            public float Weight()
            {
                return weight.ClampedValue;
            }

            public bool FilterHeight()
            {
                return heightFilter;
            }

            public Vector2 HeightBounds()
            {
                return new Vector2(heightMin.ClampedValue, heightMax.ClampedValue);
            }

            public bool FilterRadius()
            {
                return radiusFilter;
            }

            public Vector2 RadiusBounds()
            {
                return new Vector2(radiusMin.ClampedValue, radiusMax.ClampedValue);
            }

            public bool FilterType()
            {
                return typeFilter;
            }

            public NodeBaseType FilteredNodeBaseType()
            {
                return nodeBaseType;
            }
        }

        public SplitNodesHelper.MatchType matchType;

        [SerializeField] public int guiExpanded;
        public Inlet<NodesByOffset> inputNodes = new Inlet<NodesByOffset>();

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputNodes;
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            foreach (var layer in layers) yield return layer.outputNodes;
        }

        public override void Generate(TileData data, StopToken stop)
        {
            Init();

            if (!enabled || stop != null && stop.stop || layers.Length == 0 || MandatoryInputMissing(inputNodes)) return;

            var nodesByOffset = data.ReadInletProduct(inputNodes);

            var dst = SplitNodesHelper.SplitNodes(nodesByOffset, layers, matchType, data.random.Seed);

            for (var i = 0; i < layers.Length; i++) data.StoreProduct(layers[i].outputNodes, dst[i]);
        }
    }
}

#endif
