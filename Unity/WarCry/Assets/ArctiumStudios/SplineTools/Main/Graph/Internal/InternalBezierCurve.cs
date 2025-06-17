using System;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    public class InternalBezierCurve : BezierCurve
    {
        [SerializeField] private InternalBezierPoint source, destination;

        private float length = -1;

        public InternalBezierCurve(Vector3 source, Vector3 sourceControl, Vector3 destination, Vector3 destinationControl)
        {
            this.source = new InternalBezierPoint(source, sourceControl);
            this.destination = new InternalBezierPoint(destination, destinationControl);
        }

        public BezierPoint Source()
        {
            return source;
        }

        public BezierPoint Destination()
        {
            return destination;
        }

        public float Length(float tStart = 0f, float tEnd = 1f)
        {
            if (tStart > 0 || tEnd < 1) return GetLengthSimpsons(tStart, tEnd);

            if (length <= 0) length = GetLengthSimpsons(0f, 1f);
            return length;
        }

        public Vector3 V3()
        {
            return destination.Position() - source.Position();
        }

        public Vector3 InterpolatedPosition(float t)
        {
            // De Casteljau's Algorithm
            var oneMinusPos = 1f - t;

            //Layer 1
            var q = oneMinusPos * source.Position() + t * source.ControlPosition();
            var r = oneMinusPos * source.ControlPosition() + t * destination.ControlPosition();
            var s = oneMinusPos * destination.ControlPosition() + t * destination.Position();

            //Layer 2
            var p = oneMinusPos * q + t * r;
            var t1 = oneMinusPos * r + t * s;

            //Final interpolated position
            return oneMinusPos * p + t * t1;
        }

        private Vector3 DeCasteljausAlgorithmDerivative(float t)
        {
            //The derivative of cubic De Casteljau's Algorithm
            var dU = (source.Position() - 3f * (source.ControlPosition() - destination.ControlPosition()) - destination.Position()) * (-3f * (t * t));

            dU += (source.Position() - 2f * source.ControlPosition() + destination.ControlPosition()) * (6f * t);
            dU += -3f * (source.Position() - source.ControlPosition());

            return dU;
        }

        //Get and infinite small length from the derivative of the curve at position t
        private float GetArcLengthIntegrand(float t)
        {
            return DeCasteljausAlgorithmDerivative(t).magnitude;
        }

        //Get the length of the curve between two t values with Simpson's rule
        private float GetLengthSimpsons(float tStart, float tEnd)
        {
            //This is the resolution and has to be even
            var n = 20;

            //Now we need to divide the curve into sections
            var delta = (tEnd - tStart) / n;

            //The main loop to calculate the length

            //Everything multiplied by 1
            var endPoints = GetArcLengthIntegrand(tStart) + GetArcLengthIntegrand(tEnd);

            //Everything multiplied by 4
            var x4 = 0f;
            for (var i = 1; i < n; i += 2)
            {
                var t = tStart + delta * i;

                x4 += GetArcLengthIntegrand(t);
            }

            //Everything multiplied by 2
            var x2 = 0f;
            for (var i = 2; i < n; i += 2)
            {
                var t = tStart + delta * i;

                x2 += GetArcLengthIntegrand(t);
            }

            //The final length
            return (delta / 3f) * (endPoints + 4f * x4 + 2f * x2);
        }

        //Use Newton–Raphsons method to find the t value at the end of this distance d
        public float FindTValue(float d, float totalLength)
        {
            if (d <= 0) return 0;
            if (d >= totalLength) return 1;

            var t = d / totalLength;

            //Need an error so we know when to stop the iteration
            const float error = 0.001f;

            //We also need to avoid infinite loops
            var iterations = 0;

            while (true)
            {
                //Newton's method
                var tNext = t - (GetLengthSimpsons(0f, t) - d) / GetArcLengthIntegrand(t);

                //Have we reached the desired accuracy?
                if (Mathf.Abs(tNext - t) < error) break;

                t = tNext;

                iterations += 1;

                if (iterations > 1000) break;
            }

            return Mathf.Clamp01(t);
        }

        public override string ToString()
        {
            return "{" + source + " -> " + destination + "}";
        }
    }
}