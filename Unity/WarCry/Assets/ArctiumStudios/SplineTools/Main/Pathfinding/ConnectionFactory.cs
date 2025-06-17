using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace ArctiumStudios.SplineTools
{
    public class ConnectionFactory
    {
        public class Options
        {
            // only connect from nodes that are not yet connected to the graph somehow
            public bool connectFromUnreachableOnly = false;

            // only connect to nodes that are connected to the graph somehow
            public bool connectToReachableOnly = false;

            // controls how many connections of the same type to the same node are allowed
            public int connectionsMax = 5;

            public bool connectionsLimit = false;

            // min and max distance between source and destination node, discard when out of range 
            public bool distanceLimit = false;
            public float distanceMin = 0f;
            public float distanceMax = 100f;

            // min angle between candidates and existing perimeter nodes
            public bool angleLimit = true;
            public bool angleLimitSource = true;
            public bool angleLimitDestination = true;
            public float angleMin = 30f;

            public bool nodesMustBelongTogether = false;
        }

        private readonly Options options;
        private readonly ConnectionPathfinder pathfinder;
        private readonly Func<bool> stopFunc;

        public ConnectionFactory(Options options, ConnectionPathfinder pathfinder, Func<bool> stopFunc)
        {
            this.options = options;
            this.pathfinder = pathfinder;
            this.stopFunc = stopFunc;
        }

        public void ConnectSplitGraphs(List<Node> nodes, ConnectionType type, bool clusterByBelongsTo)
        {
            var clusters = FindClusters(nodes);

            if (clusterByBelongsTo)
            {
                foreach (var grouped in clusters.GroupBy(cluster => cluster.First().BelongsTo())) ConnectClusters(grouped.ToList(), type, true);
            } else
            {
                ConnectClusters(clusters, type, false);
            }
        }

        private void ConnectClusters(List<HashSet<Node>> clusters, ConnectionType type, bool nodesBelongTogether)
        {
            Log.Debug(this, () => "Connecting clusters: " + Log.LogCollection(clusters));

            var lastCount = -1;

            while (clusters.Count > 1 && clusters.Count != lastCount)
            {
                if (stopFunc.Invoke()) return;
                // find nearest nodes between two clusters
                var alreadyConnected = clusters[0].ToList();
                var unconnected = new List<Node>();

                for (var i = 1; i < clusters.Count; i++) unconnected.AddRange(clusters[i]);

                var sourceNodes = alreadyConnected
                    .Where(n => n.Type().BaseType == NodeBaseType.Section && (nodesBelongTogether || n.BelongsTo() == null)
                                || n.Type().BaseType == NodeBaseType.Custom).ToList();
                var destinationNodes = unconnected
                    .Where(n => n.Type().BaseType == NodeBaseType.Section && (nodesBelongTogether || n.BelongsTo() == null)
                                || n.Type().BaseType == NodeBaseType.Custom).ToList();

                var newConnections = ConnectNearestSingleWithIntersections(sourceNodes, destinationNodes, type);
                newConnections.ForEach(newConnection =>
                {
                    if (((InternalConnection) newConnection).EdgeCount() > 0)
                        pathfinder.graph.StoreConnection(newConnection, pathfinder.reference);
                });

                // find out which cluster got connected
                var connectedClusterIndex = -1;

                for (var i = 1; i < clusters.Count; i++)
                {
                    if (clusters[i].First().HasAnyConnectionTo(alreadyConnected.First(), true))
                    {
                        connectedClusterIndex = i;
                        break;
                    }
                }

                Log.Debug(this, () => "Connected cluster #" + connectedClusterIndex);

                lastCount = clusters.Count; // remember the last count to break loop if no further connection can be found 
                clusters[0].UnionWith(clusters[connectedClusterIndex]);
                clusters.RemoveAt(connectedClusterIndex);
                if (clusters.Count == 1)
                {
                    Log.Debug(this, () => "Connected all clusters");
                } else
                {
                    Log.Debug(this, () => "Remaining clusters: " + Log.LogCollection(clusters));
                }
            }
        }

        private List<HashSet<Node>> FindClusters(List<Node> nodes)
        {
            var clusters = new List<HashSet<Node>>();

            // find clusters
            nodes.ForEach(node => // with every node as starting point
            {
                if (!clusters.Exists(cluster => cluster.Contains(node))) // if not already part of a cluster
                {
                    var connectedNodes = new HashSet<Node>();
                    Util.TraverseGraph(node, null, true, ref connectedNodes); // traverse the graph connected to the node

                    clusters.Add(connectedNodes);
                    Log.Debug(this, () => "Found Cluster: " + Log.LogCollection(connectedNodes));
                }
            });
            return clusters;
        }

        private List<Connection> ConnectNearestSingleWithIntersections(List<Node> sourceNodes, List<Node> destinationNodes, ConnectionType type)
        {
            return ConnectNearestSingle(sourceNodes, destinationNodes, type);
        }

        private List<Connection> ConnectNearestSingle(List<Node> filteredSourceNodes, List<Node> filteredDestinationNodes, ConnectionType type)
        {
            var bestDistance = float.MaxValue;
            Node bestCandidateSource = null;
            Node bestCandidateDestination = null;

            var blacklist = new HashSet<Node>(filteredSourceNodes);

            foreach (var source in filteredSourceNodes)
            foreach (var destination in filteredDestinationNodes)
            {
                if (stopFunc.Invoke()) return new List<Connection>();
                if (source.Equals(destination)) continue; // skip self

                var distance = Vector3.Distance(source.Position(), destination.Position());

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCandidateSource = source;
                    bestCandidateDestination = destination;
                }
            }

            List<Connection> connections = null;

            if (bestCandidateSource == null) throw new Exception("No candidates to connect found.");

            if (pathfinder.NotReachableWithMaxSlope(bestCandidateSource, bestCandidateDestination))
            {
                Log.Error(this, () => "NotReachableWithMaxSlope");
            }

            if (!bestCandidateSource.HasAnyConnectionTo(bestCandidateDestination, true))
            {
                if (bestCandidateSource.Type().BaseType == NodeBaseType.Section)
                    ((InternalNode) bestCandidateSource).connections
                        .ForEach(c => ((InternalConnection) c).SplitAt(bestCandidateSource, NodeType.Of(NodeBaseType.Crossing)));

                Log.Debug(this, () => "Connect " + bestCandidateSource + " to " + bestCandidateDestination);

                connections = pathfinder.ConnectIterative(bestCandidateSource, bestCandidateDestination, type, blacklist);
            }

            return connections;
        }

        /// <summary>
        /// Connect each nodes from the first set with the nearest node from the other set
        /// </summary>
        public void ConnectNearestEach(List<Node> sourceNodes, List<Node> destinationNodes, ConnectionType type, int iterations)
        {
            var doneConnections = new Dictionary<Node, HashSet<Node>>();

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                foreach (var source in sourceNodes)
                {
                    if (stopFunc.Invoke()) return;
                    if (options.connectFromUnreachableOnly && source.Connections().Count != 0) continue;

                    var candidate = destinationNodes.Where(n => !CandidateViolatesConstraint(source, n))
                        .OrderBy(n => (source.PositionV2() - n.PositionV2()).magnitude)
                        .Skip(iteration)
                        .FirstOrDefault();

                    // kick out candidates that violate the constraints
                    if (candidate == null) continue;

                    if (!doneConnections.ContainsKey(source)) doneConnections[source] = new HashSet<Node>();
                    if (!doneConnections.ContainsKey(candidate)) doneConnections[candidate] = new HashSet<Node>();

                    if (doneConnections[source].Contains(candidate) || doneConnections[candidate].Contains(source)) continue;
                    doneConnections[source].Add(candidate);
                    doneConnections[candidate].Add(source);

                    pathfinder.Connect(source, candidate, type);
                }
            }
        }

        private bool CandidateViolatesConstraint(Node source, Node candidate)
        {
            if (source.Equals(candidate)) return true; // skip self

            if (options.nodesMustBelongTogether && !GraphUtil.NodesBelongTogether(source, candidate)) return true;

            if (options.connectToReachableOnly && candidate.Connections().Count == 0) return true;
            if (options.connectionsLimit && candidate.Connections().Count >= options.connectionsMax) return true;
            if (pathfinder.NotReachableWithMaxSlope(source, candidate)) return true;

            if (options.angleLimit)
            {
                if (options.angleLimitSource && ViolatesAngle(source, candidate)) return true;
                if (options.angleLimitDestination && ViolatesAngle(candidate, source)) return true;
            }

            var distance = Vector3.Distance(source.Position(), candidate.Position());

            if (options.distanceLimit && !distance.Between(options.distanceMin, options.distanceMax)) return true;

            return false;
        }

        private bool ViolatesAngle(Node source, Node candidate)
        {
            var otherNodes = source.GetPerimeterNodes()
                .SelectMany(n => n.ConnectionsOut().Where(c => !c.Destination().Equals(source)).Select(c => c.Destination()))
                .ToList();

            otherNodes.AddRange(source.ConnectionsOut().Select(c => c.Destination()).ToList());

            foreach (var perimeterNode in otherNodes)
                if (Vector2.Angle(candidate.PositionV2() - source.PositionV2(),
                    perimeterNode.PositionV2() - source.PositionV2()) < options.angleMin)
                    return true;
            return false;
        }

        public void ConnectRandomEach(List<Node> sourceNodes, List<Node> destinationNodes, ConnectionType type, int iterations)
        {
            var rnd = new ConsistentRandom(GraphUtil.SeedFrom(sourceNodes) * GraphUtil.SeedFrom(destinationNodes));

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                foreach (var source in sourceNodes)
                {
                    if (stopFunc.Invoke()) return;
                    if (options.connectFromUnreachableOnly && source.Connections().Count != 0) continue;

                    Node bestCandidate = null;
                    var tried = new HashSet<int> {destinationNodes.IndexOf(source)};
                    while (bestCandidate == null && tried.Count < destinationNodes.Count)
                    {
                        int nextRandom;
                        do nextRandom = (int) (rnd.NextFloat() * destinationNodes.Count);
                        while (tried.Contains(nextRandom));

                        tried.Add(nextRandom);
                        var candidate = destinationNodes[nextRandom];

                        if (CandidateViolatesConstraint(source, candidate)) continue;

                        bestCandidate = candidate;
                    }

                    if (bestCandidate == null) continue;

                    pathfinder.Connect(source, bestCandidate, type);
                }
            }
        }
    }
}