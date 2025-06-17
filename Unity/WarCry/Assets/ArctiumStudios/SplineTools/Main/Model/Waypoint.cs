using System;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class Waypoint
    {
        public Vector3 Position { get; set; }
        public Vector3 Tangent { get; set; }
        public float Width { get; set; }
        public Edge Edge { get; set; }
        public float EdgeProgress { get; set; }

        public Vector3 Normal
        {
            get { return Vector3.Cross(Tangent, Vector3.up).normalized; }
        }

        public Waypoint(Vector3 position, Vector3 tangent, float width, Edge edge, float edgeProgress)
        {
            Position = position;
            Tangent = tangent;
            Width = width;
            Edge = edge;
            EdgeProgress = edgeProgress;
        }

        public Vector3 Offset(float distance, Side side)
        {
            var normal = Normal * distance;

            switch (side)
            {
                case Side.Left:
                    return Position + normal;
                case Side.Right:
                    return Position - normal;
                case Side.Both:
                case Side.Random:
                    throw new ArgumentException("Only Side.Left and Side.Right are allowed here.");
                default:
                    throw new ArgumentOutOfRangeException("side", side, null);
            }
        }
    }
}