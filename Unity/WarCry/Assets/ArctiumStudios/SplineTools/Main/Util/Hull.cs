using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public static class Hull
    {
        public static List<Vector2> ConvexFor(List<Vector2> points)
        {
            // There must be at least 3 points 
            if (points.Count < 3) return points.ToList();

            // Initialize Result 
            var hull = new List<Vector2>();
            var hullSet = new HashSet<Vector2>();

            // start with leftmost point
            var l = 0;
            for (var i = 1; i < points.Count; i++)
                if (points[i].x < points[l].x)
                    l = i;

            // keep moving counterclockwise until reaching the start point again
            var p = l;
            do
            {
                hull.Add(points[p]);
                hullSet.Add(points[p]);

                // start with next 'random' point
                var q = (p + 1) % points.Count;

                for (var i = 0; i < points.Count; i++)
                {
                    // update q if i is more counterclockwise 
                    if (Orientation(points[p], points[i], points[q]) == 2) q = i;
                }

                // q is the most counterclockwise  
                // add to hull in next iteration
                p = q;
            } while (p != l && !hullSet.Contains(points[p]));

            return hull;
        }

        public static List<Vector2> ConvexForOrdered(List<Vector2> points, bool ccw)
        {
            if (points.Count < 3) return points.ToList();

            var hull = new List<Vector2>();

            // since points already form an ordered outline, just start at the first
            var p = 0;
            do
            {
                // Add current point to result 
                hull.Add(points[p]);

                // increment new first candidate
                var q = p + 1;

                for (var i = q; i < points.Count; i++)
                {
                    // If i is more ccw/cw than current q, then update q 
                    if (Orientation(points[p], points[i], points[q]) == (ccw ? 2 : 1)) q = i;
                }

                // q is the most counterclockwise  
                // add to hull in next iteration 
                p = q;
            } while (p < points.Count); // while points are remaining 

            return hull;
        }

        public static List<Vector2> WithOffset(List<Vector2> points, float distance, bool ccw)
        {
            if (points.Count < 2 || distance < 0.1f) return points;

            var offsetPoints = new List<Vector2>();
            
            for (var i = 1; i < points.Count; i++)
            {
                var offset = Vector3.Cross((points[i] - points[i - 1]).V3(0), Vector3.up).V2().normalized;
                offset *= ccw ? -distance : distance;
                
                offsetPoints.Add(points[i - 1] + offset);

                if (i == points.Count - 1) offsetPoints.Add(points[i] + offset);
            }

            return offsetPoints;
        }

        public static List<Vector2> ConcaveForRanged(List<Vector2> points, float rangeMin, float radius)
        {
            // There must be at least 3 points 
            if (points.Count < 3) return points.ToList();

            // start with a convex hull
            var hull = ConvexFor(points);

            // increase space between convex hull points where necessary
            RemoveCloseTogetherPoints(hull, rangeMin);

            // store hull also as set for fast contains() check
            var hullSet = new HashSet<Vector2>();
            hullSet.UnionWith(hull);

            int currentSize;

            // do until no more points are left
            do
            {
                currentSize = hull.Count;

                for (var i = 0; i < hull.Count; i++)
                {
                    // get rect area around the line and seek all points within this area
                    var p1 = hull[i];
                    var p2 = hull[(i + 1) % hull.Count];

                    if ((p1 - p2).magnitude < rangeMin + 1) continue;

                    var area = GetAreaAroundLine(p1, p2, radius);
                    var divider = FindNewSectionDivider(p1, p2, points, hullSet, area, rangeMin);
                    // Debug.LogWarning("Insert " + intersection);

                    hull.Insert(i + 1, divider);
                    hullSet.Add(divider);
                }
            } while (hull.Count != currentSize);

            // Debug.LogWarning(Log.LogCollection(hull));
            return hull;
        }

        private static Vector2 FindNewSectionDivider(Vector2 p1, Vector2 p2, List<Vector2> points, HashSet<Vector2> hullSet, List<Vector2> area,
            float rangeMin)
        {
            var intersection = points
                .Where(v => !hullSet.Contains(v))
                // check angle is reasonable
                .Where(v => Vector2.Angle(p1 - v, p2 - v) > 90)
                // filter out points too close to the line endpoints (< rangeMin)
                .Where(v => (p1 - v).magnitude > rangeMin && (p2 - v).magnitude > rangeMin)
                .Where(v => v.InsidePolygon(area))
                // find point closest to outside of the area line and divide the current line into two at this position
                .MinBy(v => Util.Distance(area[0].V3(), area[1].V3(), v.V3()));

            if (intersection == Vector2.zero) intersection = p1 + (p2 - p1) / 2;
            return intersection;
        }

        private static List<Vector2> GetAreaAroundLine(Vector2 p1, Vector2 p2, float maxDistance)
        {
            var normal = Vector3.Cross(p1.V3() - p2.V3(), Vector3.up).V2().normalized * maxDistance;
            var area = new List<Vector2>()
            {
                p1 + normal,
                p2 + normal,
                p2 - normal,
                p1 - normal
            };
            return area;
        }

        private static void RemoveCloseTogetherPoints(List<Vector2> hull, float rangeMin)
        {
            for (var i = 0; i < hull.Count; i++)
            {
                var count = 0;
                var current = hull[i];
                for (var j = i + 1; j < hull.Count; j++)
                {
                    if ((hull[j] - current).magnitude > rangeMin) break;
                    count++;
                }

                if (count > 0) hull.RemoveRange(i + 1, count);
            }
        }

        private static bool IntersectsHull(List<Vector2> hull, Vector2 from, Vector2 to)
        {
            for (var i = 0; i < hull.Count; i++)
            {
                if (hull[i].Equals(from) || hull[(i + 1) % hull.Count].Equals(from)) continue;
                if (Util.Intersect(from, to, hull[i], hull[(i + 1) % hull.Count]).HasValue) return true;
            }

            return false;
        }

        private static int Orientation(Vector2 p, Vector2 q, Vector2 r)
        {
            var val = (q.y - p.y) * (r.x - q.x) - (q.x - p.x) * (r.y - q.y);

            if (val == 0) return 0; // collinear 
            return (val > 0) ? 1 : 2; // clock or counterclock wise 
        }
    }
}