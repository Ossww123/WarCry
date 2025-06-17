using System.Collections.Generic;

namespace ArctiumStudios.SplineTools
{
    /// <summary>
    /// A Connection is a composition of multiple <see cref="Edge"/>s that define a connection between two endpoint or crossing <see cref="Node"/>s
    /// (see <see cref="Node.IsEndpointOrCrossing"/>).<br/>
    /// It can be partitioned by <see cref="NodeBaseType.Crossing"/> Nodes when other Connections join at a <see cref="NodeBaseType.Section"/> Node
    /// that is part of this Connection.
    /// </summary>
    public interface Connection
    {
        /// <returns>
        /// The source Node of this Connection.
        /// </returns>
        Node Source();

        /// <returns>
        /// The destination Node of this Connection.
        /// </returns>
        Node Destination();

        /// <summary>
        /// Returns all intermediate crossings within this Connection. Intermediate means that they must not be the 'start' or 'end' Node.
        /// </summary>
        /// <returns>
        /// A list of all intermediate crossing Nodes in order of appearance.
        /// </returns>
        List<Node> IntermediateCrossings();

        /// <summary>
        /// Get the first Edge of this Connection, starting at the start Node.
        /// </summary>
        /// <returns>The first Edge.</returns>
        Edge FirstEdge();

        /// <summary>
        /// Get the last Edge of this Connection, ending at the end Node.
        /// </summary>
        /// <returns>The last Edge.</returns>
        Edge LastEdge();

        /// <summary>
        /// Get the list of all contained Edges, ordered from start to end.
        /// </summary>
        /// <returns>List of Edges.</returns>
        List<Edge> Edges();

        /// <summary>
        /// The list of all contained Edges between two given Nodes.
        /// The Edges are redirected and ordered to represent the requested direction.
        /// </summary>
        /// <param name="source">The start Node for the section.</param>
        /// <param name="destination">The end Node for the section.</param>
        /// <param name="ignoreDirection">If true, the Connection will always be traversed, even if its direction would forbid it.</param>
        /// <returns>
        /// List of Edges. May be empty if the Connection is not allowed to be traversed in the requested direction or the boundary Nodes are not found.
        /// </returns>
        List<Edge> EdgesBetween(Node source, Node destination, bool ignoreDirection = false);

        /// <summary>
        /// Get the list of all contained Nodes, ordered from start to end.
        /// </summary>
        /// <returns>List of Nodes, ordered from start to end.</returns>
        List<Node> Nodes();

        /// <summary>
        /// The list of all contained Nodes between two given Nodes.
        /// The Nodes are ordered to represent the requested direction.
        /// </summary>
        /// <param name="source">The start Node for the section.</param>
        /// <param name="destination">The end Node for the section.</param>
        /// <param name="ignoreDirection">If true, the Connection will always be traversed, even if its direction would forbid it.</param>
        /// <returns>
        /// List of Nodes. May be empty if the Connection is not allowed to be traversed in the requested direction or the boundary Nodes are not found.
        /// </returns>
        List<Node> NodesBetween(Node source, Node destination, bool ignoreDirection = false);

        /// <returns>The maximum width of any Edge in the Connection.</returns>
        float WidthMax();

        /// <param name="source">The start Node of the section. If null, it defaults to <see cref="Source"/></param>
        /// <param name="destination">The end Node of the section. If null, it defaults to <see cref="Destination"/></param>
        /// <returns>The actual total length of the Connection.</returns>
        float Length(Node source = null, Node destination = null);

        /// <summary>
        /// Get the direction of this Connection.
        /// This defines whether the Connection may be traversed from start to end, in reverse, or in any direction.
        /// </summary>
        /// <returns>The direction of this Connection.</returns>
        Directions Direction();

        /// <returns>The type of this Connection.</returns>
        ConnectionType Type();

        /// <returns>Reverse view of this Connection.</returns>
        Connection Reversed();

        /// <summary>
        /// Get all <see cref="Edge"/>s between the provided <see cref="Node"/> and the next Node that <see cref="Node.IsEndpointOrCrossing"/> when
        /// following the Connection.
        /// </summary>
        /// <param name="source">Source Node</param>
        /// <returns>List of Edges.</returns>
        List<Edge> EdgesUntilEndpointOrCrossing(Node source);

        /// <summary>
        /// Get views of this Connection so that traversal is possible starting at the provided <see cref="Node"/>.<br/>
        /// Depending on the <see cref="Direction"/> and the provided Node the returned list may contain 0, 1 or 2 elements.<br/>
        /// Be aware that start and end Nodes of the returned Connections are left unchanged.
        /// </summary>
        /// <param name="source">Source Node.</param>
        /// <returns>List of directed Connections.</returns>
        List<Connection> DirectedOutgoingFrom(Node source);

        /// <summary>
        /// Get views of this Connection so that traversal is possible ending at the provided <see cref="Node"/>.<br/>
        /// Depending on the <see cref="Direction"/> and the provided Node the returned list may contain 0, 1 or 2 elements.<br/>
        /// Be aware that start and end Nodes of the returned Connections are left unchanged.
        /// </summary>
        /// <param name="destination">Destination Node.</param>
        /// <returns>List of directed Connections.</returns>
        List<Connection> DirectedIncomingTo(Node destination);
    }
}