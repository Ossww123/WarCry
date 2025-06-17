#if ST_MM_2

using System.Linq;
using MapMagic.Nodes;

namespace ArctiumStudios.SplineTools
{
    public abstract class AbstractLayer : IUnit
    {
        public bool expandAll = false;
        
        public ulong id; //properties not serialized  //0 is empty id - reassigning it automatically
        public ulong Id { get{return id;} set{id=value;} } 
        public ulong LinkedOutletId { get; set; }  //if it's inlet. Assigned every before each clear or generate
        public ulong LinkedGenId { get; set; } 

        private Generator parent;
        public Generator Parent
        {
            get
            {
                if (parent == null) ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.graph.generators.OfType<LayeredGenerator>().ToList().ForEach(g => g.Init());
                return parent;
            }
            set { parent = value; }
        }

        public MapMagic.Nodes.Generator Gen
        {
            get { return gen ?? Parent; }
            private set { gen = value; }
        }

        public MapMagic.Nodes.Generator gen; //property is not serialized
        public void SetGen(MapMagic.Nodes.Generator gen) => this.gen = gen;
        
        public IUnit ShallowCopy() => (AbstractLayer) this.MemberwiseClone(); 
    }
}

#endif