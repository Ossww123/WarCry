#if ST_MM_1 || ST_MM_2

#if ST_RAM_2019 || ST_RAM

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public static class RamSplineHelper
    {
        public static List<Node> FindBorderNodes(EdgesByOffset connections, Rect worldRect, NodeBaseType nodeBorderType)
        {
            return connections.SelectMany(c => c.Value).SelectMany(c => c.Nodes())
                .Where(n => n.Type().BaseType == nodeBorderType)
                .Where(n => n.Position().x == worldRect.xMin
                            || n.Position().x == worldRect.xMax
                            || n.Position().z == worldRect.yMin
                            || n.Position().z == worldRect.yMax)
                .Distinct()
                .ToList();
        }

        public static void SetEndingSpline(RamSpline ramSpline, RamSpline otherSpline)
        {
            ramSpline.endingSpline = otherSpline;
            ramSpline.endingMinWidth = 0;
            ramSpline.endingMaxWidth = 1;
        }

        public static void SetBeginningSpline(RamSpline ramSpline, RamSpline otherSpline)
        {
            ramSpline.beginningSpline = otherSpline;
            ramSpline.beginningMinWidth = 0;
            ramSpline.beginningMaxWidth = 1;
        }

        public static List<RamSplineSection> GetRamSplineSections(Rect rect, EdgesByOffset connections, float markerDistance,
            float widthFactor, float maxRamSplineLength, float crossingOffset, float heightOffset, Func<Node, bool> isImportantPointForMarker,
            Func<Node, int> getDominantBiomeIdx, Func<bool> stop)
        {
            var ramSplineSections = new List<RamSplineSection>();

            var relevantConnections = connections.FilterForRect(rect);

            relevantConnections.ForEach(connection =>
            {
                var connectionImpl = (InternalConnection) connection;

                var dominantBiomeIdxAtSpring = getDominantBiomeIdx?.Invoke(connection.Source()) ?? -1;

                var subsections = connectionImpl.DivideIntoSubsections(rect, true, true);
                if (subsections.Count == 0) return;

                var joinedSubsections = new List<List<Edge>>();

                // join primary subsections at crossings
                for (var i = 0; i < subsections.Count; i++)
                {
                    var joined = subsections[i];

                    while (i < subsections.Count - 1
                           && subsections[i].Last().Destination().Type().IsCrossing()
                           && subsections[i].Last().Destination().Equals(subsections[i + 1].First().Source()))
                    {
                        joined.AddRange(subsections[i + 1]);
                        i++;
                    }

                    joinedSubsections.Add(joined);
                }

                Log.Debug(typeof(RamSplineHelper), () => "Process " + connectionImpl + " with sections " + Log.LogCollection(joinedSubsections));

                foreach (var subsection in joinedSubsections)
                {
                    Log.Debug(typeof(RamSplineHelper), () => "Process Sub " + Log.LogCollection(subsection));

                    // skip very short subsections (only one edge)
                    if (subsection.Count == 1 && (subsection[0].Source().Type().IsBorder() || subsection[0].Destination().Type().IsBorder()))
                    {
                        Log.Debug(typeof(RamSplineHelper), () => "Skipping Sub " + Log.LogCollection(subsection));
                        continue;
                    }

                    var subdividedSubsections = SubdivideSubsectionIfTooLong(subsection, maxRamSplineLength);

                    for (var i = 0; i < subdividedSubsections.Count; i++)
                    {
                        var subdividedSubsection = subdividedSubsections[i];

                        var isLast = i == subdividedSubsections.Count - 1;
                        var connectToPrevious = i != 0;

                        var subSectionStart = subdividedSubsection.SourceEndpoint();
                        var subSectionEnd = subdividedSubsection.DestinationEndpoint();

                        if (isLast) ExpandSubsection(subdividedSubsection);

                        // remember all crossings within the current subsection
                        var midCcrossings = new HashSet<Node>();
                        var startCrossings = new HashSet<Node>();
                        var endCrossings = new HashSet<Node>();

                        if (subSectionStart.Type().IsCrossing()) startCrossings.Add(subSectionStart);
                        if (subSectionEnd.Type().IsCrossing()) endCrossings.Add(subSectionEnd);

                        var markers = GetMarkers(subdividedSubsection, midCcrossings, markerDistance, widthFactor, !isLast, crossingOffset,
                            heightOffset, isImportantPointForMarker, stop);

                        if (markers.Count < 2)
                        {
                            Log.Warn(typeof(RamSplineHelper),
                                () => "Not enough markers found for RAM spline subsection " + Log.LogCollection(subdividedSubsection));
                            continue;
                        }

                        var ramSplineSection = new RamSplineSection(subdividedSubsection, markers, midCcrossings, startCrossings, endCrossings,
                            subSectionStart, subSectionEnd, connectToPrevious, dominantBiomeIdxAtSpring);

                        ramSplineSections.Add(ramSplineSection);
                    }
                }
            });

            return ramSplineSections;
        }

        public static List<List<Edge>> SubdivideSubsectionIfTooLong(List<Edge> subsection, float maxLength)
        {
            var totalLength = subsection.Sum(e => e.Length());

            var container = new List<List<Edge>>();

            if (subsection.Count < 6 || totalLength < maxLength)
            {
                container.Add(subsection);
            } else
            {
                var splitIndex = subsection.Count / 2;

                while (splitIndex < subsection.Count - 2 && !subsection[splitIndex].Source().Type().IsSection())
                    splitIndex++;

                if (subsection.Count - splitIndex < 3)
                {
                    // remaining section would be too short
                    container.Add(subsection);
                } else
                {
                    var firstHalf = subsection.GetRange(0, splitIndex);
                    container.AddRange(SubdivideSubsectionIfTooLong(firstHalf, maxLength));

                    var secondHalf = subsection.GetRange(splitIndex, subsection.Count - splitIndex);
                    container.AddRange(SubdivideSubsectionIfTooLong(secondHalf, maxLength));
                }
            }

            return container;
        }

        public static void ExpandSubsection(List<Edge> subsection)
        {
            // add the beginning of a section, if it is only one edge
            if (subsection[0].Source().Type().IsBorder() && subsection[0].Previous().Source().IsEndpointOrCrossing())
            {
                var previous = subsection[0].Previous();
                Log.Debug(typeof(RamSplineHelper), () => "Expand Sub with previous " + previous);
                subsection.Insert(0, previous);
            }

            // add the end of a section, if it is only one edge
            if (subsection[subsection.Count - 1].Destination().Type().IsBorder()
                && subsection[subsection.Count - 1].Next().Destination().IsEndpointOrCrossing())
            {
                var next = subsection[subsection.Count - 1].Next();
                Log.Debug(typeof(RamSplineHelper), () => "Expand Sub with next " + next);
                subsection.Add(next);
            }
        }

        public static List<Vector4> GetMarkers(List<Edge> subsection, HashSet<Node> crossings, float markerDistance, float widthFactor,
            bool forceKeepLast, float crossingOffset, float heightOffset, Func<Node, bool> isImportantPointForMarker, Func<bool> stop)
        {
            var markers = new List<Vector4>();

            // sometimes the last marker is important and must not be removed
            var keepLast = false;
            var lastSlopeDifference = 0f;

            var subsectionLength = subsection.Sum(e => e.BezierCurve().Length());
            // at least 3 markers per subsection must be present
            var maxMarkerDistance = subsectionLength / 3f;
            var minMarkerDistance = Mathf.Max(1f, markerDistance);

            var subsectionEndNode = subsection.DestinationEndpoint();

            foreach (var edge in subsection)
            {
                if (stop()) break;

                var minProgress = edge.Destination().Type().IsCrossingPerimeter()
                                  && edge.Source().Type().IsCrossing()
                                  && Equals(edge.Destination().BelongsTo(), edge.Source())
                    ? crossingOffset
                    : 0;
                var maxProgress = edge.Source().Type().IsCrossingPerimeter()
                                  && edge.Destination().Type().IsCrossing()
                                  && Equals(edge.Source().BelongsTo(), edge.Destination())
                    ? 1 - crossingOffset
                    : 1;

                var fromWidth = edge.Widths()[0];
                var endNode = edge.Destination();

                var nextSlopeDifference = GetSlopeDifferenceToNext(edge);
                var maxSlopeDifference = Mathf.Max(lastSlopeDifference, nextSlopeDifference);
                lastSlopeDifference = nextSlopeDifference;

                var toWidth = endNode.Type().IsCrossing() ? fromWidth : edge.Widths()[1];

                var desiredDistance = Mathf.Max(edge.Widths()[0], edge.Widths()[1]) * markerDistance * Mathf.Pow((90 - maxSlopeDifference) / 90, 2);
                desiredDistance = Mathf.Clamp(desiredDistance, minMarkerDistance, maxMarkerDistance);

                var edgeLength = edge.BezierCurve().Length();

                if (markers.Count == 0) AddMarker(edge, fromWidth, toWidth, minProgress, widthFactor, heightOffset, markers);

                var lastMarker = markers[markers.Count - 1];
                var remaining = desiredDistance - (edge.Source().Position() - lastMarker.V3()).magnitude;
                remaining = Mathf.Max(0, remaining);

                var progress = Mathf.Clamp01(minProgress + remaining / edgeLength);

                while (progress < maxProgress)
                {
                    lastMarker = AddMarker(edge, fromWidth, toWidth, progress, widthFactor, heightOffset, markers);
                    progress += desiredDistance / edgeLength;
                    keepLast = false;
                }

                // important points should always get a marker
                if (forceKeepLast && (subsectionEndNode.Equals(endNode)) || endNode.IsEndpointOrCrossing() || isImportantPointForMarker(endNode))
                {
                    var distanceToLast = (edge.BezierCurve().Destination().Position() - lastMarker.V3()).magnitude;

                    // remove the last marker if it would be too close
                    if (!keepLast && distanceToLast < desiredDistance) markers.RemoveAt(markers.Count - 1);

                    AddMarker(edge, fromWidth, toWidth, maxProgress, widthFactor, heightOffset, markers);
                    keepLast = true;
                } else
                {
                    keepLast = false;
                }

                if (edge.Source().Type().IsCrossing()) crossings.Add(edge.Source());
                if (endNode.Type().IsCrossing()) crossings.Add(endNode);
            }

            return markers;
        }

        private static float GetSlopeDifferenceToNext(Edge edge)
        {
            var startNode = edge.Source();
            var endNode = edge.Destination();

            var nextEdge = endNode.Type().IsEndpoint() ? null : endNode.Edges()[1];

            return nextEdge == null
                ? 0
                : Mathf.Abs(Mathf.Abs((nextEdge.Destination().Position() - nextEdge.Source().Position()).AngleToGround())
                            - Mathf.Abs((endNode.Position() - startNode.Position()).AngleToGround()));
        }

        public static RamSpline CreateRamSpline(Transform container, List<Vector4> markers, SplineProfile profile)
        {
            if (profile == null)
            {
                Log.Warn(typeof(RamLakeHelper), () => "Missing profile for RAM River Output");
                throw new Exception("Missing profile for RAM River Output");
            }

            var ramSpline = RamSpline.CreateSpline(profile.splineMaterial, markers);
            ramSpline.gameObject.transform.parent = container;
            ramSpline.currentProfile = profile;
            ResetToProfile(ramSpline); // will also generate the spline, required to populate spline fields like points, colors, etc.
            return ramSpline;
        }


        public static void SetAlphaFromMask(RamSpline ramSpline, HeightMapOffset heightMapOffset, float[,] mask, Vector2 vertex, int i,
            int numVertices, int tileResolution, int heightMapResolution, int maskMargin)
        {
            var mappingFactor = (float) heightMapResolution / tileResolution;
            var x = (int) (vertex.x * mappingFactor - heightMapOffset.x + maskMargin);
            var y = (int) (vertex.y * mappingFactor - heightMapOffset.z + maskMargin);

            if (x >= mask.GetLength(0) || y >= mask.GetLength(1)) return;

            var alpha = 1 - mask[x, y];

            // Log.Warn(typeof(RamSplineHelper), () => vertex + " (" + vertex * mappingFactor + ") -> " + alpha);

            if (alpha >= 1) return;

            // set alpha on the mesh edges to 0
            var mod = i % ramSpline.vertsInShape;
            if (mod == 0 || mod == ramSpline.vertsInShape - 1 || i < ramSpline.vertsInShape || i > numVertices - ramSpline.vertsInShape)
            {
                alpha = 0;
            }

            SetAlpha(ramSpline, i, alpha);
        }

        public static void FadeInSpline(RamSpline ramSpline)
        {
            var meshFilter = ramSpline.meshfilter;
            var vertices = meshFilter.sharedMesh.vertices;

            var radius = ramSpline.widths[0];

            var maxIdx = 0;

            for (var i = 0; i < vertices.Length; i++)
            {
                if ((vertices[i] - ramSpline.points[0]).magnitude > radius * 1.5f)
                {
                    maxIdx = i - 1;
                    break;
                }
            }

            maxIdx += maxIdx % ramSpline.vertsInShape;

            var centerIdx = maxIdx - (ramSpline.vertsInShape / 2);

            for (var i = 0; i < maxIdx; i++)
            {
                var alpha = Mathf.Clamp01(1 - (vertices[i] - vertices[centerIdx]).magnitude / radius);
                SetAlpha(ramSpline, i, alpha);
            }
        }

        public static void FadeOutSpline(RamSpline ramSpline)
        {
            var meshFilter = ramSpline.meshfilter;
            var vertices = meshFilter.sharedMesh.vertices;

            var radius = ramSpline.widths[ramSpline.widths.Count - 1];

            var maxIdx = 0;

            for (var i = vertices.Length - 1; i >= 0; i--)
            {
                if ((vertices[i] - ramSpline.points[ramSpline.points.Count - 1]).magnitude > radius * 1.5f)
                {
                    maxIdx = i + 1;
                    break;
                }
            }

            maxIdx -= maxIdx % ramSpline.vertsInShape;

            var centerIdx = maxIdx + (ramSpline.vertsInShape / 2);

            for (var i = vertices.Length - 1; i >= maxIdx; i--)
            {
                var alpha = Mathf.Clamp01(1 - (vertices[i] - vertices[centerIdx]).magnitude / radius);
                SetAlpha(ramSpline, i, alpha);
            }
        }

        public static void SetAlpha(RamSpline ramSpline, int i, float alpha)
        {
            var color = ramSpline.colors[i];
//                var color = Color.red;
            color.a = alpha;
            ramSpline.colors[i] = color;
        }

        public static void FadeOutSplineTowardsCrossing(RamSpline ramSpline, Node crossingNode, int tileResolution, int heightMapResolution,
            int maskMargin, EdgeWalker<float[,]> edgeWalker)
        {
            var masks = new Dictionary<HeightMapOffset, float[,]>();

            var meshFilter = ramSpline.meshfilter;
            var vertices = meshFilter.sharedMesh.vertices;
            var maskConnection = crossingNode.Connections()[0];

            for (var i = 0; i < vertices.Length; i++)
            {
                var heightMapOffset = HeightMapOffset.For(vertices[i], tileResolution, heightMapResolution);
                var offset = Offset.For(vertices[i], tileResolution);

                if (!masks.ContainsKey(heightMapOffset))
                {
                    var rect = new Rect(new Vector2(offset.x, offset.z), new Vector2(tileResolution, tileResolution));
                    var newMask = GenerateEdgeMask(offset, maskConnection, rect, heightMapResolution, edgeWalker);
                    masks.Add(heightMapOffset, newMask);
                }

                var mask = masks[heightMapOffset];

                SetAlphaFromMask(ramSpline, heightMapOffset, mask, vertices[i].V2(), i, vertices.Length, tileResolution,
                    heightMapResolution, maskMargin);
            }

            meshFilter.sharedMesh.colors = ramSpline.colors;
        }

        public static void ConnectCrossings(Dictionary<Node, List<RamSpline>> splinesByMidCrossingNode,
            Dictionary<Node, List<RamSpline>> splinesByEitherEndCrossingNode, bool reverse, Action<RamSpline, RamSpline, Node> postProcess = null)
        {
            foreach (var splinesByCrossing in splinesByEitherEndCrossingNode)
            {
                foreach (var joiningSpline in splinesByCrossing.Value)
                {
                    var node = splinesByCrossing.Key;
                    var crossingPosition = node.Position();

                    var joinedSpline = splinesByMidCrossingNode[node][0];

                    // set render queues
                    var meshRendererJoining = joiningSpline.gameObject.GetComponent<MeshRenderer>();
                    var meshRendererJoined = joinedSpline.gameObject.GetComponent<MeshRenderer>();

                    var tmpMaterial = new Material(meshRendererJoining.sharedMaterial)
                        { renderQueue = meshRendererJoined.sharedMaterial.renderQueue + 1 };
                    meshRendererJoining.sharedMaterial = tmpMaterial;

                    var joinedSplineDirection = node.Edges()[1].BezierCurve().InterpolatedPosition(0.1f) -
                                                node.Edges()[0].BezierCurve().InterpolatedPosition(0.9f);

                    var joiningSplinePoint = joiningSpline.controlPoints[reverse ? joiningSpline.controlPoints.Count - 1 : 0].V3();

                    var left = false;
                    var toCrossingDirection = Vector3.Cross(joinedSplineDirection, Vector3.up).Flattened().normalized;
                    if ((joiningSplinePoint - (crossingPosition + toCrossingDirection)).magnitude <
                        (joiningSplinePoint - (crossingPosition - toCrossingDirection)).magnitude)
                    {
                        toCrossingDirection = -toCrossingDirection;
                        left = true;
                    }

                    // Log.Warn(typeof(RamRoadHelper), () => crossingPosition + " -> " + reverse + ", " + left);

                    var joiningSplineTangent = joiningSpline.tangents[reverse ? joiningSpline.tangents.Count - 1 : 0];
                    if (!reverse) joiningSplineTangent = -joiningSplineTangent;

                    var angleTowardsCrossing = Vector3.SignedAngle(joiningSplineTangent, toCrossingDirection, Vector3.up);
                    var rotationAlign = Quaternion.AngleAxis(angleTowardsCrossing, Vector3.up);

                    var angleToGround = joinedSplineDirection.AngleToGround();
                    if (left && reverse || !left && !reverse) angleToGround = -angleToGround;
                    var rotationFromGround = Quaternion.Euler(0, 0, angleToGround);

                    var rotation = rotationAlign * rotationFromGround;

                    RotateTowardsCrossing(joiningSpline, rotation, reverse);
                    WidenTowardsCrossing(joiningSpline, rotation, reverse);

                    if (postProcess != null) postProcess(joinedSpline, joiningSpline, node);
                }
            }
        }

        public static void WidenTowardsCrossing(RamSpline joiningSpline, Quaternion rotation, bool reverse)
        {
            var startIdx = reverse ? joiningSpline.controlPointsOrientation.Count - 1 : 0;
            const int smooth = 3;

            var rotationAngle = rotation.eulerAngles.y;
            if (rotationAngle > 180) rotationAngle = 360 - rotationAngle;

            var current = startIdx;
            for (var i = 0; i < smooth; i++)
            {
                var currentRotationAngle = rotationAngle / (i + 1);
                var currentOffset = 0.05f / (i + 1);

                var controlPointCrossing = joiningSpline.controlPoints[current];
                controlPointCrossing.w *= 1f / Mathf.Cos(Mathf.Deg2Rad * currentRotationAngle);

                joiningSpline.controlPoints[current] = controlPointCrossing + new Vector4(0, currentOffset, 0, 0);

                current = reverse ? current - 1 : current + 1;
            }
        }

        public static void RotateTowardsCrossing(RamSpline joiningSpline, Quaternion rotation, bool reverse)
        {
            var startIdx = reverse ? joiningSpline.controlPointsOrientation.Count - 1 : 0;
            const int smooth = 3;

            var current = startIdx;
            for (var i = 0; i < smooth; i++)
            {
                var currentRotation = Quaternion.Lerp(Quaternion.identity, rotation, 1 - ((float) i / smooth));
                joiningSpline.controlPointsRotations[current] *= currentRotation;

                current = reverse ? current - 1 : current + 1;
            }
        }

        private static float[,] GenerateEdgeMask(Offset offset, Connection connection, Rect rect, int resolution, EdgeWalker<float[,]> edgeWalker)
        {
            var connectionsByOffset = new EdgesByOffset();
            connectionsByOffset.Add(offset, new List<Connection> { connection });

            var result = new float[resolution, resolution];
            edgeWalker.ProcessEdges(connectionsByOffset, result, rect, connection.WidthMax());

            return result;
        }

        private static Vector4 AddMarker(Edge edge, float fromWidth, float toWidth, float pos, float widthFactor,
            float heightOffset, List<Vector4> markers)
        {
            var position = ((InternalBezierCurve) edge.BezierCurve()).InterpolatedPosition(pos);
            var width = edge.Source().Type().IsCrossing() ? toWidth : Mathf.Lerp(fromWidth, toWidth, pos);
            width *= widthFactor;
            var marker = new Vector4(position.x, position.y + heightOffset, position.z, width);
            markers.Add(marker);
            return marker;
        }

        public static void StoreSplineByNode(Node node, RamSpline ramSpline, NodeBaseType requiredType,
            Dictionary<Node, List<RamSpline>> dict)
        {
            if (node.Type().BaseType == requiredType) AddSplineByNode(node, ramSpline, dict);
        }

        public static void AddSplineByNode(Node node, RamSpline ramSpline, Dictionary<Node, List<RamSpline>> dict)
        {
            if (!dict.ContainsKey(node)) dict.Add(node, new List<RamSpline>());
            dict[node].Add(ramSpline);
        }

        public static void ResetToProfile(RamSpline spline)
        {
            spline.meshCurve = new AnimationCurve(spline.currentProfile.meshCurve.keys);
            spline.flowFlat = new AnimationCurve(spline.currentProfile.flowFlat.keys);
            spline.flowWaterfall = new AnimationCurve(spline.currentProfile.flowWaterfall.keys);
            spline.terrainCarve = new AnimationCurve(spline.currentProfile.terrainCarve.keys);
            spline.terrainPaintCarve = new AnimationCurve(spline.currentProfile.terrainPaintCarve.keys);

            for (var i = 0; i < spline.controlPointsMeshCurves.Count; i++)
            {
                spline.controlPointsMeshCurves[i] = new AnimationCurve(spline.meshCurve.keys);
            }

            var ren = spline.GetComponent<MeshRenderer>();
            ren.sharedMaterial = spline.currentProfile.splineMaterial;

            spline.minVal = spline.currentProfile.minVal;
            spline.maxVal = spline.currentProfile.maxVal;


            spline.traingleDensity = spline.currentProfile.traingleDensity;
            spline.vertsInShape = spline.currentProfile.vertsInShape;

            spline.uvScale = spline.currentProfile.uvScale;

            spline.uvRotation = spline.currentProfile.uvRotation;


            spline.distSmooth = spline.currentProfile.distSmooth;
            spline.distSmoothStart = spline.currentProfile.distSmoothStart;

#if ST_RAM_2019
            spline.noiseflowMap = spline.currentProfile.noiseflowMap;
            spline.noiseMultiplierflowMap = spline.currentProfile.noiseMultiplierflowMap;
            spline.noiseSizeXflowMap = spline.currentProfile.noiseSizeXflowMap;
            spline.noiseSizeZflowMap = spline.currentProfile.noiseSizeZflowMap;

            spline.floatSpeed = spline.currentProfile.floatSpeed;

            spline.noiseCarve = spline.currentProfile.noiseCarve;
            spline.noiseMultiplierInside = spline.currentProfile.noiseMultiplierInside;
            spline.noiseMultiplierOutside = spline.currentProfile.noiseMultiplierOutside;
            spline.noiseSizeX = spline.currentProfile.noiseSizeX;
            spline.noiseSizeZ = spline.currentProfile.noiseSizeZ;
            spline.terrainSmoothMultiplier = spline.currentProfile.terrainSmoothMultiplier;
            spline.currentSplatMap = spline.currentProfile.currentSplatMap;
            spline.mixTwoSplatMaps = spline.currentProfile.mixTwoSplatMaps;
            spline.secondSplatMap = spline.currentProfile.secondSplatMap;
            spline.addCliffSplatMap = spline.currentProfile.addCliffSplatMap;
            spline.cliffSplatMap = spline.currentProfile.cliffSplatMap;
            spline.cliffAngle = spline.currentProfile.cliffAngle;
            spline.cliffBlend = spline.currentProfile.cliffBlend;

            spline.cliffSplatMapOutside = spline.currentProfile.cliffSplatMapOutside;
            spline.cliffAngleOutside = spline.currentProfile.cliffAngleOutside;
            spline.cliffBlendOutside = spline.currentProfile.cliffBlendOutside;

            spline.distanceClearFoliage = spline.currentProfile.distanceClearFoliage;
            spline.distanceClearFoliageTrees = spline.currentProfile.distanceClearFoliageTrees;
            spline.noisePaint = spline.currentProfile.noisePaint;
            spline.noiseMultiplierInsidePaint = spline.currentProfile.noiseMultiplierInsidePaint;
            spline.noiseMultiplierOutsidePaint = spline.currentProfile.noiseMultiplierOutsidePaint;
            spline.noiseSizeXPaint = spline.currentProfile.noiseSizeXPaint;
            spline.noiseSizeZPaint = spline.currentProfile.noiseSizeZPaint;

            spline.simulatedRiverLength = spline.currentProfile.simulatedRiverLength;
            spline.simulatedRiverPoints = spline.currentProfile.simulatedRiverPoints;
            spline.simulatedMinStepSize = spline.currentProfile.simulatedMinStepSize;
            spline.simulatedNoUp = spline.currentProfile.simulatedNoUp;
            spline.simulatedBreakOnUp = spline.currentProfile.simulatedBreakOnUp;
            spline.noiseWidth = spline.currentProfile.noiseWidth;
            spline.noiseMultiplierWidth = spline.currentProfile.noiseMultiplierWidth;
            spline.noiseSizeWidth = spline.currentProfile.noiseSizeWidth;
#endif
            spline.receiveShadows = spline.currentProfile.receiveShadows;
            spline.shadowCastingMode = spline.currentProfile.shadowCastingMode;

            spline.GenerateSpline();
            spline.oldProfile = spline.currentProfile;
        }

        public class RamSplineSection
        {
            public List<Edge> edges;
            public List<Vector4> markers;
            public HashSet<Node> midCrossings;
            public HashSet<Node> startCrossings;
            public HashSet<Node> endCrossings;
            public bool connectToPrevious;
            public int dominantBiomeIdxAtSpring;

            // start and end of the full section
            public Node sectionStart;
            public Node sectionEnd;

            public RamSplineSection(List<Edge> edges, List<Vector4> markers, HashSet<Node> midCrossings, HashSet<Node> startCrossings,
                HashSet<Node> endCrossings, Node sectionStart, Node sectionEnd, bool connectToPrevious, int dominantBiomeIdxAtSpring)
            {
                this.edges = edges;
                this.markers = markers;
                this.midCrossings = midCrossings;
                this.startCrossings = startCrossings;
                this.endCrossings = endCrossings;
                this.sectionStart = sectionStart;
                this.sectionEnd = sectionEnd;
                this.connectToPrevious = connectToPrevious;
                this.dominantBiomeIdxAtSpring = dominantBiomeIdxAtSpring;
            }
        }
    }
}

#endif
#endif
