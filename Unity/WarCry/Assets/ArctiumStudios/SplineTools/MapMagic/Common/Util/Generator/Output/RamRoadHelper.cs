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
    public class RamRoadHelper
    {
        public const string ContainerName = "RAM Roads";
        public readonly string generatorGuid;

        private readonly Dictionary<Node, List<RamSpline>> splinesByBorderNode = new Dictionary<Node, List<RamSpline>>();

        public RamRoadHelper(string generatorGuid)
        {
            this.generatorGuid = generatorGuid;
        }

        public IEnumerator CreateRoadSections(RamRoadOutputData outputData, SplineProfile profile, Transform parent)
        {
            var newSplines = new List<RamSpline>();
            var splinesByMidCrossingNode = new Dictionary<Node, List<RamSpline>>();
            var splinesByStartCrossingNode = new Dictionary<Node, List<RamSpline>>();
            var splinesByEndCrossingNode = new Dictionary<Node, List<RamSpline>>();


            var container = GetContainer(parent, generatorGuid, false);

            if (container != null)
            {
                // cleanup possibly existing duplicates
                CleanupRoads(container);
                GameObject.DestroyImmediate(container.gameObject);
            }

            container = GetContainer(parent, generatorGuid, true);

            yield return null;

            RamSpline previousSpline = null;

            foreach (var roadSection in outputData.roadSections)
            {
                var subsectionStart = roadSection.edges.SourceEndpoint();
                var subsectionEnd = roadSection.edges.DestinationEndpoint();

                var ramSpline = CreateRamSpline(container, roadSection.markers, profile);
                newSplines.Add(ramSpline);

                // remember the spline for all found crossings to connect them later
                foreach (var crossing in roadSection.midCrossings) AddSplineByNode(crossing, ramSpline, splinesByMidCrossingNode);
                foreach (var crossing in roadSection.startCrossings) AddSplineByNode(crossing, ramSpline, splinesByStartCrossingNode);
                foreach (var crossing in roadSection.endCrossings) AddSplineByNode(crossing, ramSpline, splinesByEndCrossingNode);

                // fade in
                if (subsectionStart.Type().IsEndpoint() && !subsectionStart.Type().IsPerimeter())
                    FadeInSpline(ramSpline);

                // fade out 
                if (subsectionEnd.Type().IsEndpoint() && !subsectionEnd.Type().IsPerimeter())
                    FadeOutSpline(ramSpline);


                lock (splinesByBorderNode)
                {
                    if (splinesByBorderNode.ContainsKey(subsectionStart))
                    {
                        var otherSpline = splinesByBorderNode[subsectionStart].First();
                        // SetBeginningSpline(ramSpline, otherSpline);
                        SetEndingSpline(otherSpline, ramSpline);
                    }

                    if (splinesByBorderNode.ContainsKey(subsectionEnd))
                    {
                        var otherSpline = splinesByBorderNode[subsectionEnd].First();
                        SetEndingSpline(ramSpline, otherSpline);
                    }

                    // remember borders to connect splines of different chunks
                    StoreSplineByNode(subsectionStart, ramSpline, NodeBaseType.Border, splinesByBorderNode);
                    StoreSplineByNode(subsectionEnd, ramSpline, NodeBaseType.Border, splinesByBorderNode);

                    // remember perimeters to connect splines
                    StoreSplineByNode(subsectionStart, ramSpline, NodeBaseType.Perimeter, splinesByBorderNode);
                    StoreSplineByNode(subsectionEnd, ramSpline, NodeBaseType.Perimeter, splinesByBorderNode);
                }

                // connect subdivided splines
                if (previousSpline != null && roadSection.connectToPrevious)
                {
                    // SetBeginningSpline(ramSpline, previousSpline);
                    SetEndingSpline(previousSpline, ramSpline);
                }

                previousSpline = ramSpline;

                yield return null;
            }

            // connect crossing splines
            ConnectCrossings(splinesByMidCrossingNode, splinesByStartCrossingNode, splinesByEndCrossingNode);

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

        public List<RamSplineSection> GetRoadSections(Rect rect, EdgesByOffset connections,
            float markerDistance, float widthFactor, float maxRamSplineLength, float crossingOffset, float heightOffset,
            Func<Node, int> getDominantBiomeIdx, Func<bool> stop)
        {
            return GetRamSplineSections(rect, connections, markerDistance, widthFactor, maxRamSplineLength, crossingOffset, heightOffset,
                IsImportantPointForMarker, getDominantBiomeIdx, stop);
        }

        public void CleanupRoads(Transform container)
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
            return RamRiverHelper.GetContainer(ContainerName, parent, generatorGuid, create);
        }

        private static bool IsImportantPointForMarker(Node node)
        {
            return node.Type().BaseType == NodeBaseType.Border
                   || node.Type().BaseType == NodeBaseType.Perimeter;
        }

        public static void ConnectCrossings(Dictionary<Node, List<RamSpline>> splinesByMidCrossingNode,
            Dictionary<Node, List<RamSpline>> splinesByStartCrossingNode, Dictionary<Node, List<RamSpline>> splinesByEndCrossingNode)
        {
            RamSplineHelper.ConnectCrossings(splinesByMidCrossingNode, splinesByStartCrossingNode, false);
            RamSplineHelper.ConnectCrossings(splinesByMidCrossingNode, splinesByEndCrossingNode, true);
        }

        public class RamRoadOutputData
        {
            public List<RamSplineSection> roadSections;

            public RamRoadOutputData(List<RamSplineSection> roadSections)
            {
                this.roadSections = roadSections;
            }
        }
    }
}

#endif
#endif
