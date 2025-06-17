using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace ArctiumStudios.SplineTools
{
    public class ScattererV2
    {
        public class Options
        {
            public readonly int terrainSize;

            public int countMax = 10;
            public int iterations = 10;
            public float safeBorders = 2;
            public float distanceToSameMin = 250;
            public float distanceToOthersMin = 250;
            public bool distanceLimit = true;
            public float heightMin = 0;
            public float heightMax = 300;
            public Placement placement = Placement.Random;
            public float radiusMin = 40;
            public float radiusMax = 50;
            public AnimationCurve radiusFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));
            public bool deviationLimit = false;
            public float deviationUpMax = 5f;
            public float deviationDownMax = 5f;
            public int deviationResolution = 2;
            public float distanceToLakePerimeterMin = 50f;
            public float distanceToRiverMin = 50f;

            public Options(int terrainSize)
            {
                this.terrainSize = terrainSize;
            }
        }

        private readonly Options options;
        private readonly InternalWorldGraph graph;
        private readonly Func<Vector2, float> heightFunc;
        private readonly Func<Vector2, float> maskFunc;
        private readonly Random rnd;
        private readonly Func<bool> stopFunc;

        private readonly InternalWorldGraph riverGraph;
        private readonly float lakeRadiusMax;
        private readonly float riverRadiusMax;
        private readonly NodeType[] riverTypes;
        private readonly NodeType[] lakeTypes;

        public ScattererV2(Options options, InternalWorldGraph graph, InternalWorldGraph riverGraph,
            Func<Vector2, float> heightFunc, Func<Vector2, float> maskFunc, int seed, Func<bool> stopFunc)
        {
            this.options = options;
            this.graph = graph;
            this.riverGraph = riverGraph;
            this.heightFunc = heightFunc;
            this.maskFunc = maskFunc;
            this.stopFunc = stopFunc;
            //initializing random
            rnd = new ConsistentRandom(seed);

            riverTypes = NodeType.riverBaseTypes.Select(t => NodeType.Of(t)).ToArray();
            lakeTypes = new[] {NodeType.Of(NodeBaseType.Lake)};
            lakeRadiusMax = RadiusMax(riverGraph, lakeTypes);
            riverRadiusMax = RadiusMax(riverGraph, riverTypes);

            // use absolute minimum of 1.5;
            if (!options.distanceLimit)
            {
                const float minimumDistance = 1.5f;
                options.distanceToSameMin = minimumDistance;
                options.distanceToOthersMin = minimumDistance;
                options.distanceToLakePerimeterMin = minimumDistance;
                options.distanceToRiverMin = minimumDistance;
            }
        }

        private static float RadiusMax(WorldGraph riverGraph, NodeType[] nodeTypes)
        {
            if (riverGraph == null) return 0;
            return riverGraph.Nodes(nodeTypes).Max(n => n.Radius());
        }

        public void GlobalRandomScatter(Rect globalRect, NodeType type, Node belongsTo)
        {
            var candidatesByRectOffset = new Dictionary<Offset, List<Vector4>>();

            for (var i = 0; i < options.countMax; i++)
            {
                var newCandidates = new List<Vector4>();

                Vector4? bestCandidate = null;

                for (var c = 0; c < options.iterations; c++)
                {
                    if (stopFunc.Invoke()) return;
                    var candidate = new Vector4(
                        Mathf.Floor(globalRect.xMin + 1 + rnd.NextFloat() * (globalRect.size.x - 2.01f)),
                        -1, // init height with -1
                        Mathf.Floor(globalRect.yMin + 1 + rnd.NextFloat() * (globalRect.size.y - 2.01f)),
                        Radius()
                    );

                    var candidateRectOffset = Offset.For(candidate.V3(), options.terrainSize);

                    // discard points outside of mask
                    if (maskFunc != null)
                    {
                        var maskValue = maskFunc.Invoke(candidate.V2());
                        if (maskValue <= 0 || rnd.NextFloat() >= maskValue) continue;
                    }

                    // discard points within safe borders
                    if (candidate.x < candidateRectOffset.x + options.safeBorders
                        || candidate.x > candidateRectOffset.x + options.terrainSize - options.safeBorders
                        || candidate.z < candidateRectOffset.z + options.safeBorders
                        || candidate.z > candidateRectOffset.z + options.terrainSize - options.safeBorders) continue;

                    // discard points too close to same types
                    if (DistanceToOtherCandidatesIsTooLow(candidate, options.distanceToSameMin, candidatesByRectOffset)) continue;

                    // discard points too close to other types
                    if (DistanceToOtherTypesIsTooLow(type, candidate, graph, graph.NodeTypes(), options.distanceToOthersMin)) continue;

                    // discard points too close to rivers & lakes
                    if (riverGraph != null &&
                        DistanceToWaterTypesIsTooLow(type, candidate, riverGraph, lakeTypes, options.distanceToLakePerimeterMin, lakeRadiusMax))
                        continue;
                    if (riverGraph != null &&
                        DistanceToWaterTypesIsTooLow(type, candidate, riverGraph, riverTypes, options.distanceToRiverMin, riverRadiusMax))
                        continue;

                    // fill height
                    candidate = new Vector4(candidate.x, heightFunc.Invoke(candidate.V2()), candidate.z, candidate.w);

                    // discard points with too much deviation inside radius
                    if (options.deviationLimit &&
                        DeviationIsTooHigh(candidate, options.deviationDownMax, options.deviationUpMax, options.deviationResolution)) continue;

                    newCandidates.Add(candidate);
                }

                if (newCandidates.Count == 0) continue;

                // var newCandidatesWithHeight = FillHeights(newCandidates);

                newCandidates.ForEach(candidate =>
                {
                    // any point is good for random placement, no further iterations needed
                    if (bestCandidate.HasValue && options.placement == Placement.Random) return;

                    // discard point out of height range
                    if (candidate.y < options.heightMin || candidate.y > options.heightMax) return;

                    switch (options.placement)
                    {
                        case Placement.Low:
                            if (bestCandidate.HasValue && candidate.y > bestCandidate.Value.y) return;
                            break;
                        case Placement.High:
                            if (bestCandidate.HasValue && candidate.y < bestCandidate.Value.y) return;
                            break;
                    }

                    bestCandidate = candidate;
                });

                if (!bestCandidate.HasValue) continue; // no candidate found

                var bestCandidateCoordOffset = Offset.For(bestCandidate.Value.V3(), options.terrainSize);

                List<Vector4> candidates;
                candidatesByRectOffset.TryGetValue(bestCandidateCoordOffset, out candidates);

                if (candidates == null) candidates = new List<Vector4>();

                candidates.Add(bestCandidate.Value);
                candidatesByRectOffset[bestCandidateCoordOffset] = candidates;

                var node = new InternalNode(rnd.NextGuid(bestCandidate.Value).ToString(), bestCandidate.Value, bestCandidate.Value.w,
                    type, belongsTo, options.terrainSize);

                graph.StoreNode(node);
            }
        }

        private bool DistanceToOtherTypesIsTooLow(NodeType type, Vector4 candidate, WorldGraph graph, HashSet<NodeType> nodeTypes, float distanceMin)
        {
            nodeTypes.Remove(type);
            var relevantNodes = graph.NodesInRange(candidate.V2(), distanceMin, nodeTypes.ToArray());

            return relevantNodes.Count > 0;
        }

        private bool DistanceToWaterTypesIsTooLow(NodeType type, Vector4 candidate, WorldGraph graph, NodeType[] nodeTypes,
            float distanceMin, float radiusMax)
        {
            var relevantNodes = graph.NodesInRange(candidate.V2(), distanceMin + radiusMax, nodeTypes)
                .Where(n => (n.PositionV2() - candidate.V2()).magnitude - n.Radius() < distanceMin).ToList();

            return relevantNodes.Count > 0;
        }

        private bool DeviationIsTooHigh(Vector4 candidate, float optionsDeviationDownMax, float optionsDeviationUpMax, int optionsDeviationResolution)
        {
            var candidateV2 = candidate.V2();

            for (var x = (int) (candidate.x - candidate.w); x < (int) (candidate.x + candidate.w); x += optionsDeviationResolution)
            for (var z = (int) (candidate.z - candidate.w); z < (int) (candidate.z + candidate.w); z += optionsDeviationResolution)
            {
                var p = new Vector2(x, z);

                // skip points outside the actual radius
                if ((p - candidateV2).magnitude > candidate.w) continue;

                var height = heightFunc.Invoke(p);
                if (height < candidate.y - optionsDeviationDownMax ||
                    height > candidate.y + optionsDeviationUpMax)
                    return true;
            }

            return false;
        }

        private bool DistanceToOtherCandidatesIsTooLow(Vector3 candidate, float minDistance, Dictionary<Offset, List<Vector4>> candidatesByOffset)
        {
            foreach (var listEntry in candidatesByOffset)
            foreach (var item in listEntry.Value)
                if (Vector2.Distance(candidate.V2(), item.V2()) < minDistance)
                    return true;

            return false;
        }


        private float Radius()
        {
            return options.radiusMin + (options.radiusFalloff.EvaluateClamped(rnd.NextFloat()) * (options.radiusMax - options.radiusMin));
        }
    }
}
