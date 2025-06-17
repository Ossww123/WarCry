using System.Collections.Generic;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public class ClampedCurve
    {
        private static readonly Dictionary<AnimationCurve, ClampedCurve> CachedCurves = new Dictionary<AnimationCurve, ClampedCurve>();

        private readonly AnimationCurve animationCurve;

        private ClampedCurve(AnimationCurve animationCurve)
        {
            this.animationCurve = animationCurve;
        }

        public static ClampedCurve For(AnimationCurve animationCurve)
        {
            lock (CachedCurves)
            {
                if (!CachedCurves.ContainsKey(animationCurve)) CachedCurves.Add(animationCurve, new ClampedCurve(animationCurve));
                
                return CachedCurves[animationCurve];
            }
        }

        public float Evaluate(float time)
        {
            var keys = animationCurve.keys;

            if (time <= keys[0].time) return keys[0].value;
            if (time >= keys[keys.Length - 1].time) return keys[keys.Length - 1].value;

            for (var p = 0; p < keys.Length - 1; p++)
            {
                if (time > keys[p].time && time <= keys[p + 1].time)
                {
                    var prev = keys[p];
                    var next = keys[p + 1];

                    var delta = next.time - prev.time;
                    var relativeTime = (time - prev.time) / delta;

                    var timeSq = relativeTime * relativeTime;
                    var timeCu = timeSq * relativeTime;

                    var a = 2 * timeCu - 3 * timeSq + 1;
                    var b = timeCu - 2 * timeSq + relativeTime;
                    var c = timeCu - timeSq;
                    var d = -2 * timeCu + 3 * timeSq;

                    return a * prev.value + b * prev.outTangent * delta + c * next.inTangent * delta + d * next.value;
                } else continue;
            }

            return 0;
        }
    }
}