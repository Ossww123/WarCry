#if ST_MM_2
#if ST_RAM_2019 || ST_RAM

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Den.Tools.Matrices;
using MapMagic.Nodes;
using MapMagic.Products;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Output/Unstable",
        name = "R.A.M Lake",
        disengageable = true,
        colorType = typeof(NodesByOffset),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/output/ram_lake")]
    public class RamLakeOutput : OutputGenerator, IMultiInlet, SplineToolsGenerator
    {
        public string guid = Guid.NewGuid().ToString();
        public Inlet<NodesByOffset> inputNodes = new Inlet<NodesByOffset>();

        public LakePolygonProfile profile;
        public float heightOffset = -0.15f;

        public OutputLevel outputLevel = OutputLevel.Main;

        [NonSerialized] private RamLakeHelper ramLakeHelper;

        public override OutputLevel OutputLevel
        {
            get { return outputLevel; }
        }

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputNodes;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            if (stop != null && stop.stop) return;
            if (!enabled)
            {
                tileData.RemoveFinalize(finalizeAction);
                return;
            }

            if (ramLakeHelper == null) ramLakeHelper = new RamLakeHelper(guid);

            var nodesByOffset = tileData.ReadInletProduct(inputNodes);

            var worldRect = tileData.area.active.ToWorldRect();

            var lakeOutputData = RamLakeHelper.NodeMarkers(nodesByOffset, heightOffset, worldRect,
                node => Generator.GetDominantBiomeIdx(node, tileData, stop));

            tileData.StoreOutput(this, typeof(RamLakeOutput), this, lakeOutputData);
            tileData.MarkFinalize(finalizeAction, stop);
        }

        public static FinalizeAction finalizeAction = Finalize; //class identified for FinalizeData

        public static void Finalize(TileData data, StopToken stop)
        {
            if (stop != null && stop.stop) return;

            var lakeDataList = new List<ApplyLakeData>();

            foreach ((RamLakeOutput output, RamLakeHelper.RamLakeOutputData outputData, MatrixWorld biomeMask)
                     in data.Outputs<RamLakeOutput, RamLakeHelper.RamLakeOutputData, MatrixWorld>(typeof(RamLakeOutput), true))
            {
                if (stop != null && stop.stop) return;

                if (outputData == null || outputData.nodeMarkers == null || outputData.nodeMarkers.Count == 0) continue;
                if (biomeMask != null && biomeMask.IsEmpty()) continue;

#if ST_MM_2_BIOMES
                if (biomeMask != null)
                {
                    var currentBiomeIdx = Generator.GetBiomeIdx(output);

                    outputData.nodeMarkers = outputData.nodeMarkers
                        .Where(pair => outputData.dominantBiomeIdxByNode[pair.Key] == currentBiomeIdx)
                        .ToDictionary(p => p.Key, p => p.Value);
                }
#endif

                //pushing to apply
                if (stop != null && stop.stop) return;
                var applyLakeData = new ApplyLakeData()
                {
                    nodeMarkers = outputData.nodeMarkers,
                    profile = output.profile,
                    ramLakeHelper = output.ramLakeHelper
                };
                lakeDataList.Add(applyLakeData);
            }

            var applyData = new ApplyObjectsData()
            {
                lakeDataList = lakeDataList,
                worldRect = data.area.active.ToWorldRect()
            };

            Graph.OnOutputFinalized?.Invoke(typeof(RamLakeOutput), data, applyData, stop);
            data.MarkApply(applyData);
        }

        public class ApplyLakeData
        {
            public Dictionary<Node, List<Vector3>> nodeMarkers;
            public LakePolygonProfile profile;
            public RamLakeHelper ramLakeHelper;
        }

        public class ApplyObjectsData : IApplyDataRoutine
        {
            public Rect worldRect;
            public List<ApplyLakeData> lakeDataList;

            public void Apply(Terrain terrain)
            {
                var applyRoutine = ApplyRoutine(terrain);
                while (applyRoutine.MoveNext())
                {
                }
            }

            public IEnumerator ApplyRoutine(Terrain terrain)
            {
                foreach (var lakeData in lakeDataList)
                {
                    var cleanupLakes = lakeData.ramLakeHelper.CleanupLakes(GetObjectsGo(), worldRect,
                        ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tiles.AllWorldRects(), true);
                    while (cleanupLakes.MoveNext())
                        yield return null;

                    var container = RamLakeHelper.GetContainer(GetObjectsGo(), Den.Tools.CoordinatesExtensions.GetWorldRect(terrain),
                        lakeData.ramLakeHelper.generatorGuid);

                    foreach (var nodeMarker in lakeData.nodeMarkers)
                    {
                        var generateLakePolygon = lakeData.ramLakeHelper.GenerateLakePolygon(nodeMarker, container, lakeData.profile);
                        while (generateLakePolygon.MoveNext()) yield return null;
                    }
                }
            }

            public int Resolution
            {
                get { return 0; }
            }
        }


        public override void ClearApplied(TileData data, Terrain terrain)
        {
            var cleanupLakes = ramLakeHelper.CleanupLakes(GetObjectsGo(), data.area.active.ToWorldRect(),
                ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tiles.AllWorldRects(), false);

            while (cleanupLakes.MoveNext())
            {
            }
        }

        public static void Cleanup()
        {
            foreach (var ramLakeOutput in MapMagicUtil.FindAllGeneratorsOfType<RamLakeOutput>().Distinct()) ramLakeOutput.ramLakeHelper?.Cleanup();
            var container = GetObjectsGo().Find(RamLakeHelper.ContainerName);
            if (container != null) GameObject.DestroyImmediate(container.gameObject);
        }

        private static Transform GetObjectsGo()
        {
            var mmTransform = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.transform;
            var objectsGo = mmTransform.Find("Objects");
            if (mmTransform.Find("Objects") == null)
            {
                objectsGo = new GameObject("Objects").transform;
                objectsGo.parent = mmTransform;
            }

            return objectsGo;
        }
    }
}

#endif
#endif
