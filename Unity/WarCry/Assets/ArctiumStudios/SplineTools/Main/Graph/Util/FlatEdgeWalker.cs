using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class FlatEdgeWalker : EdgeWalker<float[,]>
    {
        public FlatEdgeWalker(Options options, Func<bool> stopFunc) : base(options, stopFunc)
        {
        }

        protected override void CircleAroundPoint(BorderHelper.PointWithBorder p, bool inverse, ref float[,] sectionHeightMatrix,
            float[,] result, Rect rect)
        {
            var circlePoints = new List<BorderHelper.PointWithBorder>();
            var radius = (p.innerBorderLeft - p.point).magnitude;

            var circleAround = p.point.V2().CircleAround(radius);

            foreach (var border in circleAround)
                circlePoints.Add(new BorderHelper.PointWithBorder(null, p.point, border.V3(p.point.y), p.point,
                    border.V3(p.point.y), p.point, Vector3.down, 1.0f));

            SplineTools.Instance.DrawGizmos(ToString() + p.point, offset => DrawGizmos(circlePoints, rect, offset));
            FillResult(circlePoints, ref sectionHeightMatrix, result, rect);
        }

        protected override void FillResult(List<BorderHelper.PointWithBorder> points, ref float[,] sectionMatrix, float[,] result, Rect rect)
        {
            for (var i = 1; i < points.Count; i++)
            {
                var p1Mapped = points[i - 1] * options.Scale();
                var p2Mapped = points[i] * options.Scale();

                var minXMapped = Mathf.FloorToInt(Mathf.Min(p1Mapped.innerBorderLeft.x, p1Mapped.innerBorderRight.x,
                    p2Mapped.innerBorderLeft.x, p2Mapped.innerBorderRight.x,
                    p1Mapped.point.x, p2Mapped.point.x));
                var maxXMapped = Mathf.CeilToInt(Mathf.Max(p1Mapped.innerBorderLeft.x, p1Mapped.innerBorderRight.x,
                    p2Mapped.innerBorderLeft.x, p2Mapped.innerBorderRight.x,
                    p1Mapped.point.x, p2Mapped.point.x));
                var minZMapped = Mathf.FloorToInt(Mathf.Min(p1Mapped.innerBorderLeft.z, p1Mapped.innerBorderRight.z,
                    p2Mapped.innerBorderLeft.z, p2Mapped.innerBorderRight.z,
                    p1Mapped.point.z, p2Mapped.point.z));
                var maxZMapped = Mathf.CeilToInt(Mathf.Max(p1Mapped.innerBorderLeft.z, p1Mapped.innerBorderRight.z,
                    p2Mapped.innerBorderLeft.z, p2Mapped.innerBorderRight.z,
                    p1Mapped.point.z, p2Mapped.point.z));

                minXMapped = (int) Mathf.Max(minXMapped, rect.xMin * options.Scale());
                maxXMapped = (int) Mathf.Min(maxXMapped, rect.xMax * options.Scale());
                minZMapped = (int) Mathf.Max(minZMapped, rect.yMin * options.Scale());
                maxZMapped = (int) Mathf.Min(maxZMapped, rect.yMax * options.Scale());

                var polygonCenterLeftMapped = new List<Vector2>
                {
                    p1Mapped.point.V2(),
                    p1Mapped.innerBorderLeft.V2(),
                    p2Mapped.innerBorderLeft.V2(),
                    p2Mapped.point.V2()
                };
                var polygonCenterRightMapped = new List<Vector2>
                {
                    p1Mapped.point.V2(),
                    p1Mapped.innerBorderRight.V2(),
                    p2Mapped.innerBorderRight.V2(),
                    p2Mapped.point.V2()
                };

                for (var x = minXMapped; x <= maxXMapped; x++)
                for (var z = minZMapped; z <= maxZMapped; z++)
                {
                    var pMapped = new Vector2(x + options.cellOffset.x, z + options.cellOffset.y); // -0.5 to get center of cell (only MM1)

                    var offsetX = (int) (x - rect.xMin * options.Scale());
                    var offsetZ = (int) (z - rect.yMin * options.Scale());

//                    if (offsetX >= sectionMatrix.Length || offsetZ >= sectionMatrix.Length) Log.Warn(this, () => offsetX + ", " + offsetZ);

                    if (offsetX < 0 || offsetX >= options.resolution || offsetZ < 0 || offsetZ >= options.resolution) continue;

                    if (sectionMatrix[offsetX, offsetZ] != 0) continue;

                    float fo;
                    if (pMapped.InsidePolygon(polygonCenterLeftMapped) || pMapped.InsidePolygon(polygonCenterRightMapped))
                    {
                        var distance = Util.Distance(p1Mapped.point.V2(), p2Mapped.point.V2(), pMapped);
                        var radius = Mathf.Max((p1Mapped.innerBorderLeft - p1Mapped.point).magnitude,
                            (p2Mapped.innerBorderLeft - p2Mapped.point).magnitude);
                        var pos = Mathf.Clamp01(1 - distance / radius);
                        fo = options.falloff.EvaluateClamped(pos);
                    } else continue;

                    fo *= p1Mapped.alpha;

                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    if (result[offsetX, offsetZ] == 0)
                    {
                        sectionMatrix[offsetX, offsetZ] = fo;
                        result[offsetX, offsetZ] = fo;
                    } else
                    {
                        result[offsetX, offsetZ] = Mathf.Max(fo, result[offsetX, offsetZ]);
                    }
                }
            }
        }

        protected override Vector3 FindPointForOuterBorder(Vector3 from, Vector3 direction, float maxBorderDistance, float minDistance,
            float maxDistance, float[,] result, Rect rect)
        {
            return from;
        }

        protected override void PostprocessPoints(List<BorderHelper.PointWithBorder> points, List<Edge> subsection)
        {
            FixOverlappingBorders(points);
        }

        protected override void ProcessSecondaryCrossingEdge(Vector3 toLeftVector, Vector3 toRightVector, BorderHelper.PointWithBorder perimeterPoint,
            Edge edge, float length, List<BorderHelper.PointWithBorder> points, float[,] result, Rect rect)
        {
            var isStart = edge.Source().Type().IsCrossing();

            var tiltedPoints = new List<BorderHelper.PointWithBorder>();

            ((InternalEdge) edge).WalkBezier((lastPoint, point) =>
            {
                // skip the point that would be on the perimeter to leave space for the gap fillers below
                if (isStart && point.currentPart == point.totalParts) return;

                var currentDistance = isStart
                    ? point.length
                    : length - point.length;

                var fo = options.crossingWiden
                    ? options.crossingWidenFalloffMin +
                      (options.crossingWidenFalloff.EvaluateClamped(1 - currentDistance / options.crossingWidenDistance) *
                       (options.crossingWidenFalloffMax - options.crossingWidenFalloffMin))
                    : 1;

                // fade out secondaries at crossings
                var alpha = options.crossingFade
                    ? options.crossingFalloff.EvaluateClamped(currentDistance / options.crossingDistance)
                    : 1f;

                var innerBorderLeft = point.position + toLeftVector * fo;
                var innerBorderRight = point.position + toRightVector * fo;
                
                var slopeNormal = Util.GetSlopeNormal(lastPoint.position, point.position);
                
                tiltedPoints.Add(new BorderHelper.PointWithBorder(edge, point.position,
                    innerBorderLeft, innerBorderRight,
                    innerBorderLeft, innerBorderRight,
                    slopeNormal,
                    alpha, true));
            }, 2);

            // add connection points to fill a possible gap. the tilted borders are not suited to be rotated towards the perimeter
            BorderHelper.PointWithBorder startGap;
            BorderHelper.PointWithBorder endGap;

            if (isStart)
            {
                startGap = tiltedPoints.Last();
                endGap = perimeterPoint;
            } else
            {
                startGap = perimeterPoint;
                endGap = tiltedPoints.First();
            }

            var gapPoints = FillGap(perimeterPoint, startGap, endGap);

            if (isStart)
            {
                points.AddRange(tiltedPoints);
                points.AddRange(gapPoints);
            } else
            {
                points.AddRange(gapPoints);
                points.AddRange(tiltedPoints);
            }
        }
    }
}
