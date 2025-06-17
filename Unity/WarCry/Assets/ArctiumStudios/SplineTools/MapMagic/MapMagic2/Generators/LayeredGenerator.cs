#if ST_MM_2

namespace ArctiumStudios.SplineTools
{
    public interface LayeredGenerator
    {
        LayeredGenerator Init();
    }

    public abstract class LayeredGenerator<TL> : Generator, LayeredGenerator where TL : AbstractLayer, new()
    {
        // internally used values
        private int selectedLayer;
        private bool expandAll;
        
        public TL[] layers = { new TL() };
        public TL[] Layers => layers; 
        
        public virtual LayeredGenerator Init()
        {
            for (var num = Layers.Length - 1; num >= 0; num--) Layers[num].Parent = this;
            return this;
        }
    }
}

#endif