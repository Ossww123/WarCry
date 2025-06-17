#if ST_MM_2

using System;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    public class ClampedFloat
    {
        [SerializeField] public float value;
        [SerializeField] private float min;
        [SerializeField] private float max;

        public virtual float ClampedValue => Mathf.Clamp(value, min, max);

        public ClampedFloat()
        {
        }

        public ClampedFloat(float value, float min, float max)
        {
            this.value = value;
            this.min = min;
            this.max = max;
        }
    }

    [Serializable]
    public class ClampedFloatDynamic : ClampedFloat
    {
        [NonSerialized] private readonly Func<float> min;
        [NonSerialized] private readonly Func<float> max;

        public override float ClampedValue => Mathf.Clamp(value, min.Invoke(), max.Invoke());

        public ClampedFloatDynamic()
        {
        }

        public ClampedFloatDynamic(float value, Func<float> min, Func<float> max)
        {
            this.value = value;
            this.min = min;
            this.max = max;
        }
    }
}

#endif