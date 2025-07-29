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
    [SerializeField] private GameObject emptyPrefab;

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

        BootSystems();

        var cellDataGrid = gridBuilder.BuildGrid(
            gridWidth,
            gridHeight,
            wallCount,
            pointsOfInterestCount);

        roomProcessor.ProcessRooms(cellDataGrid, gridWidth, gridHeight);

        roomInstances = roomRenderer.RenderRooms(
            cellDataGrid,
            CreatePrefabMapping(),
            CreatePOIPrefabMapping(),
            new Vector2(cellWidth, cellHeight),
            transform.position,
            transform,
            emptyPrefab);

        gridManager.AssignRoomProperties(roomInstances, cellDataGrid);
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
            { UsageType.Empty,     emptyPrefab     },
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
                // Return the center position of the cell (room)
                return roomObj.transform.position;
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
    
    private List<Vector3> unusedEmptyPositions = new List<Vector3>();
    public Vector3 GetRandomEmptyPosition()
    {
        if (roomInstances == null)
            return Vector3.zero;

        // Populate unusedEmptyPositions if empty
        if (unusedEmptyPositions.Count == 0)
        {
            unusedEmptyPositions = roomInstances.Values
                .Select(roomObj => roomObj.GetComponent<RoomProperties>())
                .Where(roomProps => roomProps != null && roomProps.usageType == UsageType.Empty)
                .Select(roomProps => roomProps.gameObject.transform.position)
                .ToList();

            if (unusedEmptyPositions.Count == 0)
            {
                Debug.LogWarning("No Empty cells found in roomInstances.");
                return Vector3.zero;
            }
        }

        // Pick a random Empty from the unused list
        int idx = UnityEngine.Random.Range(0, unusedEmptyPositions.Count);
        Vector3 EmptyPos = unusedEmptyPositions[idx];
        unusedEmptyPositions.RemoveAt(idx);
        return EmptyPos;
    }

}
