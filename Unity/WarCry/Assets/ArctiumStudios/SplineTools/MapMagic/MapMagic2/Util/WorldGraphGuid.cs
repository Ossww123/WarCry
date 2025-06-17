using System;

namespace ArctiumStudios.SplineTools
{
    public class WorldGraphGuid : ICloneable
    {
        public readonly string value;

        public WorldGraphGuid(string value)
        {
            this.value = value;
        }

        public object Clone()
        {
            return new WorldGraphGuid(value);
        }
    }
}