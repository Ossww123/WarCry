#if ST_MM_2

using System.Collections.Generic;
using System.Linq;
using MapMagic.Nodes;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public static class GraphGeneratorHelper
    {
        public static List<string> GetTypes(GraphGenerator gg)
        {
            var types = new List<string>();
            
            if (gg == null) return types;
            
            if (gg.GetInputGraph() != null && MapMagicUtil.GetInputGenerator(gg.GetInputGraph()) != null)
            {
                types.AddRange(GetTypes(MapMagicUtil.GetInputGraphGenerator(gg.GetInputGraph())));
            }

            types.AddRange(gg.GetLocalTypes());

            return types;
        }

        public static float GetMinDistanceToSame(GraphGenerator gg, string type)
        {
            return gg.GetLocalTypes().Contains(type)
                ? gg.GetLocalMinDistanceToSame(type)
                : GetMinDistanceToSame(MapMagicUtil.GetInputGraphGenerator(gg.GetInputGraph()), type);
        }


        public static float GetMinDistanceToOthers(GraphGenerator gg, string type)
        {
            return gg.GetLocalTypes().Contains(type)
                ? gg.GetLocalMinDistanceToOthers(type)
                : GetMinDistanceToOthers(MapMagicUtil.GetInputGraphGenerator(gg.GetInputGraph()), type);
        }


        public static Vector2 GetHeightRange(GraphGenerator gg, string type)
        {
            return gg.GetLocalTypes().Contains(type)
                ? gg.GetLocalHeightRange(type)
                : GetHeightRange(MapMagicUtil.GetInputGraphGenerator(gg.GetInputGraph()), type);
        }


        public static Vector2 GetRadiusRange(GraphGenerator gg, string type)
        {
            return gg.GetLocalTypes().Contains(type)
                ? gg.GetLocalRadiusRange(type)
                : GetRadiusRange(MapMagicUtil.GetInputGraphGenerator(gg.GetInputGraph()), type);
        }

        public static GraphGenerator GetGeneratorForType(GraphGenerator gg, string type)
        {
            return gg.GetLocalTypes().Contains(type)
                ? gg
                : GetGeneratorForType(MapMagicUtil.GetInputGraphGenerator(gg.GetInputGraph()), type);
        }

        public static void CollectAllInputGraphGenerators(GraphGenerator gg, List<GraphGenerator> gens)
        {
            gens.Add(gg);
            var inputGraphGenerator = MapMagicUtil.GetInputGraphGenerator(gg.GetInputGraph());
            if (inputGraphGenerator != null) CollectAllInputGraphGenerators(inputGraphGenerator, gens);
        }

        public static List<string> GetAllConnectedTypes(GraphGenerator gg)
        {
            var graphGenerators = new HashSet<GraphGenerator>();
            CollectConnectedGraphGenerators(gg, graphGenerators);

            return graphGenerators.Where(gen => gen != gg).SelectMany(gen => gen.GetLocalTypes()).ToList();
        }

        private static void CollectConnectedGraphGenerators(GraphGenerator gg, HashSet<GraphGenerator> gens)
        {
            gens.Add(gg);
            ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph.generators
                .OfType<GraphGenerator>()
                .Where(gen => !gens.Contains(gen))
                .Where(gen => gen.GetInputGraph() != null && gg.Equals(gen.GetInputGraph().Gen)
                              || gg.GetInputGraph() != null && gen.Equals(gg.GetInputGraph().Gen))
                .ToList()
                .ForEach(gen => CollectConnectedGraphGenerators(gen, gens));
        }
    }

    public interface GraphGenerator
    {
        List<string> GetLocalTypes();
        float GetLocalMinDistanceToSame(string type);
        float GetLocalMinDistanceToOthers(string type);
        Vector2 GetLocalHeightRange(string type);
        Vector2 GetLocalRadiusRange(string type);
        IInlet<WorldGraphGuid> GetInputGraph();
        IOutlet<WorldGraphGuid> GetOutputGraph();
    }
}

#endif