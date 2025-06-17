using System.Collections.Generic;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    /// <summary>
    /// An Edge is the direct connection between exactly two <see cref="Node"/>s.<br/>
    /// </summary>
    public interface Edge
    {
        /// <returns>
        /// The directional vector from the position of the <see cref="Source"/> node to the position of the <see cref="Destination"/> node.
        /// </returns>
        Vector3 V3();

        /// <returns>The slope the Edge in degrees using a straight line from <see cref="Source"/> to <see cref="Destination"/>.</returns>
        float StraightSlopeDegrees();

        /// <returns>List of width, for <see cref="Source"/> and <see cref="Destination"/>.</returns>
        List<float> Widths();

        /// <summary>
        /// Get the next Edge within the same <see cref="Connection"/>. May be null.
        /// </summary>
        /// <param name="skipBorder">If true, Edges with <see cref="Source"/> Node of type <see cref="NodeBaseType.Border"/> will be skipped.</param>
        /// <returns>Next Edge, or null.</returns>
        Edge Next(bool skipBorder = false);

        /// <summary>
        /// Get the previous Edge within the same <see cref="Connection"/>. May be null.
        /// </summary>
        /// <param name="skipBorder">If true, Edges with <see cref="Destination"/> Node of type <see cref="NodeBaseType.Border"/> will be skipped.</param>
        /// <returns>Previous Edge, or null.</returns>
        Edge Previous(bool skipBorder = false);

        /// <returns>List of Nodes in this Edge.</returns>
        List<Node> Nodes();

        /// <returns>Source Node of this Edge.</returns>
        Node Source();

        /// <returns>Destination Node of this Edge.</returns>
        Node Destination();

        /// <returns>The Connection which this Edge belongs to.</returns>
        Connection Connection();

        /// <returns>Length of this Edge's <see cref="BezierCurve"/>.</returns>
        float Length();

        /// <returns>Bezier Curve which belongs to this Edge.</returns>
        BezierCurve BezierCurve();

        /// <summary>
        /// Get the Weight of this Edge. Currently the Weight is the same as the <see cref="Length"/>.
        /// </summary>
        /// <returns>Weight of this Edge.</returns>
        float Weight();
    }
}