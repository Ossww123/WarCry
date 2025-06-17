#if ST_MM_2

using System;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    public class ClampedInt
    {
        [SerializeField] public int value;
        [SerializeField] private int min;
        [SerializeField] private int max;

        public virtual int ClampedValue => Mathf.Clamp(value, min, max);

        public ClampedInt()
        {
        }

        public ClampedInt(int value, int min, int max)
        {
            this.value = value;
            this.min = min;
            this.max = max;
        }
    }

    [Serializable]
    public class ClampedIntDynamic : ClampedInt
    {
        [NonSerialized] private readonly Func<int> min;
        [NonSerialized] private readonly Func<int> max;

        public override int ClampedValue => Mathf.Clamp(value, min.Invoke(), max.Invoke());

        public ClampedIntDynamic()
        {
        }

        public ClampedIntDynamic(int value, Func<int> min, Func<int> max)
        {
            this.value = value;
            this.min = min;
            this.max = max;
        }
    }
}

#endif