using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public abstract class EdgeWalker<TR>
    {
        public class Options
        {
            public readonly int terrainSize;
            public readonly int resolution;
            public readonly float pixelSize;
            public readonly Vector2 cellOffset;

            public AnimationCurve crossingWidthFalloff = new AnimationCurve(new Keyframe(0, 0.3f, 0, 1), new Keyframe(1, 1, 0, 0));

            public EndpointHandling usedEndpointHandling = EndpointHandling.Fade;

            public float borderMax = 15f;
            public float additionalWidth = 0;
            public float borderChange = .2f;

            public float endFadeDistance = 10f;
            public AnimationCurve endFadeFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));

            public bool crossingFade = false;
            public float crossingDistance = 10f;
            public AnimationCurve crossingFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));

            public bool crossingOverflow = false;
            public float crossingOverflowDistance = 10f;
            public AnimationCurve crossingOverflowFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));

            public bool crossingWiden = false;
            public float crossingWidenDistance = 10f;
            public AnimationCurve crossingWidenFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 3, 3));
            public float crossingWidenFalloffMin = 1;
            public float crossingWidenFalloffMax = 2;
            public float detail = 0.5f;

            public AnimationCurve falloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));

            public GizmoLevel usedGizmoLevel = GizmoLevel.Off;

            public Options(int terrainSize, int resolution, float pixelSize, Vector2 cellOffset)
            {
                this.terrainSize = terrainSize;
                this.resolution = resolution;
                this.pixelSize = pixelSize;
                this.cellOffset = cellOffset;
            }

            public float Scale()
            {
                return 1f / pixelSize;
            }
        }

        protected readonly Options options;
        protected readonly Func<bool> stopFunc;

        protected EdgeWalker(Options options, Func<bool> stopFunc)
        {
            this.options = options;
            this.stopFunc = stopFunc;
        }

        public List<Edge> ProcessEdges(EdgesByOffset connections, TR result, Rect rect, float margin)
        {
            Log.Debug(this, () => "Process " + connections + " for rect " + rect);
            var processedEdges = new List<Edge>();

            // return on stop/disable/null input
            if (connections.Count == 0)
            {
                Log.Debug(this, () => "Skip processing empty connections.");
                return processedEdges;
            }

            // find edges relevant for the current chunk
            var relevantRect = new Rect(rect.xMin - margin, rect.yMin - margin, rect.size.x + 2 * margin, rect.size.y + 2 * margin);
            var relevantConnections = connections.FilterForRect(relevantRect);

            if (relevantConnections.Count == 0)
            {
                Log.Debug(this, () => "Skip processing empty relevant connections.");
                return processedEdges;
            }

            processedEdges = ProcessConnections(result, relevantConnections, rect, relevantRect);

            return processedEdges;
        }

        private List<Edge> ProcessConnections(TR result, List<Connection> relevantConnections, Rect rect, Rect relevantRect)
        {
            var processedEdges = new List<Edge>();

            relevantConnections.ForEach(connection =>
            {
                if (stopFunc.Invoke()) return;
                var connectionImpl = (InternalConnection) connection;
                var subsections = connectionImpl.DivideIntoSubsections(relevantRect);
                if (subsections.Count == 0) return;

                Log.Debug(this, () => "Process " + connectionImpl + " with sections " + Log.LogCollection(subsections));

                var totalDistance = connectionImpl.Length();
                // start with length of possibly skipped edges
                var currentDistance = connectionImpl.Length(null, subsections[0][0].Source());

                // Log.Warn(this, () => currentDistance + "/" + totalDistance);

                // these bools are just an optimization to reduce the checks below that would always be false
                var primaryConnection = connectionImpl.IsPrimary();
                var secondaryConnection = connectionImpl.IsSecondary();

                for (var subIndex = 0; subIndex < subsections.Count; subIndex++)
                {
                    var subsection = subsections[subIndex];
                    processedEdges.AddRange(subsection);

                    var points = new List<BorderHelper.PointWithBorder>();
                    Vector3? lastPosOfPreviousEdge = null;

                    // don't process the last special part of a crossing since its height is completely processed by the edge we're connecting to
                    subsection.ForEach(edge =>
                    {
                        // collect all points for this edge
                        var resolutionModifier = Mathf.Min(2f, Mathf.Max(1f, Mathf.Pow(edge.StraightSlopeDegrees() / 10, 2)));
                        var width = edge.Widths().Max() + options.additionalWidth;

                        var sectionStart = connectionImpl.Source();
                        var sectionEnd = connectionImpl.Destination();

                        if (edge.Destination().Equals(sectionEnd) && ((InternalEdge) edge).IsSecondary())
                        {
                            ProcessCrossingSecondaryConnection(sectionEnd, width, edge, points, result, rect);
                        } else if (edge.Source().Equals(sectionStart) && ((InternalEdge) edge).IsSecondary())
                        {
                            ProcessCrossingSecondaryConnection(sectionStart, width, edge, points, result, rect);
                        } else
                        {
                            ((InternalEdge) edge).WalkBezier((lastPoint, point) =>
                                    CollectPoints(connection,
                                        edge,
                                        lastPoint.position,
                                        point.position,
                                        currentDistance + point.length,
                                        totalDistance,
                                        ref lastPosOfPreviousEdge,
                                        point.width + options.additionalWidth,
                                        secondaryConnection,
                                        ref points,
                                        result,
                                        null,
                                        rect),
                                resolutionModifier * options.detail);
                        }

                        currentDistance += edge.Length();
                    });

                    if (points.Count == 0) continue;

                    // track height changes of current section
                    var sectionMatrix = new float[options.resolution, options.resolution];

                    PostprocessPoints(points, subsection);

                    SplineTools.Instance.DrawGizmos(subsection, offset => DrawGizmos(points, rect, offset));

                    FillResult(points, ref sectionMatrix, result, rect);

                    if (options.usedEndpointHandling == EndpointHandling.Round)
                    {
                        var subsectionEndpoints = new KeyValuePair<Node, Node>(
                            subsection.First().Source(), subsection.Last().Destination());

                        var subsectionEndpointsWithBorder = new KeyValuePair<BorderHelper.PointWithBorder, BorderHelper.PointWithBorder>(
                            points.First(), points.Last());

                        // circle around the endpoints, if this subsection has an endpoint at the start or end
                        CircleAroundEndpoints(subsectionEndpoints, subsectionEndpointsWithBorder, ref sectionMatrix, result, rect);
                    }

                    if (options.crossingOverflow && primaryConnection)
                        OverflowSecondaryConnections(connection, subsection, rect, relevantRect, result);

                    // add length of skipped edges
                    if (subIndex + 1 < subsections.Count)
                        currentDistance = connection.Length(null, subsections[subIndex + 1].DestinationEndpoint());
                }
            });

            return processedEdges;
        }

        protected void OverflowSecondaryConnections(Connection connection, List<Edge> subsection, Rect rect, Rect relevantRect, TR result)
        {
            var crossingNodes = subsection.GetCrossingNodes();

            if (crossingNodes.Count == 0) return;

            foreach (var crossingNode in crossingNodes.Where(n => !n.Equals(connection.Source()) && !n.Equals(connection.Destination())))
            {
                for (int i = 2; i < crossingNode.Edges().Count; i++)
                {
                    var crossingEdge = crossingNode.Edges()[i];
                    var edgesToProcess = ((InternalConnection) crossingEdge.Connection())
                        .EdgesForOffsets(Offsets.ForRect(relevantRect, InternalWorldGraph.OffsetResolution));

                    var points = new List<BorderHelper.PointWithBorder>();
                    Vector3? lastPosOfPreviousEdge = null;

                    if (edgesToProcess.Count == 0) continue;

                    var currentDistance = 0f;
                    var totalDistance = edgesToProcess.Sum(e => e.Length());

                    edgesToProcess.ForEach(edge =>
                    {
                        var width = edge.Widths().Max() + options.additionalWidth;

                        ((InternalEdge) edge).WalkBezier((lastPoint, point) => CollectPoints(crossingEdge.Connection(), edge, lastPoint.position,
                                point.position,
                                currentDistance + point.length, totalDistance, ref lastPosOfPreviousEdge, width, true,
                                ref points, result, crossingNode, rect, true), 1 * options.detail);

                        currentDistance += edge.Length();
                    });

                    var groupMatrix = new float[options.resolution, options.resolution];
                    FillResult(points, ref groupMatrix, result, rect);
                }
            }
        }

        protected void DrawGizmos(List<BorderHelper.PointWithBorder> points, Rect rect, Vector3 offset)
        {
#if UNITY_EDITOR
            if (options.usedGizmoLevel < GizmoLevel.Basic) return;

            for (var index = 0; index < points.Count; index++)
            {
                if (options.usedGizmoLevel == GizmoLevel.Basic && index % 10 != 0) continue;

                var p = points[index];

                if (!rect.Contains(p.point.V2())) continue;

                Gizmos.color = Color.white;
                Gizmos.DrawLine(offset + p.point, offset + p.innerBorderRight);
                Gizmos.DrawLine(offset + p.point, offset + p.innerBorderLeft);

                if (p.tilted)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(offset + p.point, offset + p.point + Vector3.up);
                }
                if (p.smoothed)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(offset + p.point, offset + p.point + Vector3.up);
                }

                Gizmos.color = p.overlappingRight ? Color.red : Color.cyan;
                Gizmos.DrawLine(offset + p.innerBorderRight, offset + p.outerBorderRight);

                Gizmos.color = p.overlappingLeft ? Color.red : Color.cyan;
                Gizmos.DrawLine(offset + p.innerBorderLeft, offset + p.outerBorderLeft);
            }
#endif
        }

        private void CircleAroundEndpoints(KeyValuePair<Node, Node> endpoints, KeyValuePair<BorderHelper.PointWithBorder,
            BorderHelper.PointWithBorder> points, ref float[,] sectionHeightMatrix, TR result, Rect rect)
        {
            if (endpoints.Key.Type().IsEndpoint())
                CircleAroundPoint(points.Key, false, ref sectionHeightMatrix, result, rect);

            if (endpoints.Value.Type().IsEndpoint())
                CircleAroundPoint(points.Value, true, ref sectionHeightMatrix, result, rect);
        }

        protected abstract void CircleAroundPoint(BorderHelper.PointWithBorder p, bool inverse, ref float[,] sectionHeightMatrix,
            TR result, Rect rect);

        protected abstract void FillResult(List<BorderHelper.PointWithBorder> points, ref float[,] sectionMatrix, TR result, Rect rect);

        protected virtual void CollectPoints(Connection connection, Edge edge, Vector3 pos, Vector3 nextPos, float currentDistance,
            float totalDistance, ref Vector3? lastPosOfPreviousEdge, float width, bool secondaryConnection,
            ref List<BorderHelper.PointWithBorder> points, TR result, Node referredCrossing, Rect rect, bool adjacentConnection = false)
        {
            var lastPos = pos.Equals(nextPos) && lastPosOfPreviousEdge.HasValue
                ? lastPosOfPreviousEdge.Value
                : pos;

            if (lastPos.Equals(nextPos)) return;

            lastPosOfPreviousEdge = pos;

            var normal = Vector3.Cross((nextPos.V2() - lastPos.V2()).V3(), Vector3.up).normalized;

            var last = points.LastOrDefault();

            var lastOuterBorderRight = last == null ? (Vector3?) null : last.outerBorderRight;
            var lastInnerBorderRight = last == null ? (Vector3?) null : last.innerBorderRight;
            var lastOuterBorderLeft = last == null ? (Vector3?) null : last.outerBorderLeft;
            var lastInnerBorderLeft = last == null ? (Vector3?) null : last.innerBorderLeft;

            var remainingDistance = totalDistance - currentDistance;
            var radius = CalculateRadius(connection, currentDistance, width, secondaryConnection, remainingDistance);

            var innerBorderLeft = FindPointForInnerBorder(lastPos, normal, radius);
            var outerBorderLeft = FindPointForOuterBorder(innerBorderLeft, normal, radius,
                lastInnerBorderLeft, lastOuterBorderLeft, result, rect);
            var innerBorderRight = FindPointForInnerBorder(lastPos, -normal, radius);
            var outerBorderRight = FindPointForOuterBorder(innerBorderRight, -normal, radius,
                lastInnerBorderRight, lastOuterBorderRight, result, rect);

            var alpha = 1f;

            // fade out at endpoints
            if (!adjacentConnection && options.usedEndpointHandling == EndpointHandling.Fade)
            {
                if (currentDistance < options.endFadeDistance && !connection.Source().Type().IsCrossing()
                    || remainingDistance < options.endFadeDistance && !connection.Destination().Type().IsCrossing())
                    alpha = options.endFadeFalloff.EvaluateClamped(Mathf.Min(currentDistance, remainingDistance) / options.endFadeDistance);
            }

            // fade out secondaries at crossings
            if (!adjacentConnection && secondaryConnection && options.crossingFade)
            {
                if (currentDistance < options.crossingDistance && connection.Source().Type().IsCrossing()
                    || remainingDistance < options.crossingDistance && connection.Destination().Type().IsCrossing())
                    alpha = options.crossingFalloff.EvaluateClamped(Mathf.Min(currentDistance, remainingDistance) / options.crossingDistance);
            }

            // overflow from primary to secondary at crossing
            if (adjacentConnection && currentDistance < options.crossingOverflowDistance && Equals(connection.Source(), referredCrossing))
            {
                alpha = options.crossingOverflowFalloff.EvaluateClamped(1 - currentDistance / options.crossingOverflowDistance);
            } else if (adjacentConnection && remainingDistance < options.crossingOverflowDistance &&
                       Equals(connection.Destination(), referredCrossing))
            {
                alpha = options.crossingOverflowFalloff.EvaluateClamped(1 - remainingDistance / options.crossingOverflowDistance);
            } else if (adjacentConnection)
            {
                return;
            }

            var slopeNormal = Util.GetSlopeNormal(lastPos, nextPos);

            points.Add(new BorderHelper.PointWithBorder(edge, lastPos, innerBorderLeft, innerBorderRight,
                outerBorderLeft, outerBorderRight, slopeNormal, alpha));
        }

        private float CalculateRadius(Connection connection, float currentDistance, float width, bool secondaryConnection, float remainingDistance)
        {
            var radius = width / 2;

            var radiusModifier = 1f;

            // widen up secondaries at crossings
            if (options.crossingWiden && secondaryConnection)
            {
                if (currentDistance < options.crossingWidenDistance && ((InternalEdge) connection.FirstEdge()).IsSecondary()
                    || remainingDistance < options.crossingWidenDistance && ((InternalEdge) connection.LastEdge()).IsSecondary())
                {
                    var crossingWidenFalloffPosition =
                        1 - Mathf.Min(currentDistance, remainingDistance) / options.crossingWidenDistance;
                    radiusModifier = options.crossingWidenFalloffMin
                                     + (options.crossingWidenFalloff.EvaluateClamped(crossingWidenFalloffPosition)
                                        * (options.crossingWidenFalloffMax - options.crossingWidenFalloffMin));
                }
            }

            radius *= radiusModifier;
            return radius;
        }

        protected virtual Vector3 FindPointForInnerBorder(Vector3 from, Vector3 normal, float radius)
        {
            return from + normal * radius;
        }

        private Vector3 FindPointForOuterBorder(Vector3 from, Vector3 direction, float radius, Vector3? previousFrom, Vector3? previousPoint,
            TR result, Rect rect)
        {
            var bounds = BorderDistanceBounds(from, radius, rect);

            var lastDistance = previousPoint == null || previousFrom == null ? 0f : (previousPoint.Value - previousFrom.Value).magnitude;
            var maxDistance = lastDistance == 0f ? bounds.y : Mathf.Min(bounds.y, lastDistance + options.borderChange);
            var minDistance = Mathf.Max(bounds.x, Mathf.Min(maxDistance, lastDistance) - options.borderChange);

            return FindPointForOuterBorder(from, direction, options.borderMax, minDistance, maxDistance, result, rect);
        }

        protected virtual Vector2 BorderDistanceBounds(Vector3 from, float radius, Rect rect)
        {
            return new Vector2(0, options.borderMax);
        }

        protected abstract Vector3 FindPointForOuterBorder(Vector3 from, Vector3 direction, float maxBorderDistance,
            float minDistance, float maxDistance, TR result, Rect rect);


        protected abstract void PostprocessPoints(List<BorderHelper.PointWithBorder> points, List<Edge> subsection);

        private void ProcessCrossingSecondaryConnection(Node crossingNode, float width, Edge edge, List<BorderHelper.PointWithBorder> points,
            TR result, Rect rect)
        {
            var length = edge.Length();

            var firstEdge = crossingNode.Edges().First(e => e.Source().Equals(crossingNode));
            var to1Control = firstEdge.BezierCurve().Source().ControlPosition();
            var to1End = firstEdge.Destination().Position();
            var to1Node = Vector3.Lerp(to1Control, to1End, 0.5f);

            var secondEdge = crossingNode.Edges().First(e => e.Destination().Equals(crossingNode));
            var to2Control = secondEdge.BezierCurve().Destination().ControlPosition();
            var to2End = secondEdge.Source().Position();
            var to2Node = Vector3.Lerp(to2Control, to2End, 0.5f);

            var to1Vector = (to1Node - crossingNode.Position()).normalized * (width / 2);
            var to2Vector = (to2Node - crossingNode.Position()).normalized * (width / 2);

            var bezier = (InternalBezierCurve) edge.BezierCurve();
            var lastPos = bezier.Source().Position();
            var pos = bezier.InterpolatedPosition(0.1f);
            var normal = Vector3.Cross((pos.V2() - lastPos.V2()).V3(), Vector3.up).normalized;
            var leftNode = lastPos + normal;

            var isStart = edge.Source().Type().IsCrossing();

            BorderHelper.PointWithBorder perimeterPoint;

            if (isStart)
            {
                var slopeNormal = Vector3.up;
                var perimeterNode = edge.Nodes().First(n => n.Type().IsCrossingPerimeter());
                var perimeterPosition = perimeterNode.Position();
               
                var radius = CalculateRadius(edge.Connection(), edge.Length(), width, true, 
                    edge.Connection().Length() - edge.Length());

                var innerBorderLeft = FindPointForInnerBorder(perimeterPosition, normal, radius);
                var outerBorderLeft = FindPointForOuterBorder(innerBorderLeft, normal, radius,
                    null, null, result, rect);
                var innerBorderRight = FindPointForInnerBorder(perimeterPosition, -normal, radius);
                var outerBorderRight = FindPointForOuterBorder(innerBorderRight, -normal, radius,
                    null, null, result, rect);
                
                perimeterPoint = new BorderHelper.PointWithBorder(edge, perimeterPosition,
                    innerBorderLeft,
                    innerBorderRight,
                    outerBorderLeft,
                    outerBorderRight,
                    slopeNormal,
                    1.0f);

            } else
            {
                if (points.Count == 0)
                {
                    Log.Debug(this, () => "Found no points for " + edge);
                    // this is might not be correct, but i think it's fine since the edge is also processed by an adjacent chunk
                    // happens for perimeters that are very close to chunk borders   
                    return;
                }

                if (points.LastOrDefault() == null) Log.Warn(this, () => edge);

                perimeterPoint = points.Last();
            }

            if ((lastPos + to1Vector - leftNode).magnitude < (lastPos + to2Vector - leftNode).magnitude)
                // use to1 for leftborder
                ProcessSecondaryCrossingEdge(to1Vector, to2Vector, perimeterPoint, edge, length, points, result, rect);
            else
                // use to1 for rightborder
                ProcessSecondaryCrossingEdge(to2Vector, to1Vector, perimeterPoint, edge, length, points, result, rect);
        }

        protected abstract void ProcessSecondaryCrossingEdge(Vector3 toLeftVector, Vector3 toRightVector,
            BorderHelper.PointWithBorder perimeterPoint,
            Edge edge, float length, List<BorderHelper.PointWithBorder> points, TR result, Rect rect);

        protected List<BorderHelper.PointWithBorder> FillGap(BorderHelper.PointWithBorder perimeterPoint,
            BorderHelper.PointWithBorder startGap,
            BorderHelper.PointWithBorder endGap)
        {
            var gapPoints = new List<BorderHelper.PointWithBorder>();

            var count = Mathf.CeilToInt((endGap.point - startGap.point).magnitude / 1.5f);

            var lastPos = startGap.point;

            for (float i = 1; i < count; i++)
            {
                var d = i / count;

                var innerBorderLeft = Vector3.Lerp(startGap.innerBorderLeft, endGap.innerBorderLeft, d);
                innerBorderLeft.y = perimeterPoint.innerBorderLeft.y;
                var innerBorderRight = Vector3.Lerp(startGap.innerBorderRight, endGap.innerBorderRight, d);
                innerBorderRight.y = perimeterPoint.innerBorderRight.y;
                var outerBorderLeft = Vector3.Lerp(startGap.outerBorderLeft, endGap.outerBorderLeft, d);
                outerBorderLeft.y = perimeterPoint.outerBorderLeft.y;
                var outerBorderRight = Vector3.Lerp(startGap.outerBorderRight, endGap.outerBorderRight, d);
                outerBorderRight.y = perimeterPoint.outerBorderRight.y;

                var nextPos = Vector3.Lerp(startGap.point, endGap.point, d);
                var slopeNormal = Util.GetSlopeNormal(lastPos, nextPos);

                gapPoints.Add(new BorderHelper.PointWithBorder(startGap.edge, nextPos, innerBorderLeft, innerBorderRight,
                    outerBorderLeft, outerBorderRight, slopeNormal, 1.0f));
            }

            return gapPoints;
        }

        protected void FixOverlappingBorders(List<BorderHelper.PointWithBorder> points)
        {
            Func<BorderHelper.PointWithBorder, Vector3> pointGetter = border => border.point;
            Func<BorderHelper.PointWithBorder, Vector3> outerGetterRight = border => border.outerBorderRight;
            Func<BorderHelper.PointWithBorder, Vector3> outerGetterLeft = border => border.outerBorderLeft;

            Action<BorderHelper.PointWithBorder, Vector3, Vector3> setterLeft = (p, vi, vo) =>
            {
                p.innerBorderLeft = new Vector3(vi.x, p.innerBorderLeft.y, vi.z);
                p.outerBorderLeft = vo;
                p.overlappingLeft = true;
            };
            Action<BorderHelper.PointWithBorder, Vector3, Vector3> setterRight = (p, vi, vo) =>
            {
                p.innerBorderRight = new Vector3(vi.x, p.innerBorderRight.y, vi.z);
                p.outerBorderRight = vo;
                p.overlappingRight = true;
            };

            // fix overlapping inner & outer borders
            PostProcessSections(points, pointGetter, outerGetterLeft, setterLeft);
            PostProcessSections(points, pointGetter, outerGetterRight, setterRight);
        }

        private void PostProcessSections(List<BorderHelper.PointWithBorder> points,
            Func<BorderHelper.PointWithBorder, Vector3> innerGetterLeft,
            Func<BorderHelper.PointWithBorder, Vector3> outerGetterLeft, Action<BorderHelper.PointWithBorder, Vector3, Vector3> setter)
        {
            var overlappingSections = new List<Vector2>();

            var bounds = new Vector2(
                Mathf.Floor(Mathf.Max(0, points.FindIndex(p => !p.tilted) - 2)),
                Mathf.Floor(Mathf.Min(points.Count - 1, points.FindLastIndex(p => !p.tilted) + 2))
            );

            var lastSectionEndIdx = (int) bounds.x;
            for (var i = (int) bounds.x + 1; i < bounds.y; i++)
            {
                if (BordersIntersect(points, i - 1, i, i, innerGetterLeft, outerGetterLeft))
                {
                    // overlapping sections must be capped at a reasonable number since with high border width, all borders might overlap
                    var sectionBounds = new Vector2(lastSectionEndIdx, Mathf.Min(lastSectionEndIdx + 5, bounds.y));
                    var section = FindIntersections(points, i - 1, i, innerGetterLeft, outerGetterLeft, sectionBounds);
                    overlappingSections.Add(section);
                    i = (int) section.y;
                    lastSectionEndIdx = (int) section.y;
                }
            }

            MergeSectionsAndSetBorder(points, overlappingSections, innerGetterLeft, outerGetterLeft, setter);
        }

        private void MergeSectionsAndSetBorder(List<BorderHelper.PointWithBorder> points, List<Vector2> overlappingSections,
            Func<BorderHelper.PointWithBorder, Vector3> innerGetter, Func<BorderHelper.PointWithBorder, Vector3> outerGetter,
            Action<BorderHelper.PointWithBorder, Vector3, Vector3> setter)
        {
            for (var i = 0; i < overlappingSections.Count - 1; i++)
            {
                var mergeIndex = i + 1;

                while (mergeIndex < overlappingSections.Count
                       && (overlappingSections[i].y > overlappingSections[mergeIndex].x
                           || SectionsOverlap(points, overlappingSections[i], overlappingSections[mergeIndex], innerGetter, outerGetter)))
                {
                    if (stopFunc.Invoke()) return;
                    var section = overlappingSections[i];
                    section.y = overlappingSections[mergeIndex].y;
                    overlappingSections[i] = section;
                    overlappingSections.RemoveAt(mergeIndex);
                }
            }

            overlappingSections.ForEach(section =>
            {
                // use the surrounding (not overlapping) points as reference
                var start = (int) Mathf.Max(0, section.x - 1);
                var end = (int) Mathf.Min(points.Count - 1, section.y + 1);
                AdjustBorder(points, start, end, innerGetter, outerGetter, setter);
            });
        }

        private bool SectionsOverlap(List<BorderHelper.PointWithBorder> points, Vector2 first, Vector2 second,
            Func<BorderHelper.PointWithBorder, Vector3> innerGetter, Func<BorderHelper.PointWithBorder, Vector3> outerGetter)
        {
            for (var i = (int) first.x; i < first.y; i++)
                if (BordersIntersect(points, i, (int) second.x, (int) second.y, innerGetter, outerGetter))
                    return true;

            return false;
        }

        private Vector2 FindIntersections(List<BorderHelper.PointWithBorder> points, int low, int high,
            Func<BorderHelper.PointWithBorder, Vector3> innerGetter,
            Func<BorderHelper.PointWithBorder, Vector3> outerGetter, Vector2 bounds)
        {
            const int scanDistance = 5;

            bool changed;
            do
            {
                if (stopFunc.Invoke()) break;
                changed = false;

                int noMatch = 0;
                var newHigh = high + 1;

                while (newHigh < bounds.y && noMatch < scanDistance)
                {
                    if (stopFunc.Invoke()) break;
                    if (BordersIntersect(points, newHigh, low, newHigh, innerGetter, outerGetter))
                    {
                        high = newHigh;
                        noMatch = 0;
                        changed = true;
                    } else
                    {
                        noMatch++;
                    }

                    newHigh++;
                }

                noMatch = 0;
                var newLow = low - 1;

                while (newLow > bounds.x && noMatch < scanDistance)
                {
                    if (stopFunc.Invoke()) break;
                    if (BordersIntersect(points, newLow, newLow, high, innerGetter, outerGetter))
                    {
                        low = newLow;
                        noMatch = 0;
                        changed = true;
                    } else
                    {
                        noMatch++;
                    }

                    newLow--;
                }
            } while (changed);

            // add an offset to avoid choppy paths (especially on S curves)
            var offset = scanDistance - 1;
            return new Vector2(
                Mathf.Floor(Mathf.Max(bounds.x, low - offset)),
                Mathf.Floor(Mathf.Min(bounds.y, high + offset)));
        }

        private bool BordersIntersect(List<BorderHelper.PointWithBorder> points, int index, int low, int high,
            Func<BorderHelper.PointWithBorder, Vector3> innerGetter,
            Func<BorderHelper.PointWithBorder, Vector3> outerGetter)
        {
            var p = points[index];

            for (int i = low; i <= high; i++)
            {
                if (i == index) continue;

                var candidate = points[i];
                if (Util.Intersect(innerGetter.Invoke(p).V2(), outerGetter.Invoke(p).V2(),
                    innerGetter.Invoke(candidate).V2(), outerGetter.Invoke(candidate).V2(), 0.05f).HasValue)
                {
                    return true;
                }
            }

            return false;
        }


        private void AdjustBorder(List<BorderHelper.PointWithBorder> points, int start, int end,
            Func<BorderHelper.PointWithBorder, Vector3> innerGetter,
            Func<BorderHelper.PointWithBorder, Vector3> outerGetter, Action<BorderHelper.PointWithBorder, Vector3, Vector3> setter)
        {
            if (end - start < 2) return;

            var borderStart = outerGetter.Invoke(points[start]);
            var borderEnd = outerGetter.Invoke(points[end]);

            var lengthStart = (borderStart - innerGetter.Invoke(points[start])).magnitude;
            var lengthEnd = (borderEnd - innerGetter.Invoke(points[end])).magnitude;

            var center = start + (end - start) / 2;
            var centerPoint = points[center];
            var outerBorder = Vector3.Lerp(borderStart, borderEnd, 0.5f);
            var length = Mathf.Lerp(lengthStart, lengthEnd, 0.5f);

            var innerPoint = innerGetter.Invoke(centerPoint);
            var toOuterBorder = outerBorder - innerPoint;

            if (toOuterBorder.magnitude > length)
            {
                outerBorder = innerPoint + toOuterBorder.normalized * length;
            }

            var toOldInnerBorder = centerPoint.innerBorderLeft - innerPoint;
            var innerBorderLength = toOldInnerBorder.magnitude;

            var innerBorder = innerPoint + toOuterBorder.normalized * innerBorderLength;

            setter.Invoke(points[center], innerBorder, outerBorder);

            if (outerBorder.Equals(Vector3.zero))
            {
                Log.Debug(this, () => "vector.zero as border found");
                throw new Exception("vector.zero as border found. Full rebuild necessary.");
            }

            AdjustBorder(points, start, center, innerGetter, outerGetter, setter);
            AdjustBorder(points, center, end, innerGetter, outerGetter, setter);
        }
    }
}
