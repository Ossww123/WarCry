#if ST_MM_2

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Den.Tools.Matrices;
using Den.Tools.Tasks;
using MapMagic.Nodes;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public abstract class Generator : MapMagic.Nodes.Generator, SplineToolsGenerator
    {
        [NonSerialized] private static readonly HashSet<int> guidValidatedGeneratorHashCodes = new HashSet<int>();

        // only use for generators that really need the heights from different chunks
        protected float HeightMapped(Den.Tools.Coord point, IInlet<MatrixWorld> inputHeightMap, TileData tileData, StopToken stop)
        {
            var tileResolution = LowestPossibleTileResolution(tileData);

            var actualInlet = MapMagicUtil.GetActualInlet(inputHeightMap);

            var rect = MapMagicUtil.GetCoordRectFor(point, tileResolution);

            var subResult = GetOrCacheHeightMap(actualInlet, rect, tileData, stop);
            return subResult.GetInterpolated(point.x, point.z);
        }

        protected static int LowestPossibleTileResolution(TileData tileData)
        {
            var tileResolution = (int) ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tileResolution;

            if (!((SplineToolsInstance) SplineTools.Instance).TileSubdivisionPossible) return tileResolution;

            var factor = 2;

            while (tileData.area.active.worldSize.x / (factor * 2) % 1 == 0)
            {
                factor *= 2;
            }

            tileResolution = Mathf.Max(33, (tileResolution - 1) / factor + 1);
            return tileResolution;
        }

        public static Matrix GetOrCacheHeightMap(IInlet<MatrixWorld> actualInputGenerator, Den.Tools.CoordRect rect, TileData tileData, StopToken stop)
        {
            var cache = (MapMagicCache) SplineTools.Instance.state.cache;
            var subResult = cache.GetOrAddHeightMap(actualInputGenerator.GetHashCode().ToString(), rect,
                () => ComputeHeightMap(actualInputGenerator, rect, tileData, stop), stop);
            return subResult;
        }

        protected static MatrixWorld ComputeHeightMap(IInlet<MatrixWorld> actualInputGenerator, Den.Tools.CoordRect currentRect,
            TileData tileData, StopToken stop)
        {
            var newTileData = new ReadOnlyTileData(tileData);

            var offsetNumX = currentRect.offset.x / currentRect.size.x;
            var offsetNumZ = currentRect.offset.z / currentRect.size.z;
            var currentWorldSize = tileData.area.active.worldSize / ((float) (tileData.area.active.rect.size.x - 1) / (currentRect.size.x - 1));
            var dimensions = new Area.Dimensions(
                new Den.Tools.Vector2D(offsetNumX * currentWorldSize.x, offsetNumZ * currentWorldSize.z),
                currentWorldSize, currentRect.size.x);

            var activeWorldRect = currentRect.ToWorldSpaceRect(dimensions);
            var activeWorldCoordRect = new Den.Tools.CoordRect(activeWorldRect.min.x, activeWorldRect.min.y, activeWorldRect.size.x, activeWorldRect.size.y);

            newTileData.area = new Area(activeWorldCoordRect, currentRect, 0);

            var inputGenerator = MapMagicUtil.GetInputGenerator(actualInputGenerator);

            if (inputGenerator != null)
            {
                var containingGraph = MapMagicUtil.GetContainingGraph(inputGenerator);
                containingGraph.ClearChanged(newTileData);
                containingGraph.GenerateRecursive(inputGenerator, newTileData, containingGraph.defaults, stop);
                return newTileData.ReadInletProduct(actualInputGenerator);
            }

            throw new InvalidOperationException("Unexpected null input generator for " + actualInputGenerator);
        }

        protected void CacheHeightMaps(List<Den.Tools.CoordRect> coordRects, IInlet<MatrixWorld> inputHeightMap, TileData tileData, bool blocking,
            StopToken stop)
        {
            var actualInlet = MapMagicUtil.GetActualInlet(inputHeightMap);
            CacheHeights(coordRects,  actualInlet, tileData, blocking, stop);
        }

        protected void CacheHeights(List<Den.Tools.CoordRect> allRects, IInlet<MatrixWorld> actualInputGenerator, TileData tileData,
            bool blocking, StopToken stop)
        {
            var handles = new ManualResetEvent[allRects.Count];

            for (var i = 0; i < allRects.Count; i++)
            {
                handles[i] = new ManualResetEvent(false);
                var currentHandle = handles[i];
                var idx = i;

                var task = new ThreadManager.Task
                {
                    action = () =>
                    {
                        GetOrCacheHeightMap(actualInputGenerator, allRects[idx], tileData, stop);
                        currentHandle.Set();
                    }
                };

                ThreadManager.Enqueue(task);
            }

            if (blocking) WaitHandle.WaitAll(handles);
        }

        public static float SecondaryMaskMapped(Den.Tools.Coord point, IInlet<MatrixWorld> inputMask, TileData tileData, StopToken stop)
        {
            var rect = MapMagicUtil.GetCoordRectFor(point, (int) ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tileResolution);

            var actualMaskInlet = MapMagicUtil.GetActualInlet(inputMask);

            var subResult = GetOrCacheHeightMap(actualMaskInlet, rect, tileData, stop);

            return subResult.GetInterpolated(point.x, point.z);
        }

        protected Dictionary<Den.Tools.Coord, Matrix> MasksByOffset(Inlet<MatrixWorld> inputMask, TileData tileData, Den.Tools.CoordRect worldBounds, StopToken stop)
        {
            return MapMagicUtil.GetInputGenerator(inputMask) == null
                ? null
                : GenerateMaskByCoord(inputMask, tileData, worldBounds, stop);
        }

        protected Dictionary<Den.Tools.Coord, Matrix> GenerateMaskByCoord(Inlet<MatrixWorld> inputMask, TileData tileData,
            Den.Tools.CoordRect worldBounds, StopToken stop)
        {
            var allCoordRects = MapMagicUtil.GetAllCoordRects(tileData.area.active.rect.size.x, worldBounds);
            var maskByOffset = new Dictionary<Den.Tools.Coord, Matrix>();

            allCoordRects.ForEach(currentRect =>
            {
                var actualMaskInlet = MapMagicUtil.GetActualInlet(inputMask);
                var mask = GetOrCacheMask(tileData, actualMaskInlet, currentRect, stop);

                maskByOffset.Add(currentRect.offset, mask);
            });

            return maskByOffset;
        }

        public static MatrixWorld GetOrCacheMask(TileData data, IInlet<MatrixWorld> inputMask, Den.Tools.CoordRect currentRect, StopToken stop)
        {
            if (stop != null && stop.stop) return null;

            return SplineTools.Instance.state.cache.GetOrCache("mask" + inputMask.Gen.GetHashCode() + currentRect.offset, "notRelevant",
                () => ComputeHeightMap(inputMask, currentRect, data, stop));
        }

        public static void EnsureGuidIsUnique(string guid, object holder, Action<string> setter)
        {
            if (guidValidatedGeneratorHashCodes.Contains(holder.GetHashCode())) return;

            lock (typeof(Generator))
            {
                var guids = SplineTools.Instance.state.cache.GetOrCache("usedGeneratorGuids", "", () => new Dictionary<string, object>());

                if (guids.ContainsKey(guid))
                {
                    // all as expected
                    if (guids[guid] == holder) return;

                    // found duplicate -> assign a new one
                    Log.Warn(holder, () => "Found duplicate guid. Assigning new guid to " + holder.GetType().FullName + " and resetting " +
                                           "the graph. You can ignore this message when you just duplicated a generator.");
                    var newGuid = Guid.NewGuid().ToString();
                    guids.Add(newGuid, holder);
                    setter.Invoke(newGuid);

                    // reset graph of the old guid
                    SplineTools.Instance.state.graphsById.Remove(guid);
                } else
                    guids.Add(guid, holder);
            }

            guidValidatedGeneratorHashCodes.Add(holder.GetHashCode());
        }

        protected bool MandatoryInputMissing(params IInlet<object>[] inlets)
        {
            var missing = false;

            foreach (var inlet in inlets)
            {
                if (MapMagicUtil.GetInputGenerator(inlet) == null)
                {
                    Log.Error(this, () => "Missing mandatory input for inlet " + inlet);
                    missing = true;
                }
            }

            return missing;
        }

        public static int GetDominantBiomeIdx(Node node, TileData tileData, StopToken stop)
        {
#if ST_MM_2_BIOMES
            var rootGraph = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph;
            var biomesSet200 = rootGraph.GeneratorsOfType<MapMagic.Nodes.Biomes.BiomesSet200>().FirstOrDefault();

            if (biomesSet200 == null) return -1;

            var biomeMaskValues = new float[biomesSet200.layers.Length];

            for (var layerIdx = 1; layerIdx < biomesSet200.layers.Length; layerIdx++)
            {
                var biomesSet200Layer = biomesSet200.layers[layerIdx];
                biomeMaskValues[layerIdx] = SecondaryMaskMapped(node.PositionV2().ToMapSpaceCoord(tileData.area.active),
                    biomesSet200Layer, tileData, stop);
            }

            var remainingMask = 1f;
            for (var b = biomesSet200.layers.Length - 1; b >= 1; b--) remainingMask -= biomeMaskValues[b];
            biomeMaskValues[0] = remainingMask;

            var max = biomeMaskValues[0];
            var maxIdx = 0;

            for (var i = 1; i < biomeMaskValues.Length; i++)
            {
                if (biomeMaskValues[i] > max)
                {
                    max = biomeMaskValues[i];
                    maxIdx = i;
                }
            }

            return maxIdx;
#else
            return -1;
#endif
        }

        public static int GetBiomeIdx(MapMagic.Nodes.Generator generator)
        {
#if ST_MM_2_BIOMES
            var containingGraph = MapMagicUtil.GetContainingGraph(generator);
            var subGraphs = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph.SubGraphs().ToList();
            var currentBiomeIdx = subGraphs.FindIndex(sub => sub == containingGraph);
            return currentBiomeIdx;
#else
            return -1;
#endif
        }
    }
}

#endif
