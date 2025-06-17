using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace ArctiumStudios.SplineTools
{
    public class RiverPathfinder : Pathfinder
    {
        public new class Options : Pathfinder.Options
        {
            public float carveThroughHeightMin = 3f;
            public float carveThroughHeightMax = 10f;
            public AnimationCurve carveThroughHeightFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));
            public float widthMax = 15f;
            public float widthNoise = 0.3f;
            public float dryUpWidth = 3f;
            public float growthMax = 0.08f;
            public float decayMax = 0.04f;
            public float lakeSizeFactor = 3f;
            public float riverJoinAngleMax = 50f;
            public float riverJoinSlopeMax = 15f;
            public float riverJoinSlopeChangeMax = 15f;
            public float outflowingRiverAngleMin = 60f;
            public float sectionLengthFactor = 2f;
            public float riverDirectionChangeAngleMin = 15f;
            public float riverDirectionChangeAngleMax = 120f;
            public AnimationCurve riverDirectionChangeAngleFalloff = new AnimationCurve(new Keyframe(0, 1, 1, 0), new Keyframe(1, 0, 0, 1));
            public LakeMode lakeMode;
            public bool useSpringAsLakeSize = false;
            public float initialLakeSizeModifier = 1.5f;
            public AnimationCurve outflowingRiverSizeFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));
            public float requiredSeaDepth = 10f;
            public float seaJoinWidenFactor = 1.5f;
        }

        public enum LakeMode
        {
            AllowStartWithLakes,
            EnforceStartWithLakes,
            ForbidStartWithLakes,
            OnlyLakes
        }

        private enum RiverEnd
        {
            JoinRiver,
            Lake,
            JoinLake,
            DryUp,
            Sea,
            Discard
        }

        public class RiverCandidate
        {
            public Vector3 position;
            public float width;
            public bool carving;
            public bool fixControlHeight;
            public NodeType type;
            public bool belongsToFirst;
            public bool belongsToLast;

            public RiverCandidate(Vector3 position, float width, NodeType type, bool carving = false,
                bool belongsToFirst = false, bool belongsToLast = false, bool fixControlHeight = false)
            {
                this.position = position;
                this.width = width;
                this.type = type;
                this.carving = carving;
                this.fixControlHeight = fixControlHeight;
                this.belongsToFirst = belongsToFirst;
                this.belongsToLast = belongsToLast;
            }

            public float SectionLength(float sectionLengthFactor)
            {
                return width * sectionLengthFactor;
            }

            public override string ToString()
            {
                return GetType() + "[" + position + ", " + type + "]";
            }
        }

        public class LakeCandidate : RiverCandidate
        {
            public float radius;
            public List<Vector2> outline;
            public Vector3? lakeExit;
            public Vector3? lakeExitDirection;

            public LakeCandidate(RiverCandidate riverCandidate, float radius, List<Vector2> outline) :
                base(riverCandidate.position, riverCandidate.width, riverCandidate.type, riverCandidate.carving)
            {
                this.radius = radius;
                this.outline = outline;
            }
        }

        public class SeaCandidate : LakeCandidate
        {
            public SeaCandidate(RiverCandidate riverCandidate, float radius, List<Vector2> outline) : base(riverCandidate, radius, outline)
            {
            }
        }

        public class RiverJoinCandidate : RiverCandidate
        {
            public Node existingNode;

            public RiverJoinCandidate(float width, Node existingNode, bool carving = false) :
                base(existingNode.Position(), width, NodeType.Of(NodeBaseType.RiverCrossing), carving)
            {
                this.existingNode = existingNode;
            }
        }

        public class LakeJoinCandidate : RiverCandidate
        {
            public Node existingNode;

            public LakeJoinCandidate(float width, Node existingNode, bool carving = false) :
                base(existingNode.Position(), width, NodeType.Of(NodeBaseType.Lake), carving)
            {
                this.existingNode = existingNode;
            }
        }

        private readonly Func<Vector2, float> growthFunc;
        private readonly Func<Vector2, float> decayFunc;
        private readonly Rect worldBounds;
        private readonly LakeHelper lakeHelper;

        public RiverPathfinder(InternalWorldGraph graph, string reference, Func<Vector2, float> heightFunc, Options options,
            Func<Vector2, float> growthFunc, Func<Vector2, float> decayFunc, Rect worldBounds,
            Func<bool> stopFunc) : base(graph, reference, heightFunc, options, stopFunc)
        {
            this.growthFunc = growthFunc;
            this.decayFunc = decayFunc;
            this.worldBounds = worldBounds;
            this.lakeHelper = new LakeHelper(heightFunc, stopFunc);
        }

        public void GenerateRiver(Node from)
        {
            var previous = new List<RiverCandidate>
            {
                new RiverCandidate(from.PositionV2().V3(heightFunc.Invoke(from.PositionV2())), from.Radius() * 2, from.Type())
            };
            var newConnections = GenerateRiver(from, previous);
            newConnections.ForEach(c => graph.StoreConnection(c, reference));
        }

        private List<Connection> GenerateRiver(Node from, List<RiverCandidate> candidates)
        {
            if (stopFunc.Invoke()) return new List<Connection>();

            // skip when already below or too close to sea level
            if (from.Position().y <= ((Options) options).heightMin + 1) return new List<Connection>();

            var riverEnd = RiverEnd.Sea;
            var rnd = new ConsistentRandom(((InternalNode) from).Seed());
            var opt = (Options) options;

            var connectionData = new ConnectionData(options, null, candidates[candidates.Count - 1].width, FindControlAction,
                new List<ConnectionWaypoint>(), new HashSet<Node>());

            if (opt.lakeMode == LakeMode.OnlyLakes || (from.Type().BaseType != NodeBaseType.Lake && opt.lakeMode == LakeMode.EnforceStartWithLakes))
                riverEnd = RiverEnd.Lake;
            else
                LetRiverFlow(from, candidates, ref riverEnd, connectionData, rnd);

            var connections = new List<Connection>();

            if (riverEnd == RiverEnd.Discard)
            {
                Log.Debug(this, () => "Discarding river starting at " + from);
                return connections;
            }

            var startingWithLake = false;

            if (from.Type().BaseType != NodeBaseType.Lake &&
                (candidates.Count < 3 || (from.Position().y - candidates[candidates.Count - 1].position.y) < 3f))
            {
                if (opt.lakeMode != LakeMode.OnlyLakes && opt.lakeMode != LakeMode.EnforceStartWithLakes)
                {
                    if (candidates.Count < 3)
                        Log.Debug(this, () => "Discarding river starting at " + from + ": too short. Trying to start with a lake instead.");
                    else
                        Log.Debug(this,
                            () => "Discarding river starting at " + from + ": not enough height change. Trying to start with a lake instead.");
                }

                if (opt.lakeMode == LakeMode.ForbidStartWithLakes) return connections;

                // remove all candidates but the first
                var first = candidates[0];

                // adjust the width of the 'river' since the spring node is meant to be a lake
                if ((opt.lakeMode == LakeMode.EnforceStartWithLakes && opt.useSpringAsLakeSize) || opt.lakeMode == LakeMode.OnlyLakes)
                    first.width = RiverWidthWithStartingLake(first);

                candidates.Clear();
                candidates.Add(first);
                startingWithLake = true;
                riverEnd = RiverEnd.Lake;
            }

            if (riverEnd == RiverEnd.DryUp)
            {
                ProcessRiverDryingUp(candidates);
            } else if (riverEnd == RiverEnd.Sea && candidates[candidates.Count - 1].position.y >= options.heightMin)
            {
                riverEnd = RiverEnd.Lake;
            }

            // stuck somewhere in a sink -> lake
            if (riverEnd == RiverEnd.Lake)
            {
                var lakeFilled = ProcessRiverEndingInLake(candidates, rnd);

                if (!lakeFilled)
                {
                    // lake was not possible for some reasons -> dry up if already small enough or discard the whole river
                    if (candidates.Count > 3 && candidates[candidates.Count - 1].width < ((Options) options).dryUpWidth * 1.5f)
                    {
                        riverEnd = RiverEnd.DryUp;
                        ProcessRiverDryingUp(candidates);
                    } else
                    {
                        Log.Debug(this, () => "Discarding river starting at " + from);
                        return connections;
                    }
                }
            }

            if (riverEnd == RiverEnd.Sea) ProcessRiverEndingInSea(candidates);

            Node lakeNode;

            if (startingWithLake && riverEnd == RiverEnd.Lake)
            {
                // directly create and add the lake node, since it may not be part of any connection
                var startingLakeNode = new InternalNode(rnd.NextGuid(candidates[0].position).ToString(), candidates[0].position,
                    candidates[0].width / 2, candidates[0].type, null, options.resolution);

                var lakeCandidate = candidates[0] as LakeCandidate;
                startingLakeNode.type = GetNodeType(riverEnd);
                startingLakeNode.radius = lakeCandidate.radius;
                startingLakeNode.AddData(Constants.LakeOutline, JsonHelper.ToJson(lakeCandidate.outline.ToArray()));
                graph.StoreNode(startingLakeNode);
                lakeNode = startingLakeNode;
            } else
            {
                var riverConnection = AddRiverEdges(from, candidates, connectionData, riverEnd, rnd);
                connections.Add(riverConnection);
                lakeNode = riverConnection.Destination();
            }

            if (riverEnd == RiverEnd.Lake && opt.lakeMode != LakeMode.OnlyLakes)
            {
                // if we found a possible lake exit, check if it is valid and generate next river from there
                connections.AddRange(GenerateOutflowingRiverIfFeasible(candidates, lakeNode));
            }

            return connections;
        }

        private float RiverWidthWithStartingLake(RiverCandidate first)
        {
            return Mathf.Lerp(
                ((Options) options).dryUpWidth,
                ((Options) options).widthMax,
                Mathf.Clamp01(first.width * ((Options) options).initialLakeSizeModifier / LakeSizeLimit(((Options) options).widthMax)));
        }

        private void ProcessRiverEndingInSea(List<RiverCandidate> candidates)
        {
            // find last candidate before sea & raise all above seaLevel
            var idxBeforeSea = candidates.Count - 1;

            while (candidates[idxBeforeSea].position.y < options.heightMin)
            {
                // candidates[idxBeforeSea].position.y = options.heightMin;
                idxBeforeSea--;
            }

            var firstInSea = candidates[idxBeforeSea + 1];
            var beforeSea = candidates[idxBeforeSea];

            // find more exact perimeter
            int perimeterIdx;
            var perimeterPosition = firstInSea.position;
            var outOfSeaDirection = beforeSea.position - firstInSea.position;
            var progress = 0.05f;

            // find first point above sea level
            while (perimeterPosition.y < options.heightMin)
            {
                perimeterPosition = firstInSea.position + progress * outOfSeaDirection;
                progress += 0.1f;
            }

            // set perimeter height to sea level 
            perimeterPosition.y = options.heightMin;

            // relax surrounding candidates and insert/replace perimeter
            if (progress < 0.35f)
            {
                // replace/overwrite firstInSea
                firstInSea.position = perimeterPosition;
                firstInSea.type = NodeType.Of(NodeBaseType.RiverPerimeter);
                perimeterIdx = idxBeforeSea + 1;
            } else if (progress > 0.65f)
            {
                // replace/overwrite beforeSea
                beforeSea.position = perimeterPosition;
                beforeSea.type = NodeType.Of(NodeBaseType.RiverPerimeter);
                perimeterIdx = idxBeforeSea;
            } else
            {
                // insert between beforeSea and firstInSea
                candidates.Insert(idxBeforeSea + 1, new RiverCandidate(perimeterPosition,
                    Mathf.Lerp(firstInSea.width, beforeSea.width, progress), NodeType.Of(NodeBaseType.RiverPerimeter)));
                perimeterIdx = idxBeforeSea + 1;
            }

            candidates[perimeterIdx].belongsToLast = true;

            // set inner/outer exit types
            candidates[perimeterIdx - 1].type = NodeType.Of(NodeBaseType.SeaOuterExit);
            candidates[perimeterIdx - 1].belongsToLast = true;
            // belongsToLast for innerExit will be set in the loop below
            candidates[perimeterIdx + 1].type = NodeType.Of(NodeBaseType.SeaInnerExit);

            // find the sea outline around the perimeter
            var last = candidates[candidates.Count - 1];

            var distanceLimit = (candidates[perimeterIdx - 1].position - last.position).magnitude * 1.1f;

            var seaOutlineCandidates = new List<Vector3>();

            var visited = new HashSet<Vector2>();

            var remaining = new Queue<Vector2>();
            var start = perimeterPosition.V2() + (last.position - perimeterPosition).V2();
            visited.Add(start);
            remaining.Enqueue(start);

            // expected to return false
            lakeHelper.Flood(remaining, seaOutlineCandidates, visited,
                v => v.y > options.heightMin || (v - perimeterPosition).magnitude > distanceLimit,
                v => false, out _);

            var outline = Hull.ConcaveForRanged(seaOutlineCandidates.Select(v => v.V2()).ToList(), 5, distanceLimit / 2);

            // widen river in sea & set height to sea height
            for (var i = perimeterIdx; i < candidates.Count; i++)
            {
                candidates[i].width *= ((Options) options).seaJoinWidenFactor;
                candidates[i].position.y = perimeterPosition.y;
                candidates[i].belongsToLast = true;
            }

            var seaCandidate = new SeaCandidate(
                new RiverCandidate(last.position, last.width, NodeType.Of(NodeBaseType.Sea), belongsToLast: true, fixControlHeight: true),
                distanceLimit, outline);

            candidates.RemoveAt(candidates.Count - 1);
            candidates.Add(seaCandidate);
        }

        private void ProcessRiverDryingUp(List<RiverCandidate> candidates)
        {
            RemoveCarvingCandidates(candidates);

            var smoothCandidates = 3;
            var min = Mathf.Max(0, candidates.Count - 1 - smoothCandidates);

            for (var i = candidates.Count - 1; i > min; i--)
            {
                var progress = (float) (candidates.Count - i) / smoothCandidates;
                candidates[i].width = Mathf.Lerp(((Options) options).dryUpWidth, candidates[i].width, progress);
            }
        }

        private void LetRiverFlow(Node from, List<RiverCandidate> candidates, ref RiverEnd riverEnd, ConnectionData connectionData,
            ConsistentRandom rnd)
        {
            var last = candidates[candidates.Count - 1];
            // defer creation of the actual connection until it is clear where the river ends
            var sectionLength = last.SectionLength(((Options) options).sectionLengthFactor);

            var next = FlowDownhill(last.position, candidates, sectionLength, last.width);

            var opt = (Options) options;

            var widthAmount = (last.width - opt.dryUpWidth) / (opt.widthMax - opt.dryUpWidth);
            var carveThroughHeight = opt.carveThroughHeightMin + opt.carveThroughHeightFalloff.EvaluateClamped(widthAmount) *
                (opt.carveThroughHeightMax - opt.carveThroughHeightMin);
            var riverEndingInSeaHeight = Mathf.Max(0, options.heightMin - opt.requiredSeaDepth);

            while (next.position.y < last.position.y + carveThroughHeight && last.position.y > riverEndingInSeaHeight)
            {
                if (stopFunc.Invoke()) return;

                var aboveSeaLevel = next.position.y > options.heightMin;

                if (aboveSeaLevel)
                {
                    if (EndsInExistingRiver(from, candidates, ref riverEnd, last, rnd)) return;
                    if (EndsInExistingLake(from, candidates, ref riverEnd, last, rnd)) return;
                }

                // a river can't flow uphill, so it might need to carve through the terrain a bit
                if (next.position.y > last.position.y)
                {
                    next.position.y = last.position.y;
                    next.carving = true;
                }

                // river got too small and dried out or is out of bounds
                if (next.width < opt.dryUpWidth || !worldBounds.Contains(next.position.V2()))
                {
                    Log.Debug(this, () => "River starting at " + from + " dried up");
                    riverEnd = RiverEnd.DryUp;
                    break;
                }

                candidates.Add(next);

                var growthFactor = growthFunc == null
                    ? opt.growthMax
                    : growthFunc.Invoke(next.position) * opt.growthMax;
                var decayFactor = decayFunc == null
                    ? opt.decayMax
                    : decayFunc.Invoke(next.position) * opt.decayMax;
                var sizeChange = 1f + growthFactor - decayFactor;

                var nextWidth = last.width * sizeChange;
                // clamp to max width
                nextWidth = Mathf.Min(nextWidth, opt.widthMax);
                sectionLength = last.width * opt.sectionLengthFactor;
                connectionData.fallbackWidth = nextWidth;

                last = next;
                next = FlowDownhill(candidates[candidates.Count - 1].position, candidates, sectionLength, nextWidth);
            }
        }

        private bool EndsInExistingRiver(Node from, List<RiverCandidate> candidates, ref RiverEnd riverEnd, RiverCandidate last,
            ConsistentRandom rnd)
        {
            if (candidates.Count < 2) return false;

            var radius = last.SectionLength(((Options) options).sectionLengthFactor) * 2;
            var riverDirection = (candidates[candidates.Count - 1].position - candidates[candidates.Count - 2].position).normalized;

            var reroutableNodesInRange = graph
                .NodesInRange(last.position.V2(), radius, new[] { NodeType.Of(NodeBaseType.RiverSection) })
                .Where(n => !n.Equals(from))
                .Where(n => n.BelongsTo() == null)
                .Where(n => Mathf.Abs(Util.SignedAngle((n.Position() - last.position).normalized, riverDirection, Vector3.up)) < 30)
                .ToList();

            if (reroutableNodesInRange.Count <= 0) return false;

            var count = 0;
            var targetNode = reroutableNodesInRange.First();
            var aborted = false;

            while ((targetNode.Position().y > last.position.y || !CanJoinRiverInGoodAngle(last, targetNode)) && count < 2)
            {
                if (stopFunc.Invoke()) return false;
                // select nodes down the river until it is actually lower
                var next = targetNode.Edges().DestinationEndpoint();
                if (next.Type().BaseType == NodeBaseType.RiverBorder) next = next.Edges().DestinationEndpoint();

                // no good node found for joining, discard river
                if (next.Type().BaseType != NodeBaseType.RiverSection)
                {
                    aborted = true;
                    break;
                }

                targetNode = next;
                count++;
            }

            if (aborted || targetNode.Position().y > last.position.y)
            {
                // let the river continue flowing if it is still far enough away from the target river
                if ((last.position - targetNode.Position()).magnitude < last.SectionLength(((Options) options).sectionLengthFactor)) return false;

                Log.Debug(this, () => "Can't join river.");
                riverEnd = RiverEnd.Discard;
                return true;
            }

            Log.Debug(this, () => "Join river from " + last.position + " at " + targetNode.Position());

            // insert sections up to the targetNode
            var joinAngle = -rnd.Next(45, 75);

            var isRiverJoiningFromRight = IsRiverJoiningFromRight(last, targetNode);
            if (isRiverJoiningFromRight) joinAngle = -joinAngle;

            var perimeterPosition = FindCrossingPerimeterPosition(targetNode, joinAngle, isRiverJoiningFromRight);

            // insert sections between last and perimeter
            var targetNodePosition = targetNode.Position();

            var perimeterToTargetNode = targetNodePosition - perimeterPosition;
            var joinDirection = perimeterToTargetNode.normalized;

            var crossingPerimeterCandidate = new RiverCandidate(perimeterPosition, last.width, NodeType.Of(NodeBaseType.RiverCrossingPerimeter),
                belongsToLast: true);

            var joinedRiverEdges = targetNode.Connections()[0].EdgesBetween(targetNode.Connections()[0].Source(), targetNode);

            var newCandidates = FillRiverCandidates(last, crossingPerimeterCandidate, joinDirection,
                joinedRiverEdges, rnd);

            newCandidates.Add(crossingPerimeterCandidate);

            candidates.AddRange(newCandidates);

            var joinedEdge = targetNode.Edges()[1];

            ((InternalConnection) joinedEdge.Connection()).SplitAt(targetNode, NodeType.Of(NodeBaseType.RiverCrossing));

            candidates.Add(new RiverJoinCandidate(last.width, targetNode));
            riverEnd = RiverEnd.JoinRiver;

            WidenRiver(targetNode, last.width);

            return true;
        }

        private List<RiverCandidate> FillRiverCandidates(RiverCandidate sourceCandidate, RiverCandidate destinationCandidate, Vector3 joinDirection,
            List<Edge> joinedRiverEdges, ConsistentRandom rnd)
        {
            var delta = destinationCandidate.position - sourceCandidate.position;

            var direction = delta.normalized;
            var lengthToFill = delta.magnitude;

            var sections = Mathf.Ceil(lengthToFill / sourceCandidate.SectionLength(((Options) options).sectionLengthFactor));

            var minSectionHeight = destinationCandidate.position.y;
            var maxSectionHeight = sourceCandidate.position.y;

            var newCandidates = new List<RiverCandidate>();

            for (var i = 1; i < sections; i++)
            {
                var t = 1 - i / sections;

                var width = Mathf.Lerp(sourceCandidate.width, destinationCandidate.width, t)
                            + (1 + rnd.NextFloat() * ((Options) options).widthNoise);

                var position = destinationCandidate.position + t * lengthToFill * -Vector3.Lerp(direction, joinDirection, t);

                if (joinedRiverEdges != null && joinedRiverEdges.Count > 1)
                {
                    var closest = joinedRiverEdges.MinBy(e => (e.Source().Position() - position).magnitude);
                    var fromClosest = position - closest.Source().Position();
                    var minDistance = Mathf.Min(1, 0.2f + t) * 2 * width + closest.Widths()[1];

                    if (fromClosest.magnitude < minDistance) position = closest.Source().Position() + fromClosest.normalized * minDistance;
                }

                position.y = Mathf.Lerp(destinationCandidate.position.y,
                    Mathf.Clamp(heightFunc.Invoke(position.V2()), minSectionHeight, maxSectionHeight),
                    t * 1.5f);
                maxSectionHeight = position.y;
                newCandidates.Add(new RiverCandidate(position, width, NodeType.Of(NodeBaseType.RiverSection)));
            }

            return newCandidates;
        }

        private static Vector3 FindCrossingPerimeterPosition(Node targetNode, int joinAngle, bool isRiverJoiningFromRight)
        {
            // find perimeter position
            var distanceToPointPerpendicularOnEdge = targetNode.Radius() / Mathf.Tan(Mathf.Abs(joinAngle) * Mathf.Deg2Rad);
            var progressOnEdge = (targetNode.Edges()[0].Length() - distanceToPointPerpendicularOnEdge) / targetNode.Edges()[0].Length();
            var pointOnEdge = targetNode.Edges()[0].BezierCurve().InterpolatedPosition(progressOnEdge);
            var previousPointOnEdge = targetNode.Edges()[0].BezierCurve().InterpolatedPosition(progressOnEdge - 0.05f);

            var normalToPerimeter = Vector3.Cross(pointOnEdge - previousPointOnEdge, Vector3.up);

            if (!isRiverJoiningFromRight) normalToPerimeter = -normalToPerimeter;

            var perimeterPosition = pointOnEdge + normalToPerimeter.normalized * targetNode.Radius();
            return perimeterPosition;
        }

        private static bool IsRiverJoiningFromRight(RiverCandidate last, Node targetNode)
        {
            var distanceThreshold = (last.position - targetNode.Position()).magnitude * 2;
            var relevantEdges = targetNode.Connections()[0].Edges()
                .Where(edge => (edge.Source().Position() - last.position).magnitude < distanceThreshold)
                .ToList();

            var isRightCount = relevantEdges
                .Count(edge =>
                {
                    var normalRight = Vector3.Cross(edge.Source().Position().Flattened(), edge.V3().Flattened());
                    return Vector3.Angle(normalRight, (last.position - edge.Source().Position()).Flattened()) < 90;
                });

            var isJoiningFromRight = isRightCount >= relevantEdges.Count;
            return isJoiningFromRight;
        }

        private bool CanJoinRiverInGoodAngle(RiverCandidate last, Node targetNode)
        {
            var joinedPreviousDirection = targetNode.Edges()[0].BezierCurve().V3();
            var joinedDirection = targetNode.Edges()[1].BezierCurve().Source().ControlV3();

            var joiningDirection = targetNode.Edges()[1].Destination().Position() - last.position;

            // don't join when the previous section's slope was much different from the joined one
            var previousAngleToGround = joinedPreviousDirection.AngleToGround();

            if (previousAngleToGround - joinedDirection.AngleToGround() > ((Options) options).riverJoinSlopeChangeMax) return false;
            // don't join when the slope is too high
            if (Mathf.Abs(previousAngleToGround) > ((Options) options).riverJoinSlopeMax) return false;

            return Vector3.Angle(joiningDirection, joinedDirection) < ((Options) options).riverJoinAngleMax;
        }

        /// <summary>
        /// Check if the last candidate is inside an existing lake.
        /// </summary>
        /// <returns></returns>
        private bool EndsInExistingLake(Node from, List<RiverCandidate> candidates, ref RiverEnd riverEnd, RiverCandidate last, ConsistentRandom rnd)
        {
            var lastPosition = last.position;
            var lastWidth = last.width;

            return EndsInExistingLake(from, candidates, ref riverEnd, lastPosition, lastWidth, rnd);
        }

        private bool EndsInExistingLake(Node from, List<RiverCandidate> candidates, ref RiverEnd riverEnd, Vector3 lastPosition, float lastWidth,
            ConsistentRandom rnd)
        {
            var lakeSearchRadius = LakeSizeLimit(((Options) options).widthMax);

            var reroutableNodesInRange = graph.NodesInRange(lastPosition.V2(), lakeSearchRadius, new[] { NodeType.Of(NodeBaseType.Lake) })
                .Where(n => !n.Equals(from))
                .Where(n => (n.Position() - lastPosition).magnitude < n.Radius())
                .ToList();

            if (reroutableNodesInRange.Count <= 0) return false;

            if (candidates.Count < 2)
            {
                // discard if the river was already too close to the lake
                Log.Debug(this, () => "River is too close to a Lake.");
                riverEnd = RiverEnd.Discard;
                return true;
            }

            riverEnd = RiverEnd.JoinLake;

            var lakeCenter = reroutableNodesInRange.First();

            AdjustCandidatesInLake(candidates, lakeCenter.Position(), lakeCenter.GetData<Vector2>(Constants.LakeOutline).ToList(),
                out var adjustedOutline, rnd);

            // store updated lake outline
            lakeCenter.RemoveData(Constants.LakeOutline);
            lakeCenter.AddData(Constants.LakeOutline, JsonHelper.ToJson(adjustedOutline.ToArray()));

            candidates.Add(new LakeJoinCandidate(lastWidth, lakeCenter));

            return true;
        }

        private void WidenRiver(Node from, float width)
        {
            var riverWidenAmount = 0.1f;
            var current = from;
            var last = current;

            while (current.Edges().Count >= 2 && !ReferenceEquals(last, current))
            {
                if (stopFunc.Invoke()) return;
                var currentAmount = ReferenceEquals(current, from) ? 0.3f : riverWidenAmount;
                ((InternalNode) current).radius = current.Radius() + currentAmount * width;

                var widths = current.Edges().Last().Widths();
                widths[0] += currentAmount * width;
                widths[1] += currentAmount * width;
                ((InternalEdge) current.Edges().Last()).SetWidths(widths);

                last = current;
                current = current.Edges().DestinationEndpoint();
            }
        }

        private bool ProcessRiverEndingInLake(List<RiverCandidate> candidates, ConsistentRandom rnd)
        {
            // find the size and depth of the lake and adjust overlapping sections
            RemoveCarvingCandidates(candidates);

            var lakeCenter = candidates[candidates.Count - 1].position;
            var lakeHeightLimit = candidates.Count == 1 ? options.heightMax : candidates[1].position.y;
            var initialLakeHeight = Mathf.Min(heightFunc.Invoke(lakeCenter.V2()), lakeHeightLimit);

            lakeCenter.y = initialLakeHeight;

            var outline = new List<Vector3>();

            // some possible point on the outline of the lake that might work as exit for a new river
            Vector3? lakeExit;
            Vector3? lakeExitDirection;

            var width = candidates[candidates.Count - 1].width;
            var maxLakeRadius = LakeSizeLimit(width);

            // when using LakeMode.OnlyLakes, the min lake size may be quite small since no river must be connected to it
            var minLakeRadius = ((Options) options).lakeMode == LakeMode.OnlyLakes
                ? Mathf.Max(3, width)
                : width * 2.5f;

            // abort when intersecting with an existing lake or adjust maxLakeRadius so lakes won't overlap
            if (ConflictsWithExistingLake(lakeCenter, minLakeRadius, ref maxLakeRadius, candidates))
            {
                Log.Debug(this, () => "Aborting Lake at " + lakeCenter + " due to conflict with existing lakes.");
                return false;
            }

            // try to fill new lake
            var lake = lakeHelper.FillLake(lakeCenter, lakeHeightLimit, maxLakeRadius, minLakeRadius, ref outline, out lakeExit,
                out lakeExitDirection);

            if (!lake.HasValue) return false;

            if (lakeExit.HasValue && IntersectsRiverCandidates(candidates, lakeExit.Value, lakeExitDirection.Value))
            {
                // discard lakeExit, if the outflowing river would intersect the inflowing river
                lakeExit = null;
                lakeExitDirection = null;
            }

            lakeCenter = new Vector3(lake.Value.x, lake.Value.y, lake.Value.z);

            var outlineV2 = outline.Select(v => v.V2()).ToList();

            return ProcessCandidatesInLake(candidates, lakeCenter, lake.Value.w, outlineV2, lakeExit, lakeExitDirection, rnd);
        }

        private bool ConflictsWithExistingLake(Vector3 lakeCenter, float minLakeRadius, ref float radiusLimit, List<RiverCandidate> candidates)
        {
            var lakeSearchRadius = radiusLimit + LakeSizeLimit(((Options) options).widthMax);
            var nodesInRange = graph.NodesInRange(lakeCenter.V2(), lakeSearchRadius, new[] { NodeType.Of(NodeBaseType.Lake) });
            var maxLakeRadiusByNodes = nodesInRange.Count == 0
                ? radiusLimit
                : nodesInRange.Min(n => (n.Position().V2() - lakeCenter.V2()).magnitude - n.Radius());

            // also check connections currently in creation
            var lakeCandidates = candidates.OfType<LakeCandidate>().ToList();
            var maxLakeRadiusByCandidates = lakeCandidates.Count == 0
                ? radiusLimit
                : lakeCandidates.Min(can => (can.position.V2() - lakeCenter.V2()).magnitude - can.radius);

            // update max allowed lake radius
            radiusLimit = Mathf.Min(maxLakeRadiusByNodes, maxLakeRadiusByCandidates) * 0.9f;

            // discard lake when it would be too small
            return radiusLimit < minLakeRadius;
        }

        private bool IntersectsRiverCandidates(List<RiverCandidate> candidates, Vector3 lakeExit, Vector3 lakeExitDirection)
        {
            if (candidates.Count == 0) return false;

            var last = candidates[0];
            var outflowingRiverPosition = lakeExit + lakeExitDirection * 50;

            for (var i = 1; i < candidates.Count; i++)
            {
                if (stopFunc.Invoke()) return false;
                var current = candidates[i];

                if (Util.Intersect(last.position.V2(), current.position.V2(), lakeExit.V2(), outflowingRiverPosition.V2()).HasValue)
                    return true;

                last = current;
            }

            return false;
        }

        private float LakeSizeLimit(float width)
        {
            var clampedWidth = Mathf.Clamp(width, ((Options) options).dryUpWidth, ((Options) options).widthMax);
            return (clampedWidth * clampedWidth) * ((Options) options).lakeSizeFactor;
        }

        private bool ProcessCandidatesInLake(List<RiverCandidate> candidates, Vector3 lakeCenter, float radius, List<Vector2> outline,
            Vector3? lakeExit, Vector3? lakeExitDirection, ConsistentRandom rnd)
        {
            var lakeCandidate = new LakeCandidate(candidates[candidates.Count - 1], radius, outline)
            {
                position = lakeCenter,
                lakeExit = lakeExit,
                lakeExitDirection = lakeExitDirection
            };

            if (candidates.Count == 1)
            {
                candidates[0] = lakeCandidate;
                return true;
            } else
            {
                // adjust all candidates that are lower than the lake currently (i.e. inside the lake)
                var adjusted = AdjustCandidatesInLake(candidates, lakeCenter, outline, out var adjustedOutline, rnd);
                lakeCandidate.outline = adjustedOutline;

                if (adjusted) candidates[candidates.Count - 1] = lakeCandidate;
                return adjusted;
            }
        }

        private bool AdjustCandidatesInLake(List<RiverCandidate> candidates, Vector3 lakeCenter, List<Vector2> outline,
            out List<Vector2> adjustedOutline, ConsistentRandom rnd)
        {
            var beforeLakeIndex = candidates.Count - 1;

            for (var i = candidates.Count - 2; i >= 0; i--)
            {
                var candidate = candidates[i];

                if (candidate.position.y > lakeCenter.y &&
                    outline.Min(o => (o - candidate.position.V2()).magnitude) > candidate.SectionLength(((Options) options).sectionLengthFactor))
                {
                    // candidate is high enough and at least one section length away from the lake outline
                    beforeLakeIndex = i;
                    break;
                }
            }

            if (beforeLakeIndex < 2)
            {
                // not valid river -> discard
                adjustedOutline = outline;
                return false;
            }

            // find lake entry point
            var beforeLake = candidates[beforeLakeIndex];
            var direction = (lakeCenter - beforeLake.position).normalized;
            direction.y = 0;

            var lakeEntry = beforeLake.position;

            do lakeEntry += direction;
            while (heightFunc.Invoke(lakeEntry.V2()) > lakeCenter.y);

            var lakeEntryV2 = lakeEntry.V2();
            var outlineIndex = FindClosest(lakeEntryV2, outline, out lakeEntryV2);
            lakeEntry = lakeEntryV2.V3(lakeCenter.y);

            var lakeEntryCandidate = new RiverCandidate(lakeEntry, beforeLake.width, NodeType.Of(NodeBaseType.RiverPerimeter),
                belongsToLast: true, fixControlHeight: true);

            // insert the new point if there is enough space for it, else replace the beforeLake candidate
            if ((lakeEntry - beforeLake.position).magnitude > beforeLake.SectionLength(((Options) options).sectionLengthFactor) / 2)
            {
                candidates.Insert(beforeLakeIndex + 1, lakeEntryCandidate);
            } else
            {
                candidates[beforeLakeIndex] = lakeEntryCandidate;
                // adjust this so the logic below works
                beforeLakeIndex--;
            }

            // insert outerExit point
            var directionToOuter = (candidates[beforeLakeIndex].position - lakeEntry).normalized;
            var outerExit = lakeEntry + directionToOuter * (beforeLake.SectionLength(((Options) options).sectionLengthFactor) / 2);
            var outerExitCandidate = new RiverCandidate(outerExit, beforeLake.width, NodeType.Of(NodeBaseType.LakeOuterExit), belongsToLast: true);

            candidates[beforeLakeIndex] = outerExitCandidate;

            // insert intermediate sections before outerExit if needed
            var newCandidates = FillRiverCandidates(candidates[beforeLakeIndex - 1],
                candidates[beforeLakeIndex], -directionToOuter, null, rnd);
            foreach (var newCandidate in newCandidates)
            {
                candidates.Insert(beforeLakeIndex, newCandidate);
                beforeLakeIndex++;
            }

            // clear the trailing candidates and recreate them
            var firstInLakeIndex = beforeLakeIndex + 2;
            candidates.RemoveRange(firstInLakeIndex, candidates.Count - firstInLakeIndex);

            var totalLength = (lakeEntry - lakeCenter).magnitude;
            var sections = Mathf.Max(3, Mathf.Ceil(totalLength / beforeLake.SectionLength(((Options) options).sectionLengthFactor)));

            // find direction that goes straight into the lake and stays away from the border
            var intoLakeDirection = LakeHelper.GetIntoLakeDirection(outline, outlineIndex, lakeEntry.V2()).V3();

            var lastPos = lakeEntry.V2();
            var riverSectionType = NodeType.Of(NodeBaseType.RiverSection);

            for (var i = 2f; i <= sections; i++)
            {
                var positionV2 = FindNextPositionInLake(lastPos,
                    -totalLength / sections * Vector3.Lerp(intoLakeDirection, -direction, i / sections).V2(), outline);
                var position = positionV2.V3(lakeCenter.y);

                if (graph.PositionIsFree(InternalNode.FindValidPosition(position, riverSectionType, options.resolution)))
                {
                    // omit nodes inside the lake that would be on the same position as already existing nodes
                    candidates.Add(new RiverCandidate(position,
                        Mathf.Max(((Options) options).dryUpWidth, beforeLake.width * ((sections + 1 - i) / sections)),
                        riverSectionType, belongsToLast: true));
                }

                lastPos = positionV2;
            }

            candidates[firstInLakeIndex].type = NodeType.Of(NodeBaseType.LakeInnerExit);

            // adjust lake outline to extend a bit into towards the joining river
            adjustedOutline = new List<Vector2>(outline)
            {
                [outlineIndex] = outline[outlineIndex] + (directionToOuter.V2() * lakeEntryCandidate.width * 0.7f),
                [(outlineIndex + 1) % outline.Count] = outline[(outlineIndex + 1) % outline.Count]
                                                       + (directionToOuter.V2() * lakeEntryCandidate.width * 0.35f),
                [(outlineIndex - 1 + outline.Count) % outline.Count] = outline[(outlineIndex - 1 + outline.Count) % outline.Count]
                                                                       + (directionToOuter.V2() * lakeEntryCandidate.width * 0.35f)
            };

            return true;
        }

        private InternalConnection AddRiverEdges(Node from, List<RiverCandidate> candidates, ConnectionData connectionData, RiverEnd riverEnd,
            Random rnd)
        {
            Log.Debug(this, () => "River end: " + riverEnd);

            var lastNode = from;

            var finalCandidate = candidates[candidates.Count - 1];
            var riverEndCandidate = finalCandidate as RiverJoinCandidate;
            var lakeEndCandidate = finalCandidate as LakeJoinCandidate;
            var finalNode = (InternalNode) (riverEndCandidate != null
                ? riverEndCandidate.existingNode
                : lakeEndCandidate != null
                    ? lakeEndCandidate.existingNode
                    : new InternalNode(rnd.NextGuid(finalCandidate.position).ToString(), finalCandidate.position, finalCandidate.width / 2,
                        finalCandidate.type, null, options.resolution));

            if (riverEnd != RiverEnd.JoinLake && riverEnd != RiverEnd.JoinRiver)
            {
                finalNode.type = GetNodeType(riverEnd);

                if (riverEnd == RiverEnd.Lake || riverEnd == RiverEnd.Sea)
                {
                    var lakeCandidate = finalCandidate as LakeCandidate;
                    finalNode.radius = lakeCandidate.radius;
                    finalNode.AddData(Constants.LakeOutline, JsonHelper.ToJson(lakeCandidate.outline.ToArray()));
                }
            }

            // create connection when destination node exists
            connectionData.connection = new InternalConnection(rnd.NextGuid(from.Position()).ToString(),
                ConnectionType.Of(ConnectionBaseType.River), Directions.OneWayForward, true);

            for (var i = 1; i < candidates.Count - 1; i++)
            {
                if (stopFunc.Invoke()) return connectionData.connection;
                var candidate = candidates[i];
                var belongsTo = candidate.belongsToFirst ? from : candidate.belongsToLast ? finalNode : null;

                var width = candidate.width * (1 + rnd.NextFloat() * ((Options) options).widthNoise);
                var radius = width / 2;
                connectionData.fallbackWidth = width;

                var sectionNode = new InternalNode(rnd.NextGuid(candidate.position).ToString(), candidate.position, radius, candidate.type,
                    belongsTo, options.resolution);

                NewEdgesWithBorder(lastNode, sectionNode, false, candidate.fixControlHeight,
                    lastNode.BelongsTo(), connectionData, rnd);

                lastNode = sectionNode;
            }

            NewEdgesWithBorder(lastNode, finalNode, false, finalCandidate.fixControlHeight,
                lastNode.BelongsTo(), connectionData, rnd);

            // adjust control points at edges that have a big slope difference
            var edges = connectionData.connection.Edges();

            for (var i = 0; i < edges.Count; i++)
            {
                if (stopFunc.Invoke()) return connectionData.connection;
                var edge = edges[i];
                var controlDirection = ((InternalBezierPoint) edge.BezierCurve().Source()).ControlV3();
                var controlToGround = controlDirection.AngleToGround();
                var edgeToGround = edge.BezierCurve().V3().AngleToGround();
                var angleDifference = Mathf.Abs(controlToGround - edgeToGround);
                var d = Mathf.Pow((90 - angleDifference) / 90, 2);
                ((InternalBezierPoint) edge.BezierCurve().Source()).controlPosition = edge.BezierCurve().Source().Position() + controlDirection * d;

                // align controls at perimeter into the sea/lake
                if (edge.Destination().Type().BaseType == NodeBaseType.RiverPerimeter)
                {
                    var nextEdge = edges[i + 1];

                    var outflowing = nextEdge.Destination().Type().BaseType == NodeBaseType.LakeOuterExit
                                     || (nextEdge.Destination().Type().BaseType == NodeBaseType.RiverBorder
                                         && edges[i + 2].Destination().Type().BaseType == NodeBaseType.LakeOuterExit);

                    var direction = (outflowing ? edge : nextEdge).BezierCurve().V3() / 3;

                    ((InternalBezierPoint) edge.BezierCurve().Destination()).controlPosition =
                        edge.BezierCurve().Destination().Position() - direction;
                    ((InternalBezierPoint) nextEdge.BezierCurve().Source()).controlPosition = nextEdge.BezierCurve().Source().Position() + direction;
                }
            }

            if (riverEnd == RiverEnd.JoinRiver && riverEndCandidate != null)
            {
                // adjust the control position to match the joined river
                var edge = edges[edges.Count - 1];

                // align controls with joined river direction
                var startControlPosition = edge.BezierCurve().Source().ControlPosition();
                startControlPosition.y = (edge.BezierCurve().Source().Position()
                                          + (edge.BezierCurve().Destination().Position() - edge.BezierCurve().Source().Position()).normalized
                                          * (edge.BezierCurve().Source().ControlPosition() - edge.BezierCurve().Source().Position()).magnitude).y;
                ((InternalBezierPoint) edge.BezierCurve().Source()).controlPosition = startControlPosition;

                var endControlPosition = edge.BezierCurve().Destination().ControlPosition();
                endControlPosition.y = (edge.BezierCurve().Destination().Position()
                                        + (edge.BezierCurve().Source().Position() - edge.BezierCurve().Destination().Position()).normalized
                                        * (edge.BezierCurve().Destination().ControlPosition() - edge.BezierCurve().Destination().Position())
                                        .magnitude).y;
                ((InternalBezierPoint) edge.BezierCurve().Destination()).controlPosition = endControlPosition;
            }

            return connectionData.connection;
        }

        private List<Connection> GenerateOutflowingRiverIfFeasible(List<RiverCandidate> candidates, Node lake)
        {
            var lastCandidate = candidates[candidates.Count - 1];

            var lakeCandidate = lastCandidate as LakeCandidate;
            if (lakeCandidate == null) return new List<Connection>();

            if (lakeCandidate.lakeExit.HasValue && lakeCandidate.lakeExitDirection.HasValue)
                return GenerateOutflowingRiver(candidates, lake, lakeCandidate.lakeExit.Value,
                    lakeCandidate.lakeExitDirection.Value, lastCandidate.SectionLength(((Options) options).sectionLengthFactor));

            return new List<Connection>();
        }

        private void RemoveCarvingCandidates(List<RiverCandidate> previous)
        {
            var lakeThreshold = previous.Count - 1;

            while (lakeThreshold > 1 && previous[lakeThreshold].carving) lakeThreshold--;

            previous.RemoveRange(lakeThreshold + 1, previous.Count - lakeThreshold - 1);
        }

        private List<Connection> GenerateOutflowingRiver(List<RiverCandidate> previous, Node lakeCenter, Vector3 lakeExit,
            Vector3 lakeExitDirection, float sectionLength)
        {
            if (previous.Count > 1)
            {
                // we don't want to go backwards here, so the direction must differ from where we came from by at least outflowingRiverAngleMin°
                var backwardsDirection = previous[previous.Count - 2].position - previous[previous.Count - 1].position;
                if (Vector2.Angle(backwardsDirection.V2(), lakeExitDirection.V2()) < ((Options) options).outflowingRiverAngleMin)
                    return new List<Connection>();
            }

            // possible exit
            var lakeSizeAmount = lakeCenter.Radius() / LakeSizeLimit(((Options) options).widthMax);
            var exitWidth = Mathf.Lerp(((Options) options).dryUpWidth, ((Options) options).widthMax,
                ((Options) options).outflowingRiverSizeFalloff.EvaluateClamped(lakeSizeAmount));

            var exitDirection = lakeExitDirection * sectionLength;
            var generateRiver = false;

            for (var i = 1; i <= 3; i++)
            {
                var afterLake = lakeExit + exitDirection * i;
                var height = heightFunc.Invoke(afterLake.V2());
                if (height < lakeExit.y)
                {
                    generateRiver = true;
                    break;
                }
            }

            // no good enough exit found
            if (!generateRiver) return new List<Connection>();

            var afterLakeExit = lakeExit + exitDirection * 0.7f;

            // generate new river from here
            var exitRiverCandidates = new List<RiverCandidate>();

            Log.Debug(this, () => "New outflowing river starting at " + lakeExit + ". LakeOuterExit: " + afterLakeExit);

            // set the lake as first new candidate
            exitRiverCandidates.Add(previous[previous.Count - 1]);

            ConnectLakeCenterToExit(lakeCenter.Position(), lakeExit, sectionLength, exitWidth, ref exitRiverCandidates,
                lakeCenter.GetData<Vector2>(Constants.LakeOutline), previous);

            exitRiverCandidates[exitRiverCandidates.Count - 1].type = NodeType.Of(NodeBaseType.LakeInnerExit);

            exitRiverCandidates.Add(new RiverCandidate(lakeExit, exitWidth, NodeType.Of(NodeBaseType.RiverPerimeter), belongsToFirst: true));
            exitRiverCandidates.Add(new RiverCandidate(afterLakeExit, exitWidth, NodeType.Of(NodeBaseType.LakeOuterExit)));

            return GenerateRiver(lakeCenter, exitRiverCandidates);
        }

        private void ConnectLakeCenterToExit(Vector3 lakeCenter, Vector3 lakeExit, float sectionLength, float width,
            ref List<RiverCandidate> candidates, List<Vector2> outline, List<RiverCandidate> previous)
        {
            var delta = lakeExit - lakeCenter;
            var direction = delta.normalized;
            var distance = delta.magnitude;
            var sections = (int) Mathf.Max(3, Mathf.Ceil(distance / sectionLength));

            var lakeExitV2 = lakeExit.V2();
            var outlineIndex = outline.IndexOf(lakeExitV2);
            var intoLakeDirection = LakeHelper.GetIntoLakeDirection(outline, outlineIndex, lakeExitV2).V3();

            var reverseCandidates = new List<RiverCandidate>();

            var lastPos = lakeExitV2;
            var riverSectionType = NodeType.Of(NodeBaseType.RiverSection);

            for (var i = 1f; i < sections; i++)
            {
                if (stopFunc.Invoke()) return;
                var positionV2 = FindNextPositionInLake(lastPos,
                    -distance / sections * Vector3.Lerp(intoLakeDirection, direction, i / sections).V2(), outline);
                var position = positionV2.V3(lakeExit.y);

                if (graph.PositionIsFree(InternalNode.FindValidPosition(position, riverSectionType, options.resolution))
                    && !previous.Any(pc => InternalNode.FindValidPosition(pc.position, riverSectionType, options.resolution)
                        .Equals(InternalNode.FindValidPosition(position, riverSectionType, options.resolution))))
                {
                    // omit nodes inside the lake that would be on the same position as already existing nodes
                    reverseCandidates.Add(new RiverCandidate(position,
                        Mathf.Max(((Options) options).dryUpWidth, width * ((sections + 1 - i) / sections)),
                        riverSectionType, belongsToFirst: true));
                }

                lastPos = positionV2;
            }

            reverseCandidates.Reverse();
            candidates.AddRange(reverseCandidates);
        }

        private static Vector2 FindNextPositionInLake(Vector2 source, Vector2 direction, List<Vector2> outline)
        {
            const float angleStep = 10f;

            var candidates = new List<Vector2>();

            for (var i = -5; i <= 5; i++)
            {
                var rotatedDirection = (Quaternion.Euler(0f, i * angleStep, 0f) * direction.V3()).V2();
                candidates.Add(source + rotatedDirection);
            }

            var nextPosition = candidates.Where(v => v.InsidePolygon(outline))
                // order by distance to outline
                .MaxBy(v => outline.Min(o => (o - v).magnitude));

            return nextPosition.Equals(Vector2.zero)
                // fallback to just using the direction as is
                ? source + direction
                : nextPosition;
        }

        public static int FindClosest(Vector2 from, List<Vector2> candidates, out Vector2 candidate,
            Func<Vector2, Vector2, bool> collisionHandler = null)
        {
            var best = candidates[0];
            var bestDistance = (best - from).magnitude;
            var bestIndex = 0;

            for (var i = 1; i < candidates.Count; i++)
            {
                var distance = (candidates[i] - from).magnitude;
                if (distance < bestDistance
                    || collisionHandler != null && Math.Abs(distance - bestDistance) < 0.1f && collisionHandler.Invoke(best, candidates[i]))
                {
                    best = candidates[i];
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            candidate = best;
            return bestIndex;
        }

        private Vector3 FindControlAction(Node from, Node to, float controlDistance, ConnectionData connectionData, bool fixHeight, Random rnd)
        {
            var direction = (to.Position() - from.Position()).normalized;

            if (from.Type().BaseType != NodeBaseType.RiverPerimeter)
            {
                var angle = rnd.NextFloat(-30, 30);
                direction = Quaternion.Euler(0, angle, 0) * direction;
            }

            return from.Position() + direction * controlDistance;
        }

        private RiverCandidate FlowDownhill(Vector3 from, List<RiverCandidate> previous, float sectionLength, float width)
        {
            // find the most downhill direction
            var candidates = new List<Vector3>();
            var direction = (previous.Count < 2 ? Vector3.forward : (from - previous[previous.Count - 2].position).normalized) * sectionLength;

            var opt = (Options) options;

            var widthAmount = (width - opt.dryUpWidth) / (opt.widthMax - opt.dryUpWidth);
            var directionChangeAngle = previous.Count < 2
                ? 180 // allow any direction for the first section
                : opt.riverDirectionChangeAngleMin + opt.riverDirectionChangeAngleFalloff.EvaluateClamped(widthAmount) *
                (opt.riverDirectionChangeAngleMax - opt.riverDirectionChangeAngleMin);

            var step = Mathf.Clamp(directionChangeAngle / 36, 2, 5);
            var steps = (int) (directionChangeAngle / step);

            for (var i = 1; i < steps; i++)
            {
                var rotated = Quaternion.Euler(0, step * i, 0) * direction;
                candidates.Add(from + rotated);

                var rotatedInverse = Quaternion.Euler(0, -step * i, 0) * direction;
                candidates.Add(from + rotatedInverse);
            }

            var candidatesWithHeights = FillHeights(candidates);

            var lowestCandidate = candidatesWithHeights[0];

            candidatesWithHeights.ForEach(c =>
            {
                if (previous.Count > 2 && (c - previous[previous.Count / 2].position).magnitude <
                    (previous[previous.Count - 1].position - previous[previous.Count / 2].position).magnitude) return;
                if (c.y < lowestCandidate.y) lowestCandidate = c;
            });

            return new RiverCandidate(lowestCandidate, width, NodeType.Of(NodeBaseType.RiverSection));
        }

        private List<Vector3> FillHeights(List<Vector3> candidates)
        {
            return candidates.Select(c => new Vector3(c.x, heightFunc.Invoke(c.V2()), c.z)).ToList();
        }

        private NodeType GetNodeType(RiverEnd riverEnd)
        {
            switch (riverEnd)
            {
                case RiverEnd.JoinRiver:
                    return NodeType.Of(NodeBaseType.RiverCrossing);
                case RiverEnd.JoinLake:
                case RiverEnd.Lake:
                    return NodeType.Of(NodeBaseType.Lake);
                case RiverEnd.DryUp:
                    return NodeType.Of(NodeBaseType.RiverDryUp);
                case RiverEnd.Sea:
                    return NodeType.Of(NodeBaseType.Sea);
                default:
                    throw new ArgumentOutOfRangeException("riverEnd", riverEnd, null);
            }
        }

        protected override NodeType BorderType()
        {
            return NodeType.Of(NodeBaseType.RiverBorder);
        }

        protected override float InterpolatedWidthAtBorder(Node source, Node destination, float progressAtBorder)
        {
            if (source.Type().BaseType == NodeBaseType.Lake) return destination.Radius() * 2;
            if (destination.Type().BaseType == NodeBaseType.Lake) return source.Radius() * 2;

            return Mathf.Lerp(source.Radius() * 2, destination.Radius() * 2, progressAtBorder);
        }

        protected override float NextControlDistance(Node source, Node destination, Random rnd)
        {
            return source.StraightDistanceTo(destination) / 2.2f;
        }
    }
}
