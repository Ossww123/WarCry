using System;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    public class InternalBezierPoint : BezierPoint
    {
        [SerializeField] private Vector3 position;
        [SerializeField] public Vector3 controlPosition;

        public InternalBezierPoint(Vector3 position, Vector3 controlPosition)
        {
            this.position = position;
            this.controlPosition = controlPosition;
        }

        public Vector3 Position()
        {
            return position;
        }

        public Vector3 ControlPosition()
        {
            return controlPosition;
        }

        public Vector3 ControlV3()
        {
            return ControlPosition() - Position();
        }

        public override string ToString()
        {
            return position.ToPreciseString() + ":" + controlPosition.ToPreciseString();
        }
    }
}