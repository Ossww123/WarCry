#if ST_MM_2

using System;
using System.Linq;
using MapMagic.Products;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    public abstract class EdgeWalkingGenerator : Generator, ISerializationCallbackReceiver
    {
        protected readonly AnimationCurve crossingWidthFalloff = new AnimationCurve(new Keyframe(0, 0.1f, 2, 0), new Keyframe(1, 1, 2, 0));

        public EndpointHandling usedEndpointHandling = EndpointHandling.Fade;

        public ClampedFloat borderMax = new ClampedFloat(15f, 0f, float.MaxValue);
        public ClampedFloat additionalWidth = new ClampedFloat(0, 0f, float.MaxValue);
        public ClampedFloat borderChange = new ClampedFloat(.2f, 0f, float.MaxValue);

        public ClampedFloat endFadeDistance = new ClampedFloat(10f, 0f, float.MaxValue);

        public AnimationCurve endFadeFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));

        public bool crossingFade = false;
        public ClampedFloat crossingDistance = new ClampedFloat(10f, 0f, float.MaxValue);
        public AnimationCurve crossingFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));

        public bool crossingOverflow = false;
        public ClampedFloat crossingOverflowDistance = new ClampedFloat(10f, 0f, float.MaxValue);
        public AnimationCurve crossingOverflowFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));

        public bool crossingWiden = false;
        public ClampedFloat crossingWidenDistance = new ClampedFloat(10f, 0f, float.MaxValue);
        public AnimationCurve crossingWidenFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 3, 3));
        public ClampedFloat crossingWidenFalloffMin = new ClampedFloat(1, 0f, float.MaxValue);
        public ClampedFloat crossingWidenFalloffMax;
        public ClampedFloat detail = new ClampedFloat(0.5f, 0f, 1);

        public GizmoLevel usedGizmoLevel = GizmoLevel.Off;

        public EdgeWalkingGenerator()
        {
            InitClampedDynamicValues(this);
        }

        public static void InitClampedDynamicValues(EdgeWalkingGenerator gen)
        {
            gen.crossingWidenFalloffMax = new ClampedFloatDynamic(gen.crossingWidenFalloffMax?.value ?? 2f,
                () => gen.crossingWidenFalloffMin.ClampedValue, () => float.MaxValue);
        }

        protected int Margin(TileData tileData, EdgesByOffset connections)
        {
            // find edges relevant for the current chunk
            var globalMaxBorder = GlobalMaxBorder(tileData);

            var maxWidth = connections.Max(cl => cl.Value.Max(c => c.WidthMax()));
            var maxTotalWidth = maxWidth + 2 * globalMaxBorder;

            // add the max section length because some point between the endpoints could overlap the relevant rect
            var edgeGenerators = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph.generators.OfType<EdgeGenerator>().ToList();
            var maxSectionLength = edgeGenerators.Count == 0
                ? 0f
                : edgeGenerators.Max(g => g.MaxSectionLength());

            var margin = Mathf.CeilToInt(maxTotalWidth + maxSectionLength);
            return margin;
        }

        protected virtual float GlobalMaxBorder(TileData tileData)
        {
            return 0;
        }

        public void OnBeforeSerialize()
        {
            // noop
        }

        public void OnAfterDeserialize()
        {
            InitClampedDynamicValues(this);
        }
    }
}

#endif