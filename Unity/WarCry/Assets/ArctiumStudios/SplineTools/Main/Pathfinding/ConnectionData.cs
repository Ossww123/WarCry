using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace ArctiumStudios.SplineTools
{
    public class ConnectionData
    {
        public InternalConnection connection;
        public float fallbackWidth;
        public readonly Pathfinder.Options options;
        public readonly HashSet<Node> nearbyLakes;
        public readonly List<ConnectionWaypoint> waypoints;
        public int currentWaypoint = 0;
        public HashSet<Edge> processedIntersections = new HashSet<Edge>();
        public delegate Vector3 FindControlAction<in T1, in T2, in T3, in T4, in T5, in T6>(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6);

        // source, destination, controlDistance, connectionData, fixToControlHeight, rnd
        public readonly FindControlAction<Node, Node, float, ConnectionData, bool, Random> findControlAction;

        public ConnectionData(Pathfinder.Options options, InternalConnection connection, float fallbackWidth,
            FindControlAction<Node, Node, float, ConnectionData, bool, Random> findControlAction, List<ConnectionWaypoint> waypoints, 
            HashSet<Node> nearbyLakes)
        {
            this.connection = connection;
            this.findControlAction = findControlAction;
            this.waypoints = waypoints;
            this.nearbyLakes = nearbyLakes;
            this.fallbackWidth = fallbackWidth;
            this.options = options;
        }

        public ConnectionWaypoint CurrentWaypoint()
        {
            return waypoints[currentWaypoint];
        }

    }
}