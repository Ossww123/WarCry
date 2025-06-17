using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace ArctiumStudios.SplineTools
{
    public class PathScatterer
    {
        public class Options
        {
            public int seed = 12345;
            public Side usedSide = Side.Both;

            public AnimationCurve stepLengthFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));
            public float stepLengthMin = 10;
            public float stepLengthMax = 15;

            public bool mirrorSteps = true;

            public AnimationCurve distanceFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));
            public float distanceMin = 10;
            public float distanceMax = 15;

            public bool mirrorDistance = true;

            public bool alignHeight = false;
            public bool alignRotation = true;
            public bool reverseRotation = false;

            public bool startAtSource = true;
            public bool endAtDestination = false;
            public int countMax = 0;
        }

        private readonly Options options;
        private readonly Func<bool> stopFunc;

        public PathScatterer(Options options, Func<bool> stopFunc)
        {
            this.options = options;
            this.stopFunc = stopFunc;
        }


        public List<Vector4> ProcessEdges(EdgesByOffset connections, Rect rect, float margin)
        {
            // return on stop/disable/null input
            if (connections.Count == 0) return new List<Vector4>();

            // find edges relevant for the current chunk
            var relevantRect = new Rect(rect.xMin - margin, rect.yMin - margin, rect.size.x + 2 * margin, rect.size.y + 2 * margin);
            var relevantConnections = connections.FilterForRect(relevantRect);

            if (relevantConnections.Count == 0) return new List<Vector4>();

            return ProcessConnections(relevantConnections, rect, relevantRect);
        }

        public List<Vector4> ProcessConnections(List<Connection> connections, Rect rect, Rect relevantRect)
        {
            var points = new List<Vector4>();

            connections.ForEach(connection =>
            {
                if (stopFunc.Invoke()) return;
                var connectionImpl = (InternalConnection) connection;
                var subsections = connectionImpl.DivideIntoSubsections(relevantRect);
                if (subsections.Count == 0) return;

                Log.Debug(this, () => "Process " + connection);

                var sectionLength = connection.Length();
                var sectionRandom = new ConsistentRandom(options.seed + connectionImpl.Seed());
                var sectionStepsLeft = GetSectionSteps(sectionRandom, sectionLength);
                var sectionStepsRight = options.mirrorSteps
                    ? sectionStepsLeft
                    : GetSectionSteps(sectionRandom, sectionLength);

                foreach (var subsection in subsections)
                {
                    var connectionPoints = ProcessConnection(rect, connection, subsection, sectionStepsLeft, sectionStepsRight);
                    if (options.countMax > 0) connectionPoints = connectionPoints.Take(options.countMax).ToList();

                    points.AddRange(connectionPoints);
                }
            });

            return points;
        }

        public List<Waypoint> ProcessEdges(List<Edge> edges)
        {
            var sectionRandom = new ConsistentRandom(options.seed);
            var sectionLength = edges.Sum(e => e.Length());
            var sectionSteps = GetSectionSteps(sectionRandom, sectionLength);
            
            return GetWaypoints(null, null, edges, sectionSteps);
        }

        private List<Vector4> ProcessConnection(Rect? rect, Connection connection, List<Edge> subsection,
            List<float> sectionStepsLeft, List<float> sectionStepsRight)
        {
            var points = new List<Vector4>();

            var waypointsLeft = GetWaypoints(rect, connection, subsection, sectionStepsLeft);
            var waypointsRight = GetWaypoints(rect, connection, subsection, sectionStepsRight);

            if (options.usedSide == Side.Left || options.usedSide == Side.Both || options.usedSide == Side.Random)
                CollectOffsetPoints(rect, waypointsLeft, false, points);

            if (options.usedSide == Side.Right || options.usedSide == Side.Both)
                CollectOffsetPoints(rect, waypointsRight, true, points);

            return points;
        }

        private void CollectOffsetPoints(Rect? rect, List<Waypoint> waypoints, bool invert, List<Vector4> points)
        {
            foreach (var waypointsByEdge in waypoints.GroupBy(w => w.Edge))
            {
                var edgeSeed = options.seed + GraphUtil.SeedFrom(waypointsByEdge.Key);
                var leftRnd = invert
                    ? new ConsistentRandom(edgeSeed * (options.mirrorSteps && options.mirrorDistance ? 1 : 12345))
                    : new ConsistentRandom(edgeSeed);

                foreach (var waypoint in waypointsByEdge)
                {
                    var point = GetPoint(waypoint, invert, leftRnd);
                    if (!rect.HasValue || rect.Value.Contains(point.V2())) points.Add(point);
                }
            }
        }

        private Vector4 GetPoint(Waypoint waypoint, bool invert, Random rnd)
        {
            var normal = waypoint.Normal;
            var isOtherSide = invert || (options.usedSide == Side.Random && rnd.NextFloat() < 0.5f);

            if (isOtherSide) normal = -normal;

            var distance = options.distanceMin == options.distanceMax
                ? options.distanceMin
                : options.distanceMin + (options.distanceFalloff.EvaluateClamped(rnd.NextFloat()) * (options.distanceMax - options.distanceMin));

            var p = waypoint.Position + normal * distance;
            var rotation = options.alignRotation
                ? Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg + (isOtherSide && !options.reverseRotation ? 90 : 270)
                : 0;

            var height = options.alignHeight ? p.y : 0;

            return new Vector4(p.x, height, p.z, rotation % 360);
        }

        private List<Waypoint> GetWaypoints(Rect? rect, Connection connection, List<Edge> subsection, List<float> sectionSteps)
        {
            var points = new List<Waypoint>();

            var subsectionDistanceStart = connection == null ? 0 : connection.Length(null, subsection.SourceEndpoint());
            var subsectionDistanceEnd = connection == null ? subsection.Sum(e => e.Length()) : connection.Length(null, subsection.DestinationEndpoint());

            // start with length of possibly skipped edges
            var currentDistance = subsectionDistanceStart;

            var subsectionSteps = sectionSteps.Where(d => d >= subsectionDistanceStart && d <= subsectionDistanceEnd);

            // don't process the last special part of a crossing since its height is completely processed by the edge we're connecting to
            subsection.ForEach(edge =>
            {
                // collect all points for this edge
                var edgeLength = edge.Length();

                var edgeSteps = subsectionSteps.Where(d => d >= currentDistance && d <= currentDistance + edgeLength);

                foreach (var step in edgeSteps)
                {
                    var point = GetWaypoint(edge, step - currentDistance);
                    if (!rect.HasValue || rect.Value.Contains(point.Position.V2())) points.Add(point);
                }

                currentDistance += edge.Length();
            });

            return points;
        }

        private Waypoint GetWaypoint(Edge edge, float step)
        {
            const float microStep = 0.01f;
            var edgeBezier = (InternalBezierCurve) edge.BezierCurve();

            var t = edgeBezier.FindTValue(step, edgeBezier.Length());
            if (t < microStep) t = microStep;

            var position = edgeBezier.InterpolatedPosition(t);

            // get second point very close to the actual point to find the correct normals
            var pointBefore = edgeBezier.InterpolatedPosition(t - microStep);

            // get normal
            var tangent = (position - pointBefore).normalized;

            var width = ((InternalEdge) edge).InterpolatedWidth(t);

            return new Waypoint(position, tangent, width, edge, t);
        }

        private List<float> GetSectionSteps(Random sectionRandom, float sectionLength)
        {
            var sectionSteps = new List<float>();
            var currentStepDistance = 0f;

            if (options.startAtSource) sectionSteps.Add(0f);

            do
            {
                currentStepDistance += options.stepLengthMin == options.stepLengthMax
                    ? options.stepLengthMin
                    : options.stepLengthMin + (options.stepLengthFalloff.EvaluateClamped(sectionRandom.NextFloat()) *
                                               (options.stepLengthMax - options.stepLengthMin));

                sectionSteps.Add(currentStepDistance);
            } while (currentStepDistance < sectionLength);
            
            if (options.endAtDestination) sectionSteps.Add(sectionLength);

            return sectionSteps;
        }
    }
}
