using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using Object = UnityEngine.Object;

#endif

namespace ArctiumStudios.SplineTools
{
    [Serializable]
    public abstract class AbstractSplineToolsInstance
    {
        [SerializeField] [HideInInspector] public State state;
        [SerializeField] public LogLevel logLevel = LogLevel.Warning;
        [SerializeField] public Space space = Space.World;

#if UNITY_EDITOR
        [NonSerialized] public bool showCoord = false;
        [NonSerialized] [HideInInspector] public Func<Vector3, bool> placePoiCallback;
        [NonSerialized] [HideInInspector] public Action placePoiCancelCallback;
        [NonSerialized] [HideInInspector] public float placePoiRadius;

        [NonSerialized] private Object[] tempSelection;
        [NonSerialized] private EditorWindow tempFocusedEditorWindow;

        // statistics for Editor
        [NonSerialized] [HideInInspector] public int graphsCount = 0;
        [NonSerialized] [HideInInspector] public int poiCount = 0;
        [NonSerialized] [HideInInspector] public int connectionsCount = 0;
        [NonSerialized] [HideInInspector] public int nodeCount = 0;
        [NonSerialized] [HideInInspector] public int edgeCount = 0;
#endif

        public delegate void Function(Vector3 offset);

#if UNITY_EDITOR
        [NonSerialized] private readonly Dictionary<object, Function> gizmos = new Dictionary<object, Function>();
#endif

        public enum CacheDirType
        {
            PersistentDataPath,
            DataPath,
            Custom
        }

        [SerializeField] public int heightsCacheSizeInMemoryMegaBytes = 512;
        [SerializeField] public int heightsCacheSizeOnDiskMegaBytes = 2048;
        [SerializeField] public int heightsCacheSpillToDiskThresholdMillis = 500;

        [SerializeField] public CacheDirType cacheDirectoryType = CacheDirType.PersistentDataPath;
        [SerializeField] public string cacheSubDirectory = "/st-cache";
        public string persistentDataPath;
        public string dataPath;

        public string CacheDirectory
        {
            get
            {
                switch (cacheDirectoryType)
                {
                    case CacheDirType.PersistentDataPath:
                        return persistentDataPath + cacheSubDirectory;
                    case CacheDirType.DataPath:
                        return dataPath + cacheSubDirectory;
                    case CacheDirType.Custom:
                        return cacheSubDirectory;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        protected AbstractSplineToolsInstance()
        {
            state = new State(NewCache());
        }

        public abstract void OnEnable();

        public abstract void OnDisable();

        protected abstract Cache NewCache();

        public abstract float GlobalMaxRadius();

        public void DrawGizmos(object reference, Function func)
        {
#if UNITY_EDITOR
            lock (gizmos)
            {
                gizmos[reference] = func;
            }
#endif
        }

        public void RemoveGizmos(object reference)
        {
#if UNITY_EDITOR
            lock (gizmos)
            {
                gizmos.Remove(reference);
            }
#endif
        }

        public void OnDrawGizmos()
        {
#if UNITY_EDITOR
            lock (gizmos)
            {
                foreach (var entry in gizmos)
                {
                    entry.Value(MapMagicGameObjectPosition());
                }
            }
#endif
        }

        private void ResetGizmos()
        {
#if UNITY_EDITOR
            lock (gizmos)
            {
                gizmos.Clear();
            }
#endif
        }

        public void GlobalReset(bool resetCache)
        {
            Log.Debug(this, () => "Resetting Spline Tools");

            state.Reset(resetCache);
            DoResetState();
            ResetGizmos();

            UnlockChunks();

            UpdateStatistics();
        }

        public abstract Vector3 MapMagicGameObjectPosition();

        protected abstract void DoResetState();

        protected abstract void UnlockChunks();

        public void Rebuild(bool clear)
        {
            if (GenerationIsRunning())
            {
                Log.Warn(this, () => "MapMagic generation is already running.");
                return;
            }

            GlobalReset(clear);
            Generate();
        }

        public void Generate()
        {
            Log.Debug(this, () => "Regenerate SplineTools");

            ClearResults();

            // locked chunks will be excluded from the first generate and will be unlocked in second pass
            LockChunksIfRequired();

            DoGenerate();
        }

        protected abstract bool GenerationIsRunning();

        protected abstract void DoGenerate();

        protected abstract void ClearResults();

        public abstract void LockChunksIfRequired();

        public void PlacePoi(Func<Vector3, bool> callback, Action cancelCallback, float radius)
        {
#if UNITY_EDITOR
            Log.Debug(this, () => "Placing POI");
            placePoiCancelCallback = cancelCallback;
            placePoiRadius = radius;

            tempSelection = Selection.objects;
            tempFocusedEditorWindow = EditorWindow.focusedWindow;
            Selection.objects = new Object[] {SplineTools.EditorInstance};

            placePoiCallback = vector3 =>
            {
                if (callback.Invoke(vector3))
                {
                    Log.Debug(this, () => "Placing " + vector3.ToPreciseString());
                    CancelPlacePoi();
                    return true;
                } else
                {
                    Log.Warn(this, () => "Can't place new POI here. Too close to others.");
                    return false;
                }
            };

            DrawGizmosForPlacePoi();

            // focus scene view
            if (SceneView.sceneViews.Count > 0) ((SceneView) SceneView.sceneViews[0]).Focus();
#endif
        }

        protected abstract void DrawGizmosForPlacePoi();

        public void CancelPlacePoi()
        {
#if UNITY_EDITOR
            Selection.objects = tempSelection;
            EditorWindow.FocusWindowIfItsOpen(tempFocusedEditorWindow.GetType());
            placePoiCancelCallback.Invoke();
            placePoiCallback = null;
            placePoiCancelCallback = null;
            RemoveGizmos("placePOI");
#endif
        }

        public void UpdateStatistics()
        {
#if UNITY_EDITOR
            var graphs = state.graphsById.Values;

            graphsCount = graphs.Count;
            poiCount = graphs.Sum(graph => ((InternalWorldGraph) graph).CustomTypeNodeCount());
            connectionsCount = graphs.Sum(graph => ((InternalWorldGraph) graph).ConnectionCount());
            nodeCount = graphs.Sum(graph => ((InternalWorldGraph) graph).NodeCount());
            edgeCount = graphs.Sum(graph => ((InternalWorldGraph) graph).EdgeCount());
#endif
        }
    }
}
