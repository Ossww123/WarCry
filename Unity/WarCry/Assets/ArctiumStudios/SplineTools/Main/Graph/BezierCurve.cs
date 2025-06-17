using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    /// <summary>
    /// A Bezier Curve is a parametric curve that defines the exact path between two points.<br/>
    /// For more general information about Bezier Curves, check the <a href="https://en.wikipedia.org/wiki/B%C3%A9zier_curve">Wikipedia article</a>.
    /// </summary>
    public interface BezierCurve
    {
        /// <returns>
        /// The <see cref="BezierPoint"/> that defines the start of this bezier curve.
        /// </returns>
        BezierPoint Source();

        /// <returns>
        /// The <see cref="BezierPoint"/> that defines the end of this bezier curve.
        /// </returns>
        BezierPoint Destination();

        /// <param name="tStart">The t value (between 0 and 1) that marks the beginning of the section.</param>
        /// <param name="tEnd">The t value (between 0 and 1) that marks the end of the section. Must be greater than <see cref="tStart"/></param>
        /// <returns>
        /// The actual length of the curve within the given bounds.
        /// </returns>
        float Length(float tStart = 0f, float tEnd = 1f);

        /// <returns>
        /// The directional vector from the position of the start point to the position of the end point.
        /// </returns>
        Vector3 V3();

        /// <summary>
        /// Get the interpolated position at the requested progress.
        /// </summary>
        /// <param name="t">Progress of the curve. Must be between 0 and 1.</param>
        /// <returns>Interpolated position.</returns>
        Vector3 InterpolatedPosition(float t);
    }
}