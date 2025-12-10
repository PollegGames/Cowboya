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
    private RobotStats playerStats;

    public GameObject playerInstance { get; private set; }
    public Transform playerHeadTransform { get; private set; } // Head inside WholeBody

    private void OnEnable()
    {
        RobotStateController.OnAnyRobotKilled += HandleRobotKilled;
        RobotStateController.OnAnyRobotSaved += HandleRobotSaved;
    }

    private void OnDisable()
    {
        RobotStateController.OnAnyRobotKilled -= HandleRobotKilled;
        RobotStateController.OnAnyRobotSaved -= HandleRobotSaved;
    }

    public void Initialize(MapManager mapManager, IWaypointService waypointService, VictorySetup victorySetup, IEnemiesSpawner enemiesSpawner)
    {
        this.mapManager = mapManager;
        this.waypointService = waypointService;
        this.victorySetup = victorySetup;

        SetupFactoryState();

        if (this.mapManager == null)
        {
            Debug.LogError("FactoryManager: MapManager reference is required for initialization.");
            return;
        }

        if (this.waypointService == null)
        {
            Debug.LogWarning("FactoryManager: WaypointService reference was not provided. Navigation data will not be built.");
        }

        this.mapManager.InitializeGrid();
        this.mapManager.RegisterFactoryInEachRoom(this, machineWorkerManager, machineSecurityManager, spawningWorkerManager, enemiesSpawner);

        if (this.waypointService != null)
            this.waypointService.BuildAllNeighbors(includeUnavailable: true);
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
            Debug.LogWarning("FactoryManager: FactoryAlarmStatus reference is missing.");
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
        var controller = playerInstance.GetComponent<RobotStateController>();
        if (controller != null)
        {
            playerStats = controller.Stats;
            controller.OnStateChanged += HandlePlayerStateChange;
        }
    }

    public void OnRobotSaved()
    {
        if (victorySetup != null)
        {
            victorySetup.currentSaved++;
        }
        else
        {
            Debug.LogWarning("FactoryManager: VictorySetup reference is missing when saving a robot.");
        }

        Debug.Log("Robot SAVED");
        playerStats?.UpdateMorality(1f);

        if (victorySetup != null && factoryAlarmStatus != null && victorySetup.currentSaved >= victorySetup.robotsSavedTarget)
        {
            factoryAlarmStatus.CurrentAlarmState = AlarmState.Revolt;
        }
    }

    public void OnRobotKilled()
    {
        if (victorySetup != null)
        {
            victorySetup.currentKilled++;
        }
        else
        {
            Debug.LogWarning("FactoryManager: VictorySetup reference is missing when killing a robot.");
        }

        Debug.Log("Robot KILLED");
        playerStats?.UpdateMorality(-1f);
    }

    private void HandleRobotKilled(RobotStateController controller)
    {
        if (controller == null)
            return;

        if (controller.GetComponent<PlayerBrain>() != null)
            return;

        OnRobotKilled();
    }

    private void HandleRobotSaved(RobotStateController controller)
    {
        if (controller == null)
            return;

        if (controller.GetComponent<PlayerBrain>() != null)
            return;

        var heart = controller.GetComponent<RobotHeart>();
        var role = heart != null ? heart.Role : RobotRole.Worker;
        if (role == RobotRole.Worker || role == RobotRole.WorkerSpawner)
        {
            OnRobotSaved();
        }
    }


    private void HandlePlayerStateChange(RobotState newState)
    {
        if (newState == RobotState.Dead)
        {
            playerStats?.ResetMorality();
        }
    }

}
