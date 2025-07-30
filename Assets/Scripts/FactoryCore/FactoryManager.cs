using UnityEngine;
using System;
using UnityEngine.SceneManagement;
using System.Linq;

/// <summary>
/// Coordinates initialization of the factory map and machines. Tracks alarm
/// state changes and exposes events when the factory enters a new alarm level.
/// </summary>
public class FactoryManager : MonoBehaviour, IFactoryManager
{
    [SerializeField] public FactoryAlarmStatus factoryAlarmStatus;
    [SerializeField] private MachineWorkerManager machineWorkerManager;
    [SerializeField] private MachineSecurityManager machineSecurityManager;
    [SerializeField] private SpawningWorkerManager spawningWorkerManager;

    public MachineSecurityManager SecurityManager => machineSecurityManager;

    public event Action<AlarmState> OnFactoryAlarmChanged;
    private AlarmState lastAlarmState;
    private MapManager mapManager;
    private IWaypointService waypointService;
    private VictorySetup victorySetup;

    public GameObject playerInstance { get; private set; }
    public Transform playerHeadTransform { get; private set; } // Head inside WholeBody

    public void Initialize(MapManager mapManager, IWaypointService waypointService, VictorySetup victorySetup,IEnemiesSpawner enemiesSpawner)
    {
        this.mapManager = mapManager;
        this.waypointService = waypointService;
        this.victorySetup = victorySetup;

        // Reset the alarm to a known state before any room logic or AI is created
        SetupFactoryState();

        mapManager.InitializeGrid();
        mapManager.RegisterFactoryInEachRoom(this, machineWorkerManager, machineSecurityManager, spawningWorkerManager, enemiesSpawner);
        waypointService.BuildAllNeighbors(includeUnavailable: true);
    }


    private void Update()
    {
        if (factoryAlarmStatus == null) return;
        if (factoryAlarmStatus.CurrentAlarmState != lastAlarmState)
        {
            lastAlarmState = factoryAlarmStatus.CurrentAlarmState;
            OnFactoryAlarmChanged?.Invoke(lastAlarmState);
        }
    }

    public IWaypointService GetWayPointService()
    {
        return waypointService;
    }

    private void SetupFactoryState()
    {
        if (factoryAlarmStatus != null)
        {
            factoryAlarmStatus.CurrentAlarmState = AlarmState.Normal;
            lastAlarmState = factoryAlarmStatus.CurrentAlarmState;
        }
        else
        {
            Debug.LogError("FactoryManager: FactoryAlarmStatus reference is missing.");
        }
    }

    public Vector3 GetStartCellWorldPosition()
    {
        if (mapManager != null)
        {
            return mapManager.GetStartCellWorldPosition();
        }
        return Vector3.zero;
    }

    public void SetPlayerInstanceHead(GameObject playerInstance, Transform head)
    {
        this.playerInstance = playerInstance;
        playerHeadTransform = head;
        if (playerHeadTransform == null)
        {
            Debug.LogError("FactoryManager: Player head transform is null.");
        }
    }

    public void OnRobotSaved()
    {
        victorySetup.currentSaved++;
        Debug.Log("Robot SAVED");
        // Optionally: Check for victory condition here
        if (victorySetup.currentSaved >= victorySetup.robotsSavedTarget)
        {
            factoryAlarmStatus.CurrentAlarmState = AlarmState.Revolt;
        }
    }

    public void OnRobotKilled()
    {
        victorySetup.currentKilled++;
        Debug.Log("Robot KILLED");
        // Optionally: Check for victory condition here
    }

}
