#if ST_MM_1 || ST_MM_2

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools.Generators.Spline
{
    public static class ConnectionsV2Helper
    {
        public enum Algorithm
        {
            Nearest,
            Random
        }

        public static void GenerateConnections(
#if ST_MM_1
            MapMagic.CoordRect coordRect,
#elif ST_MM_2
            Den.Tools.CoordRect coordRect,
#endif
            InternalWorldGraph graph, string layerGuid, Vector2 worldOffset, string[] types,
            int source, int destination, bool deferred, GraphGenerator inputGraphGenerator, bool useSourcePerimeter, bool useDestinationPerimeter,
            string layerConnectionType, Algorithm usedAlgorithm, ConnectionFactory connectionFactory, int iterations, bool connectedGraph, 
            Func<float, float> floatToMapSpace, Func<Vector2, Vector2> vector2ToMapSpace)
        {
            if (graph.ProcessedGraphComponentsContains(layerGuid + worldOffset)) return;

            graph.ProcessedGraphComponentsAdd(layerGuid + worldOffset);

            var layerSourceTypeName = types[source];
            var layerDestinationTypeName = types[destination];

            var sourceNodes = graph.Nodes(new[] {NodeType.Of(NodeBaseType.Custom, layerSourceTypeName)})
                .Where(n => !graph.ProcessedGraphComponentsContains(layerGuid + n.Guid())).ToList();
            var destinationNodes = graph.Nodes(new[] {NodeType.Of(NodeBaseType.Custom, layerDestinationTypeName)}).ToList();

            if (deferred)
            {
                // find relevant points for the current rect
                // which are all 'destination' nodes inside the rect and all 'source' nodes within the radius from RadiusScatter
                var sourceRadiusScatter = GraphGeneratorHelper.GetGeneratorForType(inputGraphGenerator,
                    layerSourceTypeName) as IRadiusScatterGenerator;
                var destinationRadiusScatter = GraphGeneratorHelper.GetGeneratorForType(inputGraphGenerator,
                    layerDestinationTypeName) as IRadiusScatterGenerator;

                if (sourceRadiusScatter == null && destinationRadiusScatter == null)
                    throw new Exception("Can't find RadiusScatterGenerator");

                if (sourceRadiusScatter != null)
                {
                    var outerMargin = sourceRadiusScatter.OuterMargin();
                    var maxNodeRadius = GraphGeneratorHelper.GetRadiusRange(sourceRadiusScatter, sourceRadiusScatter.GetAroundType()).y;
                    var maxScatterRadius = maxNodeRadius - outerMargin;
                    var expandedCoordRect = coordRect.Expanded(Mathf.CeilToInt(floatToMapSpace.Invoke(maxScatterRadius)));

                    if (destinationRadiusScatter == null)
                        destinationNodes = destinationNodes
                            .Where(n => expandedCoordRect.Contains(vector2ToMapSpace.Invoke(n.PositionV2()))).ToList();

                    sourceNodes = sourceNodes
                        .Where(n => GraphUtil.DistanceIsTooLow(n.Position(), maxScatterRadius, destinationNodes))
                        .ToList();
                }

                if (destinationRadiusScatter != null)
                {
                    var outerMargin = destinationRadiusScatter.OuterMargin();
                    var maxNodeRadius = GraphGeneratorHelper
                        .GetRadiusRange(destinationRadiusScatter, destinationRadiusScatter.GetAroundType()).y;
                    var maxScatterRadius = maxNodeRadius - outerMargin;
                    var expandedCoordRect = coordRect.Expanded(Mathf.CeilToInt(floatToMapSpace.Invoke(maxScatterRadius)));

                    if (sourceRadiusScatter == null)
                        sourceNodes = sourceNodes
                            .Where(n => expandedCoordRect.Contains(vector2ToMapSpace.Invoke(n.PositionV2()))).ToList();

                    destinationNodes = destinationNodes
                        .Where(n => GraphUtil.DistanceIsTooLow(n.Position(), maxScatterRadius, destinationNodes))
                        .ToList();
                }
            }

            // remember all processed 'source' nodes, they won't be processed again
            sourceNodes.ForEach(n => graph.ProcessedGraphComponentsAdd(layerGuid + n.Guid()));


            if (useSourcePerimeter) sourceNodes = sourceNodes.SelectMany(n => n.GetPerimeterNodes()).Distinct().ToList();
            if (useDestinationPerimeter)
                destinationNodes = destinationNodes.SelectMany(n => n.GetPerimeterNodes()).Distinct().ToList();

            var connectionType = ConnectionType.Of(ConnectionBaseType.Custom, layerConnectionType);

            // connect nodes
            switch (usedAlgorithm)
            {
                case Algorithm.Nearest:
                    connectionFactory.ConnectNearestEach(sourceNodes, destinationNodes, connectionType, iterations);
                    break;
                case Algorithm.Random:
                    connectionFactory.ConnectRandomEach(sourceNodes, destinationNodes, connectionType, iterations);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // connect unlinked subgraphs
            if (source == destination && connectedGraph)
            {
                var allNodes = new List<Node>();
                allNodes.AddRange(sourceNodes);
                allNodes.AddRange(destinationNodes);

                connectionFactory.ConnectSplitGraphs(allNodes, connectionType, NodesBelongTogether(source,
                    destination, inputGraphGenerator, usedAlgorithm, types));
            }
        }

        public static bool SourceBelongsToDestination(int src, int dst, GraphGenerator inputGraphGenerator, Algorithm usedAlgorithm, string[] types)
        {
            var generatorSource = GraphGeneratorHelper.GetGeneratorForType(inputGraphGenerator, types[src]) as RadiusScatterGenerator;

            return generatorSource != null
                   && generatorSource.aroundType == dst
                   && usedAlgorithm == Algorithm.Nearest;
        }

        public static bool SourceAndDestinationBelongToSameNode(int src, int dst, GraphGenerator inputGraphGenerator, Algorithm usedAlgorithm,
            string[] types)
        {
            var generatorSource = GraphGeneratorHelper.GetGeneratorForType(inputGraphGenerator, types[src]) as RadiusScatterGenerator;
            var generatorDestination = GraphGeneratorHelper.GetGeneratorForType(inputGraphGenerator, types[dst]) as RadiusScatterGenerator;

            return generatorSource != null && generatorDestination != null
                                           && generatorSource.aroundType == generatorDestination.aroundType
                                           && usedAlgorithm == Algorithm.Nearest;
        }

        public static bool IsAroundType(string type, GraphGenerator inputGraphGenerator)
        {
            if (inputGraphGenerator == null) return false;
            return GraphGeneratorHelper.GetGeneratorForType(inputGraphGenerator, type) is RadiusScatterGenerator;
        }

        public static float MinDistanceInLayer(int source, int destination, GraphGenerator inputGraphGenerator, string[] types)
        {
            float minDistance;

            if (source == destination)
            {
                minDistance = GraphGeneratorHelper.GetMinDistanceToSame(inputGraphGenerator, types[source]);
                minDistance -= 2 * GraphGeneratorHelper.GetRadiusRange(inputGraphGenerator, types[source]).y;
            } else
            {
                var higherType = types[source > destination ? source : destination];
                minDistance = GraphGeneratorHelper.GetMinDistanceToOthers(inputGraphGenerator, higherType);

                var generatorForType = GraphGeneratorHelper.GetGeneratorForType(inputGraphGenerator, higherType);

                if (generatorForType is IBoundedScatterGenerator)
                {
                    minDistance -= GraphGeneratorHelper.GetRadiusRange(inputGraphGenerator, types[source]).y
                                   + GraphGeneratorHelper.GetRadiusRange(inputGraphGenerator, types[destination]).y;
                }
            }

            return minDistance;
        }

        public static float MaxHeightDifferenceInLayer(int source, int destination, GraphGenerator inputGraphGenerator, string[] types)
        {
            float maxHeightDifference;

            if (source == destination)
            {
                var range = GraphGeneratorHelper.GetHeightRange(inputGraphGenerator, types[source]);
                maxHeightDifference = range.y - range.x;
            } else
            {
                var rangeSource = GraphGeneratorHelper.GetHeightRange(inputGraphGenerator, types[source]);
                var rangeDestination = GraphGeneratorHelper.GetHeightRange(inputGraphGenerator, types[destination]);

                maxHeightDifference = Mathf.Max(
                    rangeSource.y - rangeDestination.x,
                    rangeDestination.y - rangeSource.x);
            }

            return maxHeightDifference;
        }

        public static bool NodesBelongTogether(int source, int destination, GraphGenerator inputGraphGenerator,
            Algorithm usedAlgorithm, string[] types)
        {
            return SourceBelongsToDestination(source, destination, inputGraphGenerator, usedAlgorithm, types)
                   || SourceBelongsToDestination(destination, source, inputGraphGenerator, usedAlgorithm, types)
                   || SourceAndDestinationBelongToSameNode(source, destination, inputGraphGenerator, usedAlgorithm, types);
        }
        
        public static float MaxSectionLength(int source, int destination, GraphGenerator inputGraphGenerator, string[] types)
        {
            return Mathf.Ceil(MinDistanceInLayer(source, destination, inputGraphGenerator, types) / 3);
        }

        public static float MinSlope(int source, int destination, GraphGenerator inputGraphGenerator, string[] types)
        {
            return Mathf.Ceil(new Vector3(MinDistanceInLayer(source, destination, inputGraphGenerator, types), 
                MaxHeightDifferenceInLayer(source, destination, inputGraphGenerator, types), 0).AngleToGround());
        }
    }
}

#endif
