#if ST_MM_1 || ST_MM_2

#if ST_RAM_2019 || ST_RAM

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ArctiumStudios.SplineTools.RamSplineHelper;

namespace ArctiumStudios.SplineTools
{
    public class RamRiverHelper
    {
        public const string ContainerName = "RAM Rivers";
        public readonly string generatorGuid;

        private readonly Dictionary<Node, List<RamSpline>> splinesByBorderNode = new Dictionary<Node, List<RamSpline>>();

        public RamRiverHelper(string generatorGuid)
        {
            this.generatorGuid = generatorGuid;
        }

        public static List<Node> FindBorderNodes(EdgesByOffset connections, Rect worldRect)
        {
            return RamSplineHelper.FindBorderNodes(connections, worldRect, NodeBaseType.RiverBorder);
        }

        public IEnumerator CreateRiverSections(int tileResolution, int heightMapResolution, int maskMargin, float pixelSize, Vector2 cellOffset,
            RamRiverOutputData outputData, SplineProfile profile, Transform parent)
        {
            var newSplines = new List<RamSpline>();
            var splinesByMidCrossingNode = new Dictionary<Node, List<RamSpline>>();
            var splinesByEndCrossingNode = new Dictionary<Node, List<RamSpline>>();

            var container = GetContainer(parent, generatorGuid, false);

            if (container != null)
            {
                // cleanup possibly existing duplicate rivers
                CleanupRivers(container);
                GameObject.DestroyImmediate(container.gameObject);
            }

            container = GetContainer(parent, generatorGuid, true);

            yield return null;

            var options = new EdgeWalker<float[,]>.Options(tileResolution, heightMapResolution, pixelSize, cellOffset)
            {
                falloff = new AnimationCurve(new Keyframe(0.1f, 0, 1, 0), new Keyframe(0.9f, 1, 0, 1))
            };

            var edgeWalker = new FlatEdgeWalker(options, () => false);
            RamSpline previousSpline = null;

            foreach (var riverSection in outputData.riverSections)
            {
                var subsectionStart = riverSection.edges.SourceEndpoint();
                var subsectionEnd = riverSection.edges.DestinationEndpoint();

                var ramSpline = CreateRamSpline(container, riverSection.markers, profile);
                newSplines.Add(ramSpline);

                // remember the spline for all found crossings to connect them later
                foreach (var crossing in riverSection.midCrossings) AddSplineByNode(crossing, ramSpline, splinesByMidCrossingNode);
                foreach (var crossing in riverSection.endCrossings) AddSplineByNode(crossing, ramSpline, splinesByEndCrossingNode);

                // fade in at the start
                if (outputData.fadeIn && subsectionStart.Type().IsEndpoint() && subsectionStart.Type().BaseType != NodeBaseType.LakeInnerExit &&
                    subsectionStart.Type().BaseType != NodeBaseType.Lake)
                    FadeInSpline(ramSpline);

                // fade out at DryUp
                if (outputData.fadeOut && subsectionEnd.Type().BaseType == NodeBaseType.RiverDryUp)
                    FadeOutSpline(ramSpline);

                // fade at lakes
                if (outputData.seaMasks.Count != 0 && riverSection.edges.SelectMany(e => e.Nodes())
                        .Any(n => n.BelongsTo() != null && n.BelongsTo().Type().BaseType == NodeBaseType.Lake))
                    FadeAtLake(ramSpline, outputData.lakeMasks, tileResolution, heightMapResolution, maskMargin);

                // fade at sea
                if (outputData.seaMasks.Count != 0 && riverSection.edges.SelectMany(e => e.Nodes())
                        .Any(n => n.BelongsTo() != null && n.BelongsTo().Type().BaseType == NodeBaseType.Sea))
                    FadeAtLake(ramSpline, outputData.seaMasks, tileResolution, heightMapResolution, maskMargin);

                lock (splinesByBorderNode)
                {
                    if (splinesByBorderNode.ContainsKey(subsectionStart))
                    {
                        var otherSpline = splinesByBorderNode[subsectionStart].First();
                        SetEndingSpline(otherSpline, ramSpline);
                    }

                    if (splinesByBorderNode.ContainsKey(subsectionEnd))
                    {
                        var otherSpline = splinesByBorderNode[subsectionEnd].First();
                        SetEndingSpline(ramSpline, otherSpline);
                    }

                    // remember borders to connect splines of different chunks
                    StoreSplineByNode(subsectionStart, ramSpline, NodeBaseType.RiverBorder, splinesByBorderNode);
                    StoreSplineByNode(subsectionEnd, ramSpline, NodeBaseType.RiverBorder, splinesByBorderNode);

                    // remember lakeInnerExit to connect splines of different chunks
                    StoreSplineByNode(subsectionStart, ramSpline, NodeBaseType.LakeInnerExit, splinesByBorderNode);
                    StoreSplineByNode(subsectionEnd, ramSpline, NodeBaseType.LakeInnerExit, splinesByBorderNode);
                }

                // connect subdivided splines
                if (previousSpline != null && riverSection.connectToPrevious &&
                    previousSpline.controlPoints.Last().Equals(ramSpline.controlPoints.First()))
                {
                    SetEndingSpline(previousSpline, ramSpline);
                }

                previousSpline = ramSpline;

                yield return null;
            }

            // connect crossing splines
            ConnectCrossings(splinesByMidCrossingNode, splinesByEndCrossingNode, tileResolution, heightMapResolution, maskMargin, edgeWalker);

            foreach (var ramSpline in newSplines)
            {
                ramSpline.GenerateSpline();
                yield return null;
            }
        }

        public void Cleanup()
        {
            lock (splinesByBorderNode) splinesByBorderNode.Clear();
        }

        public static List<RamSplineSection> GetRiverSections(Rect rect, EdgesByOffset connections,
            float markerDistance, float widthFactor, float maxRamSplineLength, float crossingOffset, float heightOffset,
            Func<Node, int> getDominantBiomeIdx, Func<bool> stop)
        {
            return GetRamSplineSections(rect, connections, markerDistance, widthFactor, maxRamSplineLength, crossingOffset, heightOffset,
                IsImportantPointForMarker, getDominantBiomeIdx, stop);
        }

        public void CleanupRivers(Transform container)
        {
            foreach (Transform child in container)
            {
                var ramSpline = child.gameObject.GetComponent<RamSpline>();

                lock (splinesByBorderNode)
                {
                    foreach (var pair in splinesByBorderNode)
                        pair.Value.Remove(ramSpline);

                    var purged = splinesByBorderNode.Where(kv => kv.Value.Count != 0)
                        .ToDictionary(pair => pair.Key, pair => pair.Value);

                    splinesByBorderNode.Clear();
                    foreach (var kv in purged) splinesByBorderNode.Add(kv.Key, kv.Value);
                }
            }
        }

        public static Transform GetContainer(Transform parent, string generatorGuid, bool create = true)
        {
            return GetContainer(ContainerName, parent, generatorGuid, create);
        }

        public static Transform GetContainer(string containerName, Transform parent, string generatorGuid, bool create = true)
        {
            var container = parent.Find(containerName);

            if (container == null && !create) return null;

            if (container == null)
            {
                container = new GameObject(containerName).transform;
                container.parent = parent.transform;
            }

            var graphContainer = container.Find(generatorGuid);

            if (graphContainer != null || !create) return graphContainer;

            graphContainer = new GameObject(generatorGuid).transform;
            graphContainer.parent = container.transform;

            return graphContainer;
        }

        private static bool IsImportantPointForMarker(Node node)
        {
            return node.Type().BaseType == NodeBaseType.LakeInnerExit
                   || node.Type().BaseType == NodeBaseType.LakeOuterExit
                   || node.Type().BaseType == NodeBaseType.RiverBorder
                   || node.Type().BaseType == NodeBaseType.RiverPerimeter
                   || node.Type().BaseType == NodeBaseType.RiverCrossingPerimeter;
        }

        public static void ConnectCrossings(Dictionary<Node, List<RamSpline>> splinesByMidCrossingNode,
            Dictionary<Node, List<RamSpline>> splinesByEndCrossingNode, int tileResolution,
            int heightMapResolution, int maskMargin, EdgeWalker<float[,]> edgeWalker)
        {
            RamSplineHelper.ConnectCrossings(splinesByMidCrossingNode, splinesByEndCrossingNode, true,
                (joinedSpline, joiningSpline, crossingNode) => PostProcessConnectedCrossing(tileResolution, heightMapResolution,
                    maskMargin, edgeWalker, joinedSpline, joiningSpline, crossingNode));
        }

        private static void PostProcessConnectedCrossing(int tileResolution, int heightMapResolution, int maskMargin, EdgeWalker<float[,]> edgeWalker,
            RamSpline joinedSpline, RamSpline joiningSpline, Node crossingNode)
        {
            // need to generate after rotation to rearrange vertices
            joiningSpline.GenerateSpline();

            // raise preceding points that would be lower that the raised points to prevent the river from flowing uphill
            var minHeight = joiningSpline.controlPoints[joiningSpline.controlPoints.Count - 2].y;

            var index = joiningSpline.controlPoints.Count - 3;

            while (index >= 0 && joiningSpline.controlPoints[index].y < minHeight)
            {
                var pos = joiningSpline.controlPoints[index];
                pos.y = minHeight;
                joiningSpline.controlPoints[index] = pos;
                index--;
            }

            // fade out joining spline
            FadeOutSplineTowardsCrossing(joiningSpline, crossingNode, tileResolution, heightMapResolution, maskMargin, edgeWalker);

            // widen up joinedSpline a bit at the crossing
            var crossingPosition = crossingNode.Position();
            var joinPoint = joinedSpline.controlPoints.MinBy(p => (new Vector3(p.x, p.y, p.z) - crossingPosition).magnitude);
            var joinIndex = joinedSpline.controlPoints.IndexOf(joinPoint);
            var controlPointJoined = joinedSpline.controlPoints[joinIndex];
            controlPointJoined.w *= 1.2f;
            joinedSpline.controlPoints[joinIndex] = controlPointJoined;
        }

        public static void FadeAtLake(RamSpline ramSpline, Dictionary<HeightMapOffset, float[,]> masks, int tileResolution, int heightMapResolution,
            int maskMargin)
        {
            var meshFilter = ramSpline.meshfilter;
            var vertices = meshFilter.sharedMesh.vertices;

            for (var i = 0; i < vertices.Length; i++)
            {
                var heightMapOffset = HeightMapOffset.For(vertices[i], tileResolution, heightMapResolution);

                if (!masks.ContainsKey(heightMapOffset)) continue;

                var mask = masks[heightMapOffset];

                SetAlphaFromMask(ramSpline, heightMapOffset, mask, vertices[i].V2(), i, vertices.Length, tileResolution,
                    heightMapResolution, maskMargin);
            }
        }

        public class RamRiverOutputData
        {
            public List<RamSplineSection> riverSections;

            public Dictionary<HeightMapOffset, float[,]> lakeMasks;
            public Dictionary<HeightMapOffset, float[,]> seaMasks;

            public bool fadeIn;
            public bool fadeOut;

            public RamRiverOutputData(List<RamSplineSection> riverSections, Dictionary<HeightMapOffset, float[,]> lakeMasks,
                Dictionary<HeightMapOffset, float[,]> seaMasks, bool fadeIn, bool fadeOut)
            {
                this.riverSections = riverSections;
                this.lakeMasks = lakeMasks;
                this.seaMasks = seaMasks;
                this.fadeIn = fadeIn;
                this.fadeOut = fadeOut;
            }
        }
    }
}

#endif
#endif
