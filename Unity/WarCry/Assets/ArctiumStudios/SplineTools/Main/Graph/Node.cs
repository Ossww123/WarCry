using System.Collections.Generic;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    /// <summary>
    /// A Node is a single POI, ie. some arbitrary position in the world with a special meaning.
    /// </summary>
    public interface Node
    {
        /// <summary>
        /// Check if this Node is reachable from the provided Node by following <see cref="Edge"/>s.
        /// </summary>
        /// <param name="source">Source Node.</param>
        /// <returns>True, if this Node is connected and reachable from the source Node.</returns>
        bool IsReachableFrom(Node source);

        /// <summary>
        /// Check if this Node is connected to the provided Node. This method does not check whether the provided Node is actually reachable from
        /// this Node.
        /// </summary>
        /// <param name="destination">Destination Node.</param>
        /// <param name="bridgePerimeterToEndpoint">
        /// Continue traversal at the endpoint and other Perimeter Nodes,
        /// if a Node of type <see cref="NodeBaseType.Perimeter"/> is not actually connected to its belonging endpoint.
        /// </param>
        /// <returns>True, if this Node has any Connection to the given Node.</returns>
        bool HasAnyConnectionTo(Node destination, bool bridgePerimeterToEndpoint = false);

        /// <param name="destination">Destination Node.</param>
        /// <returns>The straight distance between this Node and the given Node.</returns>
        float StraightDistanceTo(Node destination);

        /// <summary>
        /// Find the shortest distance between this Node and the provided Node when following Connections.<br/>
        /// The destination Node must be reachable from this Node.
        /// </summary>
        /// <param name="destination">Destination Node.</param>
        /// <returns>The shortest distance between this Node and the given Node, or -1 if no path exists between the Nodes.</returns>
        float DistanceTo(Node destination);

        /// <seealso cref="NodeTypeExtensions.EndpointTypes"/>
        /// <seealso cref="NodeTypeExtensions.CrossingTypes"/>
        /// <returns>
        /// True, if this Node is an endpoint or crossing.
        /// </returns>
        bool IsEndpointOrCrossing();

        /// <summary>
        /// Get a list of Nodes that are placed on the perimeter of this Node.<br/>
        /// These mark the border between inside the Node's area and outside.
        /// </summary>
        /// <returns>Set of perimeter Nodes, may be empty but never null.</returns>
        HashSet<Node> GetPerimeterNodes();

        /// <returns>The position of the Node.</returns>
        Vector3 Position();

        /// <returns>The position of the Node without height.</returns>
        Vector2 PositionV2();

        /// <summary>
        /// Get the radius of the Node. It defines the area that belongs to the Node.
        /// </summary>
        /// <returns>The radius of the Node.</returns>
        float Radius();

        /// <returns>The type of the Node.</returns>
        NodeType Type();

        /// <summary>
        /// Add arbitrary data to the Node that will be stored alongside it.
        /// </summary>
        /// <param name="key">The key under which the data is stored.</param>
        /// <param name="value">A simple string value.</param>
        void AddData(string key, string value);

        /// <summary>
        /// Add arbitrary data to the Node that will be stored alongside it.
        /// Values must be serializable.
        /// </summary>
        /// <param name="key">The key under which the data is stored.</param>
        /// <param name="value">The array of serializable values.</param>
        void AddData<T>(string key, T[] value);

        /// <summary>
        /// Get data that is stored alongside the Node.
        /// </summary>
        /// <param name="key">The key under which the data is stored.</param>
        /// <returns>The stored data, or null if no entry was found for the given key.</returns>
        string GetData(string key);

        /// <summary>
        /// Get data that is stored alongside the Node.
        /// </summary>
        /// <param name="key">The key under which the data is stored.</param>
        /// <returns>The stored array of data or null, if no entry was found for the given key.</returns>
        List<T> GetData<T>(string key);

        /// <summary>
        /// Remove data that is stored alongside the Node.
        /// </summary>
        /// <param name="key">The key under which the data is stored.</param>
        void RemoveData(string key);

        /// <returns>The unique identifier of the Node.</returns>
        string Guid();

        /// <summary>
        /// Get the other Node that this Node belongs to.
        /// A Node may belong to another Node if it was scattered around it within the radius or it is the perimeter Node right at the border.
        /// </summary>
        /// <returns>The other Node that this Node belongs to or null.</returns>
        Node BelongsTo();

        /// <summary>
        /// Get all incoming and outgoing Connections.
        /// </summary>
        /// <returns>List of directed Connections.</returns>
        List<Connection> Connections();

        /// <summary>
        /// Get all incoming Connections.
        /// </summary>
        /// <returns>List of directed Connections.</returns>
        List<Connection> ConnectionsIn();

        /// <summary>
        /// Get all outgoing Connections.
        /// </summary>
        /// <returns>List of directed Connections.</returns>
        List<Connection> ConnectionsOut();

        /// <returns>List of all Edges in this Connection.</returns>
        List<Edge> Edges();
    }
}
