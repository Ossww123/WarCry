using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    // dictionary and hashset can't be serialized as is, the private fields are a workaround
    public class State : ISerializationCallbackReceiver
    {
        [HideInInspector, SerializeField] public Cache cache;
        [NonSerialized] public Dictionary<string, WorldGraph> graphsById = new Dictionary<string, WorldGraph>();
        
        // de-/serialization helpers
        [SerializeField] private List<InternalWorldGraph> graphs;

        public State(Cache cache)
        {
            this.cache = cache;
        }

        public void Reset(bool resetCache)
        {
            cache.Reset(resetCache);
            graphsById.Clear();
        }

        public WorldGraph Graph(string guid)
        {
            lock (graphsById)
            {
                if (!graphsById.ContainsKey(guid)) graphsById.Add(guid, new InternalWorldGraph(guid));
                return graphsById[guid];
            }
        }

        public List<WorldGraph> GraphForType(NodeType nodeType)
        {
            return graphsById.Values.Where(e => ((InternalWorldGraph) e).NodeTypes().Contains(nodeType)).ToList();
        }

        public bool IsEmpty()
        {
            var gs = SplineTools.Instance.state.graphsById.Values;
            var nodeCount = gs.Sum(graph => ((InternalWorldGraph) graph).NodeCount());
            var edgeCount = gs.Sum(graph => ((InternalWorldGraph) graph).EdgeCount());
            
            return nodeCount == 0 && edgeCount == 0;
        }

        public void OnBeforeSerialize()
        {
            graphs = graphsById.Select(e =>(InternalWorldGraph) e.Value).ToList();
        }

        public void OnAfterDeserialize()
        {
            graphsById = new Dictionary<string, WorldGraph>();
            foreach (var graph in graphs) graphsById.Add(graph.guid, graph);

            graphs = null;
        }
    }
}