#if ST_RAM_2019 || ST_RAM

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class RamLakeHelper
    {
        public const string ContainerName = "RAM Lakes";
        public readonly string generatorGuid;

        private readonly Dictionary<Node, LakePolygon> lakes = new Dictionary<Node, LakePolygon>();

        public RamLakeHelper(string generatorGuid)
        {
            this.generatorGuid = generatorGuid;
        }

        public IEnumerator GenerateLakePolygon(KeyValuePair<Node, List<Vector3>> nodeMarker, Transform container,
            LakePolygonProfile profile)
        {
            if (profile == null)
            {
                Log.Warn(typeof(RamLakeHelper), () => "Missing profile for RAM Lake");
                yield break;
            }
            
            lock (lakes)
            {
                if (lakes.ContainsKey(nodeMarker.Key)) yield break;

                Log.Debug(typeof(RamLakeHelper), () => "Placing new RAM Lake at " + nodeMarker.Key);

                var lake = LakePolygon.CreatePolygon(profile.lakeMaterial, nodeMarker.Value);

                yield return null;

                lake.currentProfile = profile;
                ResetToProfile(lake);
                lake.transform.parent = container;
                lakes.Add(nodeMarker.Key, lake);

                yield return null;

                var meshRenderer = lake.gameObject.GetComponent<MeshRenderer>();

                var tmpMaterial = new Material(meshRenderer.sharedMaterial);
                tmpMaterial.renderQueue -= 100;
                meshRenderer.sharedMaterial = tmpMaterial;

                yield return null;

                lake.GeneratePolygon();
                yield return null;
            }
        }

        public static Transform GetContainer(Transform parent, Rect rect, string generatorGuid, bool create = true)
        {
            var rectContainer = GetRectContainer(parent, rect, create);

            if (rectContainer == null) return null;

            var graphContainer = rectContainer.Find(generatorGuid);

            if (graphContainer != null || !create) return graphContainer;

            graphContainer = new GameObject(generatorGuid).transform;
            graphContainer.parent = rectContainer.transform;

            return graphContainer;
        }

        private static Transform GetRectContainer(Transform parent, Rect rect, bool create)
        {
            var container = parent.Find(ContainerName);

            if (container == null && !create) return null;

            if (container == null)
            {
                container = new GameObject(ContainerName).transform;
                container.parent = parent.transform;
            }

            var rectContainer = container.Find(rect.min.ToString());

            if (rectContainer == null && !create) return null;

            if (rectContainer == null)
            {
                rectContainer = new GameObject(rect.min.ToString()).transform;
                rectContainer.parent = container.transform;
            }

            return rectContainer;
        }

        public IEnumerator CleanupLakes(Transform parent, Rect rect, IEnumerable<Rect> activeRects, bool forceCleanCurrentRect)
        {
            lock (lakes)
            {
                var toRemove = new HashSet<Node>();

                foreach (var lake in lakes)
                {
                    // check if the lake is required for any active rect
                    var delete = activeRects.All(r => !((InternalNode) lake.Key).IsRelevantForRect(r));

                    if (((InternalNode) lake.Key).IsRelevantForRect(rect)) delete = false;

                    if (forceCleanCurrentRect && lake.Key.PositionV2().Inside(rect)) delete = true;

                    if (!delete) continue;

                    if (lake.Value != null) GameObject.DestroyImmediate(lake.Value.gameObject);
                    toRemove.Add(lake.Key);
                    yield return null;
                }

                foreach (var node in toRemove) lakes.Remove(node);

                if (forceCleanCurrentRect)
                {
                    var rectContainer = GetRectContainer(parent, rect, false);
                    if (rectContainer != null) GameObject.DestroyImmediate(rectContainer.gameObject);
                }
            }
        }

        public void Cleanup()
        {
            lock (lakes) lakes.Clear();
        }

        public static RamLakeOutputData NodeMarkers(NodesByOffset nodesByOffset, float heightOffset, Rect worldRect,
            Func<Node, int> getDominantBiomeIdx)
        {
            var nodes = nodesByOffset.SelectMany(p => p.Value)
                .Where(node => ((InternalNode) node).IsRelevantForRect(worldRect))
                .ToList();

            var nodeMarkers = new Dictionary<Node, List<Vector3>>();
            var dominantBiomeIdxByNode = new Dictionary<Node, int>();

            nodes.ForEach(node =>
            {
                // heightOffset should slightly lower lakes to enable fading of rivers
                var markers = node.GetData<Vector2>(Constants.LakeOutline)
                    .Select(pos => pos.V3(node.Position().y + heightOffset))
                    .ToList();

                nodeMarkers.Add(node, markers);
                dominantBiomeIdxByNode.Add(node, getDominantBiomeIdx.Invoke(node));
            });

            return new RamLakeOutputData(nodeMarkers, dominantBiomeIdxByNode);
        }

        public static void ResetToProfile(LakePolygon lakePolygon)
        {
            var ren = lakePolygon.GetComponent<MeshRenderer>();
            ren.sharedMaterial = lakePolygon.currentProfile.lakeMaterial;

            lakePolygon.terrainCarve = new AnimationCurve(lakePolygon.currentProfile.terrainCarve.keys);
            lakePolygon.terrainPaintCarve = new AnimationCurve(lakePolygon.currentProfile.terrainPaintCarve.keys);

            lakePolygon.distSmooth = lakePolygon.currentProfile.distSmooth;
            lakePolygon.uvScale = lakePolygon.currentProfile.uvScale;
            lakePolygon.currentSplatMap = lakePolygon.currentProfile.currentSplatMap;

            lakePolygon.maximumTriangleSize = lakePolygon.currentProfile.maximumTriangleSize;
            lakePolygon.traingleDensity = lakePolygon.currentProfile.traingleDensity;

#if ST_RAM_2019
            lakePolygon.terrainSmoothMultiplier = lakePolygon.currentProfile.terrainSmoothMultiplier;
            lakePolygon.receiveShadows = lakePolygon.currentProfile.receiveShadows;
            lakePolygon.shadowCastingMode = lakePolygon.currentProfile.shadowCastingMode;

            lakePolygon.automaticFlowMapScale = lakePolygon.currentProfile.automaticFlowMapScale;

            lakePolygon.noiseflowMap = lakePolygon.currentProfile.noiseflowMap;
            lakePolygon.noiseMultiplierflowMap = lakePolygon.currentProfile.noiseMultiplierflowMap;
            lakePolygon.noiseSizeXflowMap = lakePolygon.currentProfile.noiseSizeXflowMap;
            lakePolygon.noiseSizeZflowMap = lakePolygon.currentProfile.noiseSizeZflowMap;

            lakePolygon.noiseCarve = lakePolygon.currentProfile.noiseCarve;
            lakePolygon.noiseMultiplierInside = lakePolygon.currentProfile.noiseMultiplierInside;
            lakePolygon.noiseMultiplierOutside = lakePolygon.currentProfile.noiseMultiplierOutside;
            lakePolygon.noiseSizeX = lakePolygon.currentProfile.noiseSizeX;
            lakePolygon.noiseSizeZ = lakePolygon.currentProfile.noiseSizeZ;

            lakePolygon.noisePaint = lakePolygon.currentProfile.noisePaint;
            lakePolygon.noiseMultiplierInsidePaint = lakePolygon.currentProfile.noiseMultiplierInsidePaint;
            lakePolygon.noiseMultiplierOutsidePaint = lakePolygon.currentProfile.noiseMultiplierOutsidePaint;
            lakePolygon.noiseSizeXPaint = lakePolygon.currentProfile.noiseSizeXPaint;
            lakePolygon.noiseSizeZPaint = lakePolygon.currentProfile.noiseSizeZPaint;
            lakePolygon.mixTwoSplatMaps = lakePolygon.currentProfile.mixTwoSplatMaps;
            lakePolygon.secondSplatMap = lakePolygon.currentProfile.secondSplatMap;
            lakePolygon.addCliffSplatMap = lakePolygon.currentProfile.addCliffSplatMap;
            lakePolygon.cliffSplatMap = lakePolygon.currentProfile.cliffSplatMap;
            lakePolygon.cliffAngle = lakePolygon.currentProfile.cliffAngle;

            lakePolygon.cliffBlend = lakePolygon.currentProfile.cliffBlend;

            lakePolygon.cliffSplatMapOutside = lakePolygon.currentProfile.cliffSplatMapOutside;
            lakePolygon.cliffAngleOutside = lakePolygon.currentProfile.cliffAngleOutside;
            lakePolygon.cliffBlendOutside = lakePolygon.currentProfile.cliffBlendOutside;

            lakePolygon.distanceClearFoliage = lakePolygon.currentProfile.distanceClearFoliage;
            lakePolygon.distanceClearFoliageTrees = lakePolygon.currentProfile.distanceClearFoliageTrees;
#endif

            lakePolygon.oldProfile = lakePolygon.currentProfile;
        }

        public class RamLakeOutputData
        {
            public Dictionary<Node, List<Vector3>> nodeMarkers;
            public Dictionary<Node, int> dominantBiomeIdxByNode;

            public RamLakeOutputData(Dictionary<Node, List<Vector3>> nodeMarkers, Dictionary<Node, int> dominantBiomeIdxByNode)
            {
                this.nodeMarkers = nodeMarkers;
                this.dominantBiomeIdxByNode = dominantBiomeIdxByNode;
            }
        }
    }
}

#endif
