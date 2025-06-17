using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [ExecuteInEditMode]
    public class SplineTools : MonoBehaviour
    {
        [NonSerialized] public const string Version = "3.2.2";

        private static SplineTools _editorInstance = null;
#if ST_MM_1 || ST_MM_2
        [HideInInspector] [SerializeField] public SplineToolsInstance instance;
#else
        [HideInInspector] [SerializeField] public AbstractSplineToolsInstance instance;
#endif

        public static AbstractSplineToolsInstance Instance
        {
            get
            {
#if ST_MM_1 || ST_MM_2
                if (EditorInstance.instance == null) EditorInstance.instance = new SplineToolsInstance();
#endif
                return EditorInstance.instance;
            }
        }

        public static SplineTools EditorInstance
        {
            get
            {
                if (_editorInstance == null)
                {
                    _editorInstance = FindPrimaryEditorInstance();

                    if (_editorInstance == null)
                    {
                        throw new Exception("Can't find instance of SplineTools main script. Make sure it is attached.");
                    }
                }

                return _editorInstance;
            }
            set { _editorInstance = value; }
        }

        public void Awake()
        {
            if (this != EditorInstance) return;

            Instance.persistentDataPath = Application.persistentDataPath;
            Instance.dataPath = Application.dataPath;
#if ST_MM_2
            ((SplineToolsInstance) Instance).MapMagicInstance = FindObjectOfType<MapMagic.Core.MapMagicObject>();
#endif
        }

        public void OnEnable()
        {
            if (this != EditorInstance) return;

            //finding singleton instance
            EditorInstance = FindPrimaryEditorInstance();
            Instance.OnEnable();
        }

        private void OnDisable()
        {
            if (this != EditorInstance) return;

            Instance.OnDisable();
        }

        private void OnDrawGizmos()
        {
            if (this != EditorInstance) return;

#if UNITY_EDITOR
            Instance.OnDrawGizmos();
#endif
        }

        public static LogLevel LogLevel()
        {
            if (_editorInstance == null || _editorInstance.instance == null) return ArctiumStudios.SplineTools.LogLevel.Error;

            return Instance.logLevel;
        }

        private static SplineTools FindPrimaryEditorInstance()
        {
            var instances = FindObjectsOfType<SplineTools>();

            if (instances.Length > 1)
            {
                Log.Warn(typeof(SplineTools), () => "More than one SplineTools instance found. This is discouraged and may lead to undesired " +
                                                    "behaviour. If you use MapMagic's 'Copy Components To Terrain' option, consider moving the " +
                                                    "SplineTools script to a dedicated GameObject.");
            }

#if ST_MM_1
            if (instances.Any(st => st.GetComponent<MapMagic.MapMagic>() != null))
                return instances.First(st => st.GetComponent<MapMagic.MapMagic>() != null);
#elif ST_MM_2
            if (instances.Any(st => st.GetComponent<MapMagic.Core.MapMagicObject>() != null))
                return instances.First(st => st.GetComponent<MapMagic.Core.MapMagicObject>() != null);
#endif


            if (instances.Any(st => st.GetComponent<Terrain>() == null))
                return instances.First(st => st.GetComponent<Terrain>() == null);

            return instances.Length == 0 ? null : instances[0];
        }

        public static bool HasEditorInstance()
        {
            return _editorInstance != null;
        }

        // ########################################
        // ################# API ##################
        // ########################################

        /// <summary>
        /// Let MapMagic generate the <see cref="WorldGraph"/>s.<br/>
        /// Use this instead of <see cref="MapMagic.MapMagic.Generate"/> when generating the first Chunk.<br/>
        /// Once the <see cref="WorldGraph"/>s are fully generated, it is safe to let MapMagic generate new Chunks automatically.
        /// </summary>
        public static void Generate()
        {
            Instance.Generate();
        }

        /// <summary>
        /// Reset all state and caches.
        /// </summary>
        public static void ResetState()
        {
            Instance.GlobalReset(true);
        }

        /// <summary>
        /// Shortcut for <see cref="ResetState"/> and <see cref="Generate"/>.
        /// </summary>
        public static void Regenerate()
        {
            ResetState();
            Generate();
        }

        /// <summary>
        /// Find all <see cref="WorldGraph"/>s that contain nodes of the provided <see cref="NodeType"/>.
        /// </summary>
        /// <param name="nodeType">NodeType for filtering.</param>
        /// <returns>Set of WorldGraphs.</returns>
        public static HashSet<WorldGraph> FindWorldGraphsForNodeType(NodeType nodeType)
        {
            return new HashSet<WorldGraph>(Instance.state.GraphForType(nodeType));
        }

        /// <summary>
        /// Find all <see cref="Node"/>s of the provided <see cref="NodeType"/> within all existing <see cref="WorldGraph"/>s.
        /// </summary>
        /// <param name="nodeType">NodeType for filtering.</param>
        /// <returns>Set of Nodes with requested NodeType.</returns>
        public static HashSet<Node> FindNodes(NodeType nodeType)
        {
            return new HashSet<Node>(GetWorldGraphs(nodeType).SelectMany(g => g.Nodes(new[] { nodeType })).ToArray());
        }

        /// <summary>
        /// Find all <see cref="Node"/>s of the <see cref="NodeType"/> with <see cref="NodeBaseType.Custom"/> and the provided
        /// <see cref="NodeType.CustomType"/> within all existing <see cref="WorldGraph"/>s.<br/>
        /// </summary>
        /// <seealso cref="FindNodes(ArctiumStudios.SplineTools.NodeType)"/>
        /// <param name="customType">Custom type for filtering.</param>
        /// <returns>Set of Nodes with requested NodeType.</returns>
        public static HashSet<Node> FindNodes(string customType)
        {
            return FindNodes(NodeType.Of(NodeBaseType.Custom, customType));
        }

        /// <summary>
        /// Find all <see cref="Node"/>s of the provided <see cref="NodeType"/> within all existing <see cref="WorldGraph"/>s that are in the
        /// provided range around the provided position.<br/>
        /// The distance is calculated using a straight line between the <see cref="Vector2"/> coordinates, the height is ignored.<br/>
        /// Depending on the setup, the nodes may be unconnected or belong to different graphs. 
        /// </summary>
        /// <param name="source">Query position.</param>
        /// <param name="range">Maximum range around <see cref="source"/> to include nodes in the result.</param>
        /// <param name="nodeType">(Optional) NodeType for filtering.</param>
        /// <returns>List of Nodes within provided range around the source position, ordered by distance ascending.</returns>
        public static List<Node> FindNodesInRange(Vector2 source, float range, NodeType nodeType = null)
        {
            return GetWorldGraphs(nodeType).SelectMany(g =>
            {
                var nodeTypes = nodeType == null ? ((InternalWorldGraph) g).NodeTypes().ToArray() : new[] { nodeType };
                return g.NodesInRange(source, range, nodeTypes);
            }).ToList();
        }

        /// <summary>
        /// Find all <see cref="Node"/>s of the <see cref="NodeType"/> with <see cref="NodeBaseType.Custom"/> and the provided
        /// <see cref="NodeType.CustomType"/> within all existing <see cref="WorldGraph"/>s
        /// that are in the provided range around the provided position.<br/>
        /// The distance is calculated using a straight line between the <see cref="Vector2"/> coordinates, the height is ignored.<br/>
        /// Depending on the setup, the nodes may be unconnected or belong to different graphs. 
        /// </summary>
        /// <param name="source">Query position.</param>
        /// <param name="range">Maximum range around <see cref="source"/> to include nodes in the result.</param>
        /// <param name="customType">Custom type for filtering.</param>
        /// <returns>List of Nodes within provided range around the source position, ordered by distance ascending.</returns>
        public static List<Node> FindNodesInRange(Vector2 source, float range, string customType)
        {
            return FindNodesInRange(source, range, NodeType.Of(NodeBaseType.Custom, customType));
        }

        /// <summary>
        /// Find all <see cref="Node"/>s that are reachable via <see cref="Connection"/>s from the provided source Node.<br/>
        /// Optionally the maximum <see cref="Edge.Weight"/> can be set to limit the search for reachable Nodes.
        /// </summary>
        /// <param name="source">Source Node.</param>
        /// <param name="maxWeight">
        /// (Optional) Maximum <see cref="Edge.Weight"/> until traversal is aborted.
        /// If omitted, the graph will be traversed until <b>all</b> reachable Nodes are found
        /// </param>
        /// <param name="nodeType">(Optional) NodeType for filtering.</param>
        /// <returns>Set of reachable Nodes.</returns>
        public static HashSet<Node> FindReachableNodes(Node source, float maxWeight = float.MaxValue, NodeType nodeType = null)
        {
            return GraphUtil.FindReachableNodes(source, maxWeight, nodeType);
        }

        /// <summary>
        /// Find all <see cref="Node"/>s that are reachable via <see cref="Connection"/>s from the provided source Node.<br/>
        /// Optionally the maximum number of hops can be set to limit the search for reachable Nodes.<br/>
        /// A hop is counted for every endpoint or crossing (see <see cref="Node.IsEndpointOrCrossing"/>).
        /// The source node does not count as hop.
        /// </summary>
        /// <param name="source">Source Node.</param>
        /// <param name="maxHops">
        /// (Optional) Maximum number of hops until traversal is aborted.
        /// If omitted, the graph will be traversed until <b>all</b> reachable Nodes are found
        /// </param>
        /// <param name="nodeType">(Optional) NodeType for filtering.</param>
        /// <returns>Set of reachable Nodes.</returns>
        public static HashSet<Node> FindReachableNodesByHops(Node source, int maxHops = int.MaxValue, NodeType nodeType = null)
        {
            return GraphUtil.FindReachableNodesByHops(source, maxHops, nodeType);
        }

        /// <summary>
        /// Find the shortest path between two arbitrary <see cref="Node"/>s by <see cref="Edge.Weight"/>.<br/>
        /// The returned <see cref="Edge"/>s will always be directed towards the destination.<br/>
        /// If the destination Node is not reachable from the source Node, or both belong to different <see cref="WorldGraph"/>s,
        /// an empty List is returned.
        /// </summary>
        /// <param name="source">Source Node.</param>
        /// <param name="destination">Destination Node.</param>
        /// <returns>List of directed Edges.</returns>
        public static List<Edge> FindShortestPath(Node source, Node destination)
        {
            return GraphUtil.FindShortestPath(source, destination).edges;
        }

        /// <summary>
        /// Find the shortest path between two endpoint or crossing <see cref="Node"/>s by hops (see <see cref="Node.IsEndpointOrCrossing"/>).<br/>
        /// A hop is counted for every endpoint or crossing (see <see cref="Node.IsEndpointOrCrossing"/>).
        /// The source node does not count as hop.<br/>
        /// The returned <see cref="Edge"/>s will always be directed towards the destination.<br/>
        /// If the destination Node is not reachable from the source Node, or both belong to different <see cref="WorldGraph"/>s,
        /// an empty List is returned.
        /// </summary>
        /// <param name="source">Source Node.</param>
        /// <param name="destination">Destination Node.</param>
        /// <exception cref="ArgumentException">If the source or destination Node is not an endpoint or crossing.</exception>
        /// <returns>List of directed Edges.</returns>
        public static List<Edge> FindShortestPathByHops(Node source, Node destination)
        {
            return GraphUtil.FindShortestPathByHops(source, destination, false).edges;
        }

        /// <summary>
        /// Get waypoints along the given <see cref="Edge"/>s. The source and destination positions are included.
        /// </summary>
        /// <param name="edges">List of Edges.</param>
        /// <param name="stepLength">Distance between two adjacent waypoints, measured along the actual <see cref="BezierCurve"/> curve.</param>
        /// <param name="offset">(Optional) Absolute offset in direction of the normal of the edge in the position of the respective waypoint.</param>
        /// <param name="side">(Optional) Side to which the offset should be applied.</param>
        /// <returns>List of waypoints.</returns>
        public static List<Waypoint> GetWaypoints(List<Edge> edges, float stepLength, float offset = 0f, Side side = Side.Left)
        {
            return GraphUtil.GetWaypoints(edges, stepLength, offset, side);
        }

        // #############################
        // ######### Helpers ###########
        // #############################

        private static HashSet<WorldGraph> GetWorldGraphs(NodeType nodeType)
        {
            return nodeType == null
                ? new HashSet<WorldGraph>(Instance.state.graphsById.Values.ToArray())
                : FindWorldGraphsForNodeType(nodeType);
        }
    }
}
