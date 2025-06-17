using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class ConnectionWaypoint
    {
        public readonly Vector3 position;
        public readonly Vector3? controlDirection;
        public readonly Type type;
        public readonly Dictionary<string, string> nodeData;
        public readonly Node relatedNode;

        public ConnectionWaypoint(Vector3 position, Type type, Vector3? controlDirection = null, Dictionary<string, string> nodeData = null,
            Node relatedNode = null)
        {
            this.position = position;
            this.type = type;
            this.controlDirection = controlDirection;
            this.nodeData = nodeData;
            this.relatedNode = relatedNode;
        }
        
        public NodeType NodeType(NodeType destinationType)
        {
            switch (type)
            {
                case Type.Default:
                    return ArctiumStudios.SplineTools.NodeType.Of(NodeBaseType.Section);
                case Type.LakeBridgeSource:
                    return ArctiumStudios.SplineTools.NodeType.Of(NodeBaseType.SectionLakeBridgeSource);
                case Type.LakeBridgeDestination:
                    return ArctiumStudios.SplineTools.NodeType.Of(NodeBaseType.SectionLakeBridgeDestination);
                case Type.RiverBridgeSource:
                    return ArctiumStudios.SplineTools.NodeType.Of(NodeBaseType.SectionRiverBridgeSource);
                case Type.RiverBridgeDestination:
                    return ArctiumStudios.SplineTools.NodeType.Of(NodeBaseType.SectionRiverBridgeDestination);
                case Type.RiverFordSource:
                    return ArctiumStudios.SplineTools.NodeType.Of(NodeBaseType.SectionRiverFordSource);
                case Type.RiverFordDestination:
                    return ArctiumStudios.SplineTools.NodeType.Of(NodeBaseType.SectionRiverFordDestination);
                case Type.Destination:
                case Type.Perimeter:
                    return destinationType;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override string ToString()
        {
            return "ConnectionWaypoint{" + position + ", " + type + "}";
        }

        protected bool Equals(ConnectionWaypoint other)
        {
            return position.Equals(other.position) && type == other.type;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ConnectionWaypoint) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (position.GetHashCode() * 397) ^ (int) type;
            }
        }

        public enum Type
        {
            Default,
            LakeBridgeSource,
            LakeBridgeDestination,
            RiverBridgeSource,
            RiverBridgeDestination,
            RiverFordSource,
            RiverFordDestination,
            Destination,
            Perimeter
        }
    }
}