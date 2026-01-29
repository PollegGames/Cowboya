// -----------------------------------------------------------------------------
// MapManager.cs   (ajout de BuildFromConfig pour la preview et le run)
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    // ---------------------------------------------------------------- Grid / Prefabs
    [Header("Grid Settings")]
    [SerializeField] public int cellWidth = 47;
    [SerializeField] public int cellHeight = 11;

    [SerializeField] private RunMapConfigSO mapConfig; // can be injected via BuildFromConfig

    [Header("Cell Prefabs Mapping")]
    [SerializeField] private GameObject blockedPrefab;
    [SerializeField] private GameObject startPrefab;
    [SerializeField] private GameObject endPrefab;
    [SerializeField] private GameObject defaultPOI_Prefab;
    [SerializeField] private GameObject receptionPOI_Prefab;
    [SerializeField] private GameObject securityPOI_Prefab;
    [SerializeField] private GameObject pathToPOIPrefab;
    [SerializeField] private GameObject workPrefab;

    // ---------------------------------------------------------------- Instances & services
    private Dictionary<Vector2, GameObject> roomInstances;
    private GridManager gridManager;

    private IGridBuilder gridBuilder;
    private IRoomRenderer roomRenderer;
    private IRoomProcessor roomProcessor;

    private int gridWidth = 6;
    private int gridHeight = 8;
    private int wallCount = 5;
    private int pointsOfInterestCount = 3;

    public void Construct(IGridBuilder builder, IRoomRenderer renderer, IRoomProcessor processor)
    {
        gridBuilder = builder;
        roomRenderer = renderer;
        roomProcessor = processor;
    }

    /// <summary>
    /// Applique les champs du ScriptableObject aux variables internes.
    /// </summary>
    private void ApplyConfig(RunMapConfigSO cfg)
    {
        gridWidth = cfg.gridWidth;
        gridHeight = cfg.gridHeight;
        wallCount = cfg.blockedCount;
        pointsOfInterestCount = cfg.poiCount;

        // Initialize Random so the same seed always yields the same map
        UnityEngine.Random.InitState(cfg.seed.GetHashCode());
    }

    /// <summary>
    /// Initialise internal services with the current configuration values.
    /// </summary>
    private void BootSystems()
    {
        gridManager = new GridManager(gridWidth, gridHeight, cellWidth, cellHeight, wallCount, pointsOfInterestCount);
    }

    /// <summary>
    /// Public API used by RunSetupManager to inject the config without reloading the scene.
    /// </summary>
    public void BuildFromConfig(RunMapConfigSO cfg)
    {
        // Destroy the old visual grid if it exists
        if (roomInstances != null)
        {
            foreach (var go in roomInstances.Values)
                if (go) Destroy(go);
            roomInstances = null;
        }

        // Copy the values
        mapConfig = cfg; // keep a reference
        ApplyConfig(cfg);

    }

    /// <summary>
    /// Initializes the logical grid and rendering. Same as your previous method.
    /// </summary>
    public void InitializeGrid()
    {
        if (gridBuilder == null || roomRenderer == null || roomProcessor == null)
        {
            Debug.LogError("MapManager services not configured.");
            return;
        }

        Dictionary<Vector2, Cell> cellDataGrid = null;
        bool validPath = false;
        const int maxAttempts = 20;
        int attempts = 0;

        while (!validPath && attempts < maxAttempts)
        {
            cellDataGrid = gridBuilder.BuildGrid(
                gridWidth,
                gridHeight,
                wallCount,
                pointsOfInterestCount);

            var start = Vector2.zero;
            var end = Vector2.zero;
            bool hasStart = false, hasEnd = false;

            foreach (var kvp in cellDataGrid)
            {
                if (kvp.Value.cellProperties.usageType == UsageType.Start)
                {
                    start = kvp.Key;
                    hasStart = true;
                }
                else if (kvp.Value.cellProperties.usageType == UsageType.End)
                {
                    end = kvp.Key;
                    hasEnd = true;
                }
            }

            if (hasStart && hasEnd)
            {
                var path = new List<Cell>();
                validPath = new PathFinder().FindPath(cellDataGrid, start, end, path);
            }

            attempts++;
        }

        if (!validPath)
            Debug.LogWarning($"Could not generate a valid path between start and end after {maxAttempts} attempts.");

        BootSystems();

        bool noBlockRequiredWhenZeroEnemies = mapConfig == null || mapConfig.enemiesCount <= 0;

        roomProcessor.ProcessRooms(cellDataGrid, gridWidth, gridHeight, noBlockRequiredWhenZeroEnemies);

        roomInstances = roomRenderer.RenderRooms(
            cellDataGrid,
            CreatePrefabMapping(),
            CreatePOIPrefabMapping(),
            new Vector2(cellWidth, cellHeight),
            transform.position,
            transform,
            workPrefab);

        gridManager.AssignRoomProperties(roomInstances, cellDataGrid);
    }

    /// <summary>
    /// Returns the world-space bounds of all renderers under this map.
    /// </summary>
    /// <param name="rootOverride">Optional root transform to search under.</param>
    /// <param name="layerMask">Layer mask for filtering included renderers.</param>
    /// <returns>Bounds encompassing all included renderers.</returns>
    public Bounds GetGridWorldBounds(Transform rootOverride = null, int layerMask = ~0)
    {
        var root = rootOverride != null ? rootOverride : transform;
        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: false);

        bool hasBounds = false;
        Bounds bounds = default;

        foreach (var r in renderers)
        {
            if (((1 << r.gameObject.layer) & layerMask) == 0)
                continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (!hasBounds)
            bounds = new Bounds(root.position, Vector3.one);

        return bounds;
    }

    // ============================================================================
    //  HELPERS
    // ============================================================================
    private Dictionary<UsageType, GameObject> CreatePrefabMapping()
    {
        return new Dictionary<UsageType, GameObject>
        {
            { UsageType.Blocked,   blockedPrefab   },
            { UsageType.Start,     startPrefab     },
            { UsageType.End,       endPrefab       },
            { UsageType.POI,       defaultPOI_Prefab},
            { UsageType.PathToPOI, pathToPOIPrefab },
            { UsageType.Work,      workPrefab      },
        };
    }

    private Dictionary<POIType, GameObject> CreatePOIPrefabMapping()
    {
        return new Dictionary<POIType, GameObject>
        {
            { POIType.Reception, receptionPOI_Prefab },
            { POIType.Security, securityPOI_Prefab },
            { POIType.None, defaultPOI_Prefab }
        };
    }

    public void RegisterFactoryInEachRoom(
        FactoryManager factoryManager,
        MachineWorkerManager machineWorkerManager,
        MachineSecurityManager machineSecurityManager,
        SpawningWorkerManager spawningWorkerManager,
        IEnemiesSpawner enemiesSpawner)
    {
        foreach (var roomGO in roomInstances.Values)
        {
            var rm = roomGO.GetComponent<RoomManager>();
            if (rm != null)
                rm.Initialize(factoryManager, machineWorkerManager, machineSecurityManager, spawningWorkerManager, enemiesSpawner);
        }
    }

    /// <summary>
    /// Returns the world position of the start cell with a slight horizontal offset.
    /// Rooms on the left half of the grid are nudged right; rooms on the right half are nudged left.
    /// </summary>
    public Vector3 GetStartCellWorldPosition()
    {
        if (roomInstances == null)
            return Vector3.zero;

        foreach (var kvp in roomInstances)
        {
            GameObject roomObj = kvp.Value;
            var roomProps = roomObj.GetComponent<RoomProperties>();
            if (roomProps != null && roomProps.usageType == UsageType.Start)
            {
                Vector3 pos = roomObj.transform.position;
                float offset = cellWidth * 0.25f;
                if (roomProps.GridPosition.x < gridWidth / 2f)
                    pos += new Vector3(offset, 0f, 0f);
                else
                    pos += new Vector3(-offset, 0f, 0f);
                return pos;
            }
        }
        Debug.LogWarning("No start cell found in roomInstances.");
        return Vector3.zero;
    }
    private List<Vector3> unusedPOIPositions = new List<Vector3>();

    public Vector3 GetRandomPOIPosition()
    {
        if (roomInstances == null)
            return Vector3.zero;

        // Populate unusedPOIPositions if empty
        if (unusedPOIPositions.Count == 0)
        {
            unusedPOIPositions = roomInstances.Values
                .Select(roomObj => roomObj.GetComponent<RoomProperties>())
                .Where(roomProps => roomProps != null && roomProps.usageType == UsageType.POI)
                .Select(roomProps => roomProps.gameObject.transform.position)
                .ToList();

            if (unusedPOIPositions.Count == 0)
            {
                Debug.LogWarning("No POI cells found in roomInstances.");
                return Vector3.zero;
            }
        }

        // Pick a random POI from the unused list
        int idx = UnityEngine.Random.Range(0, unusedPOIPositions.Count);
        Vector3 poiPos = unusedPOIPositions[idx];
        unusedPOIPositions.RemoveAt(idx);
        return poiPos;
    }
    
    private List<Vector3> unusedWorkPositions = new List<Vector3>();
    public Vector3 GetRandomWorkPosition()
    {
        if (roomInstances == null)
            return Vector3.zero;

        // Populate unusedWorkPositions if empty
        if (unusedWorkPositions.Count == 0)
        {
            unusedWorkPositions = roomInstances.Values
                .Select(roomObj => roomObj.GetComponent<RoomProperties>())
                .Where(roomProps => roomProps != null && roomProps.usageType == UsageType.Work)
                .Select(roomProps => roomProps.gameObject.transform.position)
                .ToList();

            if (unusedWorkPositions.Count == 0)
            {
                Debug.LogWarning("No Work cells found in roomInstances.");
                return Vector3.zero;
            }
        }

        // Pick a random Work cell from the unused list
        int idx = UnityEngine.Random.Range(0, unusedWorkPositions.Count);
        Vector3 workPos = unusedWorkPositions[idx];
        unusedWorkPositions.RemoveAt(idx);
        return workPos;
    }

}
