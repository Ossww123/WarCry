#if ST_MM_2 && ST_MM_2_BIOMES

using System;
using System.Linq;
using System.Threading;
using Den.Tools.Matrices;
using Den.Tools.Tasks;
using MapMagic.Nodes;
using MapMagic.Nodes.Biomes;
using MapMagic.Products;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Biomes",
        name = "Biome Height Sync", colorType = typeof(MatrixWorld),
        iconName = "GeneratorIcons/PortalIn",
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/biome/biome_height_sync")]
    public class BiomeHeightSyncGenerator : Generator, IInlet<MatrixWorld>, IOutlet<MatrixWorld>
    {
        public string linkedRefGuid = "";

        public override void Generate(TileData tileData, StopToken stop)
        {
            if (stop != null && stop.stop || !enabled || MandatoryInputMissing(this))
            {
                tileData.StoreProduct(this, new MatrixWorld(tileData.area.full.rect, tileData.area.full.worldPos,
                    tileData.area.full.worldSize, tileData.globals.height));
                return;
            }

            var matrix = GetOrCacheCombinedBiomesHeightMap(tileData, linkedRefGuid, stop);

            tileData.StoreProduct(this, matrix);
        }

        public static MatrixWorld GetOrCacheCombinedBiomesHeightMap(TileData tileData, string linkedRefGuid, StopToken stop)
        {
            var cache = (MapMagicCache) SplineTools.Instance.state.cache;
            var subResult = cache.GetOrAddHeightMap(linkedRefGuid, tileData.area.full.rect,
                () => ComputeCombinedBiomesHeightMap(tileData, linkedRefGuid, stop), stop);
            return (MatrixWorld) subResult;
        }

        public static MatrixWorld ComputeCombinedBiomesHeightMap(TileData tileData, string linkedRefGuid, StopToken stop)
        {
            var rootGraph = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph;
            var biomesSet200 = rootGraph.GeneratorsOfType<BiomesSet200>().First();
            var layersCount = biomesSet200.layers.Length;
            var biomeHeights = new MatrixWorld[layersCount];
            var biomeMasks = new MatrixWorld[layersCount];

            var handles = new ManualResetEvent[layersCount];

            for (var layerIdx = 0; layerIdx < layersCount; layerIdx++)
            {
                handles[layerIdx] = new ManualResetEvent(false);
                var currentHandle = handles[layerIdx];
                var idx = layerIdx;

                var task = new ThreadManager.Task
                {
                    action = () =>
                    {
                        var biomesSet200Layer = biomesSet200.layers[idx];

                        var biomeHeightEnter = biomesSet200Layer.graph.GeneratorsOfType<BiomeHeightSyncGenerator>()
                            .FirstOrDefault(g => g.linkedRefGuid == linkedRefGuid);

                        if (biomeHeightEnter == null)
                        {
                            var refName = FindRefName(linkedRefGuid, rootGraph);
                            throw new InvalidOperationException("Biome at index " + idx + " is missing BiomeHeightSync linked to " + refName);
                        }

                        var biomeHeightInputGenerator = MapMagicUtil.GetInputGenerator(biomeHeightEnter);

                        if (biomeHeightInputGenerator == null)
                        {
                            var refName = FindRefName(linkedRefGuid, rootGraph);
                            throw new InvalidOperationException("Biome at index " + idx + " is missing input for BiomeHeightSync linked to " +
                                                                refName);
                        }

                        var newBiomeTileData = new ReadOnlyTileData(tileData);

                        // just to get RefreshInputHashIds() called. required if the biome graph has not started to generate before.
                        // else the links will not be setup correctly
                        biomesSet200Layer.graph.ClearChanged(newBiomeTileData);

                        biomesSet200Layer.graph.GenerateRecursive(biomeHeightInputGenerator, newBiomeTileData, biomesSet200Layer.graph.defaults, stop);

                        biomeHeights[idx] = newBiomeTileData.ReadInletProduct(biomeHeightEnter);

                        if (idx > 0)
                        {
                            var biomeMaskInputGenerator = MapMagicUtil.GetInputGenerator(biomesSet200Layer);
                            var newBiomeMaskTileData = new ReadOnlyTileData(tileData);
                            rootGraph.GenerateRecursive(biomeMaskInputGenerator, newBiomeMaskTileData, rootGraph.defaults, stop);
                            var biomeMask = newBiomeMaskTileData.ReadInletProduct(biomesSet200Layer);

                            biomeMasks[idx] = biomeMask;
                        }

                        currentHandle.Set();
                    }
                };

                if (ThreadManager.Count < ThreadManager.maxThreads - 1)
                {
                    // already running inside an active task from MM'2 tile generation ->
                    // only enqueue if work can be started immediately
                    ThreadManager.Enqueue(task);

                    if (!task.Active && task.Enqueued)
                    {
                        // possible race condition due to lack of locks. another task could've been enqueued in the meantime
                        // and blocks execution -> dequeue and run sync
                        Log.Debug(typeof(BiomeHeightSyncGenerator), (() => "Race Condition hit. Dequeue task and run sync."));
                        ThreadManager.Dequeue(task);
                        task.action();
                    }
                } else
                {
                    task.action();
                }
            }

            WaitHandle.WaitAll(handles);

            if (stop.stop) return null;
            if (biomeHeights[0] == null) return null;

            var combinedHeight = new MatrixWorld(biomeHeights[0].rect, biomeHeights[0].worldPos, biomeHeights[0].worldSize);

            for (var i = 0; i < combinedHeight.arr.Length; i++)
            {
                var remainingMask = 1f;
                var combined = 0f;
                for (var b = layersCount - 1; b >= 1; b--)
                {
                    var biomeMask = biomeMasks[b].arr[i];
                    combined += biomeMask * biomeHeights[b].arr[i];
                    remainingMask -= biomeMask;
                }

                combined += remainingMask * biomeHeights[0].arr[i];
                combinedHeight.arr[i] = combined;
            }

            return combinedHeight;
        }

        private static string FindRefName(string linkedRefGuid, Graph rootGraph)
        {
            var refName = rootGraph.GeneratorsOfType<BiomeHeightSyncReferenceGenerator>()
                .FirstOrDefault(n => n.refGuid == linkedRefGuid);

            return refName?.refName ?? "unlinked";
        }
    }
}

#endif
