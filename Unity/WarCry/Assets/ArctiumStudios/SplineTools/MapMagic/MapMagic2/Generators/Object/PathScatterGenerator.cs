#if ST_MM_2

using System;
using System.Collections.Generic;
using System.Linq;
using MapMagic.Nodes;
using MapMagic.Products;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    [GeneratorMenu(menu = "SplineTools/Objects",
        name = "Path Scatter",
        priority = 10,
        disengageable = true,
        iconName = "GeneratorIcons/Scatter",
        colorType = typeof(Den.Tools.TransitionsList),
        helpLink = "https://gitlab.com/Mnlk/mapmagic-spline-tools/wikis/generators/object/path_scatter")]
    public class PathScatterGenerator : Generator, IMultiInlet, IOutlet<Den.Tools.TransitionsList>, ISerializationCallbackReceiver
    {
        public Inlet<EdgesByOffset> inputEdges = new Inlet<EdgesByOffset>();
        
        public int seed = 12345;
        public Side usedSide = Side.Both;

        public AnimationCurve stepLengthFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));
        public ClampedFloat stepLengthMin = new ClampedFloat(10f, 0f, float.MaxValue);
        public ClampedFloat stepLengthMax;

        public bool mirrorSteps = true;

        public bool startAtSource = true;
        public int countMax = 0;

        public AnimationCurve distanceFalloff = new AnimationCurve(new Keyframe(0, 0, 1, 0), new Keyframe(1, 1, 0, 1));
        public ClampedFloat distanceMin = new ClampedFloat(10f, 0f, float.MaxValue);
        public ClampedFloat distanceMax;

        public bool mirrorDistance = true;

        public bool alignHeight = false;
        public bool alignRotation = true;
        public bool reverseRotation = false;

        public PathScatterGenerator()
        {
            InitClampedDynamicValues(this);
        }

        public static void InitClampedDynamicValues(PathScatterGenerator gen)
        {
            gen.stepLengthMax = new ClampedFloatDynamic(gen.stepLengthMax?.value ?? 15f, () => gen.stepLengthMin.ClampedValue, () => float.MaxValue);
            gen.distanceMax = new ClampedFloatDynamic(gen.distanceMax?.value ?? 15f, () => gen.distanceMin.ClampedValue, () => float.MaxValue);
        }
        
        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return inputEdges;
        }

        public override void Generate(TileData tileData, StopToken stop)
        {
            var connections = tileData.ReadInletProduct(inputEdges);
            var transitions = new Den.Tools.TransitionsList();

            // return on stop/disable
            if (stop != null && stop.stop || tileData.isDraft || !enabled || MandatoryInputMissing(inputEdges) || connections.Count == 0)
            {
                tileData.StoreProduct(this, transitions);
                return;
            }

            // find edges relevant for the current chunk
            var maxTotalWidth = distanceMax.ClampedValue * 2;

            // add the max section length because some point between the endpoints could overlap the relevant rect
            var maxSectionLength = ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph.generators.OfType<IConnectionsGenerator>()
                .Select(g =>
                {
                    var layeredGenerator = g as LayeredGenerator;
                    if (layeredGenerator != null) layeredGenerator.Init();
                    return g;
                })
                .Max(g => g.MaxSectionLength());

            var pathScatterer = NewPathScatterer(seed, stop);

            var points = pathScatterer.ProcessEdges(connections, tileData.area.active.ToWorldRect(),
                maxTotalWidth + maxSectionLength);

            points.ForEach(p =>
            {
                var transition = new Den.Tools.Transition(p.x, p.y, p.z);
                transition.rotation *= Quaternion.Euler(0, p.w, 0);
                transitions.Add(transition);
            });

            tileData.StoreProduct(this, transitions);
        }

        private PathScatterer NewPathScatterer(int seed, StopToken stop)
        {
            var options = new PathScatterer.Options
            {
                seed = seed + this.seed,
                alignHeight = alignHeight,
                alignRotation = alignRotation,
                distanceFalloff = distanceFalloff,
                distanceMax = distanceMax.ClampedValue,
                distanceMin = distanceMin.ClampedValue,
                mirrorDistance = mirrorDistance,
                mirrorSteps = mirrorSteps,
                reverseRotation = reverseRotation,
                usedSide = usedSide,
                stepLengthFalloff = stepLengthFalloff,
                stepLengthMax = stepLengthMax.ClampedValue,
                stepLengthMin = stepLengthMin.ClampedValue,
                countMax = countMax,
                startAtSource = startAtSource
            };

            return new PathScatterer(options, () => stop != null && stop.stop);
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
