using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    /// <summary>
    /// A Bezier Point is a container for the endpoint and the control position of either end  of a <see cref="BezierCurve"/>. 
    /// </summary>
    public interface BezierPoint
    {
        /// <returns>
        /// The position of the point.
        /// </returns>
        Vector3 Position();

        /// <returns>
        /// The position of the control point.
        /// </returns>
        Vector3 ControlPosition();

        /// <returns>
        /// The directional vector from the position of the point to the position of the control point.
        /// </returns>
        Vector3 ControlV3();
    }
}