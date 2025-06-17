using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class SplitNodesHelper
    {
        public enum MatchType
        {
            layered,
            random
        }

        public interface ISplitNodesLayer
        {
            float Weight();

            bool FilterHeight();
            Vector2 HeightBounds();

            bool FilterRadius();
            Vector2 RadiusBounds();

            bool FilterType();
            NodeBaseType FilteredNodeBaseType();
        }

        public static NodesByOffset[] SplitNodes(NodesByOffset nodesByOffset, ISplitNodesLayer[] layers, MatchType matchType, int generatorSeed)
        {
            var allNodes = nodesByOffset.SelectMany(c => c.Value).Distinct().ToList();
            var nodesByLayer = new Dictionary<int, HashSet<Node>>();

            //random
            var rnd = new ConsistentRandom(generatorSeed + 12345);

            //creating dst
            var dst = new NodesByOffset[layers.Length];
            for (var i = 0; i < dst.Length; i++) dst[i] = new NodesByOffset();

            var weightSum = layers.Select(l => l.Weight()).Sum();

            foreach (var node in allNodes)
            {
                if (matchType == MatchType.layered)
                {
                    for (var i = layers.Length - 1; i >= 0; i--)
                    {
                        if (HeightMatch(layers[i], node) && RadiusMatch(layers[i], node) && TypeMatch(layers[i], node))
                        {
                            if (!nodesByLayer.ContainsKey(i)) nodesByLayer.Add(i, new HashSet<Node>());
                            nodesByLayer[i].Add(node);
                            break;
                        }
                    }
                } else if (matchType == MatchType.random)
                {
                    var rndValue = rnd.NextFloat(0, weightSum);
                    var currentSum = 0f;

                    for (var i = 0; i < layers.Length; i++)
                    {
                        var layer = layers[i];
                        currentSum += layer.Weight();
                        if (rndValue <= currentSum)
                        {
                            if (!nodesByLayer.ContainsKey(i)) nodesByLayer.Add(i, new HashSet<Node>());
                            nodesByLayer[i].Add(node);
                            break;
                        }
                    }
                }
            }

            for (var i = 0; i < nodesByLayer.Count; i++)
            {
                var groupedByOffset = nodesByLayer[i].GroupBy(node => Offset.For(node.PositionV2(), InternalWorldGraph.OffsetResolution))
                    .ToDictionary(e => e.Key, e => e.Distinct().ToList());
                dst[i] = new NodesByOffset(groupedByOffset);
            }

            return dst;
        }

        private static bool HeightMatch(ISplitNodesLayer layer, Node node)
        {
            if (!layer.FilterHeight()) return true;

            return node.Position().y.Between(layer.HeightBounds()[0], layer.HeightBounds()[1]);
        }

        private static bool RadiusMatch(ISplitNodesLayer layer, Node node)
        {
            if (!layer.FilterRadius()) return true;

            return node.Radius().Between(layer.RadiusBounds()[0], layer.RadiusBounds()[1]);
        }

        private static bool TypeMatch(ISplitNodesLayer layer, Node node)
        {
            if (!layer.FilterType()) return true;

            return node.Type().BaseType.Equals(layer.FilteredNodeBaseType());
        }
    }
}
