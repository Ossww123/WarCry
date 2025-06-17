using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace ArctiumStudios.SplineTools
{
    public class ConnectionPathfinderV2 : ConnectionPathfinder
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

            public bool discardRedundantConnectionActive = true;
            public float discardRedundantConnectionFactor = 10f;

            // lake & river crossings
            public bool lakeBridgeEnabled = true;
            public float lakeBridgeRoutingThreshold = 1.5f;
            public float lakeBridgeWidthThreshold = 15f;
            public float lakeBridgeOffset = 5f;
            public bool riverBridgeEnabled = true;
            public float riverBridgeWidthThreshold = 10f;
            public float riverBridgeSlopeThreshold = 15f;
            public float riverBridgeOffset = 5f;
            public float riverFordOffset = 5f;

            public float lakePathOffset = 10f;
        }

        private readonly Func<Vector2, float> nodeMaskFunc;
        private readonly Action<Node, Node> preprocessingAction;
        private readonly InternalWorldGraph riverGraph;

        private readonly float lakeRadiusMax;

        private readonly Func<Edge, bool> riverIntersectionEdgeFilter = e => e.Nodes().Count(n => n.BelongsTo() == null ||
                                                                                                  n.Type().BaseType == NodeBaseType.LakeOuterExit ||
                                                                                                  n.Type().BaseType == NodeBaseType.SeaOuterExit) > 0;

        private readonly HashSet<ConnectionWaypoint.Type> waypointsAcrossRiverSkipTypes = new HashSet<ConnectionWaypoint.Type>()
        {
            // currently inside lake
            ConnectionWaypoint.Type.LakeBridgeDestination,
            // already inserted waypoints
            ConnectionWaypoint.Type.RiverBridgeSource,
            ConnectionWaypoint.Type.RiverBridgeDestination,
            ConnectionWaypoint.Type.RiverFordSource,
            ConnectionWaypoint.Type.RiverFordDestination
        };

        public ConnectionPathfinderV2(InternalWorldGraph graph, string reference, Func<Vector2, float> heightFunc, InternalWorldGraph riverGraph,
            Action<Node, Node> preprocessingAction, Options options, Func<Vector2, float> nodeMaskFunc,
            Func<bool> stopFunc) : base(graph, reference, heightFunc, options, stopFunc)
        {
            this.riverGraph = riverGraph;
            this.nodeMaskFunc = nodeMaskFunc;
            this.preprocessingAction = preprocessingAction;

            this.lakeRadiusMax = riverGraph == null || riverGraph.Nodes(new[] {NodeType.Of(NodeBaseType.Lake)}).Count == 0
                ? 0
                : riverGraph.Nodes(new[] {NodeType.Of(NodeBaseType.Lake)}).Max(n => n.Radius());
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

                // rerouting to the same connection stays explicitly allowed since those connections will be discarded later anyways
                walkedNodes.ForEach(n => blacklist.Add(n));

                var sourceConnect = walkedNodes.Count > 1 ? walkedNodes.Last() : source;

                var nearbyLakes = FindNearbyLakes(source.PositionV2(), destination.PositionV2());
                Log.Debug(this, () => "Relevant lakes: " + Log.LogCollection(nearbyLakes));

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
                    if (nearestSection == null)
                    {
                        Log.Debug(this, () => "Abort connection " + source + " to " + destination + ": no connectable section found");
                        return;
                    }

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

                var newConnections = ConnectIterative(sourceConnect, destinationConnect, nearbyLakes, type, blacklist);
                var opt = (Options) options;
                newConnections.ForEach(newConnection =>
                {
                    if (((InternalConnection) newConnection).EdgeCount() > 0)
                    {
                        if (!ConnectionIsValid(newConnection, graph))
                        {
                            Log.Debug(this, () => "Discarding invalid connection: " + newConnection);
                            return;
                        }

                        if (newConnection.Source().Connections().Intersect(newConnection.Destination().Connections()).Any())
                        {
                            Log.Debug(this, () => "Discarding looping connection: " + newConnection);
                            return;
                        }

                        if (opt.discardRedundantConnectionActive && GraphUtil.FindReachableNodes(newConnection.Source(),
                                opt.bezierSectionLength.y * opt.discardRedundantConnectionFactor)
                            .Contains(newConnection.Destination()))
                        {
                            Log.Debug(this, () => "Discarding short obsolete connection: " + newConnection);
                            return;
                        }

                        graph.StoreConnection(newConnection, reference);
                        foreach (var node in newConnection.Nodes()) blacklist.Add(node);
                    }
                });

                if (!(destinationConnect.Equals(destination)
                      || opt.skipInsideDestinationRadius
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

        private HashSet<Node> FindNearbyLakes(Vector2 sourcePositionV2, Vector2 destinationPositionV2)
        {
            if (riverGraph == null) return new HashSet<Node>();

            var delta = destinationPositionV2 - sourcePositionV2;
            var distance = delta.magnitude + lakeRadiusMax;
            var halfDistance = distance / 2;

            var center = sourcePositionV2 + (delta / 2);
            var lowerLeft = center + Vector2.left * halfDistance + Vector2.down * halfDistance;
            var size = new Vector2(distance, distance);

            var rect = new Rect(lowerLeft, size);

            var offsets = Offsets.ForRect(rect, InternalWorldGraph.OffsetResolution);

            return riverGraph.Nodes(new[] {NodeType.Of(NodeBaseType.Lake)}, offsets);
        }

        private List<ConnectionWaypoint> FindIntermediateWaypointsAcrossRivers(Vector3 sourcePosition, Vector3 destinationPosition,
            Vector3? previousPosition, ConnectionData connectionData)
        {
            var waypoints = new List<ConnectionWaypoint>();
            var currentSource = sourcePosition;

            var intersection = FindNearestNonReturningIntersectionWithExistingEdges(currentSource.V2(), destinationPosition.V2(),
                riverGraph, riverIntersectionEdgeFilter);

            while (intersection != null)
            {
                if (stopFunc.Invoke()) return new List<ConnectionWaypoint>();
                if (connectionData.processedIntersections.Contains(intersection)) break;
                connectionData.processedIntersections.Add(intersection);
                
                // if intersection is directly beneath a spring, just route around it
                if (intersection.Nodes()[0].Type().IsEndpoint()) return WaypointAroundRiverSpring(connectionData, intersection);

                // find best position to cross the river
                var bestEdge = FindBestEdgeToCrossRiver(currentSource, destinationPosition, previousPosition, intersection);
                
                // if best edge is directly beneath a spring, just route around it
                if (bestEdge.Nodes()[0].Type().IsEndpoint()) return WaypointAroundRiverSpring(connectionData, bestEdge);

                var bestPosition = FindBestPositionInEdgeToCross(bestEdge);
                var bestPositionV2 = bestPosition.V2();

                // add waypoints at each side of the river
                var opt = (Options) options;
                var width = ((InternalEdge) bestEdge).InterpolatedWidth(0.5f);
                var radius = width / 2;
                var riverDirection = bestEdge.V3().normalized;
                var normal = Vector3.Cross(riverDirection, Vector3.up).V2().normalized;

                // invert normal if facing towards us
                if ((currentSource.V2() - (bestPositionV2 + normal)).magnitude < (currentSource.V2() - (bestPositionV2 - normal)).magnitude)
                    normal = -normal;

                var useBridge = opt.riverBridgeEnabled
                                && (width > opt.riverBridgeWidthThreshold || riverDirection.AngleToGround() > opt.riverBridgeSlopeThreshold);

                var offset = useBridge
                    ? radius + opt.riverBridgeOffset
                    : radius + opt.riverFordOffset;

                const float heightOffset = 0.2f;

                var sourceWaypointPositionV2 = bestPositionV2 - normal * offset;
                var sourceWaypointPosition = sourceWaypointPositionV2.V3(bestPosition.y + heightOffset);
                var sourceType = useBridge ? ConnectionWaypoint.Type.RiverBridgeSource : ConnectionWaypoint.Type.RiverFordSource;

                var destinationWaypointPositionV2 = bestPositionV2 + normal * offset;
                var destinationWaypointPosition = destinationWaypointPositionV2.V3(bestPosition.y + heightOffset);
                var destinationType = useBridge ? ConnectionWaypoint.Type.RiverBridgeDestination : ConnectionWaypoint.Type.RiverFordDestination;

                var nodeData = new Dictionary<string, string> {{Constants.RiverDirection, JsonUtility.ToJson(riverDirection)}};

                waypoints.Add(new ConnectionWaypoint(sourceWaypointPosition, sourceType, normal.V3(), nodeData));
                waypoints.Add(new ConnectionWaypoint(destinationWaypointPosition, destinationType, normal.V3(), nodeData));

                // check if another intersection exists
                currentSource = destinationWaypointPosition;
                intersection = FindNearestNonReturningIntersectionWithExistingEdges(currentSource.V2(), destinationPosition.V2(),
                    riverGraph, riverIntersectionEdgeFilter);
            }

            return waypoints;
        }

        private static Vector3 FindBestPositionInEdgeToCross(Edge bestEdge)
        {
            // find 'easiest' position within the edge to cross
            var bestEdgeSrcAngleToGround = Mathf.Abs(bestEdge.BezierCurve().Source().ControlV3().AngleToGround());
            var bestEdgeDstAngleToGround = Mathf.Abs(bestEdge.BezierCurve().Destination().ControlV3().AngleToGround());

            float bestPositionInEdge;

            if (bestEdgeSrcAngleToGround + bestEdgeDstAngleToGround > 0.1f)
            {
                bestPositionInEdge = Mathf.Clamp(bestEdgeSrcAngleToGround / (bestEdgeSrcAngleToGround + bestEdgeDstAngleToGround), 0.25f, 0.75f);
            } else
            {
                bestPositionInEdge = (bestEdge.Source().Position().y > bestEdge.Destination().Position().y)
                    ? 0.9f
                    : 0.5f;
            }

            var bestPosition = bestEdge.BezierCurve().InterpolatedPosition(bestPositionInEdge);
            return bestPosition;
        }

        private List<ConnectionWaypoint> WaypointAroundRiverSpring(ConnectionData connectionData, Edge intersection)
        {
            var edgeV2 = intersection.V3().V2();
            var offset = -edgeV2.normalized * (edgeV2.magnitude + connectionData.fallbackWidth);
            var aroundSpringPosition = intersection.Nodes()[0].PositionV2() + offset;
            return new List<ConnectionWaypoint>
            {
                new ConnectionWaypoint(aroundSpringPosition.V3(heightFunc.Invoke(aroundSpringPosition)), ConnectionWaypoint.Type.Default)
            };
        }

        private Edge FindBestEdgeToCrossRiver(Vector3 sourcePosition, Vector3 destinationPosition, Vector3? previousPosition, Edge intersectionEdge)
        {
            var candidateEdges = new HashSet<Edge> {intersectionEdge};
            AddCandidateEdges(sourcePosition, destinationPosition, previousPosition, intersectionEdge, candidateEdges, edge => edge.Previous());
            AddCandidateEdges(sourcePosition, destinationPosition, previousPosition, intersectionEdge, candidateEdges, edge => edge.Next());

            // if possible, use section that doesn't belong to a lake or crossing
            if (candidateEdges.Any(e => e.Nodes().All(n => n.BelongsTo() == null)))
                candidateEdges.RemoveWhere(e => e.Nodes().Any(n => n.BelongsTo() != null));

            // river slopes are negative
            return candidateEdges.MinBy(e => Mathf.Abs(e.StraightSlopeDegrees()));
        }

        private void AddCandidateEdges(Vector3 sourcePosition, Vector3 destinationPosition, Vector3? previousPosition, Edge intersectionEdge,
            HashSet<Edge> candidateEdges, Func<Edge, Edge> nextEdgeSupplier)
        {
            var current = intersectionEdge;

            while (true)
            {
                if (stopFunc.Invoke()) return;
                var nextCandidate = nextEdgeSupplier.Invoke(current);

                if (nextCandidate == null ||
                    nextCandidate.Nodes().Any(n => n.Type().BaseType == NodeBaseType.RiverCrossing ||
                                                   n.Type().BaseType == NodeBaseType.RiverCrossingPerimeter))
                    break;

                current = nextCandidate;

                if (IsSharpEdge(current)) continue;

                var edgeCenter = current.BezierCurve().InterpolatedPosition(0.5f);
                var angle = Vector2.Angle((sourcePosition - edgeCenter).V2(), (destinationPosition - edgeCenter).V2());
                var angleToPrevious = previousPosition.HasValue
                    ? Vector2.Angle((edgeCenter - sourcePosition).V2(), (previousPosition.Value - sourcePosition).V2())
                    : 0f;
                var edgeAngleToPrevious = Vector2.Angle(current.V3().V2(), (previousPosition.Value - sourcePosition).V2());
                if (edgeAngleToPrevious > 90) edgeAngleToPrevious = 180 - edgeAngleToPrevious;

                if (angle < 120
                    || angleToPrevious < 180 - ((Options) options).bezierSectionAngleVariance
                    || edgeAngleToPrevious < ((Options) options).bezierSectionAngleVariance
                    || NotReachableWithMaxSlope(sourcePosition, 0, edgeCenter, 0)
                    || NotReachableWithMaxSlope(edgeCenter, 0, destinationPosition, 0))
                {
                    break;
                }

                candidateEdges.Add(current);
            }
        }

        private bool IsSharpEdge(Edge edge)
        {
            var destinationOnSourceControl = Util.ClosestPointOnLine(edge.BezierCurve().Source().Position().Flattened(),
                edge.BezierCurve().Source().ControlPosition().Flattened(), edge.Destination().Position().Flattened());
            if ((destinationOnSourceControl.V2() - edge.Source().Position().V2()).magnitude <
                edge.BezierCurve().Source().ControlV3().V2().magnitude) return true;

            var sourceOnDestinationControl = Util.ClosestPointOnLine(edge.BezierCurve().Destination().Position().Flattened(),
                edge.BezierCurve().Destination().ControlPosition().Flattened(), edge.Source().Position().Flattened());
            if ((sourceOnDestinationControl.V2() - edge.Destination().Position().V2()).magnitude <
                edge.BezierCurve().Destination().ControlV3().V2().magnitude) return true;

            return false;
        }

        private Dictionary<Node, List<int>> FindIntersectingLakes(Vector2 sourcePositionV2, Vector2 destinationPositionV2, List<Node> lakes)
        {
            // Log.Debug(this, () => "Check intersections with lakes: " + Log.LogCollection(lakes));

            // lakeNode -> [IntersectionIdx]
            var intersectionsByLake = new Dictionary<Node, List<int>>();

            foreach (var lake in lakes)
            {
                if (stopFunc.Invoke()) return new Dictionary<Node, List<int>>();
                var closestOnLine = Util.ClosestPointOnLine(sourcePositionV2.V3(), destinationPositionV2.V3(), lake.Position().Flattened());

                // use the closest point on line only, if it actually is within the relevant section
                var distance = (closestOnLine.V2() - sourcePositionV2).magnitude < (destinationPositionV2 - sourcePositionV2).magnitude
                    ? (closestOnLine.V2() - lake.PositionV2()).magnitude
                    : Mathf.Min((lake.PositionV2() - sourcePositionV2).magnitude, (lake.PositionV2() - destinationPositionV2).magnitude);


                // Log.Warn(this, () => sourcePositionV2 + " -> " + destinationPositionV2 + ": " + distance + " to " + lake);

                // too far away from lake
                if (distance > lake.Radius())
                {
                    intersectionsByLake.Add(lake, new List<int>());
                    continue;
                }

                // possibly intersecting lake
                var markers = lake.GetData<Vector2>(Constants.LakeOutline);

                var lakeIntersections = new List<Vector2>();
                var lakeIntersectionIdxs = new List<int>();

                var maxDistance = (markers[0] - markers[1]).magnitude * 2;

                for (var i = 0; i < markers.Count; i++)
                {
                    var marker = markers[i];
                    var closestPointOnLine = Util.ClosestPointOnLine(sourcePositionV2.V3(), destinationPositionV2.V3(), marker.V3()).V2();

                    if ((marker - closestPointOnLine).magnitude < maxDistance)
                    {
                        lakeIntersections.Add(closestPointOnLine);
                        lakeIntersectionIdxs.Add(i);
                    }
                }

                // Log.Warn(this, () => "intersections: " + Log.LogCollection(lakeIntersections) + ", idxs: " + Log.LogCollection(lakeIntersectionIdxs) + ", markers: " + Log.LogCollection(markers));

                // no actual intersection with lake found
                if (lakeIntersections.Count < 2)
                {
                    intersectionsByLake.Add(lake, new List<int>());
                    continue;
                }

                Log.Debug(this, () => sourcePositionV2 + " -> " + destinationPositionV2 + ": Intersecting lake " + lake);

                var firstFromSource = lakeIntersections.MinBy(i => (i - sourcePositionV2).magnitude);
                var firstFromDestination = lakeIntersections.MinBy(i => (i - destinationPositionV2).magnitude);

                var firstFromSourceIdx = lakeIntersectionIdxs[lakeIntersections.IndexOf(firstFromSource)];
                var firstFromDestinationIdx = lakeIntersectionIdxs[lakeIntersections.IndexOf(firstFromDestination)];

                intersectionsByLake.Add(lake, new List<int> {firstFromSourceIdx, firstFromDestinationIdx});
            }

            return intersectionsByLake;
        }

        private List<ConnectionWaypoint> FindWaypointsAroundLake(List<int> lakeIntersectionIdxs,
            Vector2 sourcePositionV2, Vector2 destinationPositionV2, Node lakeNode, List<Vector2> outline)
        {
            bool useLeft;
            int closestToSourceIdx;
            int closestToDestinationIdx;
            CheckLakeOutline(lakeIntersectionIdxs, sourcePositionV2, destinationPositionV2, outline,
                out useLeft, out closestToSourceIdx, out closestToDestinationIdx);

            return GetWaypointsAroundLake(sourcePositionV2, destinationPositionV2, lakeNode, outline, lakeIntersectionIdxs,
                closestToSourceIdx, closestToDestinationIdx, useLeft);
        }

        private List<ConnectionWaypoint> GetWaypointsAroundLake(Vector2 sourcePositionV2, Vector2 destinationPositionV2, Node lakeNode,
            List<Vector2> outline, List<int> lakeIntersectionIdxs, int closestToSourceIdx, int closestToDestinationIdx, bool useLeft)
        {
            var opt = (Options) options;

            var closestToSourceOnLine = Util.ClosestPointOnLine(sourcePositionV2.V3(), destinationPositionV2.V3(),
                outline[closestToSourceIdx].V3()).V2();
            var closestToDestinationOnLine = Util.ClosestPointOnLine(sourcePositionV2.V3(), destinationPositionV2.V3(),
                outline[closestToDestinationIdx].V3()).V2();

            // find convex hull around the lake
            var vertices = new List<Vector2>();
            vertices.Add(closestToSourceOnLine);

            var currentIdx = closestToSourceIdx;

            // add relevant part of the outline to vertices
            do
            {
                if (stopFunc.Invoke()) return new List<ConnectionWaypoint>();
                vertices.Add(outline[currentIdx]);

                currentIdx += useLeft ? -1 : 1;

                if (currentIdx < 0)
                    currentIdx = outline.Count - 1;
                else if (!useLeft)
                    currentIdx %= outline.Count;
            } while (currentIdx != closestToDestinationIdx);

            vertices.Add(closestToDestinationOnLine);

            var verticesWithOffset = Hull.WithOffset(vertices, opt.lakePathOffset, !useLeft);

            // push out waypoints that would be too close to a connected river
            PushAwayFromRivers(lakeNode, opt, ref verticesWithOffset);

            var hull = Hull.ConvexFor(verticesWithOffset);

            if (useLeft) hull.Reverse();

            var hullDistance = hull.SumSectionLengths();
            var straightDistance = (closestToDestinationOnLine - closestToSourceOnLine).magnitude;
            var hullLengthenFactor = hullDistance / straightDistance;

            // add source & destination to hull and find new convex hull
            hull.Insert(0, sourcePositionV2);
            hull.Add(destinationPositionV2);

            var waypointHull = Hull.ConvexForOrdered(hull, !useLeft);
            // remove endpoints from the hull because they are already waypoints
            waypointHull.RemoveAt(0);
            waypointHull.RemoveAt(waypointHull.Count - 1);

            List<ConnectionWaypoint> waypoints;

            if (opt.lakeBridgeEnabled &&
                (hullLengthenFactor > opt.lakeBridgeRoutingThreshold || straightDistance > opt.lakeBridgeWidthThreshold))
            {
                var sourceIntersectionV2 = outline[lakeIntersectionIdxs[0]];
                var destinationIntersectionV2 = outline[lakeIntersectionIdxs[1]];
                var direction = (destinationIntersectionV2 - sourceIntersectionV2).normalized;

                var sourceBridgePositionV2 = sourceIntersectionV2 - direction * opt.lakeBridgeOffset;
                var destinationBridgePositionV2 = destinationIntersectionV2 + direction * opt.lakeBridgeOffset;

                // set bridge points
                waypoints = new List<ConnectionWaypoint>
                {
                    new ConnectionWaypoint(sourceBridgePositionV2.V3(heightFunc.Invoke(sourceBridgePositionV2)),
                        ConnectionWaypoint.Type.LakeBridgeSource, direction.V3(), relatedNode: lakeNode),
                    new ConnectionWaypoint(destinationBridgePositionV2.V3(heightFunc.Invoke(destinationBridgePositionV2)),
                        ConnectionWaypoint.Type.LakeBridgeDestination, direction.V3(), relatedNode: lakeNode)
                };
            } else
            {
                // route around the lake
                waypoints = waypointHull.Select(v => v.V3(heightFunc.Invoke(v)))
                    .Select(v => new ConnectionWaypoint(v, ConnectionWaypoint.Type.Default, relatedNode: lakeNode)).ToList();
            }

            // Log.Warn(this, () => Log.LogCollection(vertices) + "\n\n" + Log.LogCollection(verticesWithOffset)
            //                      + "\n\n" + Log.LogCollection(hull) + "\n\n" + Log.LogCollection(waypointHull)
            //                      + "\n\n" + Log.LogCollection(waypoints));

            return waypoints;
        }

        private static void PushAwayFromRivers(Node lakeNode, Options opt, ref List<Vector2> verticesWithOffset)
        {
            var riverNodes = ((InternalNode) lakeNode).connections
                .SelectMany(c => c.Nodes()
                    // .Where(n => n.BelongsTo() == null)
                    .Where(n => n.Type().IsRiver())
                )
                .ToList();
            var maxOffset = Mathf.Max(opt.riverBridgeOffset, opt.riverFordOffset) * 2;

            var riverExitNodes = riverNodes.Where(n => n.Type().BaseType == NodeBaseType.RiverPerimeter && lakeNode.Equals(n.BelongsTo()))
                .ToList();

            const float minDistanceSectionLengthFactor = 1f;
            
            var minDistanceToRiverExit = riverExitNodes.Count == 0 ? 0 : riverExitNodes.Max(riverExit =>
            {
                var outerEdge = riverExit.Edges()
                    .First(e => e.Nodes().Any(n => n.Type().BaseType == NodeBaseType.LakeOuterExit));
                var distance = outerEdge.V3().V2().magnitude;

                if (outerEdge.Destination().Equals(riverExit))
                {
                    distance += outerEdge.Previous().V3().V2().magnitude * minDistanceSectionLengthFactor;
                } else
                {
                    distance += outerEdge.Next().V3().V2().magnitude * minDistanceSectionLengthFactor;
                }
                
                return distance;
            });

            for (var i = 0; i < verticesWithOffset.Count; i++)
            {
                var v = verticesWithOffset[i];

                // push away from river exits
                foreach (var riverExitNode in riverExitNodes)
                {
                    var delta = riverExitNode.PositionV2() - v;

                    if (delta.magnitude < minDistanceToRiverExit)
                    {
                        var outerEdge = riverExitNode.Edges()
                            .First(e => e.Nodes().Any(n => n.Type().BaseType == NodeBaseType.LakeOuterExit));
                        var displacement = outerEdge.V3().V2();

                        if (outerEdge.Destination().Equals(riverExitNode))
                        {
                            displacement += outerEdge.Previous().V3().V2() * minDistanceSectionLengthFactor;
                            displacement *= -1;
                        } else
                        {
                            displacement += outerEdge.Next().V3().V2() * minDistanceSectionLengthFactor;
                        }

                        v += displacement;
                        verticesWithOffset[i] = v;
                    }
                }

                // push away from rivers
                foreach (var riverNode in riverNodes.Where(n => n.Type().BaseType != NodeBaseType.RiverPerimeter && n.BelongsTo() == null))
                {
                    var firstEdge = riverNode.Edges()[0];
                    var minDistance = Mathf.Sqrt(Mathf.Pow(firstEdge.Length(), 2) + (maxOffset * maxOffset));

                    var delta = riverNode.PositionV2() - v;

                    if (delta.magnitude < minDistance)
                    {
                        var closestOnLine = Util.ClosestPointOnLine(firstEdge.Source().Position(), firstEdge.Destination().Position(), v.V3()).V2();

                        var displacement = (v - closestOnLine).normalized * maxOffset;
                        v += displacement;
                        verticesWithOffset[i] = v;
                    }
                }
            }
        }

        private void CheckLakeOutline(List<int> lakeIntersectionIdxs, Vector2 sourcePositionV2, Vector2 destinationPositionV2,
            List<Vector2> outline, out bool useLeft, out int closestToSourceIdx, out int closestToDestinationIdx)
        {
            var orderedIdxs = lakeIntersectionIdxs.ToList();
            orderedIdxs.Sort();

            // find side with the least amount of deviation
            var maxLeftDistance = -1f;
            var maxRightDistance = -1f;

            var firstIntersectionPosition = outline[orderedIdxs[0]];
            var secondIntersectionPosition = outline[orderedIdxs[1]];

            var closestLeftToSourceDistance = float.MaxValue;
            var closestLeftToSourceIdx = -1;
            var closestLeftToDestinationDistance = float.MaxValue;
            var closestLeftToDestinationIdx = -1;
            var closestRightToSourceDistance = float.MaxValue;
            var closestRightToSourceIdx = -1;
            var closestRightToDestinationDistance = float.MaxValue;
            var closestRightToDestinationIdx = -1;

            // outline is always counter-clockwise
            for (var i = 0; i < outline.Count; i++)
            {
                if (stopFunc.Invoke()) break;
                var markerV2 = outline[i];
                var markerFlat = markerV2.V3();
                var onLine = Util.ClosestPointOnLine(firstIntersectionPosition.V3(), secondIntersectionPosition.V3(), markerFlat);

                var markerDistance = (onLine - markerFlat).magnitude;
                var markerDistanceToSource = (sourcePositionV2 - markerV2).magnitude;
                var markerDistanceToDestination = (destinationPositionV2 - markerV2).magnitude;

                if (i > orderedIdxs[0] && i < orderedIdxs[1])
                {
                    // right
                    if (markerDistance > maxRightDistance)
                    {
                        maxRightDistance = markerDistance;
                    }

                    if (markerDistanceToSource < closestRightToSourceDistance)
                    {
                        closestRightToSourceDistance = markerDistanceToSource;
                        closestRightToSourceIdx = i;
                    }

                    if (markerDistanceToDestination < closestRightToDestinationDistance)
                    {
                        closestRightToDestinationDistance = markerDistanceToDestination;
                        closestRightToDestinationIdx = i;
                    }
                } else
                {
                    // left
                    if (markerDistance > maxLeftDistance)
                    {
                        maxLeftDistance = markerDistance;
                    }

                    if (markerDistanceToSource < closestLeftToSourceDistance)
                    {
                        closestLeftToSourceDistance = markerDistanceToSource;
                        closestLeftToSourceIdx = i;
                    }

                    if (markerDistanceToDestination < closestLeftToDestinationDistance)
                    {
                        closestLeftToDestinationDistance = markerDistanceToDestination;
                        closestLeftToDestinationIdx = i;
                    }
                }
            }

            useLeft = maxLeftDistance < maxRightDistance || maxRightDistance < 0;

            closestToSourceIdx = useLeft ? closestLeftToSourceIdx : closestRightToSourceIdx;
            closestToDestinationIdx = useLeft ? closestLeftToDestinationIdx : closestRightToDestinationIdx;
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
            var currentSourcePosition = source.PositionV2();

            var nearestIntersectionEdge = FindNearestIntersectionWithExistingEdges(source, destination, graph);

            Node intermediate = null;
            var tries = 0;

            while (nearestIntersectionEdge != null && tries < 10)
            {
                // connect only until the first intersection
                var blacklist = new HashSet<Node>();
                intermediate = FindNextConnectableSectionNode(nearestIntersectionEdge.Source(), ref blacklist);

                if (intermediate == null) return null;

                nearestIntersectionEdge = FindNearestIntersectionWithExistingEdges(currentSourcePosition, intermediate.PositionV2(), graph);
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
            var nodesOut = node.ConnectionsOut().Select(c => c.NodesBetween(node, c.Destination())).ToList();
            var max = nodesOut.Max(n => n.Count);

            for (var i = 1; i < max; i++)
            {
                var nodes = nodesOut.Where(l => l.Count > i)
                    .Select(l => l[i])
                    .ToList();

                foreach (var n in nodes) blacklist.Add(n);

                var sectionNode = nodes.FirstOrDefault(n => n.Type().BaseType == NodeBaseType.Section);
                if (sectionNode != null) return sectionNode;
            }

            return null;
        }

        private Edge FindNearestIntersectionWithExistingEdges(Node source, Node destination, InternalWorldGraph worldGraph)
        {
            return FindNearestIntersectionsWithExistingEdges(source.PositionV2(), destination.PositionV2(), worldGraph).FirstOrDefault().Key;
        }

        private Edge FindNearestIntersectionWithExistingEdges(Vector2 source, Vector2 destination, InternalWorldGraph worldGraph)
        {
            return FindNearestIntersectionsWithExistingEdges(source, destination, worldGraph).FirstOrDefault().Key;
        }

        private Edge FindNearestNonReturningIntersectionWithExistingEdges(Vector2 source, Vector2 destination,
            InternalWorldGraph worldGraph, Func<Edge, bool> edgeFilter)
        {
            var intersections = FindNearestIntersectionsWithExistingEdges(source, destination, worldGraph, edgeFilter);

            if (intersections.Count == 0) return null;

            // cross same connection twice -> discard intersection and stay on the current side
            if (intersections.Count >= 2 && intersections[0].Key.Connection().Equals(intersections[1].Key.Connection())) return null;

            return intersections[0].Key;
        }

        private List<KeyValuePair<Edge, float>> FindNearestIntersectionsWithExistingEdges(Vector2 source, Vector2 destination,
            InternalWorldGraph worldGraph, Func<Edge, bool> edgeFilter = null)
        {
            var relevantOffsets =
                Offsets.TouchedBetween(source, destination, InternalWorldGraph.OffsetResolution, (destination - source).magnitude / 2);

            var existingEdges = new List<Edge>();

            foreach (var offset in relevantOffsets)
            {
                var connections = worldGraph.Connections(offset);
                if (connections == null || connections.Count == 0) continue;

                existingEdges.AddRange(connections.SelectMany(c => ((InternalConnection) c).EdgesForOffsets(relevantOffsets)).ToList());
            }

            if (stopFunc.Invoke()) return new List<KeyValuePair<Edge, float>>();

            var intersections = existingEdges.Where(e =>
                    !(e.Nodes().Exists(n => n.PositionV2().Equals(source)) || e.Nodes().Exists(n => n.PositionV2().Equals(destination))))
                .Where(e => edgeFilter == null || edgeFilter.Invoke(e))
                .Select(e => new KeyValuePair<Edge, Vector2?>(e,
                    Util.Intersect(source, destination, e.Source().PositionV2(), e.Destination().PositionV2())))
                .Where(pair => pair.Value.HasValue)
                .Select(pair => new KeyValuePair<Edge, float>(pair.Key, (source - pair.Value.Value).magnitude))
                .OrderBy(pair => pair.Value)
                .Distinct()
                .ToList();

            return intersections;
        }

        public override List<Connection> ConnectIterative(Node source, Node destination, ConnectionType type, HashSet<Node> blacklist)
        {
            return ConnectIterative(source, destination, FindNearbyLakes(source.PositionV2(), destination.PositionV2()), type, blacklist);
        }

        private List<Connection> ConnectIterative(Node source, Node destination, HashSet<Node> nearbyLakes,
            ConnectionType type, HashSet<Node> blacklist)
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
                var waypoints = new List<ConnectionWaypoint> {destinationPerimeterPosition};

                // we always must create a perimeter node and connect it
                var newPerimeter = GetPerimeterNode(source, destination, rnd);

                // create crossing if starting in the middle of an existing connection
                if (source.Type().BaseType == NodeBaseType.Section)
                    ((InternalNode) source).connections.ForEach(c => ((InternalConnection) c).SplitAt(source, NodeType.Of(NodeBaseType.Crossing)));

                rerouteBlacklist.Add(newPerimeter);

                // allow rerouting here as well?
                ConnectDirectly(source, newPerimeter, nearbyLakes, source, ref connection, previousNodes, rnd);

                // do the actual connection from the new perimeter towards 'destinationPerimeterPosition'
                var rerouted = ConnectDirectlyOrReroute(newPerimeter, null, destinationPerimeterPosition.position,
                    ((InternalNode) destination).PerimeterType(), waypoints, nearbyLakes,
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

            var connectionWaypoint = new ConnectionWaypoint(destination.Position(), ConnectionWaypoint.Type.Destination);

            ConnectDirectlyOrReroute(previousNodes.Last(), destination, destination.Position(), destination.Type(),
                new List<ConnectionWaypoint> {connectionWaypoint}, nearbyLakes, belongsTo, destination, ref connection,
                previousNodes, rerouteBlacklist, rnd);

            return connections;
        }

        private static bool InsidePerimeter(Node source, Node destination)
        {
            return GraphUtil.NodesBelongTogether(source, destination);
        }

        private void ConnectDirectly(Node source, Node destination, HashSet<Node> nearbyLakes, Node relateTo, ref Connection connection,
            List<Node> previous, Random rnd)
        {
            var connectionWaypoint = new ConnectionWaypoint(destination.Position(), ConnectionWaypoint.Type.Destination);
            ConnectDirectlyOrReroute(source, destination.Position(), destination, destination.Type(),
                new List<ConnectionWaypoint> {connectionWaypoint}, nearbyLakes, false,
                ref connection, previous, new HashSet<Node>(), relateTo, relateTo, rnd);
        }

        private bool ConnectDirectlyOrReroute(Node source, Node destination, Vector3 destinationPosition, NodeType destinationType,
            List<ConnectionWaypoint> waypoints, HashSet<Node> nearbyLakes, Node relateTo, Node relateDestinationPerimeterTo,
            ref Connection connection, List<Node> previous,
            HashSet<Node> rerouteBlacklist, Random rnd)
        {
            return ConnectDirectlyOrReroute(source, destinationPosition, destination, destinationType, waypoints, nearbyLakes, true, ref connection,
                previous, rerouteBlacklist, relateTo, relateDestinationPerimeterTo, rnd);
        }


        private bool ConnectDirectlyOrReroute(Node source, Vector3 destinationPosition, Node destination, NodeType destinationType,
            List<ConnectionWaypoint> waypoints, HashSet<Node> nearbyLakes, bool reroutingAllowed, ref Connection connection, List<Node> previous,
            HashSet<Node> rerouteBlacklist,
            Node relateTo, Node relateDestinationPerimeterTo, Random rnd)
        {
            var current = source;
            var rerouted = false;

            Log.Debug(this, () => "Initial Waypoints:\n" + Log.LogCollection(waypoints));

            var fallbackWidth = ((Options) options).widthMin + (((Options) options).widthMax - ((Options) options).widthMin) / 2;
            var connectionData = new ConnectionData(options, (InternalConnection) connection, fallbackWidth, FindBestControlDirection, waypoints,
                nearbyLakes);

            while ((current.Position() - destinationPosition).magnitude > 0.9f)
            {
                if (stopFunc.Invoke()) return false;
                // var currentWaypoint = GetCurrentConnectionWaypoint(source.Position(), current.Position(), waypoints);

                var node = waypoints[waypoints.Count - 1].Equals(connectionData.CurrentWaypoint())
                    ? destination
                    : null;

                rerouted = ConnectDirectlyOrReroute(current, node, destinationType, reroutingAllowed,
                    connectionData, previous, rerouteBlacklist, relateTo, relateDestinationPerimeterTo, rnd);

                if (rerouted) break;

                var lastEdge = connection.LastEdge();
                current = lastEdge.Destination();

                // set next waypoint
                connectionData.currentWaypoint++;
            }

            return rerouted;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination">If provided it will be used as last node, else a new node will be created</param>
        /// <param name="destinationType"></param>
        /// <param name="reroutingAllowed"></param>
        /// <param name="connection"></param>
        /// <param name="previous"></param>
        /// <param name="rerouteBlacklist"></param>
        /// <param name="relateTo"></param>
        /// <param name="rnd"></param>
        /// <exception cref="Exception"></exception>
        private bool ConnectDirectlyOrReroute(Node source, Node destination, NodeType destinationType,
            bool reroutingAllowed, ConnectionData connectionData, List<Node> previous, HashSet<Node> rerouteBlacklist,
            Node relateTo, Node relateDestinationPerimeterTo, Random rnd)
        {
            var belongsTo = relateTo;

            var lastSectionNode = source;
            var currentDestinationNode = destination;

            var remainingDistance = (connectionData.CurrentWaypoint().position - lastSectionNode.Position()).magnitude;

            var sectionLength = NextSectionLength(remainingDistance, rnd);

            var rerouted = false;
            Node reroutedNode = null;

            var relatedNodes = new List<Node> {source, destination};
            if (source.BelongsTo() != null && destination != null && source.BelongsTo().Equals(destination.BelongsTo()))
                relatedNodes.Add(source.BelongsTo());

            var distanceThreshold = connectionData.CurrentWaypoint().type == ConnectionWaypoint.Type.Default
                ? ((Options) options).bezierSectionLength.y * 0.9f
                : ((Options) options).bezierSectionLength.y * 2.1f;

            // all edges but the last
            while (remainingDistance > distanceThreshold)
            {
                if (stopFunc.Invoke()) return false;
                var fixControlHeight = source.Equals(lastSectionNode);

                if (riverGraph != null)
                {
                    // purge lakes that are not relevant anymore
                    CleanupNearbyLakes(destination != null ? destination.PositionV2() : connectionData.waypoints.Last().position.V2(),
                        connectionData, lastSectionNode);

                    // check remaining lakes and insert new waypoints if required
                    InsertWaypointsAroundLakes(lastSectionNode.PositionV2(), connectionData.CurrentWaypoint().position.V2(), connectionData);

                    // check rivers until current waypoint and insert new waypoints if required
                    var previousPosition = previous.Count == 0 ? (Vector3?) null : previous.Last().Position();
                    InsertWaypointsAcrossRivers(lastSectionNode.Position(), connectionData, previousPosition);

                    // current waypoint may have changed -> check remainingDistance again
                    distanceThreshold = connectionData.CurrentWaypoint().type == ConnectionWaypoint.Type.Default
                        ? ((Options) options).bezierSectionLength.y * 0.9f
                        : ((Options) options).bezierSectionLength.y * 2.1f;
                    remainingDistance = (connectionData.CurrentWaypoint().position - lastSectionNode.Position()).magnitude;
                    if (remainingDistance < distanceThreshold) break;
                }

                // rerouting only allowed once per direct connection
                if (reroutingAllowed && !rerouted)
                {
                    rerouted = RerouteToCloseNode(previous[previous.Count - 1], connectionData.CurrentWaypoint().position, previous, rerouteBlacklist,
                        belongsTo, out reroutedNode);

                    if (rerouted)
                    {
                        currentDestinationNode = null;

                        connectionData.waypoints.Clear();
                        var reroutedWaypoint = GetPerimeterPosition(reroutedNode, lastSectionNode, rnd);
                        connectionData.waypoints.Add(reroutedWaypoint);
                        connectionData.currentWaypoint = 0;

                        Log.Debug(this, () => "New destination: " + reroutedWaypoint);

                        destinationType = ((InternalNode) reroutedNode).PerimeterType();

                        ((InternalNode) reroutedNode).connections
                            .ForEach(c => ((InternalConnection) c).SplitAt(reroutedNode, NodeType.Of(NodeBaseType.Crossing)));

                        remainingDistance = (connectionData.CurrentWaypoint().position - lastSectionNode.Position()).magnitude;
                        sectionLength = NextSectionLength(remainingDistance, rnd);

                        relatedNodes.Add(reroutedNode);

                        // already too close to the new destination
                        if (remainingDistance < distanceThreshold) break;
                    }
                }

                var sectionCandidate = FindBestDirection(lastSectionNode.Position(), connectionData.CurrentWaypoint().position, sectionLength, true,
                    false, fixControlHeight, false, relatedNodes, connectionData, rnd);

                if (sectionCandidate == null) throw new Exception("No sectionCandidate found.");

                var sectionNode = new InternalNode(rnd.NextGuid(sectionCandidate.position).ToString(), sectionCandidate.position, NodeRadius(rnd),
                    NodeType.Of(NodeBaseType.Section), belongsTo, options.resolution);

                NewEdgesWithBorder(lastSectionNode, sectionNode, fixControlHeight, fixControlHeight,
                    belongsTo, connectionData, rnd);

                lastSectionNode = sectionNode;
                remainingDistance = (connectionData.CurrentWaypoint().position - lastSectionNode.Position()).magnitude;
                sectionLength = NextSectionLength(remainingDistance, rnd);

                rerouteBlacklist.Add(sectionNode);
                previous.Add(sectionNode);
            }

            // for default waypoints, stop before actually reaching it
            if (!rerouted && connectionData.CurrentWaypoint().type == ConnectionWaypoint.Type.Default) return false;

            // last edge
            if (currentDestinationNode == null)
            {
                if (destinationType.BaseType == NodeBaseType.CrossingPerimeter) belongsTo = rerouted ? reroutedNode : relateDestinationPerimeterTo;
                if (destinationType.BaseType == NodeBaseType.Perimeter) belongsTo = rerouted ? reroutedNode : relateDestinationPerimeterTo;

                // set nodeType defined by waypoint
                destinationType = connectionData.CurrentWaypoint().NodeType(destinationType);

                currentDestinationNode = new InternalNode(rnd.NextGuid(connectionData.CurrentWaypoint().position).ToString(),
                    connectionData.CurrentWaypoint().position, NodeRadius(rnd),
                    destinationType, belongsTo, options.resolution);

                if (connectionData.CurrentWaypoint().nodeData != null)
                {
                    foreach (var pair in connectionData.CurrentWaypoint().nodeData) currentDestinationNode.AddData(pair.Key, pair.Value);
                }
            }

            rerouteBlacklist.Add(currentDestinationNode);
            previous.Add(currentDestinationNode);

            NewEdgesWithBorder(lastSectionNode, currentDestinationNode, false, true,
                belongsTo, connectionData, rnd);

            // replace destinationControlDirection on last edge if set on waypoint
            if (connectionData.CurrentWaypoint().controlDirection.HasValue)
            {
                var internalBezierPoint = (InternalBezierPoint) connectionData.connection.LastEdge().BezierCurve().Destination();
                internalBezierPoint.controlPosition = internalBezierPoint.Position() -
                                                      (connectionData.CurrentWaypoint().controlDirection.Value *
                                                       internalBezierPoint.ControlV3().magnitude);
            }

            if (rerouted)
            {
                // this step might require multiple edges?
                previous.Add(reroutedNode);
                NewEdgesWithBorder(currentDestinationNode, reroutedNode, true, true, belongsTo, connectionData, rnd);
            }

            return rerouted;
        }

        private void InsertWaypointsAroundLakes(Vector2 sourcePositionV2, Vector2 destinationPositionV2, ConnectionData connectionData)
        {
            // Log.Debug(this, () => "Remaining nearby lakes: " + Log.LogCollection(connectionData.nearbyLakes));

            var lakes = connectionData.nearbyLakes
                // lake is in relevant distance
                .Where(lake => (lake.PositionV2() - sourcePositionV2).magnitude <
                               lake.Radius() + 5 * ((Options) options).bezierSectionLength.y)
                .ToList();

            var lakeIntersections = FindIntersectingLakes(sourcePositionV2, destinationPositionV2, lakes)
                .Where(e => e.Value.Count > 0)
                .ToList();

            if (lakeIntersections.Count == 0) return;

            // List<ConnectionWaypoint> newWaypoints = new List<ConnectionWaypoint>();

            foreach (var lakeIntersection in lakeIntersections)
            {
                if (stopFunc.Invoke()) return;
                // intersecting lake
                var markers = lakeIntersection.Key.GetData<Vector2>(Constants.LakeOutline);
                var waypointsAroundLake = FindWaypointsAroundLake(lakeIntersection.Value,
                    sourcePositionV2, destinationPositionV2, lakeIntersection.Key, markers);

                if (waypointsAroundLake.Count == 0) return;

                // remove the lake from further checks
                Log.Debug(this, () => "Remove already processed nearby lake: " + lakeIntersection.Key);
                connectionData.nearbyLakes.Remove(lakeIntersection.Key);

                Log.Debug(this, () => "Insert new waypoints around lake: " + Log.LogCollection(waypointsAroundLake));

                UpdateWaypoints(sourcePositionV2, connectionData, waypointsAroundLake);
            }

            Log.Debug(this, () => "Current Waypoints: " + Log.LogCollection(connectionData.waypoints));
        }

        private void UpdateWaypoints(Vector2 sourcePositionV2, ConnectionData connectionData, List<ConnectionWaypoint> waypointsToInsert)
        {
            var firstToInsert = waypointsToInsert[0].position.V2();
            var distanceSourceToFirst = (firstToInsert - sourcePositionV2).magnitude;
            var lastToInsert = waypointsToInsert[waypointsToInsert.Count - 1].position.V2();
            var lastWaypointPosition = connectionData.waypoints[connectionData.waypoints.Count - 1].position.V2();
            var distanceLastToLastWaypoint = (lastWaypointPosition - lastToInsert).magnitude;

            var newWaypoints = connectionData.waypoints
                .Where(w => (w.position.V2() - sourcePositionV2).magnitude < distanceSourceToFirst ||
                            (w.position.V2() - lastWaypointPosition).magnitude < distanceLastToLastWaypoint)
                .ToList();

            var index = 0;

            while (index < newWaypoints.Count && (newWaypoints[index].position.V2() - sourcePositionV2).magnitude < distanceSourceToFirst)
                index++;

            // only insert waypoints before the destination
            if (index < newWaypoints.Count) newWaypoints.InsertRange(index, waypointsToInsert);
            // decrement index to match the current last index
            else index--;

            connectionData.waypoints.Clear();
            connectionData.waypoints.AddRange(newWaypoints);
            connectionData.currentWaypoint = index;
        }

        private void InsertWaypointsAcrossRivers(Vector3 sourcePosition, ConnectionData connectionData, Vector3? previousPosition)
        {
            var currentWaypoint = connectionData.CurrentWaypoint();

            if (waypointsAcrossRiverSkipTypes.Contains(currentWaypoint.type)) return;

            var waypointsAcrossRivers = FindIntermediateWaypointsAcrossRivers(sourcePosition,
                currentWaypoint.position, previousPosition, connectionData);

            if (waypointsAcrossRivers.Count == 0) return;

            Log.Debug(this, () => "Insert new waypoints across rivers: " + Log.LogCollection(waypointsAcrossRivers));

            UpdateWaypoints(sourcePosition.V2(), connectionData, waypointsAcrossRivers);

            Log.Debug(this, () => "Current Waypoints: " + Log.LogCollection(connectionData.waypoints));
        }

        private void CleanupNearbyLakes(Vector2 destinationPositionV2, ConnectionData connectionData, Node lastSectionNode)
        {
            var lakesToBeRemoved = new HashSet<Node>();
            foreach (var lake in connectionData.nearbyLakes)
            {
                if ((lake.PositionV2() - destinationPositionV2).magnitude - lake.Radius() >
                    (lastSectionNode.PositionV2() - destinationPositionV2).magnitude)
                    lakesToBeRemoved.Add(lake);
            }

            foreach (var lake in lakesToBeRemoved)
            {
                Log.Debug(this, () => "Remove already passed nearby lake: " + lake);
                connectionData.nearbyLakes.Remove(lake);
            }
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

        private ConnectionWaypoint GetPerimeterPosition(Node source, Node destination, Random rnd)
        {
            var perimeterPosition = source.Type().IsEndpoint()
                ? GetPoiPerimeterPosition(source, destination, rnd)
                : GetCrossingPerimeterPosition(source, destination);

            return new ConnectionWaypoint(perimeterPosition, ConnectionWaypoint.Type.Perimeter);
        }

        private Vector3 GetPoiPerimeterPosition(Node source, Node destination, Random rnd)
        {
            var position = FindBestDirection(source.Position(), destination.Position(), source.Radius() * 0.99f,
                false, false, false, false, new List<Node>(), null, rnd).position;

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

                found = pos.V3(posHeight);
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

            return new InternalNode(rnd.NextGuid(perimeterPosition.position).ToString(), perimeterPosition.position, NodeRadius(rnd), type,
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
                .Where(n => FindNearestIntersectionWithExistingEdges(source, n, graph) == null)
                .Where(n => riverGraph == null || FindNearestIntersectionWithExistingEdges(source, n, riverGraph) == null)
                .ToList();

            if (nodesInRange.Count == 0) return false;

            newSectionNode = nodesInRange.MinBy(a => (source.Position() - a.Position()).magnitude);
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

            var bestPath = finishedPaths.MinBy(p => (p.last.PositionV2() - destinationPosition.V2()).magnitude);

            var bestWalked = new List<Node> {source};

            if (bestPath == null ||
                (bestPath.last.PositionV2() - destinationPosition.V2()).magnitude >= (source.PositionV2() - destinationPosition.V2()).magnitude)
                return bestWalked;

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
                fixControlHeight, true, new List<Node>(), connectionData, rnd);

            if (controlDirection == null) throw new Exception("no valid control points found");

            return controlDirection.position;
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

        private Candidate FindBestDirection(Vector3 source, Vector3 destination, float length, bool checkDistance, bool fixHeight,
            bool fixControlHeight, bool isControlPoint, List<Node> relatedNodes, ConnectionData connectionData, Random rnd)
        {
            var startCandidate = new Candidate(source, 0, null, 0);

            var bestCandidate = ProcessBestCandidate(startCandidate, destination, length, checkDistance, fixHeight,
                fixControlHeight, isControlPoint, relatedNodes, connectionData, rnd);

            var bestCandidateDepthOne = bestCandidate;

            while (bestCandidateDepthOne.Depth() > 1) bestCandidateDepthOne = bestCandidateDepthOne.parent;

            return PostProcessBestDirectionCandidate(bestCandidateDepthOne);
        }

        private Candidate ProcessBestCandidate(Candidate sourceCandidate, Vector3 destination, float length, bool checkDistance,
            bool fixHeight, bool fixControlHeight, bool isControlPoint, List<Node> relatedNodes, ConnectionData connectionData, Random rnd)
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
                        relatedNodes, connectionData)
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
            bool fixControlHeight, bool isControlPoint, List<Node> relatedNodes, ConnectionData connectionData)
        {
            var newCandidates = new List<Candidate>();
            var allRelatedNodes = new List<Node>(relatedNodes);
            var currentWaypoint = connectionData != null ? connectionData.CurrentWaypoint() : null;
            if (currentWaypoint != null && currentWaypoint.relatedNode != null)
                allRelatedNodes.Add(currentWaypoint.relatedNode);

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
                .Where(n => !allRelatedNodes.Contains(n)).ToList();

            if (riverGraph != null)
            {
                var riverTypes = new List<NodeType> {NodeType.Of(NodeBaseType.RiverSection), NodeType.Of(NodeBaseType.RiverCrossing)};
                var riverNodesInRange = riverGraph.NodesInRange(source.position.V2(), SplineTools.Instance.GlobalMaxRadius() + length,
                        riverTypes.ToArray())
                    .Where(n => !allRelatedNodes.Contains(n))
                    .ToList();

                nodesInRange.AddRange(riverNodesInRange);

                if (connectionData != null && connectionData.nearbyLakes.Count > 0)
                {
                    // lakes with waypoints were already removed from nearbyLakes
                    var lakesInRange = connectionData.nearbyLakes
                        .Where(lake => (source.position.V2() - lake.PositionV2()).magnitude <
                                       length + lake.Radius() + SplineTools.Instance.GlobalMaxRadius())
                        .Where(n => !allRelatedNodes.Contains(n))
                        .ToList();

                    nodesInRange.AddRange(lakesInRange);
                }
            }

            // find best direction
            for (var i = 0; i < ((Options) options).pathfindingCandidates / 2; i++)
            {
                var newCandidate = GetNewCandidate(source, destination, checkDistance, fixHeight, fixControlHeight, isControlPoint, i,
                    angleStep, sectionVector, maxDistance, desiredHeight, heightBounds, ((Options) options).pathfindingCandidates,
                    nodesInRange, currentWaypoint);
                if (newCandidate != null)
                    newCandidates.Add(newCandidate);

                if (i == 0) continue;

                // add opposite angle as well
                var otherNewCandidate = GetNewCandidate(source, destination, checkDistance, fixHeight, fixControlHeight, isControlPoint, -i,
                    angleStep, sectionVector, maxDistance, desiredHeight, heightBounds, ((Options) options).pathfindingCandidates,
                    nodesInRange, currentWaypoint);
                if (otherNewCandidate != null)
                    newCandidates.Add(otherNewCandidate);
            }

            return newCandidates;
        }

        private Candidate GetNewCandidate(Candidate sourceCandidate, Vector3 destination, bool checkDistance, bool fixHeight, bool fixControlHeight,
            bool isControlPoint, int i, float angleStep, Vector3 sectionVector, float maxDistance, float desiredHeight,
            Vector2 heightBounds, int steps, List<Node> nodesInRange, ConnectionWaypoint currentWaypoint)
        {
            var rotatedSectionVector = Quaternion.AngleAxis(i * angleStep, Vector3.up) * sectionVector;
            var candidate = sourceCandidate.position + rotatedSectionVector;

            if (checkDistance && (destination - candidate).magnitude > maxDistance) return null;

            // find the direction that has the minimum height difference to the perfect height                    
            candidate.y = heightFunc.Invoke(candidate.V2());

            // when inside the range of the current waypoint's related node, clamp the height
            if (currentWaypoint != null && currentWaypoint.relatedNode != null &&
                (candidate.V2() - currentWaypoint.relatedNode.PositionV2()).magnitude < currentWaypoint.relatedNode.Radius())
            {
                candidate.y = Mathf.Max(candidate.y, currentWaypoint.relatedNode.Position().y + 0.1f);
            }

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
                if (nodesInRange.Where(n => n.Type().BaseType == NodeBaseType.Custom || n.Type().BaseType == NodeBaseType.Lake)
                    .Any(n => n.Radius() > (n.PositionV2() - candidate.V2()).magnitude))
                {
                    // within the range of a unrelated POI -> bad
                    penalty += 9000;
                } else
                {
                    var closest = nodesInRange.MinBy(n => (n.PositionV2() - candidate.V2()).magnitude);
                    var minDistance = (closest.PositionV2() - candidate.V2()).magnitude;
                    if (minDistance < SplineTools.Instance.GlobalMaxRadius())
                    {
                        var proximityPenaltyFactor = (closest.Type().BaseType == NodeBaseType.RiverSection ? 1f : 0.01f);
                        penalty += (SplineTools.Instance.GlobalMaxRadius() - minDistance) * proximityPenaltyFactor;
                    }
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

        private Candidate PostProcessBestDirectionCandidate(Candidate bestCandidate)
        {
            var resolution = options.resolution;

            // avoid points directly on the borders that are not explicitly a border
            if (bestCandidate != null && (int) bestCandidate.position.x % resolution == 0)
            {
                var v = bestCandidate.position;
                v.x++;
                bestCandidate.position = v;
            }

            if (bestCandidate != null && (int) bestCandidate.position.z % resolution == 0)
            {
                var v = bestCandidate.position;
                v.z++;
                bestCandidate.position = v;
            }

            return bestCandidate;
        }

        public bool NotReachableWithMaxSlope(Vector3 sourcePosition, float sourceRadius, Vector3 destinationPosition, float destinationRadius)
        {
            var deltaV2 = destinationPosition.V2() - sourcePosition.V2();
            var deltaPlain = new Vector3(deltaV2.x, 0, deltaV2.y).normalized;

            var sourceBorder = sourcePosition + deltaPlain * sourceRadius;
            var destinationBorder = destinationPosition - deltaPlain * destinationRadius;

            var angleToGround = (destinationBorder - sourceBorder).AngleToGround();

            return angleToGround > options.slopeMax;
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
