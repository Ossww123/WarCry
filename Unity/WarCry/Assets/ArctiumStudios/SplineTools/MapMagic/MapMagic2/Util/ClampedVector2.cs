#if ST_MM_2

using System;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    public class ClampedVector2
    {
        [SerializeField] public Vector2 value;
        [SerializeField] private float min;
        [SerializeField] private float max;

        public virtual Vector2 ClampedValue => new Vector2(Mathf.Clamp(value.x, min, max), Mathf.Clamp(value.y, min, max));

        public ClampedVector2()
        {
        }

        public ClampedVector2(Vector2 value, float min, float max)
        {
            this.value = value;
            this.min = min;
            this.max = max;
        }
    }

    [Serializable]
    public class ClampedVector2Dynamic : ClampedVector2
    {
        [NonSerialized] private readonly Func<float> min;
        [NonSerialized] private readonly Func<float> max;

        public override Vector2 ClampedValue => new Vector2(Mathf.Clamp(value.x, min.Invoke(), max.Invoke()), Mathf.Clamp(value.y, min.Invoke(), max.Invoke()));

        public ClampedVector2Dynamic()
        {
        }

        public ClampedVector2Dynamic(Vector2 value, Func<float> min, Func<float> max)
        {
            this.value = value;
            this.min = min;
            this.max = max;
        }
    }
}

#endif