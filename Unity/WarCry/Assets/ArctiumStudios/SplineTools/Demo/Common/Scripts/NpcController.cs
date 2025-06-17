using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools.Demo
{
    public class NpcController : MonoBehaviour
    {
        public float speed = 5f;

        private Node destination;
        private List<Waypoint> waypoints;
        private int currentWaypointIdx;
        private Waypoint currentWaypoint;
        private float heightOffset = 1f;

        // Use this for initialization
        void Start()
        {
        }

        // Update is called once per frame
        void Update()
        {
            if (destination == null)
            {
                FindDestination();
            }

            if (waypoints != null && currentWaypointIdx < waypoints.Count)
            {
                if (currentWaypoint == null)
                    currentWaypoint = waypoints[currentWaypointIdx];
                Walk();
            }
        }

        void Walk()
        {
            // rotate towards the target
            var transformPosition = transform.position;
            var target = currentWaypoint.Position;
            target.y = GetClosestTerrain(target).SampleHeight(target) + heightOffset;
            transform.forward = Vector3.RotateTowards(
                transform.forward,
                target - transformPosition,
                speed * Time.deltaTime,
                0.0f);

            // move towards the target
            transform.position = Vector3.MoveTowards(transformPosition, target, speed * Time.deltaTime);

            if ((transform.position - target).magnitude < 0.1f)
            {
                currentWaypointIdx++;
                if (currentWaypointIdx < waypoints.Count)
                {
                    currentWaypoint = waypoints[currentWaypointIdx];
                } else
                {
                    Log.Debug(this, () => "Reached destination: " + destination);
                    // reset
                    destination = null;
                    waypoints = null;
                }
            }
        }

        Terrain GetClosestTerrain(Vector3 playerPos)
        {
            var terrains = Terrain.activeTerrains;

            var lowestDistance = float.MaxValue;
            var terrainIdx = 0;

            for (var i = 0; i < terrains.Length; i++)
            {
                var center = new Vector3(terrains[i].transform.position.x + terrains[i].terrainData.size.x / 2, playerPos.y,
                    terrains[i].transform.position.z + terrains[i].terrainData.size.z / 2);

                var distance = (center - playerPos).sqrMagnitude;

                if (distance > lowestDistance) continue;

                lowestDistance = distance;
                terrainIdx = i;
            }

            return terrains[terrainIdx];
        }

        void FindDestination()
        {
            // find nearest node of any type
            var nodesInRange = SplineTools.FindNodesInRange(gameObject.transform.position.V2(), 200f);
            var source = nodesInRange.First();

            // find all nodes of type POI that are reachable from here
            var candidates = SplineTools.FindReachableNodes(source, 5000);
            candidates.Remove(source);

            // select a random destination
            var rndIdx = (int) (candidates.Count * Random.value);
            var nextDestination = candidates.ToArray()[rndIdx];

            // get waypoints from source to destination
            var shortestPath = SplineTools.FindShortestPath(source, nextDestination);
            var nextWaypoints = SplineTools.GetWaypoints(shortestPath, 2f);

            Log.Debug(this, () => "Next destination: " + nextDestination);

            // set fields
            destination = nextDestination;
            waypoints = nextWaypoints;
            currentWaypointIdx = 0;
        }
    }
}