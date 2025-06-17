using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class Flattener
    {
        private readonly BorderHelper borderHelper;

        public Flattener(BorderHelper borderHelper)
        {
            this.borderHelper = borderHelper;
        }

        public void FlattenBorder(Node node, float[,] sectionHeightMatrix, BorderHelper.HeightProcess[,] processedHeights, Rect worldSpaceRect,
            float maxBorder, float maxBorderSlope, float borderChange, float maxHeight, BorderType borderType, AnimationCurve falloff)
        {
            var direction = Vector3.left;
            var startingPoint = node.Position() + direction * node.Radius();

            var borderBounds = borderHelper.BorderDistanceBounds(startingPoint, worldSpaceRect, maxBorder, borderChange, borderType);

            var outerBorder = borderHelper.FindPointForOuterBorder(startingPoint, direction, maxBorder, borderBounds.x,
                borderBounds.y, maxBorderSlope, processedHeights, borderType, worldSpaceRect);

            var pointWithBorder = new BorderHelper.PointWithBorder(
                null,
                node.Position(),
                startingPoint,
                startingPoint,
                outerBorder,
                outerBorder,
                Vector3.down,
                1.0f);

            var circlePoints = borderHelper.CircleAroundPoint(pointWithBorder, true, maxBorder, borderChange, maxBorderSlope,
                true, processedHeights, borderType, worldSpaceRect);

            // SplineTools.Instance.DrawGizmos(circlePoints, () => DrawGizmos(circlePoints, worldSpaceRect));

            borderHelper.FillHeights(circlePoints, sectionHeightMatrix, processedHeights, falloff, worldSpaceRect, maxHeight);
        }

//         protected void DrawGizmos(List<BorderHelper.PointWithBorder> points, Rect rect)
//         {
// #if UNITY_EDITOR
//             for (var index = 0; index < points.Count; index++)
//             {
//                 // if (index % 10 != 0) continue;
//
//                 var p = points[index];
//
//                 if (!rect.Contains(p.point.V2())) continue;
//
//                 Gizmos.color = Color.white;
//                 Gizmos.DrawLine(p.point, p.innerBorderRight);
//                 Gizmos.DrawLine(p.point, p.innerBorderLeft);
//
//                 Gizmos.color = p.overlappingRight ? Color.red : Color.cyan;
//                 Gizmos.DrawLine(p.innerBorderRight, p.outerBorderRight);
//
//                 Gizmos.color = p.overlappingLeft ? Color.red : Color.gray;
//                 Gizmos.DrawLine(p.innerBorderLeft, p.outerBorderLeft);
//             }
// #endif
//         }
    }
}