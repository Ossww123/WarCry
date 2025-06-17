using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class CarvingEdgeWalker : ElevatingEdgeWalker
    {
        public new class Options : ElevatingEdgeWalker.Options
        {
            public float depthMax = 2f;
            public float depthRatio = 0.1f;

            public Options(int terrainSize, int resolution, float pixelSize, float terrainHeight, Vector2 cellOffset) : base(
                terrainSize, resolution, pixelSize, terrainHeight, cellOffset)
            {
            }
        }

        public CarvingEdgeWalker(Options options, Func<Vector2, float> heightFunc, Func<Vector2, float> maskFunc, Func<Vector2, float> varianceFunc,
            Func<bool> stopFunc) : base(options, heightFunc, maskFunc, varianceFunc, stopFunc)
        {
        }

        protected override void CollectPoints(Connection connection, Edge edge, Vector3 pos, Vector3 nextPos, float currentDistance,
            float totalDistance, ref Vector3? lastPosOfPreviousEdge, float width, bool secondaryConnection, 
            ref List<BorderHelper.PointWithBorder> points, BorderHelper.HeightProcess[,] result, Node referredCrossing, Rect rect, 
            bool adjacentConnection = false)
        {
            var depth = GetDepth(pos, nextPos, width);

            var delta = new Vector3(0, depth, 0);
            var loweredPos = pos - delta;
            var loweredNextPos = nextPos - delta;

            base.CollectPoints(connection, edge, loweredPos, loweredNextPos, currentDistance, totalDistance, ref lastPosOfPreviousEdge, width,
                secondaryConnection, ref points, result, referredCrossing, rect, adjacentConnection);
        }

        private float GetDepth(Vector3 pos, Vector3 nextPos, float width)
        {
            var depth = GetSimpleDepth(width);

            var slopeNormal = Util.GetSlopeNormal(pos, nextPos);

            if (((Options) options).inclineBySlope && !slopeNormal.Equals(Vector3.down))
            {
                depth = BorderHelper.InclineHeightChange(pos, pos, nextPos, slopeNormal, depth);
            }

            return depth;
        }

        private float GetSimpleDepth(float width)
        {
            return Mathf.Clamp((width * ((Options) options).depthRatio), 0, ((Options) options).depthMax);
        }

        protected override Vector3 FindPointForInnerBorder(Vector3 from, Vector3 normal, float radius)
        {
            return from;
        }

        protected override Vector2 BorderDistanceBounds(Vector3 from, float radius, Rect rect)
        {
            return new Vector2(radius * 2, radius * 2);
        }

        protected override float GetTiltAngleRight(BorderHelper.PointWithBorder lastTilted)
        {
            return (lastTilted.outerBorderRight - lastTilted.point).AngleToGround();
        }

        protected override float GetTiltAngleLeft(BorderHelper.PointWithBorder lastTilted)
        {
            return (lastTilted.outerBorderLeft - lastTilted.point).AngleToGround();
        }

        protected override void SmoothTiltedBorder(Vector3 p, ref Vector3 innerBorder, ref Vector3 outerBorder, float rotationAngle)
        {
            var toOuterBorder = outerBorder - innerBorder;

            var rotated = toOuterBorder.RotateAngle(rotationAngle, Vector3.Cross(toOuterBorder, Vector3.up));
            outerBorder = p + rotated;
        }

        protected override BorderHelper.PointWithBorder GetSecondaryCrossingPoint(Vector3 toLeftVector, float leftBorderLength, Vector3 toRightVector,
            float rightBorderLength, Edge edge, BorderHelper.HeightProcess[,] result, Rect rect, InternalEdge.IntermediatePoint point,
            float progress, Vector3 slopeNormal)
        {
            var delta = new Vector3(0, GetSimpleDepth(point.width), 0);
            var pointWithCurrentHeight = point.position - delta;
            return new BorderHelper.PointWithBorder(edge, pointWithCurrentHeight,
                pointWithCurrentHeight,
                pointWithCurrentHeight,
                pointWithCurrentHeight + (toLeftVector.normalized * leftBorderLength * progress),
                pointWithCurrentHeight + (toRightVector.normalized * rightBorderLength * progress),
                slopeNormal,
                progress, true);
        }
    }
}
