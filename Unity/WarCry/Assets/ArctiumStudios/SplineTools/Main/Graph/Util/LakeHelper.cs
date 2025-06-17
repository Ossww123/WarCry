using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class LakeHelper
    {
        private readonly Func<Vector2, float> heightFunc;
        private readonly Func<bool> stopFunc;

        public LakeHelper(Func<Vector2, float> heightFunc, Func<bool> stopFunc)
        {
            this.heightFunc = heightFunc;
            this.stopFunc = stopFunc;
        }

        public Vector4? FillLake(Vector3 center, float heightLimit, float radiusLimit, float minLakeRadius, ref List<Vector3> outline,
            out Vector3? lakeExit, out Vector3? lakeExitDirection)
        {
            // explore surrounding area to find possible outgoing river(s) and/or increase the size of the lake
            // increase the heightThreshold in small steps and use the previous outline as new starting points
            // only allow the lake to grow by some portion of the radius and until a maximum radius
            // if growing bigger -> abort -> might be a possible outgoing river 
            Log.Debug(this, () => "Fill Lake at " + center + " with heightLimit " + heightLimit + " and radiusLimit " + radiusLimit);

            Vector3? overflow = null;
            var adjustedCenter = center;
            var outlineCandidates = new List<Vector3>();
            var visited = new HashSet<Vector2>();

            var remaining = new Queue<Vector2>();
            var start = center.V2();
            visited.Add(start);
            remaining.Enqueue(start);

            var maxRadius = -1f;
            var heightStep = 0.2f;
            var raisedHeight = center.y + heightStep;

            while (raisedHeight < heightLimit)
            {
                if (stopFunc.Invoke()) break;
                outlineCandidates.ForEach(v => remaining.Enqueue(new Vector2(v.x, v.z)));

                var raisedOutlineCandidates = new List<Vector3>();
                if (Flood(remaining, raisedOutlineCandidates, visited,
                    v => v.y > raisedHeight,
                    v => (v - center).magnitude > radiusLimit, out overflow))
                {
                    raisedOutlineCandidates.ForEach(c => maxRadius = Mathf.Max(maxRadius, (c - center).magnitude));
                    if (outlineCandidates.Count > 0 && maxRadius > radiusLimit)
                    {
                        Log.Debug(this, () => "Lake radius limit reached for " + center);
                        break;
                    }

                    outlineCandidates = raisedOutlineCandidates;
                    adjustedCenter.y = raisedHeight;
                } else
                {
                    Log.Debug(this, () => "Lake flooding aborted for " + center);
                    break;
                }

                raisedHeight += heightStep;
            }

            // discard lakes that would be too small
            if (outlineCandidates.Count < 2 * Mathf.PI * minLakeRadius)
            {
                Log.Debug(this, () => "No valid lake found with min radius " + minLakeRadius + ". Outline candidates: " + Log.LogCollection(outlineCandidates));
                lakeExit = null;
                lakeExitDirection = null;
                return null;
            }

            // go back two steps in height to avoid flying edges
            adjustedCenter.y -= heightStep * 2;

            outlineCandidates = outlineCandidates.Where(c => c.y > adjustedCenter.y).ToList();

            // clean up candidates and bring them in order
            var hull = Hull.ConcaveForRanged(outlineCandidates.Select(v => v.V2()).ToList(), 5, maxRadius);

            // set all outline points to the same height
            outline = hull.Select(v => v.V3(adjustedCenter.y)).ToList();

            var actualCenter = GetCenter(hull);
            var actualRadius = hull.Max(v => (v - actualCenter).magnitude);

            // if we had overflow, find the point of the outline which might work as lake exit
            if (overflow.HasValue)
            {
                // find the closest outline point as a start
                var best = hull[0];
                var bestDistance = (hull[0] - overflow.Value.V2()).magnitude;
                hull.ForEach(o =>
                {
                    var distance = (o - overflow.Value.V2()).magnitude;
                    if (distance < bestDistance)
                    {
                        best = o;
                        bestDistance = distance;
                    }
                });

                // check the neighboring outline points if they serve as better exit point
                var index = hull.IndexOf(best);
                var exitDirection = (overflow.Value.V2() - best).normalized;

                var bestHeight = 1f;
                var bestDirection = exitDirection;

                for (var i = -5; i < 5; i++)
                {
                    var outlineIndex = (index - i + hull.Count) % hull.Count;
                    var current = hull[outlineIndex];

                    var currentDirection = GetIntoLakeDirection(hull, outlineIndex, current);

                    var outsidePoint = current + currentDirection * 15f;

                    var height = heightFunc.Invoke(outsidePoint);

                    if (height < bestHeight)
                    {
                        bestHeight = height;
                        best = current;
                        bestDirection = currentDirection;
                    }
                }

                lakeExit = best.V3(adjustedCenter.y);
                lakeExitDirection = bestDirection.V3().normalized;
            } else
            {
                lakeExit = null;
                lakeExitDirection = null;
            }

            return new Vector4(actualCenter.x, adjustedCenter.y, actualCenter.y, actualRadius);
        }

        private static Vector2 GetCenter(List<Vector2> outline)
        {
            var sum = Vector2.zero;

            foreach (var vector2 in outline) sum += vector2;
            var actualCenter = sum / outline.Count;
            return actualCenter;
        }

        public bool Flood(Queue<Vector2> remaining, List<Vector3> outlineCandidates, HashSet<Vector2> visited,
            Func<Vector3, bool> candidateCondition, Func<Vector3, bool> abortCondition, out Vector3? overflow)
        {
            overflow = null;

            while (remaining.Count > 0)
            {
                var current = remaining.Dequeue();

                var v = current.V3(heightFunc.Invoke(current));

                if (abortCondition.Invoke(v))
                {
                    Log.Debug(this, () => "Flooding aborted with overflow point " + v);
                    overflow = v;
                    return false;
                }

                if (candidateCondition.Invoke(v))
                {
                    // found the first coord outside the lake area
                    outlineCandidates.Add(v);
                } else
                {
                    // enqueue all 8 neighbors
                    EnqueueNeighbor(current.x - 1, current.y - 1, visited, ref remaining);
                    EnqueueNeighbor(current.x - 1, current.y, visited, ref remaining);
                    EnqueueNeighbor(current.x - 1, current.y + 1, visited, ref remaining);
                    EnqueueNeighbor(current.x, current.y - 1, visited, ref remaining);
                    EnqueueNeighbor(current.x, current.y + 1, visited, ref remaining);
                    EnqueueNeighbor(current.x + 1, current.y - 1, visited, ref remaining);
                    EnqueueNeighbor(current.x + 1, current.y, visited, ref remaining);
                    EnqueueNeighbor(current.x + 1, current.y + 1, visited, ref remaining);
                }
            }

            return true;
        }

        private void EnqueueNeighbor(float x, float z, HashSet<Vector2> visited, ref Queue<Vector2> queue)
        {
            var v = new Vector2(x, z);
            if (!visited.Contains(v))
            {
                visited.Add(v);
                queue.Enqueue(v);
            }
        }

        public static Vector2 GetIntoLakeDirection(List<Vector2> outline, int outlineIndex, Vector2 lakeEntry)
        {
            // average of normals from the 10 surrounding border points
            var surrounding = Mathf.Min(5, (outline.Count / 2));
            var vectorSum = Vector2.zero;
            var from = outlineIndex - surrounding;
            if (from < 0) from = outline.Count - 1 + from;

            for (var i = from + 1; i <= from + 2 * surrounding; i++)
            {
                var current = outline[i % outline.Count];
                vectorSum += current;
            }

            var center = vectorSum / (surrounding * 2 + 1);
            var direction = (center - lakeEntry).normalized;

            return (lakeEntry + direction).InsidePolygon(outline)
                ? -direction
                : direction;
        }
    }
}