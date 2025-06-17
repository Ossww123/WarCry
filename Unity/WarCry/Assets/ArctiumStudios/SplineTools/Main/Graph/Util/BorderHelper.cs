using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class BorderHelper
    {
        public class PointWithBorder
        {
            public Edge edge;
            public Vector3 point;
            public Vector3 innerBorderLeft;
            public Vector3 innerBorderRight;
            public Vector3 outerBorderLeft;
            public Vector3 outerBorderRight;
            public Vector3 slopeNormal;
            public bool overlappingLeft = false;
            public bool overlappingRight = false;
            public float alpha;
            public bool tilted;
            public bool smoothed;

            public PointWithBorder(Edge edge, Vector3 point, Vector3 innerBorderLeft, Vector3 innerBorderRight,
                Vector3 outerBorderLeft, Vector3 outerBorderRight, Vector3 slopeNormal, float alpha, 
                bool tilted = false, bool smoothed = false)
            {
                this.edge = edge;
                this.point = point;
                this.innerBorderLeft = innerBorderLeft;
                this.innerBorderRight = innerBorderRight;
                this.outerBorderLeft = outerBorderLeft;
                this.outerBorderRight = outerBorderRight;
                this.slopeNormal = slopeNormal;
                this.alpha = alpha;
                this.tilted = tilted;
                this.smoothed = smoothed;
            }

            public static PointWithBorder operator *(PointWithBorder p, float f)
            {
                return new PointWithBorder(
                    p.edge,
                    new Vector3(p.point.x * f, p.point.y, p.point.z * f),
                    new Vector3(p.innerBorderLeft.x * f, p.innerBorderLeft.y, p.innerBorderLeft.z * f),
                    new Vector3(p.innerBorderRight.x * f, p.innerBorderRight.y, p.innerBorderRight.z * f),
                    new Vector3(p.outerBorderLeft.x * f, p.outerBorderLeft.y, p.outerBorderLeft.z * f),
                    new Vector3(p.outerBorderRight.x * f, p.outerBorderRight.y, p.outerBorderRight.z * f),
                    p.slopeNormal,
                    p.alpha,
                    p.tilted,
                    p.smoothed)
                {
                    overlappingLeft = p.overlappingLeft,
                    overlappingRight = p.overlappingRight
                };
            }
        }

        public class Options
        {
            public readonly int terrainSize;
            public readonly int resolution;
            public readonly float pixelSize;
            public readonly Vector2 cellOffset;

            public float varianceOffset = 0f;

            public bool inclineBySlope = false;

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

        private readonly Options options;

        // Vector2 is in world space
        private readonly Func<Vector2, float> heightFunc;

        // Vector2 is in world space
        private readonly Func<Vector2, float> maskFunc;

        // Vector2 is in world space
        private readonly Func<Vector2, float> varianceFunc;

        private readonly Func<bool> stopFunc;

        public BorderHelper(Options options, Func<Vector2, float> heightFunc, Func<Vector2, float> maskFunc, Func<Vector2, float> varianceFunc,
            Func<bool> stopFunc)
        {
            this.heightFunc = heightFunc;
            this.maskFunc = maskFunc;
            this.varianceFunc = varianceFunc;
            this.options = options;
            this.stopFunc = stopFunc;
        }

        public class HeightProcess
        {
            private readonly List<KeyValuePair<float, float>> steps = new List<KeyValuePair<float, float>>();

            public void AddStep(float height, float falloff)
            {
                steps.Add(new KeyValuePair<float, float>(height, falloff));
            }

            public float ProcessedHeight(float initialHeight)
            {
                var height = initialHeight;
                steps.Where(step => step.Key >= 0).ToList().ForEach(step => height = Mathf.Lerp(height, step.Key, step.Value));
                return height;
            }
        }

        public List<PointWithBorder> CircleAroundPoint(PointWithBorder p, bool inverse, float maxBorder, float borderChange,
            float maxBorderSlope, bool onlyBorder, HeightProcess[,] result, BorderType borderType, Rect rect)
        {
            var circlePoints = new List<PointWithBorder>();

            // find correct signed angle between left and right border
            var angle = Util.SignedAngle(p.innerBorderLeft - p.point, p.innerBorderRight - p.point, Vector3.up);

            if (inverse) angle = -360f + angle;

            // long borders require more steps that short borders so it doesn't get choppy
            var perimeter = 2 * Mathf.PI * maxBorder * (Mathf.Abs(angle) / 360);
            var steps = perimeter / 2 * options.Scale();

            var stepSize = angle / steps;
            var initialDirection = p.innerBorderRight - p.point;
            var lastPoint = p;

            var finalDistance = (p.outerBorderLeft - p.innerBorderLeft).magnitude;
            var lastDistance = finalDistance;

            var frontNormal = Vector3.Cross(initialDirection, Vector3.up);
            var rotationNormal = Vector3.Cross(initialDirection, frontNormal);
            if (rotationNormal.y < 0) rotationNormal = -rotationNormal;

            // iterate right border until limit
            for (var i = 0; i < steps; i++)
            {
                if (stopFunc.Invoke()) return new List<PointWithBorder>();
                var rotation = Quaternion.AngleAxis(i * stepSize, rotationNormal);
                var direction = rotation * initialDirection;

                var innerBorder = p.point + direction;
                Vector3 outerBorder;

                if (borderType == BorderType.Adaptive)
                {
                    var minBorderToEnd = finalDistance - (steps - i) * borderChange;
                    var maxBorderToEnd = finalDistance + (steps - i) * borderChange;

                    var distanceBounds = BorderDistanceBounds(innerBorder, rect, maxBorder, borderChange, borderType);

                    var minDistance = Mathf.Max(lastDistance - borderChange, distanceBounds.x);
                    minDistance = Mathf.Clamp(minDistance, minBorderToEnd, float.MaxValue);
                    var maxDistance = Mathf.Min(lastDistance + borderChange, distanceBounds.y);
                    maxDistance = Mathf.Clamp(maxDistance, minDistance, maxBorderToEnd);

                    outerBorder = FindPointForOuterBorder(innerBorder, direction.normalized, maxBorderToEnd, minDistance, maxDistance,
                        maxBorderSlope, result, borderType, rect);
                } else
                {
                    outerBorder = FindPointForOuterBorder(innerBorder, direction.normalized, maxBorder, 0, 0,
                        0, result, borderType, rect);
                }

                lastDistance = (outerBorder - innerBorder).magnitude;

                var point = onlyBorder ? innerBorder : p.point;

                lastPoint = new PointWithBorder(null, 
                    point,
                    point, innerBorder,
                    point, outerBorder,
                    Vector3.down,
                    1.0f);

                circlePoints.Add(lastPoint);
            }

            var lastP = onlyBorder ? p.innerBorderLeft : p.point;

            circlePoints.Add(new PointWithBorder(null, lastP, lastP, p.innerBorderLeft, lastP, p.outerBorderLeft, Vector3.down, 1.0f));

            return circlePoints;
        }

        public Vector3 FindPointForOuterBorder(Vector3 from, Vector3 direction, float maxBorderDistance, float minDistance, float maxDistance,
            float maxBorderSlope, HeightProcess[,] processedHeights, BorderType borderType, Rect rect)
        {
            if (borderType == BorderType.Fixed) return from + direction * maxBorderDistance;

            if (Math.Abs(minDistance - maxDistance) < 0.01f || !rect.Contains(from.V2())) return from + direction * minDistance;

            // walk normal until coordinates that satisfy the maxSlope are found
            var stepSize = .5f / options.Scale();
            var currentStep = stepSize;

            var current = from;
            var currentSlope = 90f;
            var currentDistance = 0f;
            var proceedToNext = false;

            // the max height change should only be the second factor, so it allows more direct angle than the slope itself
            var maxHeightChangeFactor = 1.2f;
            var maxHeightChangePerStep = (Mathf.Sin(maxBorderSlope * Mathf.Deg2Rad) * stepSize) * maxHeightChangeFactor;
            var totalHeightChange = 0f;
            var currentMaxHeightChange = maxHeightChangePerStep;

            while ((currentSlope > maxBorderSlope || proceedToNext || totalHeightChange > currentMaxHeightChange)
                   && currentDistance < maxBorderDistance)
            {
                if (stopFunc.Invoke()) return from;
                var point = from + currentStep * direction;
                currentMaxHeightChange += maxHeightChangePerStep;

                currentStep += stepSize;

                var pointMappedCoord = new Vector2((int) (point.x * options.Scale()), (int) (point.z * options.Scale()));
                var currentMappedCoord = new Vector2((int) (current.x * options.Scale()), (int) (current.z * options.Scale()));

                // continue if we haven't moved at least one mapped cell
                if (pointMappedCoord.Equals(currentMappedCoord)) continue;

                var height = CurrentHeight(point.V2(), processedHeights, rect);

                // use the absolute height change so the border gets longer when it goes up and down 
                totalHeightChange += Mathf.Abs(height - current.y);

                current = new Vector3(point.x, height, point.z);
                currentSlope = Mathf.Abs((current - from).AngleToGround());
                currentDistance = (current.V2() - from.V2()).magnitude;

                // proceed to the next point when the maxSlope is satisfied to mitigate rounding issues 
                proceedToNext = false;
                if (currentSlope > maxBorderSlope) proceedToNext = true;
            }

            // clamp the border to prevent hard edges
            if (currentDistance > maxDistance)
            {
                var point = from + maxDistance * direction;
                var height = CurrentHeight(point.V2(), processedHeights, rect);

                current = new Vector3(point.x, height, point.z);
            } else if (currentDistance < minDistance)
            {
                var point = from + minDistance * direction;
                var height = CurrentHeight(point.V2(), processedHeights, rect);

                current = new Vector3(point.x, height, point.z);
            }

            return current;
        }

        public float CurrentHeight(Vector2 point, HeightProcess[,] processedHeights, Rect rect)
        {
            if (!rect.Contains(point)) return -1;

            var height = heightFunc.Invoke(point);

            var xMapped = (int) ((point.x - rect.xMin) * options.Scale());
            var zMapped = (int) ((point.y - rect.yMin) * options.Scale());

            if (processedHeights[xMapped, zMapped] != null)
                height = processedHeights[xMapped, zMapped].ProcessedHeight(height);

            return height;
        }

        public Vector2 BorderDistanceBounds(Vector3 from, Rect rect, float maxBorder, float maxBorderChange, BorderType borderType)
        {
            if (borderType == BorderType.Fixed) return new Vector2(maxBorder, maxBorder);

            // the border distance converges to MaxBorder/2 when near the chunk edges to influence of edges in adjacent chunks is known without 
            // generating the full chunk
            // the relevant border distance is defined by BorderChange
            if (!rect.Contains(from.V2())) return new Vector2(maxBorder / 2, maxBorder / 2 - maxBorderChange);

            var xInRect = from.x - rect.xMin;
            var zInRect = from.z - rect.yMin;

            var distanceToChunkBorder = Mathf.Min(
                xInRect < rect.size.x / 2 ? xInRect : rect.size.x - xInRect,
                zInRect < rect.size.y / 2 ? zInRect : rect.size.y - zInRect
            );

            // maxBorder must be used as 'offset'
            distanceToChunkBorder -= maxBorder * 1.1f;
            distanceToChunkBorder = Mathf.Max(0, distanceToChunkBorder);

            var min = Mathf.Max(0, maxBorder / 2 - maxBorderChange * distanceToChunkBorder);
            var max = Mathf.Min(maxBorder, maxBorder / 2 + maxBorderChange * distanceToChunkBorder);

            return new Vector2(min, max);
        }

        public void FillHeights(List<PointWithBorder> points, float[,] groupedHeightMatrix, HeightProcess[,] processedHeights,
            AnimationCurve falloff, Rect rect, float maxHeight)
        {
            for (var i = 1; i < points.Count; i++)
            {
                if (stopFunc.Invoke()) return;
                var p1Mapped = points[i - 1] * options.Scale();
                var p2Mapped = points[i] * options.Scale();

                var minXMapped = Mathf.FloorToInt(Mathf.Min(p1Mapped.outerBorderLeft.x, p1Mapped.outerBorderRight.x,
                    p2Mapped.outerBorderLeft.x, p2Mapped.outerBorderRight.x,
                    p1Mapped.innerBorderLeft.x, p1Mapped.innerBorderRight.x,
                    p2Mapped.innerBorderLeft.x, p2Mapped.innerBorderRight.x,
                    p1Mapped.point.x, p2Mapped.point.x));
                var maxXMapped = Mathf.CeilToInt(Mathf.Max(p1Mapped.outerBorderLeft.x, p1Mapped.outerBorderRight.x,
                    p2Mapped.outerBorderLeft.x, p2Mapped.outerBorderRight.x,
                    p1Mapped.innerBorderLeft.x, p1Mapped.innerBorderRight.x,
                    p2Mapped.innerBorderLeft.x, p2Mapped.innerBorderRight.x,
                    p1Mapped.point.x, p2Mapped.point.x));
                var minZMapped = Mathf.FloorToInt(Mathf.Min(p1Mapped.outerBorderLeft.z, p1Mapped.outerBorderRight.z,
                    p2Mapped.outerBorderLeft.z, p2Mapped.outerBorderRight.z,
                    p1Mapped.innerBorderLeft.z, p1Mapped.innerBorderRight.z,
                    p2Mapped.innerBorderLeft.z, p2Mapped.innerBorderRight.z,
                    p1Mapped.point.z, p2Mapped.point.z));
                var maxZMapped = Mathf.CeilToInt(Mathf.Max(p1Mapped.outerBorderLeft.z, p1Mapped.outerBorderRight.z,
                    p2Mapped.outerBorderLeft.z, p2Mapped.outerBorderRight.z,
                    p1Mapped.innerBorderLeft.z, p1Mapped.innerBorderRight.z,
                    p2Mapped.innerBorderLeft.z, p2Mapped.innerBorderRight.z,
                    p1Mapped.point.z, p2Mapped.point.z));

                minXMapped = (int) Mathf.Max(minXMapped, rect.xMin * options.Scale());
                maxXMapped = (int) Mathf.Min(maxXMapped, rect.xMax * options.Scale());
                minZMapped = (int) Mathf.Max(minZMapped, rect.yMin * options.Scale());
                maxZMapped = (int) Mathf.Min(maxZMapped, rect.yMax * options.Scale());

                var polygonLeftMapped = new List<Vector2>
                {
                    p1Mapped.outerBorderLeft.V2(),
                    p1Mapped.innerBorderLeft.V2(),
                    p2Mapped.innerBorderLeft.V2(),
                    p2Mapped.outerBorderLeft.V2()
                };
                var polygonRightMapped = new List<Vector2>
                {
                    p1Mapped.outerBorderRight.V2(),
                    p1Mapped.innerBorderRight.V2(),
                    p2Mapped.innerBorderRight.V2(),
                    p2Mapped.outerBorderRight.V2()
                };
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
                {
                    for (var z = minZMapped; z <= maxZMapped; z++)
                    {
                        var pMapped = new Vector2(x + options.cellOffset.x, z + options.cellOffset.y); // -0.5 to get center of cell (only MM1)
                        var p = pMapped / options.Scale();

                        var offsetX = (int) (x - rect.xMin * options.Scale());
                        var offsetZ = (int) (z - rect.yMin * options.Scale());

                        if (offsetX < 0 || offsetX >= options.resolution || offsetZ < 0 || offsetZ >= options.resolution) continue;

                        // ReSharper disable once CompareOfFloatsByEqualityOperator
                        if (groupedHeightMatrix[offsetX, offsetZ] != 0) continue;

                        float fo, height;
                        KeyValuePair<KeyValuePair<float, float>, float> res;
                        var tiltedBorder = false;
                        var center = false;

                        if (pMapped.InsidePolygon(polygonCenterLeftMapped))
                        {
                            res = InterpolatePolygon(p1Mapped.point, p2Mapped.point, p1Mapped.innerBorderLeft, p2Mapped.innerBorderLeft,
                                pMapped.V3());
                            center = true;
                        } else if (pMapped.InsidePolygon(polygonCenterRightMapped))
                        {
                            res = InterpolatePolygon(p1Mapped.point, p2Mapped.point, p1Mapped.innerBorderRight, p2Mapped.innerBorderRight,
                                pMapped.V3());
                            center = true;
                        } else if (pMapped.InsidePolygon(polygonLeftMapped))
                        {
                            res = InterpolatePolygon(p1Mapped.innerBorderLeft, p2Mapped.innerBorderLeft, p1Mapped.outerBorderLeft,
                                p2Mapped.outerBorderLeft, pMapped.V3());
                            tiltedBorder = p1Mapped.tilted;
                        } else if (pMapped.InsidePolygon(polygonRightMapped))
                        {
                            res = InterpolatePolygon(p1Mapped.innerBorderRight, p2Mapped.innerBorderRight, p1Mapped.outerBorderRight,
                                p2Mapped.outerBorderRight, pMapped.V3());
                            tiltedBorder = p1Mapped.tilted;
                        } else continue;

                        if (tiltedBorder)
                        {
                            height = Mathf.Lerp(res.Key.Key, res.Key.Value, res.Value);
                            fo = falloff.EvaluateClamped(1 - res.Value);
                        } else if (center || p1Mapped.tilted)
                        {
                            height = Mathf.Lerp(res.Key.Key, res.Key.Value, res.Value);
                            fo = 1;
                        } else
                        {
                            height = res.Key.Key;
                            fo = falloff.EvaluateClamped(1 - res.Value);
                        }

                        if (varianceFunc != null)
                        {
                            var varianceValue = varianceFunc.Invoke(p) + options.varianceOffset;

                            if (options.inclineBySlope && !p1Mapped.slopeNormal.Equals(Vector3.down))
                            {
                                var normal = Vector3.Lerp(p1Mapped.slopeNormal, p2Mapped.slopeNormal, res.Value).normalized;
                                var pMappedV3 = pMapped.V3(height);

                                height += InclineHeightChange(pMappedV3, p1Mapped.point, p2Mapped.point, normal, varianceValue);
                            } else
                            {
                                height += varianceValue;
                            }
                        }

                        height = Mathf.Clamp(height, 0, maxHeight);

                        fo *= p1Mapped.alpha;
                        fo = Mathf.Clamp01(fo);

                        if (maskFunc != null) fo *= maskFunc.Invoke(p);

                        if (processedHeights[offsetX, offsetZ] == null)
                        {
                            groupedHeightMatrix[offsetX, offsetZ] = height;

                            var processedHeight = new HeightProcess();
                            processedHeight.AddStep(height, fo);
                            processedHeights[offsetX, offsetZ] = processedHeight;
                        } else
                        {
                            processedHeights[offsetX, offsetZ].AddStep(height, fo);
                        }
                    }
                }
            }
        }

        public static float InclineHeightChange(Vector3 pos, Vector3 lastPos, Vector3 nextPos, Vector3 slopeNormal, float heightChange)
        {
            var intermediate = pos + slopeNormal * heightChange;
            var toIntermediate = intermediate - pos;
            var directionToNext = (nextPos - lastPos).normalized;

            var signedAngle = Vector3.SignedAngle(toIntermediate, Vector3.forward, Vector3.up);

            var quaternion = Quaternion.Euler(new Vector3(0, signedAngle, 0));

            var rotatedToIntermediate = quaternion * toIntermediate;
            var rotatedToIntermediateV2 = new Vector2(rotatedToIntermediate.x, rotatedToIntermediate.y);
            var rotatedDirectionToNext = quaternion * directionToNext;
            var rotatedDirectionToNextV2 = new Vector2(rotatedDirectionToNext.x, rotatedDirectionToNext.y);
            var rotatedToIntermediateForwardedV2 = rotatedToIntermediateV2 + rotatedDirectionToNextV2 * (heightChange * 10);

            var intersect = Util.Intersect(Vector2.zero, 10 * heightChange * Vector2.down, rotatedToIntermediateV2, rotatedToIntermediateForwardedV2);

            return intersect.HasValue
                ? -intersect.Value.y
                : heightChange;
        }

        private KeyValuePair<KeyValuePair<float, float>, float> InterpolatePolygon(Vector3 from1, Vector3 from2, Vector3 to1, Vector3 to2,
            Vector3 p)
        {
            var distanceToFirst = Util.Distance(from1.V2(), to1.V2(), p.V2());
            var distanceToSecond = Util.Distance(from2.V2(), to2.V2(), p.V2());

            var progress = distanceToFirst / (distanceToFirst + distanceToSecond);
            if (float.IsNaN(progress)) progress = 0;

            var heightReferenceFrom = Mathf.Lerp(from1.y, from2.y, progress);
            var heightReferenceTo = Mathf.Lerp(to1.y, to2.y, progress);

            var distance = Util.Distance(from1.V2(), from2.V2(), p.V2());
            var maxDistance = Util.Distance(from1.V2(), from2.V2(), Vector3.Lerp(to1, to2, progress).V2());

            var distanceAmount = distance / maxDistance;
            if (float.IsNaN(distanceAmount)) distanceAmount = 0;

            return new KeyValuePair<KeyValuePair<float, float>, float>(new KeyValuePair<float, float>(heightReferenceFrom, heightReferenceTo),
                distanceAmount);
        }
    }
}
