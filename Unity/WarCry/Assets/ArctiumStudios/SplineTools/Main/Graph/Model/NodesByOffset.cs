using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class NodesByOffset : Dictionary<Offset, List<Node>>, ICloneable
    {
        public NodesByOffset()
        {
        }

        public NodesByOffset([NotNull] IDictionary<Offset, List<Node>> dictionary) : base(dictionary)
        {
        }

        public object Clone()
        {
            return new NodesByOffset(this);
        }

        public List<Node> FilterForRect(Rect rect)
        {
            var offsets = Offsets.ForRect(rect, InternalWorldGraph.OffsetResolution);
            var relevantNodes = new HashSet<Node>();

            foreach (var o in offsets)
                if (ContainsKey(o))
                    foreach (var node in this[o])
                        relevantNodes.Add(node);

            return relevantNodes.ToList();
        }
    }
}
