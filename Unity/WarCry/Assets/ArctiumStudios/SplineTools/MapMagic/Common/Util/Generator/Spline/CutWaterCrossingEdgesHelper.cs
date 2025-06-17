using System.Collections.Generic;
using System.Linq;

namespace ArctiumStudios.SplineTools.Generators.Spline
{
    public static class CutWaterCrossingEdgesHelper
    {
        public class Result
        {
            public readonly EdgesByOffset bridgeEdges;
            public readonly EdgesByOffset fordEdges;
            public readonly EdgesByOffset otherEdges;

            public Result(EdgesByOffset bridgeEdges, EdgesByOffset fordEdges, EdgesByOffset otherEdges)
            {
                this.bridgeEdges = bridgeEdges;
                this.fordEdges = fordEdges;
                this.otherEdges = otherEdges;
            }
        }

        public static Result BridgeEdges(EdgesByOffset connections, int generatorSeed)
        {
            var allConnections = connections.SelectMany(c => c.Value).Distinct().ToList();
            var bridgeConnections = new HashSet<Connection>();
            var fordConnections = new HashSet<Connection>();
            var otherConnections = new HashSet<Connection>();

            //random
            var rnd = new ConsistentRandom(generatorSeed + 54321);

            foreach (var connection in allConnections)
                CutConnections(connection, bridgeConnections, fordConnections, otherConnections, rnd);

            //creating dst
            var bridgeEdges = GetConnectionsByOffset(bridgeConnections);
            var fordEdges = GetConnectionsByOffset(fordConnections);
            var otherEdges = GetConnectionsByOffset(otherConnections);
            
            return new Result(bridgeEdges, fordEdges, otherEdges);
        }

        public static EdgesByOffset GetConnectionsByOffset(HashSet<Connection> bridgeConnections)
        {
            var bridgeEdges = new EdgesByOffset();

            foreach (var connection in bridgeConnections)
            {
                var offsets = new HashSet<Offset>();

                foreach (var node in connection.Nodes())
                {
                    var offset = Offset.For(node.Position(), InternalWorldGraph.OffsetResolution);
                    if (!offsets.Contains(offset)) offsets.Add(offset);
                }

                foreach (var offset in offsets)
                {
                    if (!bridgeEdges.ContainsKey(offset)) bridgeEdges.Add(offset, new List<Connection>());
                    bridgeEdges[offset].Add(connection);
                }
            }

            return bridgeEdges;
        }

        public static void CutConnections(Connection connection, HashSet<Connection> bridgeConnections, HashSet<Connection> fordConnections,
            HashSet<Connection> otherConnections, ConsistentRandom rnd)
        {
            var edges = connection.Edges();
            var nodes = connection.Nodes();

            if (!nodes.Any(n => n.Type().IsWaterCrossing()))
            {
                otherConnections.Add(connection);
                return;
            }

            var currentConnection = new InternalConnection(rnd.NextGuid().ToString(), connection.Type(), connection.Direction(), true);

            foreach (var edge in edges)
            {
                var transientEdge = new InternalEdge(edge.Source(), edge.Destination(), edge.Widths(), edge.BezierCurve(), true);
                currentConnection.StoreEdge(transientEdge);

                if (edge.Destination().Type().IsWaterCrossing())
                {
                    AddConnectionIfMatching(currentConnection, bridgeConnections, fordConnections, otherConnections);

                    currentConnection = new InternalConnection(rnd.NextGuid().ToString(), connection.Type(), connection.Direction(), true);
                }
            }

            AddConnectionIfMatching(currentConnection, bridgeConnections, fordConnections, otherConnections);
        }

        private static void AddConnectionIfMatching(InternalConnection currentConnection, HashSet<Connection> bridgeConnections,
            HashSet<Connection> fordConnections, HashSet<Connection> otherConnections)
        {
            if (currentConnection.Source().Type().BaseType == NodeBaseType.SectionRiverBridgeSource &&
                currentConnection.Destination().Type().BaseType == NodeBaseType.SectionRiverBridgeDestination ||
                currentConnection.Source().Type().BaseType == NodeBaseType.SectionLakeBridgeSource &&
                currentConnection.Destination().Type().BaseType == NodeBaseType.SectionLakeBridgeDestination)
            {
                bridgeConnections.Add(currentConnection);
            } else if (currentConnection.Source().Type().BaseType == NodeBaseType.SectionRiverFordSource &&
                       currentConnection.Destination().Type().BaseType == NodeBaseType.SectionRiverFordDestination)
            {
                fordConnections.Add(currentConnection);
            } else
            {
                otherConnections.Add(currentConnection);
            }
        }
    }
}