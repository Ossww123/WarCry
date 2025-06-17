using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    public class InternalNode : Node, ISerializationCallbackReceiver
    {
        [SerializeField] public Vector3 position;

        [SerializeField] public string guid;

        [NonSerialized] public WorldGraph graph;
        [NonSerialized] public List<Connection> connections = new List<Connection>();

        [NonSerialized] private List<Edge> edges = new List<Edge>();

        [NonSerialized] public Node belongsTo;
        [NonSerialized] public NodeType type;
        [SerializeField] public float radius;

        [NonSerialized] private Dictionary<string, string> data = new Dictionary<string, string>();

        // de-/serialization helpers
        [SerializeField] private string[] serializedDataKeys;
        [SerializeField] private string[] serializedDataValues;
        [SerializeField] private string belongsToGuid;
        [SerializeField] private NodeBaseType nodeBaseType;
        [SerializeField] private string nodeCustomType;

        public InternalNode(string guid, Vector3 position, float radius, NodeType type, Node belongsTo, int terrainSize)
        {
            this.position = FindValidPosition(position, type, terrainSize);
            this.type = type;
            this.radius = radius;
            this.belongsTo = belongsTo;
            this.guid = guid;
        }

        public bool HasDirectConnectionTo(Node other)
        {
            if (graph != ((InternalNode) other).graph) return false;

            lock (connections)
            {
                if (other.Type().IsEndpoint()) return connections.Exists(c => c.Source().Equals(other) || c.Destination().Equals(other));
                if (other.Type().IsCrossing()) return connections.Exists(c => c.IntermediateCrossings().Contains(other));

                return connections.Exists(c => c.Nodes().Contains(other));
            }
        }

        public bool IsReachableFrom(Node other)
        {
            if (this.Equals(other)) return true;
            return GraphUtil.FindShortestPath(other, this).edges.Count > 0;
        }

        public bool HasAnyConnectionTo(Node other, bool bridgePerimeterToEndpoint = false)
        {
            if (graph != ((InternalNode) other).graph) return false;

            var connectedNodes = new HashSet<Node>();
            Util.TraverseGraph(this, null, bridgePerimeterToEndpoint, ref connectedNodes);
            return connectedNodes.Contains(other);
        }

        public float StraightDistanceTo(Node other)
        {
            return (other.Position() - position).magnitude;
        }

        public float DistanceTo(Node other)
        {
            var shortestPath = GraphUtil.FindShortestPath(this, other);

            if (shortestPath.edges.Count == 0) return -1;

            return shortestPath.edges.Sum(e => e.Length());
        }

        public bool IsEndpointOrCrossing()
        {
            return type.IsEndpoint() || type.IsCrossing();
        }

        public HashSet<Node> GetPerimeterNodes()
        {
            return new HashSet<Node>(graph.NodesInRange(PositionV2(), radius + 1, new[] {PerimeterType()})
                .Where(n => Equals(n.BelongsTo(), this)).ToList());
        }

        public NodeType Type()
        {
            return type;
        }

        public Vector3 Position()
        {
            return position;
        }

        public Vector2 PositionV2()
        {
            return new Vector2(position.x, position.z);
        }

        public float Radius()
        {
            return radius;
        }

        public void AddData(string key, string value)
        {
            data.Add(key, value);
        }

        public void AddData<T>(string key, T[] value)
        {
            data.Add(key, JsonHelper.ToJson(value));
        }

        public string GetData(string key)
        {
            return data[key];
        }

        public List<T> GetData<T>(string key)
        {
            if (!data.ContainsKey(key)) return null;

            return JsonHelper.FromJson<T>(data[key]).ToList();
        }

        public void RemoveData(string key)
        {
            data.Remove(key);
        }

        public string Guid()
        {
            return guid;
        }

        public Node BelongsTo()
        {
            return belongsTo;
        }

        public List<Connection> Connections()
        {
            lock (connections)
            {
                var inAndOut = new List<Connection>();

                connections.ForEach(c =>
                {
                    inAndOut.AddRange(c.DirectedIncomingTo(this));
                    inAndOut.AddRange(c.DirectedOutgoingFrom(this).Where(dc => !inAndOut.Contains(dc)).ToList());
                });

                return inAndOut;
            }
        }

        public List<Connection> ConnectionsIn()
        {
            lock (connections)
            {
                return connections.SelectMany(c => c.DirectedIncomingTo(this)).ToList();
            }
        }

        public List<Connection> ConnectionsOut()
        {
            lock (connections)
            {
                List<Connection> list = new List<Connection>();
                foreach (Connection c in connections)
                foreach (Connection connection in c.DirectedOutgoingFrom(this))
                    list.Add(connection);
                return list;
            }
        }

        public List<Edge> Edges()
        {
            return edges;
        }

        public void AddEdge(InternalEdge edge)
        {
            if (edge.IsTransient()) return;

            lock (edges)
            {
                if (!edges.Contains(edge)) edges.Add(edge);
            }
        }

        public void AddConnection(InternalConnection connection)
        {
            if (connection.IsTransient()) return;

            lock (connections)
            {
                if (!connections.Contains(connection)) connections.Add(connection);
            }
        }

        public static Vector3 FindValidPosition(Vector3 position, NodeType type, int terrainSize)
        {
            var x = Mathf.RoundToInt(position.x);
            var y = position.y;
            var z = Mathf.RoundToInt(position.z);

            if (type.IsBorder()) return new Vector3(x, y, z);

            if (x % terrainSize == 0) x += 1;
            if (z % terrainSize == 0) z += 1;

            return new Vector3(x, y, z);
        }

        public void SetType(NodeType newType)
        {
            if (type.Equals(newType)) return;

            if (graph != null) ((InternalWorldGraph) graph).RemoveNode(this);

            type = newType;
            if (graph != null) ((InternalWorldGraph) graph).StoreNode(this);
        }

        public NodeType PerimeterType()
        {
            switch (type.BaseType)
            {
                case NodeBaseType.Custom: return NodeType.Of(NodeBaseType.Perimeter);
                case NodeBaseType.Sea:
                case NodeBaseType.Lake: return NodeType.Of(NodeBaseType.RiverPerimeter);
                case NodeBaseType.RiverSection: return NodeType.Of(NodeBaseType.RiverCrossingPerimeter);
                default: return NodeType.Of(NodeBaseType.CrossingPerimeter);
            }
        }

        public bool IsRelevantForRect(Rect rect)
        {
            if (rect.Contains(position.V2())) return true;

            var nodeRectWidth = radius * 2;
            var nodeRect = new Rect(position.x - radius, position.z - radius, nodeRectWidth, nodeRectWidth);
            var currentRect = new Rect(rect.min, rect.size);

            return currentRect.Overlaps(nodeRect);
        }

        public int Seed()
        {
            var seed = 341625275;
            seed *= (int) (Mathf.Abs(position.x) + 377);
            seed *= (int) (Mathf.Abs(position.y) + 377);
            seed *= (int) (Mathf.Abs(position.z) + 377);
            seed *= (int) (radius + 377);
            seed *= ((int) type.BaseType + 377);

            if (type.CustomType != null)
                foreach (var c in type.CustomType.ToCharArray())
                    seed *= c;

            return seed;
        }

        private bool Equals(InternalNode other)
        {
            return guid.Equals(other.guid)
                   && Equals(position, other.position)
                   && Equals(type, other.type);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((InternalNode) obj);
        }

        public override int GetHashCode()
        {
            return Seed();
        }

        public override string ToString()
        {
            return "{" + Util.ShortenGuid(guid) + ", " + position.ToPreciseString() + ", " + type + ", " + radius + ", " +
                   (belongsTo == null ? "" : Util.ShortenGuid(belongsTo.Guid())) + "}";
        }


        public void OnBeforeSerialize()
        {
            if (data != null && data.Count != 0)
            {
                serializedDataKeys = data.Keys.ToArray();
                serializedDataValues = data.Values.Select(k => Convert.ToBase64String(Encoding.UTF8.GetBytes(k))).ToArray();
            }

            if (belongsTo != null) belongsToGuid = belongsTo.Guid();

            nodeBaseType = type.BaseType;
            nodeCustomType = type.CustomType;
        }

        public void OnAfterDeserialize()
        {
            edges = new List<Edge>();
            connections = new List<Connection>();
            data = new Dictionary<string, string>();

            type = NodeType.Of(nodeBaseType, nodeCustomType);
            nodeCustomType = null;

            if (serializedDataKeys != null && serializedDataKeys.Length != 0)
                for (var i = 0; i < serializedDataKeys.Length; i++)
                    data.Add(serializedDataKeys[i], Encoding.UTF8.GetString(Convert.FromBase64String(serializedDataValues[i])));

            // deserialization of references done in WorldGraph
        }

        public void DeserializeReferences(Dictionary<string, InternalNode> nodesByGuid)
        {
            if (belongsToGuid != null && !belongsToGuid.Equals("")) belongsTo = nodesByGuid[belongsToGuid];
        }
    }
}
