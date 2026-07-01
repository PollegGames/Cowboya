using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Coordinates initialization of the factory map and machines. Tracks alarm
/// state changes and exposes events when the factory enters a new alarm level.
/// </summary>
public readonly struct FactoryMachinesSummaryChangedEvent
{
    public FactoryMachinesSummaryChangedEvent(
        int totalOn,
        int totalOff,
        int totalRegistered,
        IReadOnlyDictionary<MachineType, int> onByType)
    {
        TotalOn = totalOn;
        TotalOff = totalOff;
        TotalRegistered = totalRegistered;
        OnByType = onByType;
    }

    public int TotalOn { get; }
    public int TotalOff { get; }
    public int TotalRegistered { get; }
    public IReadOnlyDictionary<MachineType, int> OnByType { get; }
}

public class FactoryManager : MonoBehaviour, IFactoryManager
{
    [SerializeField] public FactoryAlarmStatus factoryAlarmStatus;
    [SerializeField] private MachineWorkerManager machineWorkerManager;
    [SerializeField] private MachineSecurityManager machineSecurityManager;
    [SerializeField] private SpawningWorkerManager spawningWorkerManager;

    public MachineSecurityManager SecurityManager => machineSecurityManager;

    public event Action<AlarmState> OnFactoryAlarmChanged;
    public event Action<FactoryMachinesSummaryChangedEvent> OnFactoryMachinesSummaryChanged;
    public event Action OnFactoryAllMachinesOff;

    private AlarmState lastAlarmState;
    private MapManager mapManager;
    private IWaypointService waypointService;
    private VictorySetup victorySetup;
    private RobotStats playerStats;
    private readonly List<RoomManager> roomManagers = new();
    private readonly Dictionary<BaseMachine, bool> machinePowerStates = new();
    private bool allMachinesOffRaised;
    private bool alarmStatusSubscribed;

    public GameObject playerInstance { get; private set; }
    public Transform playerHeadTransform { get; private set; } // Head inside WholeBody

    private void OnEnable()
    {
        RobotStateController.OnAnyRobotKilled += HandleRobotKilled;
        RobotStateController.OnAnyRobotSaved += HandleRobotSaved;
        SubscribeToAlarmStatus();
    }

    private void OnDisable()
    {
        RobotStateController.OnAnyRobotKilled -= HandleRobotKilled;
        RobotStateController.OnAnyRobotSaved -= HandleRobotSaved;
        UnsubscribeFromAlarmStatus();
        UnsubscribeFromRoomEventHubs();
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
        var rooms = this.mapManager.RegisterFactoryInEachRoom(this, machineWorkerManager, machineSecurityManager, spawningWorkerManager, enemiesSpawner);
        RegisterRooms(rooms);

        if (this.waypointService != null)
            this.waypointService.BuildAllNeighbors(includeUnavailable: true);

        EmitMachineSummary();
    }

    public void InitializeStatic(VictorySetup victorySetup)
    {
        this.mapManager = null;
        this.waypointService = null;
        this.victorySetup = victorySetup;
        SetupFactoryState();
        roomManagers.Clear();
        machinePowerStates.Clear();
        allMachinesOffRaised = false;
        EmitMachineSummary();
    }

    public void RegisterStaticRooms(IEnumerable<RoomManager> rooms, Transform playerHead)
    {
        var registeredRooms = new List<RoomManager>();
        if (rooms != null)
        {
            foreach (RoomManager room in rooms)
            {
                if (room == null)
                    continue;

                var doorConfig = room.GetComponent<StaticRoomDoorConfig>();
                doorConfig?.Apply(room);
                room.InitializeStatic(this, playerHead);
                registeredRooms.Add(room);
            }
        }

        RegisterRooms(registeredRooms);
        EmitMachineSummary();
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
            SubscribeToAlarmStatus();
        }
        else
        {
            Debug.LogWarning("FactoryManager: FactoryAlarmStatus reference is missing.");
        }
    }

    public Vector3 GetStartCellWorldPosition()
    {
        if (mapManager != null)
            return mapManager.GetStartCellWorldPosition();

        return Vector3.zero;
    }

    public void SetPlayerInstanceHead(GameObject playerInstance, Transform head)
    {
        this.playerInstance = playerInstance;
        playerHeadTransform = head;
        if (playerHeadTransform == null)
            Debug.LogError("FactoryManager: Player head transform is null.");

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
            factoryAlarmStatus.CurrentAlarmState = AlarmState.Revolt;
    }

    public void OnRobotKilled()
    {
        if (victorySetup != null)
        {
            victorySetup.currentKilled++;
            if (factoryAlarmStatus != null)
            {
                // Escalate to Wanted on any robot death (do not downgrade more severe states).
                if (factoryAlarmStatus.CurrentAlarmState == AlarmState.Normal)
                    factoryAlarmStatus.CurrentAlarmState = AlarmState.Wanted;
            }
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

        var heart = controller.GetComponent<RobotHeartNew>();
        var role = heart != null ? heart.Role : RobotRole.Worker;
        if (role == RobotRole.Worker || role == RobotRole.WorkerSpawner)
            OnRobotSaved();
    }

    private void HandlePlayerStateChange(RobotState newState)
    {
        if (newState == RobotState.Dead)
            playerStats?.ResetMorality();
    }

    private void RegisterRooms(List<RoomManager> rooms)
    {
        UnsubscribeFromRoomEventHubs();
        roomManagers.Clear();
        machinePowerStates.Clear();
        allMachinesOffRaised = false;

        if (rooms == null || rooms.Count == 0)
            return;

        foreach (var room in rooms)
        {
            if (room == null)
                continue;

            roomManagers.Add(room);
            room.OnRoomMachineChanged += HandleRoomMachineChanged;
            SeedRoomMachineStates(room);
        }
    }

    private void UnsubscribeFromRoomEventHubs()
    {
        foreach (var room in roomManagers)
        {
            if (room == null)
                continue;
            room.OnRoomMachineChanged -= HandleRoomMachineChanged;
        }
    }

    private void SeedRoomMachineStates(RoomManager room)
    {
        RegisterMachinePowerStates(room.factorymMachinesInRoom);
        RegisterMachinePowerStates(room.restingMachinesInRoom);
        RegisterMachinePowerStates(room.securityMachinesInRoom);
        RegisterMachinePowerStates(room.spawningMachinesInRoom);
    }

    private void RegisterMachinePowerStates<TMachine>(IEnumerable<TMachine> machines) where TMachine : BaseMachine
    {
        if (machines == null)
            return;

        foreach (var machine in machines)
        {
            if (machine == null)
                continue;
            machinePowerStates[machine] = machine.IsOn;
        }
    }

    private void HandleRoomMachineChanged(RoomMachineChangedEvent evt)
    {
        if (evt.Machine == null)
            return;

        bool shouldUpdate = false;
        bool newIsOn = false;

        if (evt.EventKind == RoomMachineEventKind.PowerChanged && evt.IsOn.HasValue)
        {
            shouldUpdate = true;
            newIsOn = evt.IsOn.Value;
        }
        else if (evt.EventKind == RoomMachineEventKind.TurnedOff)
        {
            shouldUpdate = true;
            newIsOn = false;
        }

        if (!shouldUpdate)
            return;

        machinePowerStates[evt.Machine] = newIsOn;
        EmitMachineSummary();
    }

    private void EmitMachineSummary()
    {
        CleanupDestroyedMachineEntries();

        var onByType = new Dictionary<MachineType, int>
        {
            [MachineType.WorkStation] = 0,
            [MachineType.RestStation] = 0,
            [MachineType.SecurityMachine] = 0,
            [MachineType.SpawningMachine] = 0
        };

        int totalOn = 0;
        int totalRegistered = machinePowerStates.Count;
        foreach (var pair in machinePowerStates)
        {
            if (!pair.Value)
                continue;

            totalOn++;
            var machineType = pair.Key.Type;
            if (!onByType.ContainsKey(machineType))
                onByType[machineType] = 0;
            onByType[machineType]++;
        }

        int totalOff = Mathf.Max(0, totalRegistered - totalOn);
        OnFactoryMachinesSummaryChanged?.Invoke(
            new FactoryMachinesSummaryChangedEvent(totalOn, totalOff, totalRegistered, onByType));

        if (totalRegistered == 0)
            return;

        if (totalOn == 0)
        {
            if (!allMachinesOffRaised)
            {
                allMachinesOffRaised = true;
                OnFactoryAllMachinesOff?.Invoke();
            }
        }
        else
        {
            allMachinesOffRaised = false;
        }
    }

    private void CleanupDestroyedMachineEntries()
    {
        var toRemove = new List<BaseMachine>();
        foreach (var pair in machinePowerStates)
        {
            if (pair.Key == null)
                toRemove.Add(pair.Key);
        }

        foreach (var machine in toRemove)
            machinePowerStates.Remove(machine);
    }

    private void HandleFactoryAlarmStateChanged(AlarmState newState)
    {
        if (newState == lastAlarmState)
            return;

        lastAlarmState = newState;
        OnFactoryAlarmChanged?.Invoke(newState);
    }

    private void SubscribeToAlarmStatus()
    {
        if (factoryAlarmStatus == null || alarmStatusSubscribed)
            return;

        factoryAlarmStatus.OnAlarmStateChanged += HandleFactoryAlarmStateChanged;
        alarmStatusSubscribed = true;
    }

    private void UnsubscribeFromAlarmStatus()
    {
        if (factoryAlarmStatus == null || !alarmStatusSubscribed)
            return;

        factoryAlarmStatus.OnAlarmStateChanged -= HandleFactoryAlarmStateChanged;
        alarmStatusSubscribed = false;
    }
}
