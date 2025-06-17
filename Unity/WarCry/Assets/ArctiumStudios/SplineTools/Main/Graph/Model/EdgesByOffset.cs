using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class EdgesByOffset : Dictionary<Offset, List<Connection>>, ICloneable
    {
        public EdgesByOffset()
        {
        }

        public EdgesByOffset([NotNull] IDictionary<Offset, List<Connection>> dictionary) : base(dictionary)
        {
        }

        public object Clone()
        {
            return new EdgesByOffset(this);
        }

        public List<Connection> FilterForRect(Rect rect)
        {
            var offsets = Offsets.ForRect(rect, InternalWorldGraph.OffsetResolution);
            var relevantConnections = new HashSet<Connection>();

            foreach (var o in offsets)
                if (ContainsKey(o))
                    foreach (var connection in this[o])
                        if (!relevantConnections.Contains(connection))
                            relevantConnections.Add(connection);

            // sort secondary connections to the end
            var sortedConnections = new List<Connection>();

            foreach (var connection in relevantConnections)
            {
                var index = 0;

                for (var i = 0; i < sortedConnections.Count; i++)
                    if (((InternalConnection) connection).StartsOrEndsWithCrossingOfOther(sortedConnections[i]))
                        index = i + 1;

                sortedConnections.Insert(index, connection);
            }

            return sortedConnections;
        }
    }
}
