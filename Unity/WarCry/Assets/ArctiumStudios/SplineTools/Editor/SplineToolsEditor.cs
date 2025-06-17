#if ST_MM_1 || ST_MM_2

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArctiumStudios.SplineTools
{
    [CustomEditor(typeof(SplineTools))]
    public class SplineToolsEditor : Editor
    {
        private Vector3 position;

        public void OnEnable()
        {
            if (NotPrimaryInstance()) return;

            SetInstances();
            SplineToolsIntegrationsManager.RefreshIntegrations();
            SplineTools.Instance.persistentDataPath = Application.persistentDataPath;
            SplineTools.Instance.dataPath = Application.dataPath;
            SplineTools.Instance.UpdateStatistics();
        }

        public override void OnInspectorGUI()
        {
            if (NotPrimaryInstance())
            {
                GUILayout.Label("Disabled for non-primary instance");
                return;
            }

            SetInstances();

            if (SplineTools.Instance.placePoiCallback == null)
            {
                SplineTools.Instance.logLevel = (LogLevel) EditorGUILayout.EnumPopup("Log Level", SplineTools.Instance.logLevel);
#if ST_MM_1
                // space for MM2 is always World, only allow to choose for MM1
                SplineTools.Instance.space = (Space) EditorGUILayout.EnumPopup("Space", SplineTools.Instance.space);
#endif
                SplineTools.Instance.heightsCacheSizeInMemoryMegaBytes =
                    EditorGUILayout.IntField("Height Cache Size (Memory)", SplineTools.Instance.heightsCacheSizeInMemoryMegaBytes);
                SplineTools.Instance.heightsCacheSizeOnDiskMegaBytes =
                    EditorGUILayout.IntField("Height Cache Size (Disk)", SplineTools.Instance.heightsCacheSizeOnDiskMegaBytes);

                if (SplineTools.Instance.heightsCacheSizeOnDiskMegaBytes > 0)
                {
                    SplineTools.Instance.heightsCacheSpillToDiskThresholdMillis =
                        EditorGUILayout.IntField("Spill to Disk Threshold (ms)", SplineTools.Instance.heightsCacheSpillToDiskThresholdMillis);
                    SplineTools.Instance.cacheDirectoryType = (AbstractSplineToolsInstance.CacheDirType) EditorGUILayout
                        .EnumPopup("Cache Directory", SplineTools.Instance.cacheDirectoryType);
                    SplineTools.Instance.cacheSubDirectory = EditorGUILayout.TextField("Cache Subdirectory", SplineTools.Instance.cacheSubDirectory);
                    GUILayout.Label(SplineTools.Instance.CacheDirectory, EditorStyles.wordWrappedLabel);
                }

                GUILayout.BeginHorizontal("box");
                GUILayout.Label("Clear");

                if (GUILayout.Button("All"))
                {
                    SplineTools.Instance.GlobalReset(true);
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }

                if (GUILayout.Button("Height Cache Only"))
                {
                    SplineTools.Instance.state.cache.ResetHeightCache();
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }

                GUILayout.EndHorizontal();

                if (GUILayout.Button("Rebuild"))
                {
                    SplineTools.Instance.Rebuild(false);
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }

                if (GUILayout.Button("Clear & Rebuild"))
                {
                    SplineTools.Instance.Rebuild(true);
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }

                GUILayout.Label("Height Cache usage: Memory: " + SplineTools.Instance.state.cache.MemoryCachedMegaBytes
                                                               + "MB; Disk: " + SplineTools.Instance.state.cache.DiskCachedMegaBytes + "MB");
                GUILayout.Label("Cached " + SplineTools.Instance.graphsCount + " graphs\n"
                                + SplineTools.Instance.poiCount + " POI and " + SplineTools.Instance.connectionsCount + " connections\n"
                                + "(" + SplineTools.Instance.nodeCount + " nodes and " + SplineTools.Instance.edgeCount + " edges)");

                SplineTools.Instance.showCoord = EditorGUILayout.Toggle("Show Coord", SplineTools.Instance.showCoord);

                if (SplineTools.Instance.showCoord)
                {
#if ST_MM_1
                    GUILayout.Label("Coord: " + position.WorldToSelectedSpace().ToPreciseString());
                    var rect = MapMagicUtil.GetCoordRectFor(position.ToMapSpaceCoord(1));
                    GUILayout.Label("Chunk: " + new Vector2(rect.offset.x / MapMagic.MapMagic.instance.resolution,
                                        rect.offset.z / MapMagic.MapMagic.instance.resolution));
#elif ST_MM_2
                    GUILayout.Label("Coord: " + position.ToPreciseString());
                    var dimensions = new MapMagic.Terrains.Area.Dimensions(Den.Tools.Vector2D.zero,
                        ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tileSize,
                        (int) ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tileResolution);
                    var mapSpaceCoord = position.V2().ToMapSpaceCoord(dimensions);
                    GUILayout.Label("Coord: " + mapSpaceCoord);
                    var rect = MapMagicUtil.GetUiCoordRectFor(mapSpaceCoord);
                    GUILayout.Label("Chunk: " + new Vector2(
                        rect.offset.x / (int) ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tileResolution,
                        rect.offset.z / (int) ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance.tileResolution));
#endif
                }

                if (SplineToolsIntegrationsManager.ActiveIntegrations.Count != 0)
                {
                    GUILayout.Label("Integrations: " + Log.LogCollection(SplineToolsIntegrationsManager.ActiveIntegrations));
                }

                if (GUILayout.Button("Refresh Integrations"))
                {
                    SplineToolsIntegrationsManager.RefreshIntegrations();
                }

                if (GUILayout.Button("Open Online Documentation"))
                {
                    Application.OpenURL("https://gitlab.com/Mnlk/mapmagic-spline-tools/-/wikis/home");
                }

                GUILayout.Label("Spline Tools Version " + SplineTools.Version);
            } else
            {
                // GUILayout.Label("Coord: " + position.WorldToSelectedSpace().ToPreciseString());
                GUILayout.Label("Place with Left-Click\n" +
                                "Cancel with ESC");
            }
        }

        [MenuItem("GameObject/3D Object/Map Magic - Spline Tools")]
        static void CreateSplineTools()
        {
#if ST_MM_1
            var mapMagicInstance = FindObjectOfType<MapMagic.MapMagic>();
#elif ST_MM_2
            var mapMagicInstance = FindObjectOfType<MapMagic.Core.MapMagicObject>();
#endif
            if (mapMagicInstance == null)
            {
                Debug.LogError("Could not add Spline Tools. No Map Magic instance found in scene.");
                return;
            }

            if (FindObjectOfType<SplineTools>() != null)
            {
                Debug.LogError("Could not add Spline Tools. Already exists in scene.");
                return;
            }

            var go = new GameObject("MapMagic - SplineTools");
            var splineTools = go.AddComponent<SplineTools>();
            SplineTools.EditorInstance = splineTools;

#if ST_MM_2
            ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance = mapMagicInstance;
#endif

            //registering undo
            Undo.RegisterCreatedObjectUndo(go, "MapMagic - Spline Tools Create");
            EditorUtility.SetDirty(splineTools);
        }

        //when selected
        public void OnSceneGUI()
        {
            if (NotPrimaryInstance()) return;

            SetInstances();

            if (SplineTools.Instance.showCoord || SplineTools.Instance.placePoiCallback != null)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    position = hit.point;

                    Handles.DrawLine(position, position + Vector3.up * 100);
                    if (SplineTools.Instance.placePoiCallback != null)
                        Handles.DrawWireDisc(position, Vector3.up, SplineTools.Instance.placePoiRadius);

                    EditorUtility.SetDirty(target);

                    if (SplineTools.Instance.placePoiCallback != null
                        && Event.current.type == EventType.MouseUp && Event.current.button == 0
                        && !Event.current.alt && !Event.current.shift && !Event.current.control)
                    {
                        var offset = SplineTools.Instance.MapMagicGameObjectPosition();

                        if (SplineTools.Instance.placePoiCallback.Invoke(position - offset))
                        {
                            SplineTools.Instance.placePoiCallback = null;
                        }

                        Event.current.Use();
                        HandleUtility.Repaint();
                    }
                }
            }

            if (SplineTools.Instance.placePoiCallback != null && Event.current.keyCode == KeyCode.Escape) SplineTools.Instance.CancelPlacePoi();
        }

        private void SetInstances()
        {
#if ST_MM_2
            if (((SplineToolsInstance) SplineTools.Instance).MapMagicInstance == null)
                ((SplineToolsInstance) SplineTools.Instance).MapMagicInstance = FindObjectOfType<MapMagic.Core.MapMagicObject>();
#endif
        }

        private bool NotPrimaryInstance()
        {
            return target != SplineTools.EditorInstance;
        }
    }
}

#endif
