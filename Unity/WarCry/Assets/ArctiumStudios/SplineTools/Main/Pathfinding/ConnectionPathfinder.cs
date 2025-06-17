using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public abstract class ConnectionPathfinder : Pathfinder
    {
        protected ConnectionPathfinder(InternalWorldGraph graph, string reference, Func<Vector2, float> heightFunc, Options options, 
            Func<bool> stopFunc) : base(graph, reference, heightFunc, options, stopFunc)
        {
        }

        public abstract void Connect(Node source, Node destination, ConnectionType type, HashSet<Node> blacklist = null);

        public abstract List<Connection> ConnectIterative(Node source, Node destination, ConnectionType type, HashSet<Node> blacklist);
        
        public bool ReachableWithMaxSlope(Node source, Node destination)
        {
            return !NotReachableWithMaxSlope(source, destination);
        }

        public bool NotReachableWithMaxSlope(Node source, Node destination)
        {
            var deltaV2 = destination.PositionV2() - source.PositionV2();
            var deltaPlain = new Vector3(deltaV2.x, 0, deltaV2.y).normalized;

            var sourceBorder = source.Position() + deltaPlain * source.Radius();
            var destinationBorder = destination.Position() - deltaPlain * destination.Radius();

            var angleToGround = (destinationBorder - sourceBorder).AngleToGround();

            return angleToGround > options.slopeMax;
        }
        
        protected override float InterpolatedWidthAtBorder(Node source, Node destination, float progressAtBorder)
        {
            return Mathf.Lerp(source.Radius() * 2, destination.Radius() * 2, progressAtBorder);
        }
        
    }
}