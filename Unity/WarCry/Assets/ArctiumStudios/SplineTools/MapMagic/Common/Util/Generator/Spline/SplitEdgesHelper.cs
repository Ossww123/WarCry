using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools.Generators.Spline
{
    public class SplitEdgesHelper
    {
        public enum MatchType { layered, random }
        
         public enum CrossingConstraint
        {
            MustContain,
            MustNotContain,
            MustEndWith,
            MustNotEndWith,
            Ignore
        }
        
        public interface ISplitEdgesLayer
        {
            float Weight();
            
            Vector2 Length();

            CrossingConstraint Crossings();
        }

        public static EdgesByOffset[] SplitConnections(EdgesByOffset connections,
            ISplitEdgesLayer[] layers, MatchType matchType, int generatorSeed)
        {
            var allConnections = connections.SelectMany(c => c.Value).Distinct().ToList();
            var connectionsByLayer = new Dictionary<int, HashSet<Connection>>();

            //random
            var rnd = new ConsistentRandom(generatorSeed + 12345);

            //creating dst
            var dst = new EdgesByOffset[layers.Length];
            for (var i = 0; i < dst.Length; i++) dst[i] = new EdgesByOffset();

            var weightSum = layers.Select(l => l.Weight()).Sum();

            foreach (var connection in allConnections)
            {
                var edges = connection.Edges();

                if (matchType == MatchType.layered)
                {
                    for (var i = layers.Length - 1; i >= 0; i--)
                    {
                        if (CrossingsMatch(layers[i], edges) && LengthMatch(layers[i], connection))
                        {
                            if (!connectionsByLayer.ContainsKey(i)) connectionsByLayer.Add(i, new HashSet<Connection>());
                            connectionsByLayer[i].Add(connection);
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
                            if (!connectionsByLayer.ContainsKey(i)) connectionsByLayer.Add(i, new HashSet<Connection>());
                            connectionsByLayer[i].Add(connection);
                            break;
                        }
                    }
                }
            }

            foreach (var entry in connections)
            foreach (var connection in entry.Value)
            {
                for (var i = 0; i < layers.Length; i++)
                {
                    if (!connectionsByLayer.ContainsKey(i) || !connectionsByLayer[i].Contains(connection)) continue;

                    if (!dst[i].ContainsKey(entry.Key)) dst[i].Add(entry.Key, new List<Connection>());
                    dst[i][entry.Key].Add(connection);
                }
            }

            return dst;
        }

        private static bool LengthMatch(ISplitEdgesLayer layer, Connection connection)
        {
            var length = connection.Length();
            return length.Between(layer.Length()[0], layer.Length()[1]);
        }

        private static bool CrossingsMatch(ISplitEdgesLayer layer, List<Edge> edges)
        {
            var crossingsMatch = true;

            switch (layer.Crossings())
            {
                case CrossingConstraint.MustContain:
                    crossingsMatch = edges.Any(e => e.Nodes().Any(n => n.Type().IsCrossing()));
                    break;
                case CrossingConstraint.MustNotContain:
                    crossingsMatch = !edges.Any(e => e.Nodes().Any(n => n.Type().IsCrossing()));
                    break;
                case CrossingConstraint.MustEndWith:
                    crossingsMatch = edges.First().Source().Type().IsCrossing()
                                     || edges.Last().Destination().Type().IsCrossing();
                    break;
                case CrossingConstraint.MustNotEndWith:
                    crossingsMatch = !edges.First().Source().Type().IsCrossing()
                                     && !edges.Last().Destination().Type().IsCrossing();
                    break;
                case CrossingConstraint.Ignore:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return crossingsMatch;
        }
    }
}
