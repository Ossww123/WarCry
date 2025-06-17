using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Den.Tools;
using MapMagic.Core;
using MapMagic.Nodes;
using MapMagic.Terrains;
using Mirror;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

/// <summary>
/// Handles procedural generation of battle maps using MapMagic for terrain generation
/// and automatically builds NavMesh for AI navigation.
/// </summary>
public class BattleMapGenerator : NetworkBehaviour
{
    /// <summary>The MapMagic object responsible for terrain generation</summary>
    public MapMagicObject mapMagic;

    /// <summary>Number of tiles along the X axis</summary>
    public Int32 mapXSize = 3;

    /// <summary>Number of tiles along the Y axis</summary>
    public Int32 mapYSize = 3;

    /// <summary>Size of each terrain tile in units</summary>
    public Int32 tileSize = 1024;

    /// <summary>Resolution of the generated terrain tiles</summary>
    public MapMagicObject.Resolution resolution = MapMagicObject.Resolution._513;

    /// <summary>The graph defining the terrain generation rules</summary>
    public Graph graph;

    /// <summary>Seed for random generation. -1 means use random seed</summary>
    [SyncVar(hook = nameof(OnSeedChanged))]
    private Int32 seed = -1;

    [SyncVar(hook = nameof(OnGenerationStateChanged))]
    public GenerationState generationState = GenerationState.NotStarted;

    // Add a clientReady flag to track client initialization
    private bool clientReady = false;

    public enum GenerationState
    {
        NotStarted,
        Generating,
        Complete
    }

    /// <summary>Maximum time in seconds to wait for generation to complete</summary>
    public float generationCompleteTimeout = 60.0f;

    /// <summary>Some screen element to mask out loading progress and every item</summary>
    public GameObject loadingScreen;

    public GameObject playerInfoUI;
    /// <summary>
    /// Initializes the map generator and starts the generation process when the object is enabled.
    /// Validates settings and initializes MapMagic with the specified or random seed.
    /// </summary>
    public override void OnStartClient()
    {
        ValidateSettings();
        mapMagic.graph = graph;
        mapMagic.tileSize = new Vector2D(tileSize, tileSize);
        seed = Random.Range(0, int.MaxValue);

        StartGeneration();
    }

    /// <summary>
    /// Validates all required settings and parameters before generation starts.
    /// Logs error messages if any settings are invalid.
    /// </summary>
    private void ValidateSettings()
    {
        if (mapMagic == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] Map Magic is not set");
            return;
        }

        if (tileSize <= 0)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] Tile size must be greater than 0");
            return;
        }

        if (mapXSize <= 0)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] Map X size must be greater than 0");
            return;
        }

        if (mapYSize <= 0)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] Map Y size must be greater than 0");
            return;
        }

        if (graph == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] Graph is not set");
            return;
        }
    }

    /// <summary>
    /// Initiates the map generation process by setting up MapMagic parameters
    /// and starting the generation coroutine.
    /// </summary>
    public void StartGeneration()
    {
        generationState = GenerationState.Generating;
        
        // Setup mapMagic with the current seed and parameters
        mapMagic.tileSize = new Vector2D(tileSize, tileSize);
        mapMagic.tileResolution = resolution;
        mapMagic.graph.random = new Noise(seed, permutationCount: 32_768);
        
        StartCoroutine(GenerateMap());
    }


    /// <summary>
    /// Coroutine that handles the map generation process.
    /// Pins all required tiles, initiates generation, waits for completion,
    /// and builds NavMesh for AI navigation.
    /// </summary>
    public IEnumerator GenerateMap()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Starting map generation with seed: {seed}.");

        // Pin all required tiles
        for (var y = -mapYSize / 2; y < mapYSize - mapYSize / 2; y++)
        {
            for (var x = -mapXSize / 2; x < mapXSize - mapXSize / 2; x++)
            {
                var newTileCoord = new Coord(x, y);
                mapMagic.tiles.Pin(newTileCoord, false, mapMagic);
            }
        }

        // Start generation
        mapMagic.StartGenerate(true, false);
        
        float timeWaited = 0;
        while (mapMagic.IsGenerating() && timeWaited < generationCompleteTimeout)
        {
            timeWaited += Time.deltaTime;
            yield return null;
        }

        if (timeWaited >= generationCompleteTimeout)
        {
            Debug.LogWarning(
                $"[{DebugUtils.ResolveCallerMethod()}] Map generation timeout reached. Some terrains may not be fully generated.");
        }

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Building NavMesh on generated terrains");
        BuildNavMeshes();

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Map generation complete");
        stopwatch.Stop();
        Debug.Log(
            $"[{DebugUtils.ResolveCallerMethod()}] Map generation took {stopwatch.ElapsedMilliseconds}ms");
		loadingScreen.SetActive(false);
        playerInfoUI.SetActive(true);
    }

    /// <summary>
    /// Builds NavMesh surfaces on all generated terrain tiles.
    /// Adds NavMeshSurface components and configures them for AI navigation.
    /// </summary>
    private void BuildNavMeshes()
    {
        foreach ((var tileKey, var tile) in mapMagic.tiles.pinned)
        {
            if (tile != null && tile.gameObject != null)
            {
                var terrainCollider = tile.gameObject.GetComponentInChildren<TerrainCollider>();
                if (terrainCollider != null)
                {
                    terrainCollider.gameObject.tag = "Ground";
                    var navMeshSurface = terrainCollider.gameObject.AddComponent<NavMeshSurface>();
                    if (navMeshSurface != null)
                    {
                        navMeshSurface.BuildNavMesh();
                        navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                    }
                    else
                    {
                        Debug.LogError(
                            $"[{DebugUtils.ResolveCallerMethod()}] Failed to add NavMeshSurface to tile at {tileKey}");
                    }
                }
                else
                {
                    Debug.LogError(
                        $"[{DebugUtils.ResolveCallerMethod()}] No TerrainCollider found on tile at {tileKey}");
                }
            }
        }
    }

    /// <summary>
    /// Checks if MapMagic has completed generating all terrain tiles.
    /// </summary>
    /// <returns>True if generation is complete, false if still in progress</returns>
    private bool IsGenerationComplete()
    {
        return !mapMagic.IsGenerating();
    }
    
        
    // The rest of your terrain generation code, but marked as [Server]
    // and updating generationState when complete
    
    // Hook methods to respond to changes
    private void OnSeedChanged(Int32 oldSeed, Int32 newSeed)
    {
        // Clients can respond to seed changes here if needed
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Map seed changed to {newSeed}");
    }
    
    private void OnGenerationStateChanged(GenerationState oldState, GenerationState newState)
    {
        if (newState == GenerationState.Complete && isClient)
        {
            // Terrain generation is complete, clients can respond
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Map generation is complete on server");
        }
    }

}