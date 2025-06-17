using System;

namespace ArctiumStudios.SplineTools
{
    public enum Directions
    {
        OneWayForward, OneWayBackward, TwoWay
    }

    public static class DirectionsExtensions
    {
        public static Directions Reversed(this Directions direction)
        {
            switch (direction)
            {
                case Directions.OneWayForward:
                    return Directions.OneWayBackward;
                case Directions.OneWayBackward:
                    return Directions.OneWayForward;
                case Directions.TwoWay:
                    return Directions.TwoWay;
                default:
                    throw new ArgumentOutOfRangeException("direction", direction, null);
            }
        }
    }
}