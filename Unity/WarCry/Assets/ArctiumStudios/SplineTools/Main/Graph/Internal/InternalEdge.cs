using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

#endif

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    public class InternalEdge : Edge, ISerializationCallbackReceiver
    {
        [NonSerialized] private Edge previous;
        [NonSerialized] private Edge next;
        [NonSerialized] private Connection connection;

        [NonSerialized] private List<Node> nodes;
        [NonSerialized] private InternalBezierCurve internalBezierCurve;
        [SerializeField] private List<float> widths;

        // cached reversed edge
        [NonSerialized] private InternalEdge reversed;
        [NonSerialized] private bool transient = false;

        // de-/serialization helpers
        [SerializeField] public string[] serializedNodeGuids;
        [SerializeField] private Vector3 startBezierPosition;
        [SerializeField] private Vector3 startBezierControlPosition;
        [SerializeField] private Vector3 endBezierPosition;
        [SerializeField] private Vector3 endBezierControlPosition;

        public InternalEdge(Node source, Node destination, List<float> widths, BezierCurve bezierCurve = null, bool transient = false)
        {
            nodes = new List<Node> {source, destination};
            this.internalBezierCurve = (InternalBezierCurve) bezierCurve;
            this.widths = widths;
            this.transient = transient;
        }

        public Vector3 V3()
        {
            return nodes[1].Position() - nodes[0].Position();
        }

        public float StraightSlopeDegrees()
        {
            return 90 - Vector3.Angle(Vector3.up, V3());
        }

        public List<float> Widths()
        {
            return widths.ToList();
        }

        public Edge Next(bool skipBorder = false)
        {
            return skipBorder && next.Source().Type().IsBorder()
                ? next.Next(skipBorder)
                : next;
        }

        public Edge Previous(bool skipBorder = false)
        {
            return skipBorder && previous.Destination().Type().IsBorder()
                ? previous.Previous(skipBorder)
                : previous;
        }

        public List<Node> Nodes()
        {
            return nodes;
        }

        public Node Source()
        {
            return nodes[0];
        }

        public Node Destination()
        {
            return nodes[1];
        }

        public Connection Connection()
        {
            return connection;
        }

        public float Length()
        {
            return internalBezierCurve.Length();
        }

        public BezierCurve BezierCurve()
        {
            return internalBezierCurve;
        }


        public float Weight()
        {
            return Length();
        }

        public bool IsSecondary()
        {
            return nodes.Any(node => node.Type().IsCrossing())
                   && (Equals(nodes[0].BelongsTo(), nodes[1]) || Equals(nodes[1].BelongsTo(), nodes[0]))
                   && nodes.All(node => node.Type().BaseType != NodeBaseType.Custom);
        }

        public Edge Reversed(Connection reversedConnection)
        {
            if (reversed == null)
            {
                var reverseWidths = widths.AsReadOnly().Reverse().ToList();
                var reversedEdge = new InternalEdge(nodes[1], nodes[0], reverseWidths,
                    new InternalBezierCurve(internalBezierCurve.Destination().Position(), internalBezierCurve.Destination().ControlPosition(),
                        internalBezierCurve.Source().Position(), internalBezierCurve.Source().ControlPosition()));
                reversedEdge.reversed = this;
                reversedEdge.transient = true;
                reversedEdge.next = previous;
                reversedEdge.previous = next;
                reversedEdge.connection = reversedConnection;

                reversed = reversedEdge;
            }

            return reversed;
        }

        public void SetNext(Edge nextEdge)
        {
            this.next = nextEdge;
            if (reversed != null) reversed.previous = nextEdge;
        }

        public void SetPrevious(Edge previousEdge)
        {
            this.previous = previousEdge;
            if (reversed != null) reversed.next = previousEdge;
        }

        public void SetWidths(List<float> widths)
        {
            this.widths = widths;
            if (reversed != null) reversed.widths = widths.AsReadOnly().Reverse().ToList();
        }

        public void SetConnection(Connection newConnection)
        {
            this.connection = newConnection;
            if (reversed != null) reversed.connection = newConnection;
        }

        public bool Inside(Rect rect)
        {
            return rect.Contains(nodes[0].PositionV2())
                   || rect.Contains(nodes[1].PositionV2());
        }

        public bool FullInside(Rect rect)
        {
            var firstInside = rect.Contains(nodes[0].PositionV2());
            var firstBorder = rect.HasBorder(nodes[0].PositionV2());
            var lastInside = rect.Contains(nodes[1].PositionV2());
            var lastBorder = rect.HasBorder(nodes[1].PositionV2());

            return (firstInside || firstBorder) && (lastInside || lastBorder);
        }

        public void WalkBezier(Action<IntermediatePoint, IntermediatePoint> callback, float resolutionModifier)
        {
            //Find the total length of the curve
            var totalLength = internalBezierCurve.Length();

            //How many sections do we want to divide the curve into
            var parts = (int) (totalLength * resolutionModifier);

            //What's the length of one section?
            var sectionLength = totalLength / parts;

            //Init the variables we need in the loop
            var currentDistance = 0f;

            //The curve's start position
            var firstNode = nodes[0];

            var lastPoint = new IntermediatePoint(firstNode.Position(), widths[0], 0, totalLength, 0, parts);

            for (var i = 1; i <= parts; i++)
            {
                //Add to the distance traveled on the line so far
                currentDistance += sectionLength;

                //Use Newton–Raphsons method to find the t value from the start of the curve 
                //to the end of the distance we have
                var t = internalBezierCurve.FindTValue(currentDistance, totalLength);

                if (t < 0 || t > 1) throw new Exception("t is negative at " + currentDistance + "/" + totalLength + ": " + t);

                //Get the coordinate on the Bezier curve at this t value
                var pos = internalBezierCurve.InterpolatedPosition(t);
                var interpolatedWidth = InterpolatedWidth(t);
                var point = new IntermediatePoint(pos, interpolatedWidth, currentDistance, totalLength, i, parts);

                callback(lastPoint, point);

                //Save the last position
                lastPoint = point;
            }
        }

        public float InterpolatedWidth(float t)
        {
            return Mathf.Lerp(widths[0], widths[1], t);
        }

        public bool IsTransient()
        {
            return transient;
        }

        public void SetTransient(bool transient)
        {
            this.transient = transient;
        }

        public void DrawGizmos(GizmoLevel gizmoLevel)
        {
#if UNITY_EDITOR
            SplineTools.Instance.DrawGizmos(this, (offset) =>
            {
                if (gizmoLevel >= GizmoLevel.Basic)
                {
                    //The Bezier curve's color
                    Gizmos.color = Color.white;

                    //Find the total length of the curve
                    var totalLength = internalBezierCurve.Length();

                    //How many sections do we want to divide the curve into
                    var parts = 10;

                    //What's the length of one section?
                    var sectionLength = totalLength / parts;

                    //Init the variables we need in the loop
                    var currentDistance = 0f;

                    //The curve's start position
                    var lastPos = internalBezierCurve.Source().Position();

                    for (var i = 0; i <= parts; i++)
                    {
                        //Use Newton–Raphsons method to find the t value from the start of the curve 
                        //to the end of the distance we have
                        var t = internalBezierCurve.FindTValue(currentDistance, totalLength);

                        //Get the coordinate on the Bezier curve at this t value
                        var pos = internalBezierCurve.InterpolatedPosition(t);

                        //Draw this line segment
                        Gizmos.DrawLine(offset + lastPos, offset + pos);

                        //Save the last position
                        lastPos = pos;

                        //Add to the distance traveled on the line so far
                        currentDistance += sectionLength;
                    }

                    if (Connection().Direction() == Directions.OneWayForward || Connection().Direction() == Directions.TwoWay)
                    {
                        var beforeDestination = internalBezierCurve.InterpolatedPosition(0.9f);
                        var destination = internalBezierCurve.Destination().Position();
                        var destinationNormal = Vector3.Cross(destination - beforeDestination, Vector3.up).normalized;
                        
                        Gizmos.color = Color.green;
                        Gizmos.DrawLine(offset + destination, offset + beforeDestination + destinationNormal);
                        Gizmos.DrawLine(offset + destination, offset + beforeDestination - destinationNormal);
                        
                    }
                    
                    if (Connection().Direction() == Directions.OneWayBackward || Connection().Direction() == Directions.TwoWay)
                    {
                        var afterSource = internalBezierCurve.InterpolatedPosition(0.1f);
                        var source = internalBezierCurve.Source().Position();
                        var sourceNormal = Vector3.Cross(source - afterSource, Vector3.up).normalized;
                        
                        Gizmos.color = Color.white;
                        Gizmos.DrawLine(offset + source, offset + afterSource + sourceNormal);
                        Gizmos.DrawLine(offset + source, offset + afterSource - sourceNormal);
                    }

                }

                if (gizmoLevel >= GizmoLevel.Full)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(offset + internalBezierCurve.Source().Position(), offset + internalBezierCurve.Source().ControlPosition());
                    Gizmos.DrawRay(offset + internalBezierCurve.Source().ControlPosition(), Vector3.up);

                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(offset + internalBezierCurve.Destination().Position(), offset + internalBezierCurve.Destination().ControlPosition());
                    Gizmos.DrawRay(offset + internalBezierCurve.Destination().ControlPosition(), Vector3.up);
                    
                    //Also draw lines between the control points and endpoints
                    Handles.Label(offset + internalBezierCurve.Source().Position(), nodes[0].ToString());
                    Handles.Label(offset + internalBezierCurve.Destination().Position(), nodes[1].ToString());
                }
            });
#endif
        }

        private bool Equals(InternalEdge other)
        {
            return nodes.SequenceEqual(other.nodes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((InternalEdge) obj);
        }

        public override int GetHashCode()
        {
            if (nodes == null) return 0;

            var hashCode = 74672451;
            foreach (var node in nodes) hashCode *= node.GetHashCode();
            return hashCode;
        }

        public override string ToString()
        {
            return "{" + nodes[0] + " -> " + nodes[1] + "}";
//            return "{" + nodes[0] + "->" + nodes[1] + ", " + bezier + "}";
        }

        public void OnBeforeSerialize()
        {
            if (nodes == null || nodes.Count == 0) return;

            serializedNodeGuids = nodes.Select(n => n.Guid()).ToArray();
            startBezierPosition = internalBezierCurve.Source().Position();
            startBezierControlPosition = internalBezierCurve.Source().ControlPosition();
            endBezierPosition = internalBezierCurve.Destination().Position();
            endBezierControlPosition = internalBezierCurve.Destination().ControlPosition();
        }

        public void OnAfterDeserialize()
        {
            // deserialization of references done in Connection
            nodes = new List<Node>();
            internalBezierCurve = new InternalBezierCurve(
                startBezierPosition, startBezierControlPosition,
                endBezierPosition, endBezierControlPosition);
        }

        public class IntermediatePoint
        {
            public Vector3 position;
            public float width;
            public float length;
            public float totalLength;
            public int currentPart;
            public int totalParts;

            public IntermediatePoint(Vector3 position, float width, float length, float totalLength, int currentPart, int totalParts)
            {
                this.position = position;
                this.width = width;
                this.length = length;
                this.totalLength = totalLength;
                this.currentPart = currentPart;
                this.totalParts = totalParts;
            }
        }
    }
}