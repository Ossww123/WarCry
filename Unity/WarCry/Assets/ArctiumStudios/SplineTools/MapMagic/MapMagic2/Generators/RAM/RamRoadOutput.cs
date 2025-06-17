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
        name = "R.A.M Road",
        disengageable = true,
        colorType = typeof(EdgesByOffset),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/output/ram_road")]
    public class RamRoadOutput : OutputGenerator, IMultiInlet, SplineToolsGenerator
    {
        //FIXME splines are not connected on borders when tiles are added

        public string guid = Guid.NewGuid().ToString();
        public Inlet<EdgesByOffset> inputEdges = new Inlet<EdgesByOffset>();

        public SplineProfile profile;

        public float heightOffset = 0f;
        public ClampedFloat markerDistance = new ClampedFloat(0.9f, 0f, float.MaxValue);
        public ClampedFloat widthFactor = new ClampedFloat(1f, 0f, float.MaxValue);
        public ClampedFloat ramSplineLengthMax = new ClampedFloat(250f, 0f, float.MaxValue);
        public ClampedFloat crossingOffset = new ClampedFloat(0.8f, 0f, 1f);

        [NonSerialized] private RamRoadHelper ramRoadHelper;

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputEdges;
        }

        public OutputLevel outputLevel = OutputLevel.Main;

        public override OutputLevel OutputLevel
        {
            get { return outputLevel; }
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            var connections = tileData.ReadInletProduct(inputEdges);

            if (stop != null && stop.stop) return;
            if (!enabled || connections == null || connections.Count == 0)
            {
                tileData.RemoveFinalize(finalizeAction);
                return;
            }

            if (ramRoadHelper == null) ramRoadHelper = new RamRoadHelper(guid);

            var worldRect = tileData.area.active.ToWorldRect();

            var roadSections = ramRoadHelper.GetRoadSections(worldRect, connections,
                markerDistance.ClampedValue, widthFactor.ClampedValue, ramSplineLengthMax.ClampedValue, crossingOffset.ClampedValue, heightOffset,
                node => Generator.GetDominantBiomeIdx(node, tileData, stop),
                () => stop != null && stop.stop);

            var data = new RamRoadHelper.RamRoadOutputData(roadSections);

            tileData.StoreOutput(this, typeof(RamRoadOutput), this, data);
            tileData.MarkFinalize(finalizeAction, stop);
        }

        public static FinalizeAction finalizeAction = Finalize; //class identified for FinalizeData

        public static void Finalize(TileData data, StopToken stop)
        {
            if (stop != null && stop.stop) return;

            var roadDataList = new List<ApplyRoadData>();

            foreach ((RamRoadOutput output, RamRoadHelper.RamRoadOutputData outputData, MatrixWorld biomeMask)
                     in data.Outputs<RamRoadOutput, RamRoadHelper.RamRoadOutputData, MatrixWorld>(typeof(RamRoadOutput), true))
            {
                if (stop != null && stop.stop) return;

                if (outputData == null || outputData.roadSections.Count == 0) continue;
                if (biomeMask != null && biomeMask.IsEmpty()) continue;

#if ST_MM_2_BIOMES
                if (biomeMask != null)
                {
                    var currentBiomeIdx = Generator.GetBiomeIdx(output);

                    outputData.roadSections = outputData.roadSections
                        .Where(section => section.dominantBiomeIdxAtSpring == currentBiomeIdx)
                        .ToList();
                }
#endif

                var roadData = new ApplyRoadData()
                {
                    outputData = outputData,
                    profile = output.profile,
                    ramRoadHelper = output.ramRoadHelper
                };
                roadDataList.Add(roadData);
            }

            //pushing to apply
            if (stop != null && stop.stop) return;
            var applyData = new ApplyObjectsData()
            {
                roadDataList = roadDataList
            };

            Graph.OnOutputFinalized?.Invoke(typeof(RamRoadOutput), data, applyData, stop);
            data.MarkApply(applyData);
        }

        public class ApplyRoadData
        {
            public RamRoadHelper.RamRoadOutputData outputData;
            public SplineProfile profile;
            public RamRoadHelper ramRoadHelper;
        }

        public class ApplyObjectsData : IApplyDataRoutine
        {
            public List<ApplyRoadData> roadDataList;

            public void Apply(Terrain terrain)
            {
                var applyRoutine = ApplyRoutine(terrain);
                while (applyRoutine.MoveNext())
                {
                }
            }

            public IEnumerator ApplyRoutine(Terrain terrain)
            {
                foreach (var roadData in roadDataList)
                {
                    var roadSections = roadData.ramRoadHelper.CreateRoadSections(roadData.outputData, roadData.profile,
                        terrain.transform.parent.Find("Objects"));

                    while (roadSections.MoveNext())
                        yield return null;
                }
            }

            public int Resolution
            {
                get { return 0; }
            }
        }

        public override void ClearApplied(TileData data, Terrain terrain)
        {
            Cleanup(this);
        }

        public static void Cleanup()
        {
            foreach (var ramRoadOutput in MapMagicUtil.FindAllGeneratorsOfType<RamRoadOutput>().Distinct()) ramRoadOutput.ramRoadHelper?.Cleanup();
            foreach (var terrainTile in ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tiles.All())
            {
                var parent = terrainTile.ActiveTerrain.transform.parent.Find("Objects");
                var container = parent.Find(RamRoadHelper.ContainerName);
                if (container != null) UnityEngine.Object.DestroyImmediate(container.gameObject);
            }
        }

        public static void Cleanup(RamRoadOutput generator)
        {
            generator.ramRoadHelper?.Cleanup();
            foreach (var terrainTile in ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tiles.All())
            {
                var parent = terrainTile.ActiveTerrain.transform.parent.Find("Objects");
                var container = RamRoadHelper.GetContainer(parent, generator.guid, false);
                if (container != null) UnityEngine.Object.DestroyImmediate(container.gameObject);
            }
        }
    }
}

#endif
#endif
