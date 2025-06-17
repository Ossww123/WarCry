using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class ElevatingEdgeWalker : EdgeWalker<BorderHelper.HeightProcess[,]>
    {
        public new class Options : EdgeWalker<BorderHelper.HeightProcess[,]>.Options
        {
            public readonly float terrainHeight;

            public float borderSlopeMax = 35f;
            public float varianceOffset = 0f;
            public float crossingTiltSmoothingDistance = 15f;
            public BorderType borderType = BorderType.Fixed;

            public bool inclineBySlope = false;

            public Options(int terrainSize, int resolution, float pixelSize, float terrainHeight, Vector2 cellOffset) : base(
                terrainSize, resolution, pixelSize, cellOffset)
            {
                this.terrainHeight = terrainHeight;
            }
        }

        private readonly BorderHelper borderHelper;

        public ElevatingEdgeWalker(Options options, Func<Vector2, float> heightFunc,
            Func<Vector2, float> maskFunc, Func<Vector2, float> varianceFunc, Func<bool> stopFunc) : base(options, stopFunc)
        {
            var borderHelperOptions = new BorderHelper.Options(options.terrainSize, options.resolution, options.pixelSize, options.cellOffset)
            {
                varianceOffset = options.varianceOffset,
                inclineBySlope = options.inclineBySlope
            };

            borderHelper = new BorderHelper(borderHelperOptions, heightFunc, maskFunc, varianceFunc, stopFunc);
        }

        protected override void CircleAroundPoint(BorderHelper.PointWithBorder p, bool inverse, ref float[,] sectionHeightMatrix,
            BorderHelper.HeightProcess[,] result, Rect rect)
        {
            var circlePoints = borderHelper.CircleAroundPoint(p, inverse, options.borderMax, options.borderChange,
                ((Options) options).borderSlopeMax, false, result, ((Options) options).borderType, rect);

            SplineTools.Instance.DrawGizmos(ToString() + p.point, offset => DrawGizmos(circlePoints, rect, offset));
            FillResult(circlePoints, ref sectionHeightMatrix, result, rect);
        }

        protected override void FillResult(List<BorderHelper.PointWithBorder> points, ref float[,] sectionMatrix,
            BorderHelper.HeightProcess[,] result, Rect rect)
        {
            borderHelper.FillHeights(points, sectionMatrix, result, options.falloff, rect, ((Options) options).terrainHeight);
        }

        protected override Vector3 FindPointForOuterBorder(Vector3 from, Vector3 direction, float maxBorderDistance, float minDistance,
            float maxDistance, BorderHelper.HeightProcess[,] result, Rect rect)
        {
            return borderHelper.FindPointForOuterBorder(from, direction, maxBorderDistance, minDistance, maxDistance,
                ((Options) options).borderSlopeMax,
                result, ((Options) options).borderType, rect);
        }

        protected override void PostprocessPoints(List<BorderHelper.PointWithBorder> points, List<Edge> subsection)
        {
            TiltRiverFords(points, subsection);
            SmoothTiltedCrossingConnections(points);
            FixOverlappingBorders(points);
        }

        private void TiltRiverFords(List<BorderHelper.PointWithBorder> points, List<Edge> subsection)
        {
            if (subsection.Count == 0) return;

            var source = subsection.First().Source();
            var destination = subsection.Last().Destination();

            // set first point
            if (source.Type().BaseType == NodeBaseType.SectionRiverFordSource
                || source.Type().BaseType == NodeBaseType.SectionRiverFordDestination)
            {
                var rotation = RotationForRiverDirection(source);
                TiltBorders(points[0], rotation);
            }

            // adapt last point
            if (destination.Type().BaseType == NodeBaseType.SectionRiverFordSource
                || destination.Type().BaseType == NodeBaseType.SectionRiverFordDestination)
            {
                var rotation = RotationForRiverDirection(destination);
                TiltBorders(points[points.Count - 1], rotation);
            }

            // adapt all middle points
            if (source.Type().BaseType == NodeBaseType.SectionRiverFordSource
                && destination.Type().BaseType == NodeBaseType.SectionRiverFordDestination)
            {
                var rotation = RotationForRiverDirection(source);

                for (var i = 1; i < points.Count - 1; i++) TiltBorders(points[i], rotation);
            }
        }

        private static Quaternion RotationForRiverDirection(Node source)
        {
            var riverDirection = (Vector3) JsonUtility.FromJson(source.GetData(Constants.RiverDirection), typeof(Vector3));
            var riverDirectionFlat = riverDirection.Flattened();

            var rotation = Quaternion.FromToRotation(riverDirectionFlat, riverDirection);
            return rotation;
        }

        private void TiltBorders(BorderHelper.PointWithBorder p, Quaternion rotation)
        {
            p.tilted = true;
            p.innerBorderRight = p.point + (rotation * (p.innerBorderRight - p.point));
            p.outerBorderRight = p.point + (rotation * (p.outerBorderRight - p.point));
            p.innerBorderLeft = p.point + (rotation * (p.innerBorderLeft - p.point));
            p.outerBorderLeft = p.point + (rotation * (p.outerBorderLeft - p.point));
        }

        protected override void ProcessSecondaryCrossingEdge(Vector3 toLeftVector, Vector3 toRightVector,
            BorderHelper.PointWithBorder perimeterPoint, Edge edge, float length, List<BorderHelper.PointWithBorder> points,
            BorderHelper.HeightProcess[,] result, Rect rect)
        {
            var isStart = edge.Source().Type().IsCrossing();

            var leftBorderLength = (perimeterPoint.outerBorderLeft - perimeterPoint.innerBorderLeft).magnitude;
            var rightBorderLength = (perimeterPoint.outerBorderRight - perimeterPoint.innerBorderRight).magnitude;
            
            var tiltedPoints = new List<BorderHelper.PointWithBorder>();

            ((InternalEdge) edge).WalkBezier((lastPoint, point) =>
            {
                // skip the point that would be on the perimeter to leave space for the gap fillers below
                if (isStart && point.currentPart == point.totalParts) return;

                var currentDistance = point.length;
                var progress = Mathf.Clamp01(currentDistance / length);
                if (!isStart) progress = 1 - progress;

                var fo = options.crossingWidthFalloff.EvaluateClamped(progress);
                var slopeNormal = Util.GetSlopeNormal(lastPoint.position, point.position);

                tiltedPoints.Add(GetSecondaryCrossingPoint(toLeftVector, leftBorderLength, toRightVector, rightBorderLength,
                    edge, result, rect, point, fo, slopeNormal));
            }, 2 * options.detail);

            if (tiltedPoints.Count == 0) return;

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

        protected virtual BorderHelper.PointWithBorder GetSecondaryCrossingPoint(Vector3 toLeftVector, float leftBorderLength, Vector3 toRightVector,
            float rightBorderLength, Edge edge, BorderHelper.HeightProcess[,] result, Rect rect, InternalEdge.IntermediatePoint point, 
            float progress, Vector3 slopeNormal)
        {
            return new BorderHelper.PointWithBorder(edge, point.position,
                point.position + toLeftVector * progress,
                point.position + toRightVector * progress,
                point.position + toLeftVector * progress + toLeftVector.normalized
                    * (options.borderMax * 0.2f + leftBorderLength * progress),
                point.position + toRightVector * progress + toRightVector.normalized
                    * (options.borderMax * 0.2f + rightBorderLength * progress),
                slopeNormal,
                progress, true);
        }

        protected override Vector2 BorderDistanceBounds(Vector3 from, float radius, Rect rect)
        {
            return borderHelper.BorderDistanceBounds(from, rect, options.borderMax, options.borderChange, ((Options) options).borderType);
        }


        private void SmoothTiltedCrossingConnections(List<BorderHelper.PointWithBorder> points)
        {
            var middle = points[points.Count / 2];
            var smoothingDistance = Mathf.Min(((Options) options).crossingTiltSmoothingDistance, (middle.point - points[0].point).magnitude);

            var first = points.First();
            if (first.tilted)
            {
                var index = points.FindIndex(p => !p.tilted);

                if (index != -1)
                {
                    var lastTilted = points[index - 1];
                    var smoothedDistance = 0f;

                    var tiltAngleLeft = GetTiltAngleLeft(lastTilted);
                    var tiltAngleRight = GetTiltAngleRight(lastTilted);

                    for (var i = index; i < points.Count; i++)
                    {
                        var point = points[i];
                        var prevPoint = points[i - 1];

                        smoothedDistance += (point.point - prevPoint.point).magnitude;

                        var smoothAmount = 1 - (smoothedDistance / smoothingDistance);

                        SmoothTilt(point, smoothAmount * tiltAngleLeft, smoothAmount * tiltAngleRight);

                        if (smoothedDistance >= smoothingDistance) break;
                    }
                }
            }

            var last = points[points.Count - 1];
            if (last.tilted)
            {
                var index = points.FindLastIndex(p => !p.tilted);

                if (index != -1)
                {
                    var lastTilted = points[index + 1];
                    var smoothedDistance = 0f;

                    var tiltAngleLeft = GetTiltAngleLeft(lastTilted);
                    var tiltAngleRight = GetTiltAngleRight(lastTilted);

                    for (var i = index; i > 0; i--)
                    {
                        var point = points[i];
                        var nextPoint = points[i + 1];

                        smoothedDistance += (point.point - nextPoint.point).magnitude;

                        var smoothAmount = 1 - (smoothedDistance / smoothingDistance);

                        SmoothTilt(point, smoothAmount * tiltAngleLeft, smoothAmount * tiltAngleRight);

                        if (smoothedDistance >= smoothingDistance) break;
                    }
                }
            }
        }

        protected virtual float GetTiltAngleRight(BorderHelper.PointWithBorder lastTilted)
        {
            return (lastTilted.innerBorderRight - lastTilted.point).AngleToGround();
        }

        protected virtual float GetTiltAngleLeft(BorderHelper.PointWithBorder lastTilted)
        {
            return (lastTilted.innerBorderLeft - lastTilted.point).AngleToGround();
        }

        private void SmoothTilt(BorderHelper.PointWithBorder p, float tiltAngleLeft, float tiltAngleRight)
        {
            SmoothTiltedBorder(p.point, ref p.innerBorderLeft, ref p.outerBorderLeft, -tiltAngleLeft);
            SmoothTiltedBorder(p.point, ref p.innerBorderRight, ref p.outerBorderRight, -tiltAngleRight);

            p.smoothed = true;
        }

        protected virtual void SmoothTiltedBorder(Vector3 p, ref Vector3 innerBorder, ref Vector3 outerBorder, float rotationAngle)
        {
            var toInnerBorder = innerBorder - p;
            var toOuterBorder = outerBorder - innerBorder;

            var rotated = toInnerBorder.RotateAngle(rotationAngle, Vector3.Cross(toInnerBorder, Vector3.up));
            innerBorder = p + rotated;

            outerBorder = innerBorder + rotated.normalized * toOuterBorder.magnitude;
        }

        protected Vector3 WithCurrentHeight(Vector3 vector3, BorderHelper.HeightProcess[,] processedHeights, Rect rect)
        {
            var height = borderHelper.CurrentHeight(vector3.V2(), processedHeights, rect);

            return height < 0 ? vector3 : new Vector3(vector3.x, height, vector3.z);
        }
    }
}
