using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    public class InternalWorldGraph : WorldGraph, ISerializationCallbackReceiver
    {
        [Serializable]
        public class ConnectionEntry
        {
            public string key;
            public InternalConnection[] value;

            public ConnectionEntry(string key, InternalConnection[] value)
            {
                this.key = key;
                this.value = value;
            }
        }

        public const int OffsetResolution = 64;

        [SerializeField] public string guid;

        [NonSerialized] private Dictionary<string, EdgesByOffset> connectionsByReferenceAndOffset =
            new Dictionary<string, EdgesByOffset>();

        [NonSerialized] private HashSet<string> processedGraphComponents = new HashSet<string>();

        [NonSerialized] private Dictionary<Offset, Dictionary<NodeType, List<Node>>> nodesByOffsetAndType =
            new Dictionary<Offset, Dictionary<NodeType, List<Node>>>();

        [NonSerialized] private HashSet<string> nodeGuids = new HashSet<string>();
        [NonSerialized] private HashSet<Vector3> nodePositions = new HashSet<Vector3>();

        // de-/serialization helpers
        [SerializeField] private ConnectionEntry[] serializedConnections;
        [SerializeField] private string[] serializedProcessedGraphComponents;
        [SerializeField] private InternalNode[] serializedNodes;

        public InternalWorldGraph(string guid)
        {
            this.guid = guid;
        }

        public void StoreNode(Node node)
        {
            Log.Debug(this, () => guid + ": Add Node " + node + " at " + Offset.For(node.Position(), OffsetResolution));
            StoreNodeInternal(node);
        }

        public void StoreConnection(Connection connection, string reference, bool storeNodes = true)
        {
            if (((InternalConnection) connection).EdgeCount() == 0) Log.Warn(this, () => "Adding connection without any edges: " + connection);

            var optimizedConnection = ((InternalConnection) connection).Optimized();
            Log.Debug(this, () => guid + ": Add Connection " + optimizedConnection);
            StoreConnectionInternal(optimizedConnection, reference, storeNodes);
        }

        public HashSet<Connection> Connections()
        {
            lock (connectionsByReferenceAndOffset)
            {
                if (connectionsByReferenceAndOffset == null || connectionsByReferenceAndOffset.Count == 0) return new HashSet<Connection>();

                var connections = new HashSet<Connection>();
                foreach (var byReference in connectionsByReferenceAndOffset)
                foreach (var byOffset in byReference.Value)
                foreach (var connection in byOffset.Value)
                    if (!connections.Contains(connection))
                        connections.Add(connection);

                return connections;
            }
        }

        public HashSet<NodeType> CustomTypes()
        {
            return new HashSet<NodeType>(NodeTypes().Where(t => t.BaseType == NodeBaseType.Custom).ToArray());
        }

        public HashSet<NodeType> NodeTypes()
        {
            lock (nodesByOffsetAndType)
            {
                return new HashSet<NodeType>(nodesByOffsetAndType.Values.SelectMany(e => e.Keys).ToArray());
            }
        }

        public int NodeCount()
        {
            var count = 0;

            lock (nodesByOffsetAndType)
            {
                foreach (var pair in nodesByOffsetAndType)
                foreach (var nodes in pair.Value.Values)
                    count += nodes.Count;
            }

            return count;
        }

        public int CustomTypeNodeCount()
        {
            var count = 0;
            lock (nodesByOffsetAndType)
            {
                foreach (var pair in nodesByOffsetAndType)
                foreach (var customType in CustomTypes())
                    if (pair.Value.ContainsKey(customType))
                        count += pair.Value[customType].Count;
            }

            return count;
        }

        public List<Node> NodesInRange(Vector2 p, float range, NodeType[] types)
        {
            lock (nodesByOffsetAndType)
            {
                var offsets = Offsets.ForRange(p, range, OffsetResolution);

                var nodesInRange = new List<Node>();

                foreach (var offset in offsets)
                {
                    Dictionary<NodeType, List<Node>> nodesByType;
                    nodesByOffsetAndType.TryGetValue(offset, out nodesByType);
                    if (nodesByType == null) continue;

                    foreach (var nodeType in types)
                    {
                        List<Node> nodes;
                        nodesByType.TryGetValue(nodeType, out nodes);
                        if (nodes == null) continue;

                        var currentInRange = nodes
                            .Where(n => (n.PositionV2() - p).magnitude <= range)
                            .ToList();

                        nodesInRange.AddRange(currentInRange);
                    }
                }

                return nodesInRange.OrderBy(n => (n.PositionV2() - p).magnitude).ToList();
            }
        }

        public int EdgeCount()
        {
            return Connections().Sum(c => ((InternalConnection) c).EdgeCount());
        }

        public int ConnectionCount()
        {
            return Connections().Count;
        }

        public HashSet<Node> Nodes(NodeType[] types = null)
        {
            return Nodes(types, null);
        }

        public HashSet<Node> Nodes(NodeType[] types, HashSet<Offset> offsets)
        {
            lock (nodesByOffsetAndType)
            {
                var collectedNodes = new HashSet<Node>();
                if (types == null) types = NodeTypes().ToArray();

                var byOffsetAndType = offsets == null
                    ? nodesByOffsetAndType
                    : NodesByOffsetAndTypeFiltered(offsets);

                foreach (var nodesByOffset in byOffsetAndType) AddNodesForTypes(nodesByOffset.Value, types, collectedNodes);

                return collectedNodes;
            }
        }

        private Dictionary<Offset, Dictionary<NodeType, List<Node>>> NodesByOffsetAndTypeFiltered(HashSet<Offset> offsets)
        {
            var filtered = new Dictionary<Offset, Dictionary<NodeType, List<Node>>>();

            foreach (var offset in offsets)
                if (nodesByOffsetAndType.ContainsKey(offset))
                    filtered.Add(offset, nodesByOffsetAndType[offset]);

            return filtered;
        }

        private void StoreNodeInternal(Node node)
        {
            lock (nodesByOffsetAndType)
            {
                if (!NodeCanBePersisted(node))
                {
                    var existingNode = Nodes().First(n => n.Guid().Equals(node.Guid()) || n.Position().Equals(node.Position()));
                    Log.Error(this, () => "Node " + node + " collides with existing node " + existingNode);
//                    throw new Exception("Node " + node + " collides with existing node " + existingNode);
                }

                ((InternalNode) node).graph = this;
                nodeGuids.Add(node.Guid());
                nodePositions.Add(node.Position());

                var offset = Offset.For(node.Position(), OffsetResolution);
                if (!nodesByOffsetAndType.ContainsKey(offset)) nodesByOffsetAndType.Add(offset, new Dictionary<NodeType, List<Node>>());
                var nodesByType = nodesByOffsetAndType[offset];
                if (!nodesByType.ContainsKey(node.Type())) nodesByType.Add(node.Type(), new List<Node>());
                var nodeList = nodesByType[node.Type()];

                if (nodeList.Contains(node))
                    throw new Exception("Node " + node + " collides with existing node " + nodeList.First(n => n.Equals(node)));

                nodeList.Add(node);
            }
        }

        public bool RemoveNode(Node node)
        {
            lock (nodesByOffsetAndType)
            {
                var offset = Offset.For(node.Position(), OffsetResolution);
                if (!nodesByOffsetAndType.ContainsKey(offset)) return false;
                var nodesByType = nodesByOffsetAndType[offset];
                if (!nodesByType.ContainsKey(node.Type())) return false;
                var nodeList = nodesByType[node.Type()];

                if (nodeList.Contains(node))
                {
                    Log.Debug(this, () => "Remove node " + node);
                    nodeList.Remove(node);
                    nodeGuids.Remove(node.Guid());
                    nodePositions.Remove(node.Position());

                    // clean up empty containers
                    if (nodeList.Count == 0)
                    {
                        nodesByType.Remove(node.Type());
                        if (nodesByType.Count == 0)
                        {
                            nodesByOffsetAndType.Remove(offset);
                        }
                    }

                    return true;
                }

                return false;
            }
        }

        public bool NodeCanBePersisted(Node node)
        {
            return !nodeGuids.Contains(node.Guid()) && PositionIsFree(node.Position());
        }

        public bool PositionIsFree(Vector3 position)
        {
            return !nodePositions.Contains(position);
        }

        private bool NodeIsStored(Node node)
        {
            lock (nodesByOffsetAndType)
            {
                var offset = Offset.For(node.Position(), OffsetResolution);

                if (!nodesByOffsetAndType.ContainsKey(offset)) return false;

                var nodesByType = nodesByOffsetAndType[offset];
                if (!nodesByType.ContainsKey(node.Type())) return false;

                var nodeList = nodesByType[node.Type()];

                return nodeList.Contains(node);
            }
        }

        private void StoreConnectionInternal(Connection connection, string reference, bool storeNodes)
        {
            lock (connectionsByReferenceAndOffset)
            {
                var offsets = new HashSet<Offset>();

                foreach (var node in connection.Nodes())
                {
                    if (storeNodes && !NodeIsStored(node)) StoreNode(node);

                    var offset = Offset.For(node.Position(), OffsetResolution);
                    if (!offsets.Contains(offset)) offsets.Add(offset);
                }

                // add edges to nodes, if not already there
                foreach (var edge in connection.Edges())
                foreach (var node in edge.Nodes())
                {
                    ((InternalNode) node).AddEdge((InternalEdge) edge);
                    ((InternalNode) node).AddConnection((InternalConnection) connection);
                }

                if (!connectionsByReferenceAndOffset.ContainsKey(reference))
                    connectionsByReferenceAndOffset.Add(reference, new EdgesByOffset());

                foreach (var offset in offsets) StoreConnectionByOffset(connection, connectionsByReferenceAndOffset[reference], offset);
            }
        }

        public EdgesByOffset ConnectionsByOffset(string reference)
        {
            lock (connectionsByReferenceAndOffset)
            {
                return connectionsByReferenceAndOffset.ContainsKey(reference)
                    ? new EdgesByOffset(connectionsByReferenceAndOffset[reference])
                    : new EdgesByOffset();
            }
        }

        public List<Connection> Connections(Offset offset)
        {
            if (connectionsByReferenceAndOffset == null || connectionsByReferenceAndOffset.Count == 0) return new List<Connection>();

            var connections = new HashSet<Connection>();
            foreach (var byReference in connectionsByReferenceAndOffset)
            {
                if (byReference.Value.ContainsKey(offset))
                {
                    foreach (var connection in byReference.Value[offset])
                        if (!connections.Contains(connection))
                            connections.Add(connection);
                }
            }

            return connections.ToList();
        }

        public bool ProcessedGraphComponentsContains(string item)
        {
            lock (processedGraphComponents)
            {
                return processedGraphComponents.Contains(item);
            }
        }

        public void ProcessedGraphComponentsAdd(string item)
        {
            lock (processedGraphComponents)
            {
                processedGraphComponents.Add(item);
            }
        }

        private static void StoreConnectionByOffset(Connection connection, EdgesByOffset byOffset, Offset offset)
        {
            if (!byOffset.ContainsKey(offset)) byOffset.Add(offset, new List<Connection>());
            var connections = byOffset[offset];

            if (connections.Contains(connection))
            {
                throw new Exception("Connection " + connection + " collides with existing connection " +
                                    connections.First(c => c.Equals(connection)));
            }

            connections.Add(connection);
        }

        public HashSet<Node> NodesInRect(Rect rect, NodeType[] types = null)
        {
            var offsets = Offsets.ForRect(rect, OffsetResolution);
            var nodes = NodesForOffsets(offsets, types).Where(n => rect.Contains(n.PositionV2())).ToList();

            return new HashSet<Node>(nodes);
        }

        public NodesByOffset NodeByOffset()
        {
            lock (nodesByOffsetAndType)
            {
                return new NodesByOffset(nodesByOffsetAndType.ToDictionary(
                    e => e.Key,
                    e => e.Value.SelectMany(pair => pair.Value).ToList()
                    ));
            }
        }

        public NodesByOffset NodeByOffset(NodeType nodeType)
        {
            lock (nodesByOffsetAndType)
            {
                return new NodesByOffset(nodesByOffsetAndType.ToDictionary(
                    e => e.Key,
                    e => e.Value.ContainsKey(nodeType) ? e.Value[nodeType] : new List<Node>()));
            }
        }

        private HashSet<Node> NodesForOffsets(HashSet<Offset> offsets, NodeType[] types = null)
        {
            lock (nodesByOffsetAndType)
            {
                var collectedNodes = new HashSet<Node>();

                if (types == null) types = NodeTypes().ToArray();

                foreach (var offset in offsets)
                {
                    if (!nodesByOffsetAndType.ContainsKey(offset)) continue;
                    AddNodesForTypes(nodesByOffsetAndType[offset], types, collectedNodes);
                }

                return collectedNodes;
            }
        }

        private static void AddNodesForTypes(Dictionary<NodeType, List<Node>> nodesByType, NodeType[] types, HashSet<Node> collectedNodes)
        {
            foreach (var nodeType in types)
            {
                if (!nodesByType.ContainsKey(nodeType)) continue;
                nodesByType[nodeType].ForEach(n => collectedNodes.Add(n));
            }
        }

        public void OnBeforeSerialize()
        {
            // save connections
            lock (connectionsByReferenceAndOffset)
            {
                if (connectionsByReferenceAndOffset != null && connectionsByReferenceAndOffset.Count > 0)
                {
                    serializedConnections = new ConnectionEntry[connectionsByReferenceAndOffset.Count];

                    var idx = 0;
                    foreach (var byReference in connectionsByReferenceAndOffset)
                    {
                        serializedConnections[idx] =
                            new ConnectionEntry(byReference.Key, byReference.Value
                                .SelectMany(e => e.Value.Select(c => (InternalConnection) c)).Distinct().ToArray());
                        idx++;
                    }
                }
            }

            // save processedGraphComponents
            lock (processedGraphComponents)
            {
                if (processedGraphComponents != null && processedGraphComponents.Count > 0)
                    serializedProcessedGraphComponents = processedGraphComponents.ToArray();
            }

            // save nodes
            lock (nodesByOffsetAndType)
            {
                if (nodesByOffsetAndType != null && nodesByOffsetAndType.Count > 0)
                    serializedNodes = Nodes().Select(n => (InternalNode) n).ToArray();
            }
        }

        public void OnAfterDeserialize()
        {
            // init fields
            nodesByOffsetAndType = new Dictionary<Offset, Dictionary<NodeType, List<Node>>>();
            connectionsByReferenceAndOffset = new Dictionary<string, EdgesByOffset>();
            processedGraphComponents = new HashSet<string>();
            nodeGuids = new HashSet<string>();
            nodePositions = new HashSet<Vector3>();

            if (serializedConnections == null || serializedNodes == null || serializedProcessedGraphComponents == null) return;

            // read nodes & set references
            foreach (var node in serializedNodes) StoreNodeInternal(node);

            var nodesByGuid = serializedNodes.ToDictionary(n => n.Guid());

            // set node's belongsTo
            foreach (var node in serializedNodes) node.DeserializeReferences(nodesByGuid);

            // read connections
            foreach (var connectionEntry in serializedConnections)
            foreach (var connection in connectionEntry.value)
            {
                connection.DeserializeEdges(nodesByGuid);
                StoreConnectionInternal(connection, connectionEntry.key, false);
            }

            // read processedGraphComponents
            foreach (var s in serializedProcessedGraphComponents) processedGraphComponents.Add(s);

            // clear serialization helpers
            serializedConnections = null;
            serializedProcessedGraphComponents = null;
            serializedNodes = null;
        }
    }
}
