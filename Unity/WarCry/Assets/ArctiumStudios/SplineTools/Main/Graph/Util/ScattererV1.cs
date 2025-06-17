using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace ArctiumStudios.SplineTools
{
    [Obsolete(message: "Use ScattererV2 instead")]
    public class ScattererV1
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

        public ScattererV1(Options options, InternalWorldGraph graph, Func<Vector2, float> heightFunc, Func<Vector2, float> maskFunc, int seed, 
            Func<bool> stopFunc)
        {
            this.options = options;
            this.graph = graph;
            this.heightFunc = heightFunc;
            this.maskFunc = maskFunc;
            this.stopFunc = stopFunc;
            //initializing random
            rnd = new ConsistentRandom(seed);
        }

        public void GlobalRandomScatter(Rect globalRect, NodeType type)
        {
            var candidatesByRectOffset = new Dictionary<Offset, List<Vector3>>();

            for (var i = 0; i < options.countMax; i++)
            {
                var newCandidates = new List<Vector3>();

                Vector3? bestCandidate = null;

                for (var c = 0; c < options.iterations; c++)
                {
                    if (stopFunc.Invoke()) return;
                    var candidate = new Vector3(
                        Mathf.Floor(globalRect.xMin + 1 + rnd.NextFloat() * (globalRect.size.x - 2.01f)),
                        -1, // init height with -1
                        Mathf.Floor(globalRect.yMin + 1 + rnd.NextFloat() * (globalRect.size.y - 2.01f))
                    );

                    var candidateRectOffset = Offset.For(candidate, options.terrainSize);

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
                    if (options.distanceLimit &&
                        DistanceIsTooLow(candidate, options.distanceToSameMin, candidatesByRectOffset)) continue;

                    // discard points too close to other types
                    var nodeTypes = graph.NodeTypes();
                    nodeTypes.Remove(type);
                    var relevantNodes = graph.NodesInRange(candidate.V2(), options.distanceToOthersMin, nodeTypes.ToArray());

                    if (options.distanceLimit && relevantNodes.Count > 0) continue;

                    newCandidates.Add(candidate);
                }

                if (newCandidates.Count == 0) continue;

                var newCandidatesWithHeight = FillHeights(newCandidates);

                newCandidatesWithHeight.ForEach(candidate =>
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

                var bestCandidateCoordOffset = Offset.For(bestCandidate.Value, options.terrainSize);

                List<Vector3> candidates;
                candidatesByRectOffset.TryGetValue(bestCandidateCoordOffset, out candidates);

                if (candidates == null) candidates = new List<Vector3>();

                candidates.Add(bestCandidate.Value);
                candidatesByRectOffset[bestCandidateCoordOffset] = candidates;

                var node = new InternalNode(rnd.NextGuid(bestCandidate.Value).ToString(), bestCandidate.Value, Radius(),
                    type, null, options.terrainSize);

                graph.StoreNode(node);
            }
        }

        private bool DistanceIsTooLow(Vector3 candidate, float minDistance, Dictionary<Offset, List<Vector3>> candidatesByOffset)
        {
            foreach (var listEntry in candidatesByOffset)
            foreach (var item in listEntry.Value)
                if (Vector2.Distance(candidate.V2(), item.V2()) < minDistance)
                    return true;

            return false;
        }

        private List<Vector3> FillHeights(List<Vector3> candidates)
        {
            return candidates.Select(c => new Vector3(c.x, heightFunc.Invoke(c.V2()), c.z)).ToList();
        }

        private float Radius()
        {
            return options.radiusMin + (options.radiusFalloff.EvaluateClamped(rnd.NextFloat()) * (options.radiusMax - options.radiusMin));
        }
    }
}