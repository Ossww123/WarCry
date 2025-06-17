using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace ArctiumStudios.SplineTools
{
    public abstract class Pathfinder
    {
        public class Options
        {
            // the max slope in degrees 0° - 80°
            public float slopeMax = 45f;

            // minimum allowed height for a connection, most likely above sea level
            public float heightMin = 0f;

            // maximum allowed height for a connection, most likely the max height of the terrain
            public float heightMax = 1000f;

            public int resolution = 1000;

            // angle degrees, 0-180° 
            public float bezierControlPointAngleVariance = 45f;
        }

        public readonly InternalWorldGraph graph;
        public readonly string reference;
        protected readonly Func<Vector2, float> heightFunc;
        protected readonly Options options;
        protected readonly Func<bool> stopFunc;

        protected Pathfinder(InternalWorldGraph graph, string reference, Func<Vector2, float> heightFunc, Options options, Func<bool> stopFunc)
        {
            this.graph = graph;
            this.reference = reference;
            this.heightFunc = heightFunc;
            this.options = options;
            this.stopFunc = stopFunc;
        }

        protected abstract NodeType BorderType();

        public void NewEdgesWithBorder(Node source, Node destination, bool fixFromControlHeight, bool fixToControlHeight,
            Node belongsTo, ConnectionData connectionData, Random rnd)
        {
            var delta = destination.Position() - source.Position();
            var resolution = options.resolution;

            if (!destination.Type().IsBorder() && (int) destination.Position().x % resolution == 0 ||
                (int) destination.Position().z % resolution == 0)
            {
                // change section node that already lie directly on the border
                if (!destination.Type().IsSection())
                    Log.Error(typeof(Pathfinder), () => "Found invalid node " + destination + " placed on chunk border. " +
                                                        "Only Border and Section type allowed here.");
                ((InternalNode) destination).type = BorderType();
            }

            Rect rectFrom;

            if ((int) source.Position().x % resolution == 0)
            {
                rectFrom = delta.x < 0
                    ? Util.GetRectFor(source.Position() + Vector3.left, options.resolution)
                    : Util.GetRectFor(source.Position() + Vector3.right, options.resolution);
            } else if ((int) source.Position().z % resolution == 0)
            {
                rectFrom = delta.z < 0
                    ? Util.GetRectFor(source.Position() + Vector3.back, options.resolution)
                    : Util.GetRectFor(source.Position() + Vector3.forward, options.resolution);
            } else
            {
                rectFrom = Util.GetRectFor(source.Position(), options.resolution);
            }

            var rectTo = Util.GetRectFor(destination.Position(), options.resolution);

            if (!rectFrom.Equals(rectTo))
            {
                var sourcePosition = source.PositionV2();
                var destinationPosition = destination.PositionV2();

                // intersect with border
                var borderIntersection = Util.BorderIntersection(sourcePosition, destinationPosition, rectFrom);

                if (borderIntersection == null)
                {
                    Log.Error(typeof(Pathfinder), () => "no intersection with borders found from  " + source + " to " + destination + ". " +
                                                        "sourcePosition: " + sourcePosition +
                                                        ", destinationPosition: " + destinationPosition +
                                                        ", rectFrom:" + rectFrom +
                                                        ", rectTo: " + rectTo);
                    throw new Exception("no intersection with borders found");
                }

                if (borderIntersection.Value.Equals(sourcePosition))
                {
                    Log.Error(typeof(Pathfinder), () => "loop found with intersection from " + source + " to " + destination);
                    throw new Exception("loop found");
                }

                // interpolate the height for the border node
                var interpolatedVector = (destination.Position() - source.Position()).normalized *
                                         (borderIntersection.Value - sourcePosition).magnitude;

                var progressAtBorder = interpolatedVector.magnitude / (destinationPosition - sourcePosition).magnitude;
                var interpolatedWidth = InterpolatedWidthAtBorder(source, destination, progressAtBorder);

                var height = (source.Position() + interpolatedVector).y;
                var borderIntersectionWithHeight = new Vector3(borderIntersection.Value.x, height, borderIntersection.Value.y);

                var borderNode = new InternalNode(rnd.NextGuid(borderIntersectionWithHeight).ToString(), borderIntersectionWithHeight,
                    interpolatedWidth / 2, BorderType(), belongsTo, options.resolution);

                AddEdge(source, borderNode, fixFromControlHeight, fixToControlHeight, connectionData, rnd);

                NewEdgesWithBorder(borderNode, destination, fixFromControlHeight, fixToControlHeight, belongsTo, connectionData, rnd);
            } else
            {
                AddEdge(source, destination, fixFromControlHeight, fixToControlHeight, connectionData, rnd);
            }
        }

        protected abstract float InterpolatedWidthAtBorder(Node source, Node destination, float progressAtBorder);

        private Edge AddEdge(Node source, Node destination, bool fixFromControlHeight, bool fixToControlHeight, ConnectionData connectionData,
            Random rnd)
        {
            var controlDistance = NextControlDistance(source, destination, rnd);

            var delta = source.Position() - destination.Position();

            var controlEndVector = delta.normalized * controlDistance;
            var controlEnd = connectionData.findControlAction(destination, source, controlDistance, connectionData, fixToControlHeight, rnd);

            var controlEndDirectionVector = controlEnd - destination.Position();

            // adjust the start control point
            if (fixToControlHeight)
            {
                controlEnd = new Vector3(controlEnd.x, destination.Position().y, controlEnd.z);
            } else if (delta.y >= 0 && controlEndDirectionVector.y >= 0 || delta.y < 0 && controlEndDirectionVector.y < 0)
            {
                // adjust the start control point height for the max slope
                controlEnd = destination.Position() + (controlEnd - destination.Position()).RotateForSlope(connectionData.options.slopeMax);
                controlEnd = new Vector3(controlEnd.x, Mathf.Clamp(controlEnd.y, connectionData.options.heightMin,
                    options.heightMax), controlEnd.z);
            } else
            {
                // adjust the height to the interpolated value
                var interpolatedHeight = (destination.Position() + controlEndVector).y;
                controlEnd = new Vector3(controlEnd.x,
                    Mathf.Clamp(interpolatedHeight, connectionData.options.heightMin, options.heightMax), controlEnd.z);
            }

            Vector3 controlStart;

            // find previous 
            if (connectionData.connection.EdgeCount() > 0)
            {
                // use the opposite of the last control point to avoid hard corners
                var previousBezierEnd = (InternalBezierPoint) connectionData.connection.LastEdge().BezierCurve().Destination();
                var controlStartVector = -(previousBezierEnd.ControlPosition() - source.Position()).normalized * controlDistance;

                // check if the angle would be too big
                var angle = Util.SignedAngle((destination.PositionV2() - source.PositionV2()),
                    new Vector2(controlStartVector.x, controlStartVector.z));
                if (Mathf.Abs(angle) > connectionData.options.bezierControlPointAngleVariance)
                {
                    // find the direction and angle to rotate
                    var sign = angle >= 0 ? -1 : 1;
                    var angleToRotate = Mathf.Abs(angle) - connectionData.options.bezierControlPointAngleVariance;

                    // rotate the vector to match the max angle
                    var rotation = Quaternion.AngleAxis(sign * angleToRotate, Vector3.up);
                    controlStartVector = rotation * controlStartVector;

                    // adjust the controlStartVector of the adjacent previous edge
                    var previousControlEndVector = previousBezierEnd.ControlPosition() - previousBezierEnd.Position();
                    previousBezierEnd.controlPosition = (previousBezierEnd.Position() + rotation * previousControlEndVector);
                }

                controlStart = source.Position() + controlStartVector;

                var simpleHeight = controlStart.y;

                var height = fixFromControlHeight
                    ? source.Position().y
                    : simpleHeight;

                controlStart = new Vector3(controlStart.x, height, controlStart.z);

                // adjust the end control point height for the max slope
                controlStart = source.Position() + (controlStart - source.Position()).RotateForSlope(connectionData.options.slopeMax);

                if ((controlStart - source.Position()).AngleToGround() > connectionData.options.slopeMax)
                {
                    controlStart = new Vector3(controlStart.x, simpleHeight, controlStart.z);
                }
            } else
            {
                controlStart = connectionData.findControlAction(source, destination, controlDistance, connectionData, fixToControlHeight, rnd);
            }

            controlStart = new Vector3(controlStart.x,
                Mathf.Clamp(controlStart.y, connectionData.options.heightMin, options.heightMax), controlStart.z);

            var bezier = new InternalBezierCurve(source.Position(), controlStart, destination.Position(), controlEnd);
            var widths = new List<float> {EdgeWidth(source, connectionData.fallbackWidth), EdgeWidth(destination, connectionData.fallbackWidth)};

            return connectionData.connection.AddEdge(source, destination, widths, bezier);
        }

        protected abstract float NextControlDistance(Node source, Node destination, Random rnd);

        private float EdgeWidth(Node node, float fallbackWidth)
        {
            if (node.Type().IsSection() || node.Type().IsBorder() || node.Type().IsPerimeter() || node.Type().IsCrossingPerimeter())
            {
                return node.Radius() * 2;
            }

            return fallbackWidth;
        }
    }
}