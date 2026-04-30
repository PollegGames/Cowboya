using UnityEngine;
using System;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    public FactoryManager FactoryManager { get; private set; }
    [SerializeField] public Transform PlayerHead;

    [Header("Room Settings")]
    public RoomProperties roomProperties;

    public event Action<AlarmState> OnRoomAlarmChanged;
    public event Action<RoomMachineChangedEvent> OnRoomMachineChanged
    {
        add => roomEventHub.OnRoomMachineChanged += value;
        remove => roomEventHub.OnRoomMachineChanged -= value;
    }
    public event Action<RoomThreatChangedEvent> OnRoomThreatChanged
    {
        add => roomEventHub.OnRoomThreatChanged += value;
        remove => roomEventHub.OnRoomThreatChanged -= value;
    }
    public event Action<RoomManager> PlayerEntered;
    public event Action<RoomManager> PlayerExited;

    [Header("Zone Detection")]
    public PositionTriggerZone triggerZone;
    [SerializeField] private List<RoomWaypoint> waypoints;
    public List<FactoryMachine> factorymMachinesInRoom = new();
    public List<RestingMachine> restingMachinesInRoom = new();
    public List<SecurityMachine> securityMachinesInRoom = new();
    public List<SpawningMachine> spawningMachinesInRoom = new();

    public IWaypointService waypointService;
    private bool alarmSubscribed;
    private bool machineEventsSubscribed;
    private readonly RoomEventHub roomEventHub = new RoomEventHub();

    public RoomEventHub RoomEventHub => roomEventHub;
    public AlarmState CurrentRoomAlarmState => GetFactoryAlarmStatus() != null
        ? GetFactoryAlarmStatus().CurrentAlarmState
        : AlarmState.Normal;

    /// <summary>
    /// Call this immediately after Instantiate().
    /// </summary>
    public void Initialize(
        FactoryManager factoryManager,
        MachineWorkerManager machineWorkerManager,
        MachineSecurityManager machineSecurityManager,
        SpawningWorkerManager spawningWorkerManager,
        IEnemiesSpawner enemiesSpawner)
    {
        FactoryManager = factoryManager;

        if (factoryManager != null)
        {
            var service = factoryManager.GetWayPointService();
            if (service != null)
                waypointService = service;
        }

        waypoints ??= new List<RoomWaypoint>();

        if (waypointService != null)
        {
            foreach (var wp in waypoints)
            {
                if (wp != null)
                    wp.parentRoom = this;
            }
            waypointService.RegisterRoomWaypoints(this, waypoints);
        }
        else
        {
            Debug.LogWarning($"{nameof(RoomManager)} '{name}' has no waypoint service to register with.");
        }

        if (waypointService != null)
        {
            foreach (var factoryMachine in factorymMachinesInRoom)
            {
                if (factoryMachine == null)
                    continue;
                factoryMachine.InitializeWaypointService(waypointService);
                machineWorkerManager?.RegisterMachine(factoryMachine);
                machineSecurityManager?.RegisterFactoryMachine(factoryMachine);
            }

            foreach (var spawningMachine in spawningMachinesInRoom)
            {
                if (spawningMachine == null)
                    continue;
                spawningMachine.InitializeWaypointService(waypointService);
                if (enemiesSpawner != null)
                    spawningMachine.InitializeSpawner(enemiesSpawner);
                spawningMachine.InitializeSecurityManager(machineSecurityManager);
                spawningWorkerManager?.RegisterMachine(spawningMachine);
            }

            foreach (var restingMachine in restingMachinesInRoom)
            {
                if (restingMachine == null)
                    continue;
                restingMachine.InitializeWaypointService(waypointService);
                machineWorkerManager?.RegisterMachine(restingMachine);
                machineSecurityManager?.RegisterRestingMachine(restingMachine);
            }

            foreach (var securityMachine in securityMachinesInRoom)
            {
                if (securityMachine == null)
                    continue;
                securityMachine.InitializeWaypointService(waypointService);
                machineSecurityManager?.RegisterSecurityMachine(securityMachine);
            }
        }

        if (factoryManager != null && !alarmSubscribed)
        {
            factoryManager.OnFactoryAlarmChanged += HandleFactoryAlarmChanged;
            alarmSubscribed = true;
        }

        SubscribeRoomMachineEvents();
    }

    private void OnDestroy()
    {
        if (triggerZone != null)
        {
            triggerZone.onEnter.RemoveListener(OnPlayerEnterRoom);
            triggerZone.onExit.RemoveListener(OnPlayerExitRoom);
        }

        UnsubscribeRoomMachineEvents();

        if (waypointService != null)
            waypointService.UnregisterRoomWaypoints(this);
        if (FactoryManager != null && alarmSubscribed)
        {
            FactoryManager.OnFactoryAlarmChanged -= HandleFactoryAlarmChanged;
            alarmSubscribed = false;
        }
    }

    // Exemple d’API pour fermer/ouvrir une porte (et notifier le service)
    public void SetWaypointStatus(RoomWaypoint waypoint, bool open)
    {
        waypoint.IsAvailable = open;
        if (waypointService != null)
        {
            waypointService.NotifyWaypointStatusChanged(waypoint, open);
        }
        else
        {
            Debug.LogWarning($"{nameof(RoomManager)} '{name}' cannot notify waypoint changes without a waypoint service.");
        }
    }

    public List<RoomWaypoint> GetWaypoints()
    {
        if (waypoints == null)
            waypoints = new List<RoomWaypoint>();
        return waypoints;
    }
    private void Start()
    {
        if (roomProperties == null)
        {
            Debug.LogError($"RoomManager '{gameObject.name}' is missing a RoomProperties reference.");
        }

        if (FactoryManager != null && !alarmSubscribed)
        {
            FactoryManager.OnFactoryAlarmChanged += HandleFactoryAlarmChanged;
            alarmSubscribed = true;
        }

        if (triggerZone != null)
        {
            triggerZone.onEnter.AddListener(OnPlayerEnterRoom);
            triggerZone.onExit.AddListener(OnPlayerExitRoom);

        }

        SubscribeRoomMachineEvents();
    }

    private void HandleFactoryAlarmChanged(AlarmState newAlarmState)
    {
        OnRoomAlarmChanged?.Invoke(newAlarmState);
    }

    public void OnPlayerEnterRoom(Collider2D playerCollider)
    {
        PlayerEntered?.Invoke(this);
    }

    public void OnPlayerExitRoom()
    {
        PlayerExited?.Invoke(this);
    }

    public void RaiseRoomThreat(AlarmState desiredAlarmState, RoomThreatSource source)
    {
        RaiseRoomThreat(desiredAlarmState, source, hasKnownPlayerPosition: false, knownPlayerPosition: Vector3.zero);
    }

    public void RaiseRoomThreat(AlarmState desiredAlarmState, RoomThreatSource source, Vector3 knownPlayerPosition)
    {
        RaiseRoomThreat(desiredAlarmState, source, hasKnownPlayerPosition: true, knownPlayerPosition);
    }

    public void RaiseRoomThreat(AlarmState desiredAlarmState, RoomThreatSource source, bool hasKnownPlayerPosition, Vector3 knownPlayerPosition)
    {
        var evt = new RoomThreatChangedEvent(
            this,
            desiredAlarmState,
            source,
            hasKnownPlayerPosition,
            knownPlayerPosition);

        roomEventHub.PublishThreatChanged(evt);
        ApplyThreatToFactoryAlarm(evt);
    }

    public void UpdateTrackedPlayerPositionIfAlarmActive(Vector3 playerPosition)
    {
        var alarmStatus = GetFactoryAlarmStatus();
        if (alarmStatus == null || alarmStatus.CurrentAlarmState == AlarmState.Normal)
            return;

        UpdateLastKnownPlayerPosition(playerPosition);
    }

    public void UpdateLastKnownPlayerPosition(Vector3 playerPosition)
    {
        var alarmStatus = GetFactoryAlarmStatus();
        if (alarmStatus != null)
            alarmStatus.LastPlayerPosition = playerPosition;

        waypointService?.UpdateClosestWaypointToPlayer(playerPosition);
    }

    public Bounds GetRoomBounds()
    {
        if (triggerZone == null)
        {
            Debug.LogWarning($"RoomManager '{gameObject.name}' has no triggerZone assigned.");
            return new Bounds(transform.position, Vector3.zero);
        }

        Vector2 center = (Vector2)triggerZone.transform.position + triggerZone.offset;
        Vector2 size = triggerZone.zoneSize;

        return new Bounds(center, size);
    }

    private void SubscribeRoomMachineEvents()
    {
        if (machineEventsSubscribed)
            return;

        SubscribeMachines(factorymMachinesInRoom);
        SubscribeMachines(restingMachinesInRoom);
        SubscribeMachines(securityMachinesInRoom);
        SubscribeMachines(spawningMachinesInRoom);

        machineEventsSubscribed = true;
    }

    private void UnsubscribeRoomMachineEvents()
    {
        if (!machineEventsSubscribed)
            return;

        UnsubscribeMachines(factorymMachinesInRoom);
        UnsubscribeMachines(restingMachinesInRoom);
        UnsubscribeMachines(securityMachinesInRoom);
        UnsubscribeMachines(spawningMachinesInRoom);

        machineEventsSubscribed = false;
    }

    private void SubscribeMachines<TMachine>(IEnumerable<TMachine> machines) where TMachine : BaseMachine
    {
        if (machines == null)
            return;

        foreach (var machine in machines)
        {
            if (machine == null)
                continue;
            machine.OnMachinePowerChanged += HandleMachinePowerChanged;
            machine.OnMachineOccupancyChanged += HandleMachineOccupancyChanged;
            machine.OnMachineTurnedOff += HandleMachineTurnedOff;
        }
    }

    private void UnsubscribeMachines<TMachine>(IEnumerable<TMachine> machines) where TMachine : BaseMachine
    {
        if (machines == null)
            return;

        foreach (var machine in machines)
        {
            if (machine == null)
                continue;
            machine.OnMachinePowerChanged -= HandleMachinePowerChanged;
            machine.OnMachineOccupancyChanged -= HandleMachineOccupancyChanged;
            machine.OnMachineTurnedOff -= HandleMachineTurnedOff;
        }
    }

    private void HandleMachinePowerChanged(MachinePowerChangedEvent evt)
    {
        var roomEvent = new RoomMachineChangedEvent(
            this,
            evt.Machine,
            RoomMachineEventKind.PowerChanged,
            evt.IsOn,
            isOccupied: null,
            robot: null,
            previousRobot: null);
        roomEventHub.PublishMachineChanged(roomEvent);
    }

    private void HandleMachineOccupancyChanged(MachineOccupancyChangedEvent evt)
    {
        var roomEvent = new RoomMachineChangedEvent(
            this,
            evt.Machine,
            RoomMachineEventKind.OccupancyChanged,
            isOn: null,
            evt.IsOccupied,
            evt.Robot,
            previousRobot: null);
        roomEventHub.PublishMachineChanged(roomEvent);
    }

    private void HandleMachineTurnedOff(MachineTurnedOffEvent evt)
    {
        var roomEvent = new RoomMachineChangedEvent(
            this,
            evt.Machine,
            RoomMachineEventKind.TurnedOff,
            isOn: false,
            isOccupied: null,
            robot: null,
            evt.PreviousRobot);
        roomEventHub.PublishMachineChanged(roomEvent);
    }

    private void ApplyThreatToFactoryAlarm(RoomThreatChangedEvent evt)
    {
        var alarmStatus = GetFactoryAlarmStatus();
        if (alarmStatus == null)
            return;

        if (evt.HasKnownPlayerPosition)
            UpdateLastKnownPlayerPosition(evt.KnownPlayerPosition);

        switch (evt.DesiredAlarmState)
        {
            case AlarmState.Wanted:
                if (alarmStatus.CurrentAlarmState == AlarmState.Normal)
                    alarmStatus.CurrentAlarmState = AlarmState.Wanted;
                break;
            case AlarmState.Lockdown:
                if (alarmStatus.CurrentAlarmState == AlarmState.Normal
                    || alarmStatus.CurrentAlarmState == AlarmState.Wanted)
                {
                    alarmStatus.CurrentAlarmState = AlarmState.Lockdown;
                }
                break;
            case AlarmState.Revolt:
                alarmStatus.CurrentAlarmState = AlarmState.Revolt;
                break;
            case AlarmState.Normal:
            default:
                break;
        }
    }

    private FactoryAlarmStatus GetFactoryAlarmStatus()
    {
        if (FactoryManager == null)
            return null;
        return FactoryManager.factoryAlarmStatus;
    }
}
