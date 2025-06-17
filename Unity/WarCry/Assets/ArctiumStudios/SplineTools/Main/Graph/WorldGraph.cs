using System.Collections.Generic;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    /// <summary>
    /// A WorldGraph contains all <see cref="Node"/>s, <see cref="Connection"/>s and <see cref="Edge"/>s that somehow belong together.<br/>
    /// Multiple instances of this don't interact with each other unless explicitly defined.<br/>
    /// Connections and Edges can only be created for Nodes that belong to the same WorldGraph.<br/>
    /// It is possible to have multiple graphs, which will act independently.
    /// </summary>
    public interface WorldGraph
    {
        /// <summary>
        /// Get all <see cref="Connection"/>s stored in this graph.
        /// </summary>
        /// <returns>Set of Connections.</returns>
        HashSet<Connection> Connections();

        /// <summary>
        /// Get all <see cref="Node"/>s with the provided <see cref="NodeType"/>s stored in this graph.
        /// </summary>
        /// <param name="types"></param>
        /// <returns>Set of Nodes.</returns>
        HashSet<Node> Nodes(NodeType[] types = null);
        
        /// <summary>
        /// Get the <see cref="NodeType"/>s of all <see cref="Node"/>s stored in this graph.
        /// </summary>
        /// <returns>Set of NodeTypes.</returns>
        HashSet<NodeType> NodeTypes();

        /// <summary>
        /// Get the <see cref="NodeType"/>s of all <see cref="Node"/>s stored in this graph that have
        /// <see cref="NodeBaseType"/>.<see cref="NodeBaseType.Custom"/>, which means they were added by the Scatter generators.
        /// </summary>
        /// <returns>Set of NodeTypes.</returns>
        HashSet<NodeType> CustomTypes();

        /// <summary>
        /// Get all <see cref="Node"/>s of the provided <see cref="NodeType"/>s that are in the provided range around the provided position.<br/>
        /// The distance is calculated using a straight line between the <see cref="Vector2"/> coordinates, the height is ignored.<br/>
        /// </summary>
        /// <param name="source">Query position.</param>
        /// <param name="range">Maximum range around <see cref="source"/> to include nodes in the result.</param>
        /// <param name="nodeTypes">Array of NodeTypes for filtering.</param>
        /// <returns>List of Nodes within provided range around the source position, ordered by distance ascending.</returns>
        List<Node> NodesInRange(Vector2 source, float range, NodeType[] nodeTypes);
    }
}