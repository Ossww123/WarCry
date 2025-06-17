#if ST_MM_2

using System;
using System.Collections.Generic;
using System.Linq;
using MapMagic.Core;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using MapMagic.Nodes.GUI;
using UnityEditor;

#endif

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    public class SplineToolsInstance : AbstractSplineToolsInstance
    {
        [SerializeField] private MapMagicObject mapMagicInstance;

        private static float _globalMaxRadius = float.MinValue;

        private static Func<Type, Color?> getGeneratorColor = (genericType) =>
        {
            if (genericType == typeof(Bounds)) return new Color(0.8f, 0.42f, 1f);
            if (genericType == typeof(WorldGraphGuid)) return new Color(0.05f, 0.9f, 1f);
            if (genericType == typeof(EdgesByOffset)) return new Color(1f, 0.89f, 0f);
            if (genericType == typeof(NodesByOffset)) return new Color(0f, 1f, 0.61f);
            return null;
        };

        /// <summary>
        /// Tells whether the used generators allow to use smaller CoordRects for heightMap caching. Generators that scale with the tile
        /// size prevent this, eg. SimpleForm, Import.
        /// </summary>
        public bool TileSubdivisionPossible { get; set; } = true;

        public MapMagicObject MapMagicInstance
        {
            get
            {
                if (mapMagicInstance == null) mapMagicInstance = Object.FindObjectOfType<MapMagicObject>();
                return mapMagicInstance;
            }
            set => mapMagicInstance = value;
        }

        protected override Cache NewCache()
        {
            return new MapMagicCache();
        }

        public override float GlobalMaxRadius()
        {
            if (_globalMaxRadius < 0)
            {
                var allGraphGenerators = MapMagicInstance.graph.generators.ToList();
                foreach (var subGraph in MapMagicInstance.graph.SubGraphs().Distinct())
                {
                    allGraphGenerators.AddRange(subGraph.generators);
                }

                allGraphGenerators.OfType<LayeredGenerator>().ToList().ForEach(g => g.Init());
                _globalMaxRadius = allGraphGenerators
                    .OfType<GraphGenerator>()
                    .Where(g => g.GetLocalTypes().Count != 0)
                    .Max(g => g.GetLocalTypes().Max(type => g.GetLocalRadiusRange(type).y));
            }

            return _globalMaxRadius;
        }

        public override Vector3 MapMagicGameObjectPosition()
        {
            return mapMagicInstance.transform.position;
        }

        protected override void DoResetState()
        {
            _globalMaxRadius = float.MinValue;

#if ST_RAM_2019 || ST_RAM
            RamLakeOutput.Cleanup();
            RamRiverOutput.Cleanup();
            RamRoadOutput.Cleanup();
#endif
        }

        protected override void DoGenerate()
        {
            var mmGraph = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph;
            if (mmGraph.ContainsGeneratorOfType<SimpleForm200>() || mmGraph.ContainsGeneratorOfType<Import200>())
            {
                Log.Warn(typeof(Generator), () => "Using SimpleForm or Import nodes may slow down generation with SplineTools.");
                TileSubdivisionPossible = false;
            } else
            {
                TileSubdivisionPossible = true;
            }

            StartGenerate();
        }

        /// copied from MapMagicObject, where this is private since 2.1.12 -.-
        private void StartGenerate(bool main = true, bool draft = true)
            /// Start generating all tiles (if the specified lod is enabled)
        {
            if (mapMagicInstance.graph == null)
                throw new Exception("MapMagic: Graph data is not assigned");

            if (draft || main)
                foreach (TerrainTile tile in mapMagicInstance.tiles.All())
                    tile.StartGenerate(mapMagicInstance.graph, main, draft); //enqueue all of chunks before starting generate
        }

        protected override bool GenerationIsRunning()
        {
            return MapMagicInstance.IsGenerating();
        }

        protected override void ClearResults()
        {
            // .Refresh will clear everything, but also starts new generation, if instantGenerate is true -> disable for the call
            var tmp = MapMagicInstance.instantGenerate;
            MapMagicInstance.instantGenerate = false;
            MapMagicInstance.Refresh(true);
            MapMagicInstance.instantGenerate = tmp;
        }

        public override void LockChunksIfRequired()
        {
            // no locked chunks for MM2
        }

        protected override void UnlockChunks()
        {
            // no locked chunks for MM2
        }

        public override void OnEnable()
        {
#if UNITY_EDITOR
            Log.Warn(this, () => "Attention: Using SplineTools with MapMagic 2 is currently experimental, at least until MapMagic 2 " +
                                 "officially supports third party generators. Any update may break compatibility without notice. " +
                                 "Feel free to try it out, but productive usage is discouraged!");

            var types = Den.Tools.ReflectionExtensions.Subtypes(typeof(SplineToolsGenerator));
            foreach (var type in types)
                if (!CreateRightClick.generatorTypes.Contains(type))
                    CreateRightClick.generatorTypes.Add(type);

#if ST_MM_2_COLORS
            if (!GeneratorDraw.getThirdPartyGeneratorColors.Contains(getGeneratorColor))
                GeneratorDraw.getThirdPartyGeneratorColors.Add(getGeneratorColor);
            if (!GeneratorDraw.getThirdPartyGeneratorProColors.Contains(getGeneratorColor))
                GeneratorDraw.getThirdPartyGeneratorProColors.Add(getGeneratorColor);
            if (!GeneratorDraw.getThirdPartyLinkColors.Contains(getGeneratorColor))
                GeneratorDraw.getThirdPartyLinkColors.Add(getGeneratorColor);
            if (!GeneratorDraw.getThirdPartyLinkProColors.Contains(getGeneratorColor))
                GeneratorDraw.getThirdPartyLinkProColors.Add(getGeneratorColor);
#endif
#endif

#if UNITY_EDITOR
            TerrainTile.OnTileApplied += UpdateStatistics;
            TerrainTile.OnBeforeTilePrepare += CheckGraphForLoops;
#endif
        }

        public override void OnDisable()
        {
#if UNITY_EDITOR
            TerrainTile.OnTileApplied -= UpdateStatistics;
            TerrainTile.OnBeforeTilePrepare -= CheckGraphForLoops;
#endif
        }

        private void CheckGraphForLoops(TerrainTile terrainTile, TileData tileData)
        {
#if ST_MM_2_BIOMES
            var visited = new HashSet<MapMagic.Nodes.Generator>();
            foreach (var generator in MapMagicInstance.Graph.generators.Reverse())
            {
                if (visited.Contains(generator)) continue;
                var stack = new LinkedList<MapMagic.Nodes.Generator>();
                CheckForLoops(generator, ref stack, ref visited);
            }
#endif
        }

#if ST_MM_2_BIOMES
        private void CheckForLoops(MapMagic.Nodes.Generator generator, ref LinkedList<MapMagic.Nodes.Generator> stack,
            ref HashSet<MapMagic.Nodes.Generator> visited)
        {
            if (stack.Contains(generator))
                throw new ApplicationException("Detected cycle in MapMagic graph at " + generator + " in chain "
                                               + Log.LogCollection(stack) + "!");

            if (visited.Contains(generator)) return;

            stack.AddLast(generator);
            visited.Add(generator);

            if (generator is IInlet<object> inlet)
            {
                var input = MapMagicUtil.GetActualInputGenerator(inlet);
                CheckForLoops(input, ref stack, ref visited);
            }

            if (generator is IMultiInlet multiInlet)
            {
                foreach (var inletItem in multiInlet.Inlets())
                {
                    var input = MapMagicUtil.GetActualInputGenerator(inletItem);
                    CheckForLoops(input, ref stack, ref visited);
                }
            }

            if (generator is IBiomePortalExit<object> biomePortalExit)
            {
                var input = (Generator) biomePortalExit.Enter();
                CheckForLoops(input, ref stack, ref visited);
            }

            if (generator is BiomeHeightSyncReferenceGenerator biomeHeightSyncReferenceGenerator)
            {
                var rootGraph = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph;
                var biomesSet200 = rootGraph.GeneratorsOfType<MapMagic.Nodes.Biomes.BiomesSet200>().First();
                foreach (var biomesSet200Layer in biomesSet200.layers)
                {
                    var biomeHeightEnter = biomesSet200Layer.graph.GeneratorsOfType<BiomeHeightSyncGenerator>()
                        .FirstOrDefault(g => g.linkedRefGuid == biomeHeightSyncReferenceGenerator.refGuid);

                    if (biomeHeightEnter != null) CheckForLoops(biomeHeightEnter, ref stack, ref visited);
                }
            }

            if (generator is MapMagic.Nodes.Biomes.BiomesSet200 biomesSet)
            {
                foreach (var layer in biomesSet.layers)
                {
                    foreach (var outputGenerator in layer.graph.GeneratorsOfType<OutputGenerator>())
                    {
                        CheckForLoops(outputGenerator, ref stack, ref visited);
                    }
                }
            }

            stack.RemoveLast();
        }
#endif

        private void UpdateStatistics(TerrainTile tile, TileData tileData, StopToken stop)
        {
            UpdateStatistics();
        }

        protected override void DrawGizmosForPlacePoi()
        {
#if UNITY_EDITOR
            // draw gizmos for all existing POI
            DrawGizmos("placePOI", (offset) =>
            {
                var manualScatterGenerators = MapMagicInstance.graph.generators.OfType<ManualScatterGenerator>();
                var tempColor = Handles.color;

                foreach (var gen in manualScatterGenerators)
                {
                    foreach (var layer in gen.layers)
                    {
                        Handles.color = layer.placingPoi ? Color.gray : Color.cyan;

                        var position = offset + layer.position;
                        Handles.DrawLine(position, position + Vector3.up * 100);
                        Handles.DrawWireDisc(position, Vector3.up, gen.useRadiusPerLayer ? layer.radius.ClampedValue : gen.radius.ClampedValue);
                    }
                }

                Handles.color = tempColor;
            });
#endif
        }
    }
}

#endif
