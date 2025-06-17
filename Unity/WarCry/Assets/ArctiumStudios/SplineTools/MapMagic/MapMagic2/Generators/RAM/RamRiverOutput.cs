#if ST_MM_2
#if ST_RAM_2019 || ST_RAM

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Den.Tools.Matrices;
using MapMagic.Nodes;
using MapMagic.Products;
using MapMagic.Terrains;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Output/Unstable",
        name = "R.A.M River",
        disengageable = true,
        colorType = typeof(EdgesByOffset),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/output/ram_river")]
    public class RamRiverOutput : OutputGenerator, IMultiInlet, SplineToolsGenerator
    {
        public string guid = Guid.NewGuid().ToString();
        public Inlet<EdgesByOffset> inputEdges = new Inlet<EdgesByOffset>();
        public Inlet<MatrixWorld> inputLakeMask = new Inlet<MatrixWorld>();
        public Inlet<MatrixWorld> inputSeaMask = new Inlet<MatrixWorld>();

        public SplineProfile profile;

        public float heightOffset = 0.05f;
        public ClampedFloat markerDistance = new ClampedFloat(0.9f, 0f, float.MaxValue);
        public ClampedFloat widthFactor = new ClampedFloat(1.3f, 0f, float.MaxValue);
        public ClampedFloat ramSplineLengthMax = new ClampedFloat(250f, 0f, float.MaxValue);
        public ClampedFloat crossingOffset = new ClampedFloat(0.1f, 0f, 1f);
        public bool fadeIn = true;
        public bool fadeOut = true;

        [NonSerialized] private RamRiverHelper ramRiverHelper;

        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputEdges;
            yield return inputLakeMask;
            yield return inputSeaMask;
        }

        public OutputLevel outputLevel = OutputLevel.Main;

        public override OutputLevel OutputLevel
        {
            get { return outputLevel; }
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            var connections = tileData.ReadInletProduct(inputEdges);
            var lakeMask = tileData.ReadInletProduct(inputLakeMask);
            var seaMask = tileData.ReadInletProduct(inputSeaMask);

            if (stop != null && stop.stop) return;
            if (connections == null || connections.Count == 0) return;

            if (!enabled)
            {
                tileData.RemoveFinalize(finalizeAction);
                return;
            }

            if (ramRiverHelper == null) ramRiverHelper = new RamRiverHelper(guid);

            var worldRect = tileData.area.active.ToWorldRect();

            var lakeMaskByOffset = new Dictionary<HeightMapOffset, float[,]>();
            var seaMaskByOffset = new Dictionary<HeightMapOffset, float[,]>();

            if (lakeMask != null || seaMask != null)
            {
                var heightMapOffset = HeightMapOffset.For(tileData.area.active.worldPos.x, tileData.area.active.worldPos.z,
                    (int) tileData.area.active.worldSize.x, tileData.area.active.rect.size.x);

                if (lakeMask != null) lakeMaskByOffset.Add(heightMapOffset, lakeMask.ToFloatArray2D());
                if (seaMask != null) seaMaskByOffset.Add(heightMapOffset, seaMask.ToFloatArray2D());

                // find all required other rects and include their masks as well
                var borderNodes = RamRiverHelper.FindBorderNodes(connections, worldRect);

                var otherCoordRects = borderNodes
                    .Select(node => GetOtherCoordRect(node.PositionV2().ToMapSpaceCoord(tileData.area.active), tileData.area.active.rect))
                    .ToList();

                foreach (var currentRect in otherCoordRects)
                {
                    if (lakeMask != null) AddMaskForCoordRect(lakeMaskByOffset, inputLakeMask, currentRect, tileData, stop);
                    if (seaMask != null) AddMaskForCoordRect(seaMaskByOffset, inputSeaMask, currentRect, tileData, stop);
                }
            }

            var riverSections = RamRiverHelper.GetRiverSections(worldRect, connections,
                markerDistance.ClampedValue, widthFactor.ClampedValue, ramSplineLengthMax.ClampedValue, crossingOffset.ClampedValue, heightOffset,
                node => Generator.GetDominantBiomeIdx(node, tileData, stop),
                () => stop != null && stop.stop);

            var data = new RamRiverHelper.RamRiverOutputData(riverSections, lakeMaskByOffset, seaMaskByOffset, fadeIn, fadeOut);

            tileData.StoreOutput(this, typeof(RamRiverOutput), this, data);
            tileData.MarkFinalize(Finalize, stop);
        }

        public static FinalizeAction finalizeAction = Finalize; //class identified for FinalizeData

        public static void Finalize(TileData data, StopToken stop)
        {
            if (stop != null && stop.stop) return;

            var riverDataList = new List<ApplyRiverData>();

            var generatorIdx = 0;
            foreach ((RamRiverOutput output, RamRiverHelper.RamRiverOutputData outputData, MatrixWorld biomeMask)
                     in data.Outputs<RamRiverOutput, RamRiverHelper.RamRiverOutputData, MatrixWorld>(typeof(RamRiverOutput), true))
            {
                if (stop != null && stop.stop) return;

                if (outputData == null || outputData.riverSections.Count == 0) continue;
                if (biomeMask != null && biomeMask.IsEmpty()) continue;

#if ST_MM_2_BIOMES
                if (biomeMask != null)
                {
                    var currentBiomeIdx = Generator.GetBiomeIdx(output);

                    outputData.riverSections = outputData.riverSections
                        .Where(section => section.dominantBiomeIdxAtSpring == currentBiomeIdx)
                        .ToList();
                }
#endif

                var riverData = new ApplyRiverData()
                {
                    outputData = outputData,
                    profile = output.profile,
                    ramRiverHelper = output.ramRiverHelper
                };
                riverDataList.Add(riverData);
            }

            //pushing to apply
            if (stop != null && stop.stop) return;
            var applyData = new ApplyObjectsData()
            {
                riverDataList = riverDataList,
                area = data.area
            };

            Graph.OnOutputFinalized?.Invoke(typeof(RamRiverOutput), data, applyData, stop);
            data.MarkApply(applyData);
        }



        public class ApplyRiverData
        {
            public RamRiverHelper.RamRiverOutputData outputData;
            public SplineProfile profile;
            public RamRiverHelper ramRiverHelper;
        }

        public class ApplyObjectsData : IApplyDataRoutine
        {
            public Area area;
            public List<ApplyRiverData> riverDataList;

            public void Apply(Terrain terrain)
            {
                var applyRoutine = ApplyRoutine(terrain);
                while (applyRoutine.MoveNext())
                {
                }
            }

            public IEnumerator ApplyRoutine(Terrain terrain)
            {
                var tileResolution = (int) area.active.worldSize.x;
                var heightMapResolution = area.active.rect.size.x;
                var maskMargin = (area.full.rect.size.x - area.active.rect.size.x) / 2;

                foreach (var riverData in riverDataList)
                {
                    var riverSections = riverData.ramRiverHelper.CreateRiverSections(tileResolution, heightMapResolution, maskMargin,
                        area.active.PixelSize.x, Vector2.zero, riverData.outputData, riverData.profile, terrain.transform.parent.Find("Objects"));

                    while (riverSections.MoveNext())
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
            foreach (var ramRiverOutput in MapMagicUtil.FindAllGeneratorsOfType<RamRiverOutput>().Distinct()) ramRiverOutput.ramRiverHelper?.Cleanup();
            foreach (var terrainTile in ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tiles.All())
            {
                var parent = terrainTile.ActiveTerrain.transform.parent.Find("Objects");
                var container = parent.Find(RamRiverHelper.ContainerName);
                if (container != null) UnityEngine.Object.DestroyImmediate(container.gameObject);
            }
        }

        public static void Cleanup(RamRiverOutput generator)
        {
            generator.ramRiverHelper?.Cleanup();
            foreach (var terrainTile in ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tiles.All())
            {
                var parent = terrainTile.ActiveTerrain.transform.parent.Find("Objects");
                var container = RamRiverHelper.GetContainer(parent, generator.guid, false);
                if (container != null) UnityEngine.Object.DestroyImmediate(container.gameObject);
            }
        }

        private static void AddMaskForCoordRect(Dictionary<HeightMapOffset, float[,]> masksByCoordOffset, Inlet<MatrixWorld> maskInput,
            Den.Tools.CoordRect rect, TileData tileData, StopToken stop)
        {
            var heightMapOffset = new HeightMapOffset(rect.offset.x, rect.offset.z);

            if (masksByCoordOffset.ContainsKey(heightMapOffset)) return;

            var mask = Generator.GetOrCacheMask(tileData, maskInput, rect, stop);
            masksByCoordOffset.Add(heightMapOffset, mask.ToFloatArray2D());
        }

        private static Den.Tools.CoordRect GetOtherCoordRect(Den.Tools.Coord border, Den.Tools.CoordRect thisRect)
        {
            if (border.x == thisRect.Min.x) return new Den.Tools.CoordRect(new Den.Tools.Coord(thisRect.Min.x - thisRect.size.x, thisRect.Min.z), thisRect.size);
            if (border.x == thisRect.Max.x) return new Den.Tools.CoordRect(new Den.Tools.Coord(thisRect.Min.x + thisRect.size.x, thisRect.Min.z), thisRect.size);
            if (border.z == thisRect.Min.z) return new Den.Tools.CoordRect(new Den.Tools.Coord(thisRect.Min.x, thisRect.Min.z - thisRect.size.z), thisRect.size);
            if (border.z == thisRect.Max.z) return new Den.Tools.CoordRect(new Den.Tools.Coord(thisRect.Min.x, thisRect.Min.z + thisRect.size.z), thisRect.size);

            throw new Exception(border + " is no border for " + thisRect);
        }
    }
}

#endif
#endif
