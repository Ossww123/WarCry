using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class ConnectionPart
    {
        public List<Edge> edges = new List<Edge>();
    }

    [Serializable]
    public class InternalConnection : Connection, ISerializationCallbackReceiver
    {
        [SerializeField] private string guid;

        [NonSerialized] private ConnectionType type;

        [NonSerialized] private Node source;
        [NonSerialized] private Node destination;
        [SerializeField] private Directions direction;

        [NonSerialized] private List<ConnectionPart> parts = new List<ConnectionPart>();

        [NonSerialized] private float widthMax = 0;
        [NonSerialized] private float length = 0;

        // cached reversed connection
        [NonSerialized] private InternalConnection reversed;
        [NonSerialized] private bool transient = false;

        // de-/serialization helpers
        [SerializeField] private InternalEdge[] serializedEdges;
        [SerializeField] private ConnectionBaseType connectionBaseType;
        [SerializeField] private string connectionCustomType;

        public InternalConnection(string guid, ConnectionType type, Directions direction, bool transient)
        {
            this.guid = guid;
            this.type = type;
            this.direction = direction;
            this.transient = transient;
        }

        public Node Source()
        {
            return source;
        }

        public Node Destination()
        {
            return destination;
        }

        public List<Node> IntermediateCrossings()
        {
            var crossings = new List<Node>();

            for (var i = 1; i < parts.Count; i++) crossings.Add(parts[i].edges[0].Source());

            return crossings;
        }

        public Edge FirstEdge()
        {
            return parts[0].edges[0];
        }

        public Edge LastEdge()
        {
            var lastPart = parts[parts.Count - 1];
            return lastPart.edges[lastPart.edges.Count - 1];
        }

        public List<Edge> Edges()
        {
            return parts.SelectMany(p => p.edges).ToList();
        }

        public List<Edge> EdgesBetween(Node source, Node destination, bool ignoreDirection = false)
        {
            if (source.Equals(destination)) return new List<Edge>();

            var c = this;
            var nodes = Nodes();

            var indexOfSource = nodes.IndexOf(source);
            var indexOfDestination = nodes.IndexOf(destination);

            // one of the nodes is not part of this connection
            if (indexOfSource < 0 || indexOfDestination < 0) return new List<Edge>();

            if (indexOfSource > indexOfDestination)
            {
                if (direction == Directions.OneWayForward && !ignoreDirection) return new List<Edge>();
                c = (InternalConnection) Reversed();
            } else if (direction == Directions.OneWayBackward && !ignoreDirection)
            {
                return new List<Edge>();
            }

            var allEdges = c.Edges();
            var edges = new List<Edge>();

            var add = false;

            foreach (var edge in allEdges)
            {
                if (edge.Source().Equals(source)) add = true;

                if (add)
                {
                    edges.Add(edge);
                    if (edge.Destination().Equals(destination)) break;
                }
            }

            return edges;
        }

        public List<Node> Nodes()
        {
            var edges = Edges();

            if (edges.Count == 0) return new List<Node>();

            var nodes = new List<Node> {source};
            edges.ForEach(e => nodes.Add(e.Destination()));
            return nodes;
        }

        public List<Node> NodesBetween(Node source, Node destination, bool ignoreDirection = false)
        {
            var nodes = new List<Node> {source};

            var collectedEdges = EdgesBetween(source, destination, ignoreDirection);
            collectedEdges.ForEach(e => nodes.Add(e.Destination()));

            return nodes;
        }

        public float WidthMax()
        {
            return widthMax;
        }

        public float Length(Node source = null, Node destination = null)
        {
            if (source == null && destination == null) return length;

            if (source == null) source = Source();
            if (destination == null) destination = Destination();

            return EdgesBetween(source, destination).Sum(e => e.Length());
        }

        public Directions Direction()
        {
            return direction;
        }

        public ConnectionType Type()
        {
            return type;
        }

        public Connection Reversed()
        {
            if (reversed == null)
            {
                var reversedConnection = new InternalConnection(guid, type, direction.Reversed(), true);
                reversedConnection.reversed = this;
                var reversedEdges = Edges().Select(e => ((InternalEdge) e).Reversed(reversedConnection)).Reverse().ToList();

                reversedEdges.ForEach(e => reversedConnection.StoreEdge((InternalEdge) e));

                reversed = reversedConnection;
            }

            return reversed;
        }

        public List<Edge> EdgesUntilEndpointOrCrossing(Node source)
        {
            var traversed = new List<Edge>();
            var add = false;

            foreach (var edge in Edges())
            {
                if (edge.Source().Equals(source)) add = true;

                if (add)
                {
                    traversed.Add(edge);
                    if (edge.Destination().IsEndpointOrCrossing()) break;
                }
            }

            return traversed;
        }

        public List<Connection> DirectedOutgoingFrom(Node source)
        {
            switch (direction)
            {
                case Directions.OneWayForward:
                    return source.Equals(destination) ? new List<Connection>() : new List<Connection> {this};
                case Directions.OneWayBackward:
                    return source.Equals(this.source) ? new List<Connection>() : new List<Connection> {Reversed()};
                case Directions.TwoWay:
                    if (source.Equals(this.source)) return new List<Connection> {this};
                    if (source.Equals(destination)) return new List<Connection> {Reversed()};
                    return new List<Connection> {this, Reversed()};
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public List<Connection> DirectedIncomingTo(Node target)
        {
            switch (direction)
            {
                case Directions.OneWayForward:
                    return target.Equals(source) ? new List<Connection>() : new List<Connection> {this};
                case Directions.OneWayBackward:
                    return target.Equals(destination) ? new List<Connection>() : new List<Connection> {Reversed()};
                case Directions.TwoWay:
                    if (target.Equals(destination)) return new List<Connection> {this};
                    if (target.Equals(source)) return new List<Connection> {Reversed()};
                    return new List<Connection> {this, Reversed()};
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public int EdgeCount()
        {
            return parts.Sum(part => part.edges.Count);
        }

        public Edge EdgeFrom(Node source)
        {
            return Edges().FirstOrDefault(e => e.Source().Equals(source));
        }

        public void StoreEdge(InternalEdge internalEdge)
        {
            if (parts.Count == 0)
            {
                source = internalEdge.Source();
                parts.Add(new ConnectionPart());
            }

            destination = internalEdge.Destination();

            var part = parts[parts.Count - 1];

            if (part.edges.Count > 0)
            {
                var previousEdge = (InternalEdge) part.edges[part.edges.Count - 1];
                previousEdge.SetNext(internalEdge);
                internalEdge.SetPrevious(previousEdge);

                // start a new part at crossings
                if (internalEdge.Source().Type().IsCrossing())
                {
                    parts.Add(new ConnectionPart());
                    part = parts[parts.Count - 1];
                }
            }

            part.edges.Add(internalEdge);

            internalEdge.SetConnection(this);

            foreach (var node in internalEdge.Nodes())
            {
                var nodeImpl = (InternalNode) node;

                nodeImpl.AddEdge(internalEdge);
                nodeImpl.AddConnection(this);
            }

            widthMax = Mathf.Max(widthMax, internalEdge.Widths().Max());
            length += internalEdge.Length();
        }

        public Edge AddEdge(Node source, Node destination, List<float> widths, BezierCurve bezierCurve)
        {
            if (parts.Count != 0 && !LastEdge().Destination().Equals(source)) Log.Error(this, () => "invalid edge");

            DeleteCachedReversedOnChanges();

            var edge = new InternalEdge(source, destination, widths, bezierCurve, transient);

            Log.Debug(this, () => "Connection " + Util.ShortenGuid(guid) + ": Add Edge " + edge);

            StoreEdge(edge);

            return edge;
        }

        public void SplitAt(Node crossing, NodeType crossingType)
        {
            DeleteCachedReversedOnChanges();

            var partIdx = parts.FindIndex(p => p.edges.Exists(e => e.Nodes().Contains(crossing)));

            var partNodes = new List<Node> {parts[partIdx].edges.First().Source()};
            partNodes.AddRange(parts[partIdx].edges.Select(e => e.Destination()));

            // no need to split when the crossing node already is at the end or beginning of a part
            if (Equals(crossing, partNodes.First()) || Equals(crossing, partNodes.Last())) return;

            if (!crossing.IsEndpointOrCrossing()) ((InternalNode) crossing).SetType(crossingType);

            var firstPartEdges = new List<Edge>();
            var secondPartEdges = new List<Edge>();
            var currentEdges = firstPartEdges;

            foreach (var edge in parts[partIdx].edges)
            {
                currentEdges.Add(edge);
                if (edge.Destination().Equals(crossing)) currentEdges = secondPartEdges;
            }

            parts[partIdx].edges = firstPartEdges;

            var secondPart = new ConnectionPart {edges = secondPartEdges};
            parts.Insert(partIdx + 1, secondPart);
        }


        private void DeleteCachedReversedOnChanges()
        {
            if (!transient && reversed != null) reversed = null;
        }

        public List<Edge> EdgesForOffsets(HashSet<Offset> offsets)
        {
            return Edges().Where(e => offsets.Contains(Offset.For(e.Source().Position(), InternalWorldGraph.OffsetResolution))
                                      || offsets.Contains(Offset.For(e.Destination().Position(), InternalWorldGraph.OffsetResolution))).ToList();
        }

        public List<List<Edge>> DivideIntoSubsections(Rect rect, bool fullInside = false, bool startAtCrossing = false)
        {
            var allEdges = Edges();
            var subsections = new List<List<Edge>>();

            for (var i = 0; i < allEdges.Count; i++)
            {
                var edge = (InternalEdge) allEdges[i];
                if (!fullInside && edge.Inside(rect) ||
                    fullInside && edge.FullInside(rect)
                               && (edge.Source().Type().IsBorder()
                                   || edge.Source().Type().IsEndpoint()
                                   || startAtCrossing && edge.Source().Type().IsCrossing()))
                {
                    var subsection = new List<Edge> {edge};
                    subsections.Add(subsection);
                    
                    // subsection with only one section is possible if source and destination meet the criteria
                    if (fullInside
                        && (edge.Destination().Type().IsBorder()
                            || edge.Destination().Type().IsEndpoint()
                            || startAtCrossing && edge.Destination().Type().IsCrossing())
                        && edge.Destination().PositionV2().Inside(rect)) continue;

                    for (var j = i + 1; j < allEdges.Count; j++)
                    {
                        var edgeImpl = (InternalEdge) allEdges[j];
                        if (edgeImpl.Inside(rect))
                        {
                            subsection.Add(edgeImpl);
                            i = j;

                            if (fullInside
                                && (edgeImpl.Destination().Type().IsBorder()
                                    || edgeImpl.Destination().Type().IsEndpoint()
                                    || startAtCrossing && edgeImpl.Destination().Type().IsCrossing())
                                && edgeImpl.Destination().PositionV2().Inside(rect)) break;
                        } else break;
                    }

                }
            }

            return subsections;
        }

        public bool IsPrimary()
        {
            return parts.Count > 1;
        }

        public bool IsSecondary()
        {
            return source.Type().IsCrossing() || destination.Type().IsCrossing();
        }

        public bool StartsOrEndsWithCrossingOfOther(Connection other)
        {
            if (this == other || !IsSecondary()) return false;

            var otherImpl = (InternalConnection) other;

            if (source.Type().IsCrossing() && otherImpl.parts
                .Any(p => p.edges.SourceEndpoint().Equals(source) || p.edges.DestinationEndpoint().Equals(source)))
                return true;

            if (destination.Type().IsCrossing() && otherImpl.parts
                .Any(p => p.edges.SourceEndpoint().Equals(destination) || p.edges.DestinationEndpoint().Equals(destination)))
                return true;

            return false;
        }

        public InternalConnection Optimized()
        {
            var rnd = new ConsistentRandom(((InternalNode) source).Seed() * ((InternalNode) destination).Seed());

            if (direction == Directions.OneWayBackward)
            {
                var optimized = new InternalConnection(guid, type, Directions.OneWayForward, false);
                var reversedEdges = Edges().Select(e => ((InternalEdge) e).Reversed(optimized)).Reverse();

                foreach (var edge in reversedEdges) optimized.AddEdge(edge.Source(), edge.Destination(), edge.Widths(), edge.BezierCurve());

                optimized.guid = rnd.NextGuid(optimized.source.Position(), optimized.destination.Position()).ToString();

                return optimized;
            }

            transient = false;
            Edges().ForEach(e => ((InternalEdge) e).SetTransient(false));
            guid = rnd.NextGuid(source.Position(), destination.Position()).ToString();

            return this;
        }

        public int Seed()
        {
            var seed = 341625236;
            foreach (var node in Nodes()) seed *= ((InternalNode) node).Seed();
            return seed;
        }

        public string Guid()
        {
            return guid;
        }

        public bool IsTransient()
        {
            return transient;
        }

        public void OnBeforeSerialize()
        {
            if (EdgeCount() > 0)
                serializedEdges = Edges().Select(e => (InternalEdge) e).ToArray();

            connectionBaseType = type.BaseType;
            connectionCustomType = type.CustomType;
        }

        public void OnAfterDeserialize()
        {
            parts = new List<ConnectionPart>();

            type = ConnectionType.Of(connectionBaseType, connectionCustomType);
            connectionCustomType = null;
        }

        public void DeserializeEdges(Dictionary<string, InternalNode> nodesByGuid)
        {
            if (serializedEdges != null && serializedEdges.Length > 0)
                foreach (var edge in serializedEdges)
                {
                    foreach (var edgeNodeGuid in edge.serializedNodeGuids)
                        edge.Nodes().Add(nodesByGuid[edgeNodeGuid]);

                    StoreEdge(edge);
                }

            serializedEdges = null;
        }

        public override string ToString()
        {
            return Util.ShortenGuid(guid) + " " + source + " -> " + destination + ": " + EdgeCount() + " edges\nNodes:\n" +
                   Log.LogCollection(Nodes());
        }
    }
}