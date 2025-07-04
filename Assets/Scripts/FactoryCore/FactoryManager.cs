using UnityEngine;
using System;
using UnityEngine.SceneManagement;
using System.Linq;

public class FactoryManager : MonoBehaviour
{
    [SerializeField] private FactoryAlarmStatus factoryAlarmStatus;
    [SerializeField] private VictorySetup victorySetup;

    public event Action<AlarmState> OnFactoryAlarmChanged;
    private AlarmState lastAlarmState;
    private MapManager mapManager;
    private WaypointService waypointService;

    public GameObject playerInstance { get; private set; }
    public Transform playerHeadTransform { get; private set; } // Head inside WholeBody

    public void Initialize(MapManager mapManager, WaypointService waypointService, VictorySetup victorySetup)
    {
        this.mapManager = mapManager;
        this.waypointService = waypointService;
        this.victorySetup = victorySetup;
        mapManager.InitializeGrid();
        mapManager.RegisterFactoryInEachRoom(this);
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

    public WaypointService GetWayPointService()
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
        victorySetup.RegisterRobotSaved();
        // Optionally: Check for victory condition here
    }

    public void OnRobotKilled()
    {
        victorySetup.RegisterRobotKilled();
        // Optionally: Check for victory condition here
    }

}
