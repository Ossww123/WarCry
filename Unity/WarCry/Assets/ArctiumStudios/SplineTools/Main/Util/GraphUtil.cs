using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public static class GraphUtil
    {
        public static bool DistanceIsTooLow(Vector3 candidate, float minDistance, List<Node> nodes)
        {
            var others = nodes.Select(n => n.Position()).ToList();
            return Util.DistanceIsTooLow(candidate, minDistance, others);
        }

        public static int SeedFrom(Edge edge)
        {
            return SeedFrom(edge.Nodes());
        }

        public static int SeedFrom(List<Node> nodes)
        {
            int seed = 341625236;
            foreach (var node in nodes) seed *= ((InternalNode) node).Seed();
            return seed;
        }

        public static bool NodesBelongTogether(Node node1, Node node2)
        {
            return (node1.BelongsTo() != null || node2.BelongsTo() != null)
                   && (Equals(node1.BelongsTo(), node2)
                       || Equals(node2.BelongsTo(), node1)
                       || Equals(node1.BelongsTo(), node2.BelongsTo()));
        }

        public static Path FindShortestPathByHops(Node source, Node destination, bool bridgePerimeterToEndpoint)
        {
            if (!source.IsEndpointOrCrossing() || !destination.IsEndpointOrCrossing())
                throw new ArgumentException("Shortest path by hops only allowed for endpoints and crossings.");

            var startPath = new Path(0, new List<Edge>(), source);

            if (((InternalNode) source).graph != ((InternalNode) destination).graph) return startPath;

            var visited = new HashSet<Node>();
            var remaining = new Queue<Path>();
            remaining.Enqueue(startPath);

            while (remaining.Count > 0)
            {
                var current = remaining.Dequeue();
                visited.Add(current.last);

                var connections = current.last.ConnectionsOut().Where(c => !visited.Contains(c.Destination())).ToList();

                if (bridgePerimeterToEndpoint && (current.last.Type().IsPerimeter() || current.last.Type().IsEndpoint()))
                    connections.AddRange(GetBridgedConnections(current.last));

                foreach (var c in connections)
                {
                    var edges = new List<Edge>(current.edges);
                    var edgeStart = c.Nodes().Contains(current.last) ? current.last : c.Source();
                    edges.AddRange(c.EdgesUntilEndpointOrCrossing(edgeStart));
                    var last = edges[edges.Count - 1].Destination();

                    var path = new Path(current.weight + 1, edges, last);

                    // found shortest path
                    if (last.Equals(destination) || bridgePerimeterToEndpoint && last.Type().IsPerimeter() && last.BelongsTo().Equals(destination))
                        return path;

                    remaining.Enqueue(path);
                }
            }

            return startPath;
        }

        public static List<Connection> GetBridgedConnections(Node source)
        {
            var endpoint = source.Type().BaseType == NodeBaseType.Perimeter
                ? source.BelongsTo()
                : source;

            var otherPerimeters = endpoint.GetPerimeterNodes()
                .Where(n => !n.Equals(source))
                .ToList();

            // filter out perimeter nodes that are actually connected
            var directConnections = endpoint.Connections();
            if (directConnections.Count > 0)
                otherPerimeters = otherPerimeters.Where(n => directConnections.All(c => c.EdgesBetween(endpoint, n).Count == 0)).ToList();

            // enqueue all bridged connections
            var bridgedConnections = otherPerimeters.SelectMany(n => n.ConnectionsOut().Where(c => c.Source().Equals(n))).ToList();
            return bridgedConnections;
        }

        public static HashSet<Node> FindReachableNodesByHops(Node source, int maxHops, NodeType nodeType = null)
        {
            if (!source.IsEndpointOrCrossing())
                throw new ArgumentException("Reachable nodes by hops only allowed for endpoints and crossings.");

            var visited = new HashSet<Node>();
            var remaining = new Queue<Path>();
            remaining.Enqueue(new Path(0, new List<Edge>(), source));

            while (remaining.Count > 0)
            {
                var current = remaining.Dequeue();
                visited.Add(current.last);

                foreach (var c in current.last.ConnectionsOut().Where(c => !visited.Contains(c.Destination())))
                {
                    var edges = new List<Edge>(current.edges);
                    edges.AddRange(c.EdgesUntilEndpointOrCrossing(current.last));
                    var last = edges[edges.Count - 1].Destination();
                    var path = new Path(current.weight + 1, edges, last);

                    // found shortest path
                    if (path.weight <= maxHops) remaining.Enqueue(path);
                }
            }

            if (nodeType != null)
                visited = new HashSet<Node>(visited.Where(n => n.Type().Equals(nodeType)).ToArray());

            return visited;
        }

        public static Path FindShortestPath(Node source, Node destination)
        {
            var startPath = new Path(0, new List<Edge>(), source);

            if (((InternalNode) source).graph != ((InternalNode) destination).graph) return startPath;

            var found = new List<Path>();
            var visited = new HashSet<Node>();
            var remaining = new List<Path> {startPath};
            var bestWeight = float.MaxValue;

            while (remaining.Count > 0)
            {
                remaining = remaining.OrderBy(p => p.weight).ToList();
                var current = remaining[0];
                remaining.RemoveAt(0);
                visited.Add(current.last);

                var nextEdges = current.last.ConnectionsOut()
                    .Select(c => ((InternalConnection) c).EdgeFrom(current.last))
                    .Where(e => !visited.Contains(e.Destination()));

                foreach (var e in nextEdges)
                {
                    var edges = new List<Edge>(current.edges) {e};
                    var last = e.Destination();
                    var path = new Path(current.weight + e.Weight(), edges, last);

                    // found shortest path
                    if (last.Equals(destination))
                    {
                        bestWeight = Mathf.Min(path.weight, bestWeight);
                        found.Add(path);
                    } else if (path.weight < bestWeight)
                        remaining.Add(path);
                }
            }

            if (found.Count == 0) return startPath;

            return found.MinBy(p => p.weight);
        }

        public static HashSet<Node> FindReachableNodes(Node source, float maxDistance, NodeType nodeType = null)
        {
            var visited = new HashSet<Node>();

            var remaining = new List<Path> {new Path(0, new List<Edge>(), source)};

            while (remaining.Count > 0)
            {
                remaining = remaining.OrderBy(p => p.weight).ToList();
                var current = remaining[0];
                remaining.RemoveAt(0);
                visited.Add(current.last);

                var nextEdges = current.last.ConnectionsOut()
                    .Select(c => ((InternalConnection) c).EdgeFrom(current.last))
                    .Where(e => !visited.Contains(e.Destination()));

                foreach (var e in nextEdges)
                {
                    var edges = new List<Edge>(current.edges) {e};
                    var last = e.Destination();
                    var path = new Path(current.weight + e.Weight(), edges, last);

                    if (path.weight <= maxDistance) remaining.Add(path);
                }
            }

            if (nodeType != null)
                visited = new HashSet<Node>(visited.Where(n => n.Type().Equals(nodeType)).ToArray());

            return visited;
        }

        public static List<Waypoint> GetWaypoints(List<Edge> edges, float stepLength, float offset = 0f, Side side = Side.Left)
        {
            var options = new PathScatterer.Options()
            {
                usedSide = side,
                stepLengthMin = stepLength,
                stepLengthMax = stepLength,
                distanceMin = offset,
                distanceMax = offset,
                alignHeight = true,
                startAtSource = true,
                endAtDestination = true,
                countMax = 0
            };
            var pathScatterer = new PathScatterer(options, () => false);
            return pathScatterer.ProcessEdges(edges);
        }

        public class Path
        {
            public readonly float weight;
            public readonly List<Edge> edges;
            public readonly Node last;

            public Path(float weight, List<Edge> edges, Node last)
            {
                this.weight = weight;
                this.edges = edges;
                this.last = last;
            }
        }
    }
}