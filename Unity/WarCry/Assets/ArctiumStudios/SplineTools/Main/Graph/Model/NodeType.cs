using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public enum NodeBaseType
    {
        /// <summary>
        /// Endpoint Node placed by the User.
        /// </summary>
        Custom,

        /// <summary>
        /// Intermediate Node of normal Connections that always has exactly two Edges.
        /// </summary>
        Section,

        /// <summary>
        /// Marks the position where an Edge crosses between Chunks.
        /// </summary>
        Border,

        /// <summary>
        /// Marks the position where a Connection enters/leaves the area of a Node, that is defined by the Node's radius and mask.
        /// </summary>
        Perimeter,

        /// <summary>
        /// Marks the position where multiple Connections meet that is not of type Custom. Always has more than two Edges.  
        /// </summary>
        Crossing,

        /// <summary>
        /// Marks the position directly before the actual Crossing where a Connection enters the area of another Connection,
        /// that is defined by the Connection's width.
        /// </summary>
        CrossingPerimeter,

        /// <summary>
        /// Endpoint Node that is the center of a Lake. A Node of this type always has the <see cref="Constants.LakeOutline"/> stored within its Data.
        /// </summary>
        Lake,

        /// <summary>
        /// Endpoint Node that marks the position at which a River became too small and dried up.
        /// </summary>
        RiverDryUp,

        /// <summary>
        /// Endpoint Node that marks the position at which a River ends in the sea.
        /// </summary>
        Sea,

        /// <summary>
        /// Marks the position next to the <see cref="RiverPerimeter"/> inside the lake.
        /// </summary>
        LakeInnerExit,

        /// <summary>
        /// Marks the position next to the <see cref="RiverPerimeter"/> outside the lake.
        /// </summary>
        LakeOuterExit,

        /// <summary>
        /// Marks the position next to the <see cref="RiverPerimeter"/> inside the sea.
        /// </summary>
        SeaInnerExit,
        
        /// <summary>
        /// Marks the position next to the <see cref="RiverPerimeter"/> outside the sea.
        /// </summary>
        SeaOuterExit,

        /// <summary>
        /// Intermediate Node of a River that always has exactly two Edges.
        /// </summary>
        RiverSection,

        /// <summary>
        /// Marks the position where an Edge of a River crosses between Chunks.
        /// </summary>
        RiverBorder,

        /// <summary>
        /// Marks the position where a River enters/leaves the area of a Lake or the Sea, as defined by its outline.
        /// </summary>
        RiverPerimeter,

        /// <summary>
        /// Marks the position where multiple Rivers meet that is not of type Lake. Always has more than two Edges.  
        /// </summary>
        RiverCrossing,

        /// <summary>
        /// Marks the position directly before the actual RiverCrossing where a River enters the area of another River,
        /// that is defined by the River's width.
        /// </summary>
        RiverCrossingPerimeter,

        /// <summary>
        /// Marks the position where a bridge across a lake starts.
        /// </summary>
        SectionLakeBridgeSource,
        
        /// <summary>
        /// Marks the position where a bridge across a lake ends.
        /// </summary>
        SectionLakeBridgeDestination,
        
        /// <summary>
        /// Marks the position where a bridge across a river starts.
        /// </summary>
        SectionRiverBridgeSource,
        
        /// <summary>
        /// Marks the position where a bridge across a river ends.
        /// </summary>
        SectionRiverBridgeDestination,
        
        /// <summary>
        /// Marks the position where a ford across a river starts.
        /// </summary>
        SectionRiverFordSource,
        
        /// <summary>
        /// Marks the position where a ford across a river ends.
        /// </summary>
        SectionRiverFordDestination
    }

    [Serializable]
    public class NodeType
    {
        [SerializeField] private NodeBaseType baseType;
        [SerializeField] private string customType;

        private static Dictionary<NodeBaseType, Dictionary<string, NodeType>> _cachedTypes =
            new Dictionary<NodeBaseType, Dictionary<string, NodeType>>();

        public static readonly HashSet<NodeBaseType> endpointBaseTypes = new HashSet<NodeBaseType>
        {
            NodeBaseType.Custom, NodeBaseType.Lake, NodeBaseType.LakeInnerExit, NodeBaseType.Sea, NodeBaseType.RiverDryUp, NodeBaseType.Perimeter,
            NodeBaseType.SectionLakeBridgeSource, NodeBaseType.SectionLakeBridgeDestination,
            NodeBaseType.SectionRiverBridgeSource, NodeBaseType.SectionRiverBridgeDestination,
            NodeBaseType.SectionRiverFordSource, NodeBaseType.SectionRiverFordDestination
        };

        public static readonly HashSet<NodeBaseType> borderBaseTypes = new HashSet<NodeBaseType>
            {NodeBaseType.Border, NodeBaseType.RiverBorder};

        public static readonly HashSet<NodeBaseType> sectionBaseTypes = new HashSet<NodeBaseType>
            {NodeBaseType.Section, NodeBaseType.RiverSection,
                NodeBaseType.LakeInnerExit, NodeBaseType.LakeOuterExit,
                NodeBaseType.SeaInnerExit, NodeBaseType.SeaOuterExit};

        public static readonly HashSet<NodeBaseType> perimeterBaseTypes = new HashSet<NodeBaseType>
            {NodeBaseType.Perimeter, NodeBaseType.RiverPerimeter};

        public static readonly HashSet<NodeBaseType> crossingBaseTypes = new HashSet<NodeBaseType>
            {NodeBaseType.Crossing, NodeBaseType.RiverCrossing};

        public static readonly HashSet<NodeBaseType> crossingPerimeterBaseTypes = new HashSet<NodeBaseType>
            {NodeBaseType.CrossingPerimeter, NodeBaseType.RiverCrossingPerimeter};

        public static readonly HashSet<NodeBaseType> waterCrossingBaseTypes = new HashSet<NodeBaseType>
        {
            NodeBaseType.SectionLakeBridgeSource, NodeBaseType.SectionLakeBridgeDestination,
            NodeBaseType.SectionRiverBridgeSource, NodeBaseType.SectionRiverBridgeDestination,
            NodeBaseType.SectionRiverFordSource, NodeBaseType.SectionRiverFordDestination
        };
        
        public static readonly HashSet<NodeBaseType> riverBaseTypes = new HashSet<NodeBaseType>
        {
            NodeBaseType.RiverBorder, NodeBaseType.RiverCrossing, NodeBaseType.RiverPerimeter, NodeBaseType.RiverSection, 
            NodeBaseType.RiverCrossingPerimeter, NodeBaseType.RiverDryUp, NodeBaseType.LakeInnerExit, NodeBaseType.LakeOuterExit,
            NodeBaseType.SeaInnerExit, NodeBaseType.SeaOuterExit
        };

        private NodeType(NodeBaseType baseType, string customType)
        {
            this.baseType = baseType;
            this.customType = customType;
        }

        public NodeBaseType BaseType
        {
            get { return baseType; }
        }

        public string CustomType
        {
            get { return customType; }
        }

        public static NodeType Of(NodeBaseType baseType, string customType = null)
        {
            if (!_cachedTypes.ContainsKey(baseType)) _cachedTypes.Add(baseType, new Dictionary<string, NodeType>());
            var cachedCustomTypes = _cachedTypes[baseType];
            var nonNullCustomType = customType == null ? "" : customType;
            if (cachedCustomTypes.ContainsKey(nonNullCustomType)) return cachedCustomTypes[nonNullCustomType];

            var nodeType = new NodeType(baseType, customType);
            cachedCustomTypes.Add(nonNullCustomType, nodeType);

            return nodeType;
        }


        protected bool Equals(NodeType other)
        {
            return baseType == other.baseType && string.Equals(customType, other.customType);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((NodeType) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int) baseType + 397) ^ (customType != null ? customType.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return baseType + " " + (customType == null ? "" : customType);
        }
    }

    public static class NodeTypeExtensions
    {
        public static bool IsEndpoint(this NodeType nodeType)
        {
            return NodeType.endpointBaseTypes.Contains(nodeType.BaseType);
        }

        public static bool IsSection(this NodeType nodeType)
        {
            return NodeType.sectionBaseTypes.Contains(nodeType.BaseType);
        }

        public static bool IsBorder(this NodeType nodeType)
        {
            return NodeType.borderBaseTypes.Contains(nodeType.BaseType);
        }

        public static bool IsPerimeter(this NodeType nodeType)
        {
            return NodeType.perimeterBaseTypes.Contains(nodeType.BaseType);
        }

        public static bool IsCrossing(this NodeType nodeType)
        {
            return NodeType.crossingBaseTypes.Contains(nodeType.BaseType);
        }

        public static bool IsCrossingPerimeter(this NodeType nodeType)
        {
            return NodeType.crossingPerimeterBaseTypes.Contains(nodeType.BaseType);
        }

        public static bool IsWaterCrossing(this NodeType nodeType)
        {
            return NodeType.waterCrossingBaseTypes.Contains(nodeType.BaseType);
        }
        
        public static bool IsRiver(this NodeType nodeType)
        {
            return NodeType.riverBaseTypes.Contains(nodeType.BaseType);
        }
    }
}
