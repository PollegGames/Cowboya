using UnityEngine;
using System;
using UnityEngine.SceneManagement;
using System.Linq;

public class FactoryManager : MonoBehaviour, IFactoryManager
{
    [SerializeField] private FactoryAlarmStatus factoryAlarmStatus;
    [SerializeField] private MachineWorkerManager machineWorkerManager;
    [SerializeField] private MachineSecurityManager machineSecurityManager;

    public MachineSecurityManager SecurityManager => machineSecurityManager;

    public event Action<AlarmState> OnFactoryAlarmChanged;
    private AlarmState lastAlarmState;
    private MapManager mapManager;
    private IWaypointService waypointService;
    private VictorySetup victorySetup;

    public GameObject playerInstance { get; private set; }
    public Transform playerHeadTransform { get; private set; } // Head inside WholeBody

    public void Initialize(MapManager mapManager, IWaypointService waypointService, VictorySetup victorySetup)
    {
        this.mapManager = mapManager;
        this.waypointService = waypointService;
        this.victorySetup = victorySetup;
        mapManager.InitializeGrid();
        mapManager.RegisterFactoryInEachRoom(this, machineWorkerManager, machineSecurityManager);
        waypointService.BuildAllNeighbors();
        SetupFactoryState();
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
    }

    public void OnRobotKilled()
    {
        victorySetup.currentKilled++;
        Debug.Log("Robot KILLED");
        // Optionally: Check for victory condition here
    }

}
