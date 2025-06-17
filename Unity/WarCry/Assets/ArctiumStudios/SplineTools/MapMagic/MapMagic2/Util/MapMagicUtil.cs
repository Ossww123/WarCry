#if ST_MM_2

using System.Collections.Generic;
using System.Linq;
using MapMagic.Nodes;
using MapMagic.Products;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public static class MapMagicUtil
    {
        public static bool OutOfBounds(Den.Tools.CoordRect smallRect, Den.Tools.CoordRect bigRect)
        {
            return !Den.Tools.CoordinatesExtensions.Intersects(
                new Rect(smallRect.offset.vector2, smallRect.size.vector2),
                new Rect(bigRect.offset.vector2, bigRect.size.vector2));
        }

        public static List<Den.Tools.CoordRect> GetTouchedCoordRects(Den.Tools.Coord source, Den.Tools.Coord destination, int resolution)
        {
            var startRect = GetCoordRectFor(source, resolution);
            var endRect = GetCoordRectFor(destination, resolution);

            if (startRect.Equals(endRect)) return new List<Den.Tools.CoordRect>();

            var minX = Mathf.Min(startRect.Min.x, endRect.Min.x);
            var minZ = Mathf.Min(startRect.Min.z, endRect.Min.z);
            var maxX = Mathf.Max(startRect.Max.x, endRect.Max.x);
            var maxZ = Mathf.Max(startRect.Max.z, endRect.Max.z);

            var bounds = new Den.Tools.CoordRect(minX, minZ, maxX - minX, maxZ - minZ);

            var allCoordRects = GetAllCoordRects(resolution, bounds);

            return allCoordRects.Where(rect => rect.Equals(startRect)
                                               || rect.Equals(endRect)
                                               || Util.BorderIntersection(source.vector2,
                                                   destination.vector2, new Rect(rect.offset.vector2, rect.size.vector2)).HasValue).ToList();
        }

        public static Den.Tools.CoordRect GetUiCoordRectFor(Den.Tools.Coord point)
        {
            var tileResolution = (int) ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tileResolution;
            return GetCoordRectFor(point, tileResolution);
        }

        public static Den.Tools.CoordRect GetCoordRectFor(Den.Tools.Coord point, int resolution)
        {
            var x = Util.OffsetValue(point.x, resolution);
            var z = Util.OffsetValue(point.z, resolution);

            // compute input map for a small sub region to get the (approximate) terrain height at the candidate's position
            var subRect = new Den.Tools.CoordRect(x, z, resolution, resolution);
            return subRect;
        }

        public static List<Den.Tools.CoordRect> GetAllCoordRects(int baseResolution, Den.Tools.CoordRect worldBounds)
        {
            var allRects = new List<Den.Tools.CoordRect>();
            var resolution = baseResolution;

            for (var x = worldBounds.Min.x; x < worldBounds.Max.x; x = x + resolution)
            {
                for (var z = worldBounds.Min.z; z < worldBounds.Max.z; z = z + resolution)
                {
                    allRects.Add(new Den.Tools.CoordRect(x, z, resolution, resolution));
                }
            }

            return allRects;
        }

        public static GraphGenerator GetInputGraphGenerator(IInlet<WorldGraphGuid> inputGraph)
        {
            if (inputGraph == null) return null;

            return (GraphGenerator) GetActualInputGenerator(inputGraph);
        }

        public static MapMagic.Nodes.Generator GetInputGenerator<T>(IInlet<T> inlet) where T : class
        {
            var graph = GetContainingGraph(inlet.Gen);
            return graph != null ? Den.Tools.Extensions.CheckGet(graph.links, inlet)?.Gen : null;
        }

        public static Graph GetContainingGraph(MapMagic.Nodes.Generator generator)
        {
            var mapMagicInstance = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance;

            // search in root graph first
            var generatorInRootGraph = mapMagicInstance.graph.ContainsGenerator(generator);

            if (generatorInRootGraph) return mapMagicInstance.graph;

            // then search in biomes/subgraphs
            var subGraphs = mapMagicInstance.graph.SubGraphs().Distinct().ToList();

            return subGraphs.FirstOrDefault(subGraph => subGraph.ContainsGenerator(generator));
        }

        public static IEnumerable<T> FindAllGeneratorsOfType<T>()
        {
            var mapMagicInstance = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance;

            return FindAllGeneratorsOfType<T>(mapMagicInstance.graph);
        }

        private static IEnumerable<T> FindAllGeneratorsOfType<T>(Graph graph)
        {
            var found = new List<T>();

            found.AddRange(graph.GeneratorsOfType<T>());

            foreach (var subGraph in graph.SubGraphs())
            {
                found.AddRange(FindAllGeneratorsOfType<T>(subGraph));
            }

            return found;
        }

        public static MapMagic.Nodes.Generator GetActualInputGenerator<T>(IInlet<T> input) where T : class
        {
            var inputGenerator = GetInputGenerator(input);

            return inputGenerator is IPortalExit<T> portal
                ? GetActualInputGenerator(portal.RefreshEnter(((SplineToolsInstance)SplineTools.Instance).MapMagicInstance.graph))
                : inputGenerator;
        }

        public static IInlet<T> GetActualInlet<T>(IInlet<T> input) where T : class
        {
            var inputGenerator = GetInputGenerator(input);

            return inputGenerator is IPortalExit<T> portal
                ? portal.RefreshEnter(((SplineToolsInstance)SplineTools.Instance).MapMagicInstance.graph)
                : input;
        }

        public static TileData NewTileData(TileData tileData)
        {
            var newTileData = new TileData();
            newTileData.area = tileData.area;
            newTileData.globals = tileData.globals;
            newTileData.random = new Den.Tools.Noise(tileData.random);
            newTileData.isDraft = tileData.isDraft;
            newTileData.isPreview = tileData.isPreview;

            newTileData.Clear();
            return newTileData;
        }
    }
}

#endif