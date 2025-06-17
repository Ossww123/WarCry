using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace ArctiumStudios.SplineTools
{
    public class ConnectionPathfinderV1 : ConnectionPathfinder
    {
        public new class Options : Pathfinder.Options
        {
            public float widthMin = 10;
            public float widthMax = 15;
            public AnimationCurve widthFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));

            // angle degrees, 0-180° 
            public float bezierSectionAngleVariance = 45f;

            public float bias = 0.2f;

            // min - max length of a section
            public Vector2 bezierSectionLength = new Vector2(25, 25);

            public int lookahead = 2;
            public int pathfindingCandidates = 10;

            public float nodeMaskThreshold = 0.9f;

            public bool preferDirectConnection = false;

            public bool skipInsideSourceRadius = false;
            public bool skipInsideDestinationRadius = false;
        }

        private readonly Func<Vector2, float> nodeMaskFunc;
        private readonly Action<Node, Node> preprocessingAction;

        public ConnectionPathfinderV1(InternalWorldGraph graph, string reference, Func<Vector2, float> heightFunc,
            Action<Node, Node> preprocessingAction, Options options, Func<Vector2, float> nodeMaskFunc, 
            Func<bool> stopFunc) : base(graph, reference, heightFunc, options, stopFunc)
        {
            this.nodeMaskFunc = nodeMaskFunc;
            this.preprocessingAction = preprocessingAction;
        }

        public override void Connect(Node source, Node destination, ConnectionType type, HashSet<Node> blacklist = null)
        {
            if (stopFunc.Invoke()) return;
            var insidePerimeter = InsidePerimeter(source, destination);
            var bridgePerimeterToEndpoint = !insidePerimeter;

            if (!source.HasAnyConnectionTo(destination, bridgePerimeterToEndpoint) || NeedsDirectConnection(source, destination))
            {
                Log.Debug(this, () => "Try connect " + source + " -> " + destination);
                if (blacklist == null) blacklist = new HashSet<Node>();

                // walk along edges if possible
                var walkedNodes = WalkAlongExistingConnections(source, destination.Position(), destination);

                // walked until the destination already
                if (walkedNodes.Last().Equals(destination)
                    || (!insidePerimeter
                        && walkedNodes.Last().Type().IsPerimeter()
                        && Equals(walkedNodes.Last().BelongsTo(), destination))) return;

                walkedNodes.ForEach(n => blacklist.Add(n));
                walkedNodes.SelectMany(n => ((InternalNode) n).connections).Distinct()
                    .SelectMany(c => c.Nodes()).ToList()
                    .ForEach(n => blacklist.Add(n));

                var sourceConnect = walkedNodes.Count > 1 ? walkedNodes.Last() : source;

                // check if the new connection would intersect an existing one
                // if so, only do first part directly and recurse for the rest
                var intermediateNode = FindIntermediateNode(sourceConnect, destination);

                Node destinationConnect;

                if (intermediateNode != null)
                {
                    // connect only until the first intersection
                    destinationConnect = intermediateNode;
                    Log.Debug(this, () => "Intersection for new connection: " + destinationConnect);
                } else
                {
                    // connect the nodes directly
                    destinationConnect = destination;
                }

                // check existing perimeters at source
                var existingSourcePerimeter = FindExistingReusablePerimeterNode(sourceConnect, destinationConnect);

                if (existingSourcePerimeter != null)
                {
                    var nearestSection = FindNextConnectableSectionNode(existingSourcePerimeter, ref blacklist);
                    // abort if no free node is found
                    if (nearestSection == null) return;
                    sourceConnect = nearestSection;
                    
                    // add all nodes of the branched out connection to the blacklist so the new connection won't be rerouted there
                    nearestSection.Connections().ForEach(c => c.Nodes().ForEach(n => blacklist.Add(n)));
                    
                    Log.Debug(this, () => "Connect from existing node instead: " + nearestSection);
                }

                // check existing perimeters at destination
                var existingDestinationPerimeter = FindExistingReusablePerimeterNode(destinationConnect, sourceConnect);

                if (existingDestinationPerimeter != null)
                {
                    var nearestSection = FindNextConnectableSectionNode(existingDestinationPerimeter, ref blacklist);
                    // abort if no free node is found
                    if (nearestSection == null) return;
                    destinationConnect = nearestSection;
                    Log.Debug(this, () => "Connect to existing node instead: " + nearestSection);
                }

                var newConnections = ConnectIterative(sourceConnect, destinationConnect, type, blacklist);
                newConnections.ForEach(newConnection =>
                {
                    if (((InternalConnection) newConnection).EdgeCount() > 0)
                    {
                        if (!ConnectionIsValid(newConnection, graph))
                        {
                            Log.Debug(this, () => "Discarding invalid connection: " + newConnection);
                            return;
                        }

                        graph.StoreConnection(newConnection, reference);
                        foreach (var node in newConnection.Nodes()) blacklist.Add(node);
                    }
                });

                if (!(destinationConnect.Equals(destination)
                      || ((Options) options).skipInsideDestinationRadius
                      && destinationConnect.Type().IsPerimeter()
                      && Equals(destinationConnect.BelongsTo(), destination)))
                {
                    // continue with the remaining part
                    Connect(destinationConnect, destination, type, blacklist);
                }
            } else
            {
                Log.Debug(this, () => " Skip connect " + source + " -> " + destination);
            }
        }

        private bool NeedsDirectConnection(Node source, Node destination)
        {
            if (!((Options) options).preferDirectConnection) return false;

            var sources = ((Options) options).skipInsideSourceRadius
                ? source.GetPerimeterNodes()
                : new HashSet<Node> {source};
            var destinations = ((Options) options).skipInsideSourceRadius
                ? destination.GetPerimeterNodes()
                : new HashSet<Node> {destination};

            foreach (var s in sources)
            foreach (var d in destinations)
                if (s.ConnectionsOut().Any(c => c.Destination().Equals(d)))
                    return false;

            return true;
        }

        private Node FindIntermediateNode(Node source, Node destination)
        {
            var nearestIntersectionEdge = FindNearestIntersectionWithExistingEdges(source, destination);

            Node intermediate = null;
            var tries = 0;

            while (nearestIntersectionEdge != null && tries < 10)
            {
                // connect only until the first intersection
                var blacklist = new HashSet<Node>();
                intermediate = FindNextConnectableSectionNode(nearestIntersectionEdge.Source(), ref blacklist);

                if (intermediate == null) return null;

                nearestIntersectionEdge = FindNearestIntersectionWithExistingEdges(source, intermediate);
                tries++;
            }

            return intermediate;
        }

        private bool ConnectionIsValid(Connection connection, WorldGraph graph)
        {
            return connection.Nodes()
                .Where(n => n.Type().BaseType == NodeBaseType.Perimeter || n.Type().BaseType == NodeBaseType.CrossingPerimeter)
                .Where(n => !(n.Equals(connection.Source()) || n.Equals(connection.Destination())))
                .All(node => ((InternalWorldGraph) graph).NodeCanBePersisted(node));
        }

        private Node FindNextConnectableSectionNode(Node node, ref HashSet<Node> blacklist)
        {
            var blacklistNodes = blacklist;

            var nodesOut = node.ConnectionsOut().Select(c => c.NodesBetween(node, c.Destination())).ToList();
            var max = nodesOut.Max(n => n.Count);

            for (var i = 1; i < max; i++)
            {
                var nodes = nodesOut.Where(l => l.Count > i)
                    .Select(l => l[i])
                    .Where(n => !blacklistNodes.Contains(n))
                    .ToList();

                foreach (var n in nodes) blacklist.Add(n);

                var sectionNode = nodes.FirstOrDefault(n => n.Type().BaseType == NodeBaseType.Section);
                if (sectionNode != null) return sectionNode;
            }

            return null;
        }

        private Edge FindNearestIntersectionWithExistingEdges(Node source, Node destination)
        {
            return FindNearestIntersectionWithExistingEdges(source.PositionV2(), destination.PositionV2());
        }

        private Edge FindNearestIntersectionWithExistingEdges(Vector2 source, Vector2 destination)
        {
            Edge nearestIntersectionEdge = null;
            var intersectionDistance = float.MaxValue;

            var relevantOffsets = Offsets.TouchedBetween(source, destination, InternalWorldGraph.OffsetResolution);

            var existingEdges = new List<Edge>();

            foreach (var offset in relevantOffsets)
            {
                var connections = graph.Connections(offset);
                if (connections == null || connections.Count == 0) continue;

                existingEdges.AddRange(connections.SelectMany(c => ((InternalConnection) c).EdgesForOffsets(relevantOffsets)).ToList());
            }

            existingEdges.ForEach(e =>
            {
                if (stopFunc.Invoke()) return;
                if (e.Nodes().Exists(n => n.PositionV2().Equals(source)) || e.Nodes().Exists(n => n.PositionV2().Equals(destination))) return;

                var intersect = Util.Intersect(source, destination, e.Source().PositionV2(), e.Destination().PositionV2());
                if (intersect.HasValue)
                {
                    var dist = (source - intersect.Value).magnitude;
                    if (dist < intersectionDistance)
                    {
                        nearestIntersectionEdge = e;
                        intersectionDistance = dist;
                    }
                }
            });
            return nearestIntersectionEdge;
        }

        public override List<Connection> ConnectIterative(Node source, Node destination, ConnectionType type, HashSet<Node> blacklist)
        {
            Log.Debug(this, () => "Connect " + source + " -> " + destination);
            var rnd = new ConsistentRandom(((InternalNode) source).Seed() * (((InternalNode) destination).Seed() + 377));

            if (preprocessingAction != null) preprocessingAction.Invoke(source, destination);

            var connections = new List<Connection>();
            var connection = (Connection) new InternalConnection(rnd.NextGuid(source.Position(), destination.Position()).ToString(),
                type, Directions.TwoWay, true);
            connections.Add(connection);

            var previousNodes = new List<Node> {source};
            var rerouteBlacklist = new HashSet<Node> {source, destination};
            foreach (var node in blacklist) rerouteBlacklist.Add(node);

            var insidePerimeter = InsidePerimeter(source, destination);

            if (!insidePerimeter)
            {
                // create startPerimeter node and connect directly 'start -> startPerimeter'
                // or walk along existing edges to find a new start

                // create edges to the position of the destinationPerimeter but don't create the node yet
                // only create destinationPerimeter when the connection was not rerouted
                var destinationPerimeterPosition = GetPerimeterPosition(destination, source, rnd);

                // we always must create a perimeter node and connect it
                var newPerimeter = GetPerimeterNode(source, destination, rnd);

                // create crossing if starting in the middle of an existing connection
                if (source.Type().BaseType == NodeBaseType.Section)
                    ((InternalNode) source).connections.ForEach(c => ((InternalConnection) c).SplitAt(source, NodeType.Of(NodeBaseType.Crossing)));

                rerouteBlacklist.Add(newPerimeter);

                // allow rerouting here as well?
                ConnectDirectly(source, newPerimeter, source, ref connection,
                    previousNodes, rnd);

                // do the actual connection from the new perimeter towards 'destinationPerimeterPosition'
                var rerouted = ConnectDirectlyOrReroute(newPerimeter, null, destinationPerimeterPosition,
                    ((InternalNode) destination).PerimeterType(),
                    null, destination, ref connection, previousNodes, rerouteBlacklist, rnd);

                // remove edges inside radius if configured
                // they had to be created at first to get deterministic results independent from 'skipInsideSourceRadius' option
                if (((Options) options).skipInsideSourceRadius && newPerimeter.Type().BaseType == NodeBaseType.Perimeter)
                {
                    connections.Remove(connection);
                    var toBeReplaced = connection;
                    connection = new InternalConnection(((InternalConnection) connection).Guid(), type, connection.Direction(), true);
                    connections.Add(connection);
                    toBeReplaced.EdgesBetween(newPerimeter, previousNodes.Last())
                        .ForEach(
                            edge => ((InternalConnection) connection).AddEdge(edge.Source(), edge.Destination(), edge.Widths(), edge.BezierCurve()));
                }

                // if the connection was rerouted, check if the destination is already reachable or walk towards it along existing edges
                if (rerouted)
                {
                    if (!previousNodes.Last().HasAnyConnectionTo(destination, true)) Connect(previousNodes.Last(), destination, type);

                    // return if the connection was rerouted
                    return connections;
                }
            }

            if (((Options) options).skipInsideDestinationRadius
                && connection.Destination().Type().IsPerimeter()) return connections;

            // if the connection was not rerouted, connect directly 'destinationPerimeter -> destination'
            var belongsTo = insidePerimeter ? (source.BelongsTo() != null ? source.BelongsTo() : destination.BelongsTo()) : destination;

            if (destination.Type().BaseType == NodeBaseType.Section)
            {
                ((InternalNode) destination).connections
                    .ForEach(c => ((InternalConnection) c).SplitAt(destination, NodeType.Of(NodeBaseType.Crossing)));
            }

            ConnectDirectlyOrReroute(previousNodes.Last(), destination, destination.Position(), destination.Type(), belongsTo,
                destination, ref connection, previousNodes, rerouteBlacklist, rnd);

            return connections;
        }

        private static bool InsidePerimeter(Node source, Node destination)
        {
            return GraphUtil.NodesBelongTogether(source, destination);
        }

        private void ConnectDirectly(Node source, Node destination, Node relateTo, ref Connection connection, List<Node> previous, Random rnd)
        {
            ConnectDirectlyOrReroute(source, destination.Position(), destination, destination.Type(), false,
                ref connection, previous, new HashSet<Node>(), relateTo, relateTo, rnd);
        }

        private bool ConnectDirectlyOrReroute(Node source, Node destination, Vector3 destinationPosition, NodeType destinationType, Node relateTo,
            Node relateDestinationPerimeterTo, ref Connection connection, List<Node> previous, HashSet<Node> rerouteBlacklist, Random rnd)
        {
            return ConnectDirectlyOrReroute(source, destinationPosition, destination, destinationType, true, ref connection,
                previous, rerouteBlacklist, relateTo, relateDestinationPerimeterTo, rnd);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destinationPosition"></param>
        /// <param name="destination">If provided it will be used as last node, else a new node will be created</param>
        /// <param name="destinationType"></param>
        /// <param name="reroutingAllowed"></param>
        /// <param name="connection"></param>
        /// <param name="previous"></param>
        /// <param name="rerouteBlacklist"></param>
        /// <param name="relateTo"></param>
        /// <param name="rnd"></param>
        /// <exception cref="Exception"></exception>
        private bool ConnectDirectlyOrReroute(Node source, Vector3 destinationPosition, Node destination, NodeType destinationType,
            bool reroutingAllowed, ref Connection connection, List<Node> previous, HashSet<Node> rerouteBlacklist, Node relateTo,
            Node relateDestinationPerimeterTo, Random rnd)
        {
            var belongsTo = relateTo;

            var lastSectionNode = source;
            var currentDestinationNode = destination;

            var remainingDistance = (destinationPosition - lastSectionNode.Position()).magnitude;

            var sectionLength = NextSectionLength(remainingDistance, rnd);

            var rerouted = false;
            Node reroutedNode = null;
            var fallbackWidth = ((Options) options).widthMin + (((Options) options).widthMax - ((Options) options).widthMin) / 2;

            var connectionData = new ConnectionData(options, (InternalConnection) connection, fallbackWidth, FindBestControlDirection, null, null);
            var relatedNodes = new List<Node> {source, destination};
            if (source.BelongsTo() != null && destination != null && source.BelongsTo().Equals(destination.BelongsTo()))
                relatedNodes.Add(source.BelongsTo());

            // all edges but the last
            while (remainingDistance > ((Options) options).bezierSectionLength.y * 2.1f)
            {
                if (stopFunc.Invoke()) return false;
                var fixControlHeight = source.Equals(lastSectionNode);

                // rerouting only allowed once per direct connection
                if (reroutingAllowed && !rerouted)
                {
                    rerouted = RerouteToCloseNode(previous[previous.Count - 1], destinationPosition, previous, rerouteBlacklist,
                        belongsTo, out reroutedNode);

                    if (rerouted)
                    {
                        currentDestinationNode = null;

                        destinationPosition = GetPerimeterPosition(reroutedNode, lastSectionNode, rnd);
                        Log.Debug(this, () => "New destination: " + destinationPosition);

                        destinationType = ((InternalNode) reroutedNode).PerimeterType();

                        ((InternalNode) reroutedNode).connections
                            .ForEach(c => ((InternalConnection) c).SplitAt(reroutedNode, NodeType.Of(NodeBaseType.Crossing)));

                        remainingDistance = (destinationPosition - lastSectionNode.Position()).magnitude;
                        sectionLength = NextSectionLength(remainingDistance, rnd);

                        relatedNodes.Add(reroutedNode);

                        // already too close to the new destination
                        if (remainingDistance < ((Options) options).bezierSectionLength.y * 2.1f) break;
                    }
                }

                var sectionPosition = FindBestDirection(lastSectionNode.Position(), destinationPosition, sectionLength, true,
                    false, fixControlHeight, false, relatedNodes, rnd);

                if (!sectionPosition.HasValue) throw new Exception("No sectionPosition found.");

                var sectionNode = new InternalNode(rnd.NextGuid(sectionPosition.Value).ToString(), sectionPosition.Value, NodeRadius(rnd),
                    NodeType.Of(NodeBaseType.Section), belongsTo, options.resolution);

                NewEdgesWithBorder(lastSectionNode, sectionNode, fixControlHeight, fixControlHeight,
                    belongsTo, connectionData, rnd);

                lastSectionNode = sectionNode;
                remainingDistance = (destinationPosition - lastSectionNode.Position()).magnitude;
                sectionLength = NextSectionLength(remainingDistance, rnd);

                rerouteBlacklist.Add(sectionNode);
                previous.Add(sectionNode);
            }

            // last edge
            if (currentDestinationNode == null)
            {
                if (destinationType.BaseType == NodeBaseType.CrossingPerimeter) belongsTo = rerouted ? reroutedNode : relateDestinationPerimeterTo;
                if (destinationType.BaseType == NodeBaseType.Perimeter) belongsTo = rerouted ? reroutedNode : relateDestinationPerimeterTo;

                currentDestinationNode = new InternalNode(rnd.NextGuid(destinationPosition).ToString(), destinationPosition, NodeRadius(rnd),
                    destinationType, belongsTo,
                    options.resolution);
            }

            rerouteBlacklist.Add(currentDestinationNode);
            previous.Add(currentDestinationNode);

            NewEdgesWithBorder(lastSectionNode, currentDestinationNode, false, true,
                belongsTo, connectionData, rnd);

            if (rerouted)
            {
                // this step might require multiple edges?
                previous.Add(reroutedNode);
                NewEdgesWithBorder(currentDestinationNode, reroutedNode, true, true, belongsTo, connectionData, rnd);
            }

            return rerouted;
        }

        private float NextSectionLength(float remainingDistance, Random rnd)
        {
            var bounds = ((Options) options).bezierSectionLength;

            // last section of current connection
            if (remainingDistance < bounds.y) return remainingDistance;

            var minSections = Mathf.CeilToInt(remainingDistance / bounds.y);
            var maxSections = Mathf.FloorToInt(remainingDistance / bounds.x);

            var minLength = remainingDistance / maxSections;
            var maxLength = remainingDistance / minSections;

            return rnd.NextFloat(minLength, maxLength);
        }

        private Vector3 GetPerimeterPosition(Node source, Node destination, Random rnd)
        {
            return source.Type().IsEndpoint()
                ? GetPoiPerimeterPosition(source, destination, rnd)
                : GetCrossingPerimeterPosition(source, destination);
        }

        private Vector3 GetPoiPerimeterPosition(Node source, Node destination, Random rnd)
        {
            var position = FindBestDirection(source.Position(), destination.Position(), source.Radius() * 0.99f,
                false, false, false, false, new List<Node>(), rnd).Value;

            if (nodeMaskFunc == null) return position;

            var delta = source.Position() - position;

            // find outermost intersection with the mask
            Vector3? found = null;
            var step = delta.V2().normalized;
            var steps = 0;
            var maxSteps = delta.magnitude / step.magnitude;
            var positionV2 = position.V2();

            while (steps < maxSteps)
            {
                if (stopFunc.Invoke()) return position;
                var pos = (positionV2 + step * steps);
                steps++;

                var nodeMaskValue = nodeMaskFunc.Invoke(pos);

                if (nodeMaskValue < ((Options) options).nodeMaskThreshold) continue;

                // found intersection with node mask
                var posHeight = heightFunc.Invoke(pos);

                found = new Vector3(pos.x, posHeight, pos.y);
                break;
            }

            if (!found.HasValue) return position;

            if (found.Value.V2().Equals(position.V2()))
            {
                position.y = found.Value.y;
                return position;
            } else
            {
                return found.Value;
            }
        }

        private Vector3 GetCrossingPerimeterPosition(Node source, Node destination)
        {
            var existingFirstEdge = source.Edges()[0];
            var existingSecondEdge = source.Edges()[1];

            // find normal in the right direction
            var normal = Vector3.Cross(Vector3.Lerp(existingFirstEdge.BezierCurve().Destination().ControlPosition(),
                                           existingFirstEdge.Destination().Position(), 0.5f)
                                       - existingFirstEdge.BezierCurve().Destination().Position(), Vector3.up).normalized;

            normal *= source.Radius();

            // check which direction doesn't intersect with the existing adjacent edges
            if (InvertNormalIfIntersects(destination, source, normal, existingFirstEdge, existingSecondEdge)) normal = -normal;

            return source.Position() + normal;
        }

        private Node GetPerimeterNode(Node source, Node destination, Random rnd)
        {
            var perimeterPosition = GetPerimeterPosition(source, destination, rnd);
            var type = ((InternalNode) source).PerimeterType();

            return new InternalNode(rnd.NextGuid(perimeterPosition).ToString(), perimeterPosition, NodeRadius(rnd), type,
                source, options.resolution);
        }

        private static Node FindExistingReusablePerimeterNode(Node source, Node destination)
        {
            if (InsidePerimeter(source, destination)) return null;

            if (source.Type().BaseType == NodeBaseType.Perimeter) return null;

            return source
                .GetPerimeterNodes()
                .Select(n => new KeyValuePair<float, Node>(
                    Vector2.Angle(n.PositionV2() - source.PositionV2(), destination.PositionV2() - source.PositionV2()), n))
                .OrderBy(e => e.Key)
                .Where(e => e.Key < 60)
                .Select(e => e.Value)
                .FirstOrDefault();
        }

        private bool RerouteToCloseNode(Node source, Vector3 destination, List<Node> previous, HashSet<Node> rerouteBlacklist,
            Node belongsTo, out Node newSectionNode)
        {
            newSectionNode = null;
            var maxDistance = (destination.V2() - source.PositionV2()).magnitude;

            var relevantNodeTypes = new HashSet<NodeType> {NodeType.Of(NodeBaseType.Section)};

            if (source.BelongsTo() != null) relevantNodeTypes.Add(source.Type());
            // reroute outside of perimeter can use a greater range
            var rangeModifier = belongsTo == null ? 3f : 2f;
            var range = (((Options) options).bezierSectionLength.y + ((Options) options).widthMax) * rangeModifier;

            var reroutableNodesInRange = graph.NodesInRange(source.PositionV2(),
                range, relevantNodeTypes.ToArray());
            
            if (stopFunc.Invoke()) return false;

            var nodesInRange = reroutableNodesInRange
                .Where(n => !Equals(n, source))
                .Where(n => Equals(n.BelongsTo(), belongsTo))
                .Where(n => !rerouteBlacklist.Contains(n))
                .Where(n => !previous.Contains(n))
                .Where(n => (destination.V2() - n.PositionV2()).magnitude < maxDistance)
                .Where(n => (source.PositionV2() - n.PositionV2()).magnitude < maxDistance)
                .Where(n => previous.Count != 0 && Mathf.Abs(Util.SignedAngle(n.PositionV2() - source.PositionV2(),
                                destination.V2() - source.PositionV2())) <= Mathf.Min(45, ((Options) options).bezierSectionAngleVariance))
                .Where(n => ReachableWithMaxSlope(source, n))
                .Where(n => FindNearestIntersectionWithExistingEdges(source, n) == null)
                .ToList();

            if (nodesInRange.Count == 0) return false;

            newSectionNode = nodesInRange.OrderBy(a => (source.Position() - a.Position()).magnitude).First();
            var node = newSectionNode;

            Log.Debug(this, () => "Reroute to " + node);
            return true;
        }

        private List<Node> WalkAlongExistingConnections(Node source, Vector3 destinationPosition, Node destination)
        {
            var skipTypes = new[] {NodeBaseType.Border, NodeBaseType.CrossingPerimeter, NodeBaseType.Perimeter};
            var startPath = new GraphUtil.Path(0, new List<Edge>(), source);

            var remaining = new Queue<GraphUtil.Path>();
            remaining.Enqueue(startPath);
            // always add the startPath since it may already have the shortest distance to the destination
            var finishedPaths = new List<GraphUtil.Path> {startPath};
            var visited = new HashSet<Node>();
            var skipped = new HashSet<Edge>();

            while (remaining.Count > 0)
            {
                if (stopFunc.Invoke()) return new List<Node>();
                var current = remaining.Dequeue();
                var walked = new List<Node> {current.last};
                walked.AddRange(current.edges.Select(e => e.Destination()));

                var connections = current.last.ConnectionsOut().Where(c => !walked.Contains(c.Destination())).ToList();

                if ((current.last.Type().IsPerimeter() && !current.last.Equals(source)) || current.last.Type().IsEndpoint())
                {
                    // add bridged connection with source node closest to the destination
                    var bridgedConnections = GraphUtil.GetBridgedConnections(current.last)
                        .OrderBy(c => (c.Source().PositionV2() - destinationPosition.V2()).magnitude)
                        .Take(1);
                    connections.AddRange(bridgedConnections);
                }

                foreach (var c in connections)
                {
                    Log.Debug(this, () => "Walk along " + c);
                    var edges = new List<Edge>(current.edges);
                    var edgeStart = c.Nodes().Contains(current.last) ? current.last : c.Source();
                    var nextEdges = c.EdgesUntilEndpointOrCrossing(edgeStart);

                    var lastEdge = edges.LastOrDefault();
                    var lastDistanceToDestination = lastEdge == null
                        ? float.MaxValue
                        : (destinationPosition.V2() - lastEdge.Destination().PositionV2()).magnitude;
                    var tmpWalked = new List<Edge>();

                    var aborted = false;

                    foreach (var e in nextEdges)
                    {
                        var n = e.Destination();

                        if (visited.Contains(n))
                        {
                            aborted = true;
                            break;
                        }

                        visited.Add(n);

                        var distanceToDestination = (destinationPosition.V2() - n.PositionV2()).magnitude;

                        if ((skipTypes.Contains(n.Type().BaseType)
                             // only use 'free' nodes between POIs, but allow perimeter if connections should end there anyways
                             // always include the current start Node
                             || n.BelongsTo() != null && !(GraphUtil.NodesBelongTogether(source, n) && GraphUtil.NodesBelongTogether(destination, n)))
                            && ShouldWalkToNode(source, destination, distanceToDestination, lastDistanceToDestination, n))
                        {
                            tmpWalked.Add(e);
                            skipped.Add(e);
                            Log.Debug(this, () => "Skipped " + n);
                            continue;
                        }

                        if (ShouldWalkToNode(source, destination, distanceToDestination, lastDistanceToDestination, n))
                        {
                            edges.AddRange(tmpWalked);
                            walked.AddRange(tmpWalked.Select(te => te.Destination()));
                            tmpWalked.Clear();
                            Log.Debug(this, () => "Walked " + n);
                            walked.Add(n);
                            lastDistanceToDestination = distanceToDestination;
                            edges.Add(e);
                        } else
                        {
                            Log.Debug(this, () => "Aborted walking " + c);
                            aborted = true;
                            break;
                        }
                    }

                    if (!aborted)
                    {
                        edges.AddRange(tmpWalked);
                        walked.AddRange(tmpWalked.Select(te => te.Destination()));
                        tmpWalked.Clear();
                    }

                    var last = edges.Count > 0
                        ? edges[edges.Count - 1].Destination()
                        : current.last;

                    if (last.Equals(current.last) || last.Equals(destination) || last.Type().IsPerimeter() && last.BelongsTo().Equals(destination))
                        aborted = true;

                    var path = new GraphUtil.Path(current.weight + 1, edges, last);

                    if (aborted)
                        finishedPaths.Add(path);
                    else
                        remaining.Enqueue(path);
                }
            }

            var bestPath = finishedPaths
                .Where(p => (p.last.PositionV2() - destinationPosition.V2()).magnitude < (source.PositionV2() - destinationPosition.V2()).magnitude)
                .OrderBy(p => (p.last.PositionV2() - destinationPosition.V2()).magnitude).FirstOrDefault();

            var bestWalked = new List<Node>() {source};

            if (bestPath == null) return bestWalked;

            var bestPathEdges = bestPath.edges;

            if (!bestPathEdges.Last().Destination().Equals(destination) && !destination.Equals(bestPathEdges.Last().Destination().BelongsTo()))
            {
                bestPathEdges.Reverse();
                bestPathEdges = bestPathEdges.SkipWhile(e => skipped.Contains(e)).Reverse().ToList();
            }

            bestWalked.AddRange(bestPathEdges.Select(e => e.Destination()).ToList());

            Log.Debug(this, () => "Walked to " + bestWalked.Last());
            return bestWalked;
        }

        private static bool ShouldWalkToNode(Node source, Node destination, float distanceToDestination, float lastDistanceToDestination, Node n)
        {
            return distanceToDestination < lastDistanceToDestination
                   // only allow nodes that go actually into the direction of the destination
                   && Vector2.Angle(n.PositionV2() - source.PositionV2(), destination.PositionV2() - n.PositionV2()) < 45;
        }

        private bool InvertNormalIfIntersects(Node source, Node destination, Vector3 normal, Edge existingFirstEdge, Edge existingSecondEdge)
        {
            var currentNormal = normal;
            var intersectsNormal = true;
            var intersectsInvertedNormal = true;
            var tries = 0;

            while (intersectsNormal && intersectsInvertedNormal && tries < 4)
            {
                intersectsNormal = NormalIntersects(source, destination, currentNormal, existingFirstEdge, existingSecondEdge);
                intersectsInvertedNormal = NormalIntersects(source, destination, -currentNormal, existingFirstEdge, existingSecondEdge);

                // double normal length
                currentNormal *= 2;
                tries++;
            }

            return intersectsNormal;
        }

        private bool NormalIntersects(Node source, Node destination, Vector3 normal, Edge existingFirstEdge, Edge existingSecondEdge)
        {
            var intersectsFirstEdge = Util.Intersect(destination.PositionV2() + normal.V2(), source.PositionV2(),
                destination.Edges().First().Source().PositionV2(), destination.Edges().First().Destination().PositionV2()).HasValue;

            if (intersectsFirstEdge) return true;

            var intersectsSecondEdge = Util.Intersect(destination.PositionV2() + normal.V2(), source.PositionV2(),
                destination.Edges().Last().Source().PositionV2(), destination.Edges().Last().Destination().PositionV2()).HasValue;

            if (intersectsSecondEdge) return true;

            var firstBezier = existingFirstEdge.BezierCurve();
            var secondBezier = existingSecondEdge.BezierCurve();

            var firstControl = firstBezier.Destination().Position()
                               + (firstBezier.Destination().ControlPosition() - firstBezier.Destination().Position()) * 100;
            var secondControl = secondBezier.Source().Position()
                                + (secondBezier.Source().ControlPosition() - secondBezier.Source().Position()) * 100;

            var intersectsFirstControl = Util.Intersect(destination.PositionV2() + normal.V2(), source.PositionV2(),
                firstBezier.Destination().Position().V2(), firstControl.V2()).HasValue;

            if (intersectsFirstControl) return true;

            var intersectsSecondControl = Util.Intersect(destination.PositionV2() + normal.V2(), source.PositionV2(),
                secondBezier.Source().Position().V2(), secondControl.V2()).HasValue;

            if (intersectsSecondControl) return true;

            return false;
        }

        // called for control points
        private Vector3 FindBestControlDirection(Node source, Node destination, float length, ConnectionData connectionData,
            bool fixControlHeight, Random rnd)
        {
            var controlDirection = FindBestDirection(source.Position(), destination.Position(), length, false, false,
                fixControlHeight, true, new List<Node>(), rnd);

            if (!controlDirection.HasValue) throw new Exception("no valid control points found");

            return controlDirection.Value;
        }

        public class Candidate
        {
            public Vector3 position;
            public float heightDifference;
            public Candidate parent;
            public float penalty;

            public Candidate(Vector3 position, float heightDifference, Candidate parent, float penalty)
            {
                this.position = position;
                this.heightDifference = Mathf.Abs(heightDifference);
                this.parent = parent;
                this.penalty = penalty;
            }

            // lower is better
            public float GetTotalSuitability()
            {
                var difference = heightDifference + penalty;

                if (parent != null) difference += parent.GetTotalSuitability();

                return difference;
            }

            public int Depth()
            {
                return parent == null ? 0 : parent.Depth() + 1;
            }
        }

        private Vector3? FindBestDirection(Vector3 source, Vector3 destination, float length, bool checkDistance, bool fixHeight,
            bool fixControlHeight, bool isControlPoint, List<Node> relatedNodes, Random rnd)
        {
            var startCandidate = new Candidate(source, 0, null, 0);

            var bestCandidate = ProcessBestCandidate(startCandidate, destination, length, checkDistance, fixHeight,
                fixControlHeight, isControlPoint, relatedNodes, rnd);

            var bestCandidateDepthOne = bestCandidate;

            while (bestCandidateDepthOne.Depth() > 1) bestCandidateDepthOne = bestCandidateDepthOne.parent;

            return PostProcessBestDirectionCandidate(bestCandidateDepthOne.position);
        }

        private Candidate ProcessBestCandidate(Candidate sourceCandidate, Vector3 destination, float length, bool checkDistance,
            bool fixHeight, bool fixControlHeight, bool isControlPoint, List<Node> relatedNodes, Random rnd)
        {
            var remainingCandidates = new Queue<Candidate>();

            remainingCandidates.Enqueue(sourceCandidate);

            var lookahead = isControlPoint ? 1 : ((Options) options).lookahead;
            var bestDifference = float.MaxValue;
            Candidate bestCandidate = null;

            while (remainingCandidates.Count > 0)
            {
                if (stopFunc.Invoke()) return null;
                var current = remainingCandidates.Dequeue();

                var childCandidates = current.Depth() < lookahead
                    // look further ahead
                    ? FindBestDirectionCandidates(current, destination, length, checkDistance, fixHeight, fixControlHeight, isControlPoint,
                        relatedNodes)
                    : null;

                if (childCandidates != null && childCandidates.Count != 0)
                {
                    // shuffle candidates so for equal suitability not always the same candidate wins
                    childCandidates.OrderBy(c => rnd.Next()).ToList().ForEach(c => remainingCandidates.Enqueue(c));
                } else
                {
                    // compute total height difference
                    var difference = current.GetTotalSuitability();
                    if (difference < bestDifference)
                    {
                        bestDifference = difference;
                        bestCandidate = current;
                    }
                }
            }

            return bestCandidate;
        }

        private List<Candidate> FindBestDirectionCandidates(Candidate source, Vector3 destination, float length, bool checkDistance, bool fixHeight,
            bool fixControlHeight, bool isControlPoint, List<Node> relatedNodes)
        {
            var newCandidates = new List<Candidate>();

            var sectionVector = (destination - source.position).normalized * length;
            var maxDistance = checkDistance ? (destination - source.position).magnitude : 0;

            var estimatedRemainingSections = maxDistance / sectionVector.magnitude;

            var heightBounds = GetReverseHeightBounds(source.position, destination, sectionVector);

            // will always be less than max slope since the heights are already clamped to ensure that
            var sectionAngle = sectionVector.AngleToGround();

            var angleVariance = isControlPoint
                // straighten control points when the slope angle is high
                ? options.bezierControlPointAngleVariance * (1 - sectionAngle / (options.slopeMax + 5))
                : ((Options) options).bezierSectionAngleVariance;

            // straighten up when getting closer to the destination node
            const float sectionLimit = 2f;

            var maxAngle = checkDistance && estimatedRemainingSections < sectionLimit
                ? angleVariance * 0.5f * (1 / (sectionLimit - estimatedRemainingSections))
                : angleVariance;

            var angleStep = maxAngle / ((Options) options).pathfindingCandidates;

            // the interpolated height on a straight line to the endpoint, or fixed
            var desiredHeight = fixHeight || isControlPoint && fixControlHeight ? source.position.y : (source.position + sectionVector).y;
            desiredHeight = Mathf.Clamp(desiredHeight, heightBounds.x, heightBounds.y);

            var types = new List<NodeType> {NodeType.Of(NodeBaseType.Section), NodeType.Of(NodeBaseType.Crossing)};
            types.AddRange(graph.CustomTypes());

            var nodesInRange = graph.NodesInRange(source.position.V2(), SplineTools.Instance.GlobalMaxRadius() + length, types.ToArray())
                .Where(n => !relatedNodes.Contains(n)).ToList();

            // find best direction
            for (var i = 0; i < ((Options) options).pathfindingCandidates / 2; i++)
            {
                var newCandidate = GetNewCandidate(source, destination, checkDistance, fixHeight, fixControlHeight, isControlPoint, i,
                    angleStep, sectionVector, maxDistance, desiredHeight, heightBounds, ((Options) options).pathfindingCandidates, nodesInRange);
                if (newCandidate != null)
                    newCandidates.Add(newCandidate);

                if (i == 0) continue;

                // add opposite angle as well
                var otherNewCandidate = GetNewCandidate(source, destination, checkDistance, fixHeight, fixControlHeight, isControlPoint, -i,
                    angleStep, sectionVector, maxDistance, desiredHeight, heightBounds, ((Options) options).pathfindingCandidates, nodesInRange);
                if (otherNewCandidate != null)
                    newCandidates.Add(otherNewCandidate);
            }

            return newCandidates;
        }

        private Candidate GetNewCandidate(Candidate sourceCandidate, Vector3 destination, bool checkDistance, bool fixHeight, bool fixControlHeight,
            bool isControlPoint, int i, float angleStep, Vector3 sectionVector, float maxDistance, float desiredHeight,
            Vector2 heightBounds, int steps, List<Node> nodesInRange)
        {
            var rotatedSectionVector = Quaternion.AngleAxis(i * angleStep, Vector3.up) * sectionVector;
            var candidate = sourceCandidate.position + rotatedSectionVector;

            if (checkDistance && (destination - candidate).magnitude > maxDistance) return null;

            // find the direction that has the minimum height difference to the perfect height                    
            candidate.y = heightFunc.Invoke(candidate.V2());
            var heightDifference = Mathf.Abs(candidate.y - desiredHeight);

            // fix the height for control points if required
            if (fixHeight || isControlPoint && fixControlHeight) candidate.y = sourceCandidate.position.y;
            else
            {
                // or ensure that the max slope is not violated
                candidate.y = (sourceCandidate.position + (candidate - sourceCandidate.position).RotateForSlope(options.slopeMax)).y;
                candidate.y = Mathf.Clamp(candidate.y, heightBounds.x, heightBounds.y);
            }

            var penalty = ((Options) options).bias * Mathf.Abs((float) i / steps) * 0.01f;

            // check distance to unrelated nodes, increase penalty if too close
            if (nodesInRange.Count > 0)
            {
                if (nodesInRange.Where(n => n.Type().BaseType == NodeBaseType.Custom)
                    .Any(n => n.Radius() > (n.PositionV2() - candidate.V2()).magnitude))
                {
                    // within the range of a unrelated POI -> bad
                    penalty += 9000;
                } else
                {
                    var minDistance = nodesInRange.Min(n => (n.PositionV2() - candidate.V2()).magnitude);
                    if (minDistance < SplineTools.Instance.GlobalMaxRadius())
                        penalty += (SplineTools.Instance.GlobalMaxRadius() - minDistance) * 0.01f;
                }
            }

            return new Candidate(candidate, heightDifference, sourceCandidate, penalty);
        }

        private Vector2 GetReverseHeightBounds(Vector3 source, Vector3 destination, Vector3 sectionVector)
        {
            // clamp the height so that it is possible to reach the destination while not violating max slope
            var straightCandidate = source + sectionVector;
            var border = destination;
            var sourceBorder = straightCandidate - border;
            sourceBorder.y = 0;

            var rotatedHeight = (border + sourceBorder.RotateAngle(options.slopeMax,
                                     Vector3.Cross(sourceBorder, Vector3.up))).y;
            var deltaHeight = Mathf.Abs(rotatedHeight - border.y);

            var minHeight = Mathf.Max(options.heightMin, border.y - deltaHeight);
            var maxHeight = Mathf.Min(options.heightMax, border.y + deltaHeight);
            var heightBounds = new Vector2(minHeight, maxHeight);
            return heightBounds;
        }

        private Vector3? PostProcessBestDirectionCandidate(Vector3? bestCandidate)
        {
            var resolution = options.resolution;

            // avoid points directly on the borders that are not explicitly a border
            if (bestCandidate != null && (int) bestCandidate.Value.x % resolution == 0)
            {
                var v = bestCandidate.Value;
                v.x++;
                bestCandidate = v;
            }

            if (bestCandidate != null && (int) bestCandidate.Value.z % resolution == 0)
            {
                var v = bestCandidate.Value;
                v.z++;
                bestCandidate = v;
            }

            return bestCandidate;
        }

        protected override NodeType BorderType()
        {
            return NodeType.Of(NodeBaseType.Border);
        }
        
        protected override float NextControlDistance(Node source, Node destination, Random rnd)
        {
            return source.StraightDistanceTo(destination) / rnd.NextFloat(4, 5);
        }

        private float NodeRadius(Random rnd)
        {
            var opt = ((Options) options);
            var width = opt.widthMin + (opt.widthFalloff.EvaluateClamped(rnd.NextFloat()) * (opt.widthMax - opt.widthMin));
            return width / 2;
        }
    }
}