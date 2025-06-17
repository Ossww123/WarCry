using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public static class Util
    {
        /**
         * Get the distance from a point to a line.
         * Calculated using the cross product.
         */
        public static float Distance(Vector3 from, Vector3 to, Vector3 point)
        {
            var onLine = ClosestPointOnLine(from, to, point);
            return (onLine - point).magnitude;
        }

        public static Vector3 ClosestPointOnLine(Vector3 from, Vector3 to, Vector3 point)
        {
            var lineDir = (to - from).normalized; //this needs to be a unit vector
            var v = point - from;
            var d = Vector3.Dot(v, lineDir);
            var onLine = from + lineDir * d;
            return onLine;
        }

        public static int OffsetValue(float value, int resolution)
        {
            return (int) Mathf.Floor(value / resolution) * resolution;
        }

        /**
         * Intersect two finite lines.
         * Always check that this method is not accidentally called with Vector3.
         */
        public static Vector2? Intersect(Vector2 from1, Vector2 to1, Vector2 from2, Vector2 to2, float padding = -1f)
        {
            //Line1
            var a1 = to1.y - from1.y;
            var b1 = from1.x - to1.x;
            var c1 = a1 * from1.x + b1 * from1.y;

            //Line2
            var a2 = to2.y - from2.y;
            var b2 = from2.x - to2.x;
            var c2 = a2 * from2.x + b2 * from2.y;

            var det = a1 * b2 - a2 * b1;

            if (det == 0) return null; //parallel lines

            var x = (b2 * c1 - b1 * c2) / det;
            var y = (a1 * c2 - a2 * c1) / det;

            if (x < from1.x && x < to1.x || x > from1.x && x > to1.x || y < from1.y && y < to1.y || y > from1.y && y > to1.y
                || x < from2.x && x < to2.x || x > from2.x && x > to2.x || y < from2.y && y < to2.y || y > from2.y && y > to2.y)
                return null;

            var match = new Vector2(x, y);

            if (padding > 0f && ((from1 - match).magnitude < padding
                                 || (from2 - match).magnitude < padding
                                 || (to1 - match).magnitude < padding
                                 || (to2 - match).magnitude < padding)) return null;

            return match;
        }

        /**
        * Recursively traverse the graph and store all visited nodes.
        */
        public static void TraverseGraph(Node current, Node previous, bool bridgePerimeterToEndpoint,
            ref HashSet<Node> connectedNodes, Func<Node, Node, bool> stopCondition = null)
        {
            if (current == null || connectedNodes.Contains(current)) return;
            connectedNodes.Add(current);

            foreach (var edge in current.Edges())
            {
                if (previous == null || !edge.Nodes().Contains(previous)) // don't go backwards
                {
                    var next = edge.Nodes().Where(n => !n.Equals(current)).ToList().First();
                    if (stopCondition != null && stopCondition.Invoke(current, next)) continue;
                    TraverseGraph(next, current, bridgePerimeterToEndpoint, ref connectedNodes, stopCondition);
                }
            }

            if (bridgePerimeterToEndpoint)
            {
                if (current.Type().IsPerimeter())
                {
                    TraverseGraph(current.BelongsTo(), current, bridgePerimeterToEndpoint, ref connectedNodes, stopCondition);
                    return;
                }

                if (current.Type().BaseType != NodeBaseType.Custom) return;

                var otherPerimeters = current.GetPerimeterNodes()
                    .Where(n => !n.Equals(previous))
                    .ToList();

                // filter out perimeter nodes that are actually connected
                var connections = current.Connections();
                if (connections.Count > 0)
                    otherPerimeters = otherPerimeters.Where(n => connections.All(c => c.EdgesBetween(current, n).Count == 0)).ToList();

                foreach (var node in otherPerimeters)
                    TraverseGraph(node, current, bridgePerimeterToEndpoint, ref connectedNodes, stopCondition);
            }
        }

        public static Vector2? BorderIntersection(Vector2 insidePosition, Vector2 outsidePosition, Rect rect)
        {
            Vector2? borderIntersection = null;
            var delta = outsidePosition - insidePosition;

            // TOP
            if (delta.y > 0)
            {
                if (delta.x == 0 && insidePosition.y < rect.yMax && outsidePosition.y > rect.yMax) return new Vector2(insidePosition.x, rect.yMax);
                
                var fromCorner = new Vector2(rect.xMin, rect.yMax);
                var toCorner = new Vector2(rect.xMax, rect.yMax);
                borderIntersection = Intersect(insidePosition, outsidePosition, fromCorner, toCorner);
            }

            // BOTTOM
            if (delta.y < 0 && borderIntersection == null)
            {
                if (delta.x == 0 && insidePosition.y > rect.yMin && outsidePosition.y < rect.yMin) return new Vector2(insidePosition.x, rect.yMin);
                
                var fromCorner = new Vector2(rect.xMin, rect.yMin);
                var toCorner = new Vector2(rect.xMax, rect.yMin);
                borderIntersection = Intersect(insidePosition, outsidePosition, fromCorner, toCorner);
            }

            // LEFT
            if (delta.x < 0 && borderIntersection == null)
            {
                if (delta.y == 0 && insidePosition.x > rect.xMin && outsidePosition.x < rect.xMin) return new Vector2(rect.xMin, insidePosition.y);
                
                var fromCorner = new Vector2(rect.xMin, rect.yMin);
                var toCorner = new Vector2(rect.xMin, rect.yMax);
                borderIntersection = Intersect(insidePosition, outsidePosition, fromCorner, toCorner);
            }

            // RIGHT
            if (delta.x > 0 && borderIntersection == null)
            {
                if (delta.y == 0 && insidePosition.x < rect.xMax && outsidePosition.x > rect.xMax) return new Vector2(rect.xMax, insidePosition.y);
                
                var fromCorner = new Vector2(rect.xMax, rect.yMin);
                var toCorner = new Vector2(rect.xMax, rect.yMax);
                borderIntersection = Intersect(insidePosition, outsidePosition, fromCorner, toCorner);
            }

            return borderIntersection;
        }

        public static bool DistanceIsTooLow(Vector3 candidate, float minDistance, List<Vector3> others)
        {
            foreach (var other in others)
            {
                if (Vector3.Distance(candidate, other) < minDistance)
                    return true;
            }

            return false;
        }

        public static float SignedAngle(Vector3 v1, Vector3 v2, Vector3 normal)
        {
            return Mathf.Atan2(
                       Vector3.Dot(normal, Vector3.Cross(v1, v2)),
                       Vector3.Dot(v1, v2)) * Mathf.Rad2Deg;
        }

        public static float SignedAngle(Vector2 v1, Vector2 v2)
        {
            var angle = Vector2.Angle(v1, v2);
            var sign = Vector3.Cross(v1.V3(), v2.V3()).y < 0 ? -1 : 1;
            return angle * sign;
        }

        public static Rect GetRectFor(Vector3 point, int resolution)
        {
            var x = OffsetValue(point.x, resolution);
            var z = OffsetValue(point.z, resolution);

            // compute input map for a small sub region to get the (approximate) terrain height at the candidate's position
            var subRect = new Rect(x, z, resolution, resolution);
            return subRect;
        }

        public static string ShortenGuid(string guid)
        {
            return guid == null || guid.Length != 36 ? guid : guid.Substring(0, 3) + ".." + guid.Substring(32, 3);
        }
        
        public static Vector3 GetSlopeNormal(Vector3 source, Vector3 destination)
        {
            var normal = Vector3.Cross((destination.V2() - source.V2()).V3(), Vector3.up).normalized;
            return Vector3.Cross(destination - source, normal).normalized;
        }
    }
}