using UnityEngine;
using System;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    public FactoryManager FactoryManager { get; private set; }
    public Transform PlayerHead { get; private set; }

    [Header("Room Settings")]
    public RoomProperties roomProperties;

    public event Action<AlarmState> OnRoomAlarmChanged;
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

        // 1) pull the service
        waypointService = factoryManager.GetWayPointService();

        // 2) register room & its grid cell and register all the door/lift waypoints
        foreach (var wp in waypoints)
            wp.parentRoom = this;
        waypointService.RegisterRoomWaypoints(this, waypoints);

        // 3) register machines in this room
        foreach (var factoryMachine in factorymMachinesInRoom)
        {
            factoryMachine.InitializeWaypointService(waypointService);
            machineWorkerManager.RegisterMachine(factoryMachine);
            machineSecurityManager?.RegisterFactoryMachine(factoryMachine);
        }

        // 4) hook up alarm + triggers
        factoryManager.OnFactoryAlarmChanged += HandleFactoryAlarmChanged;

        // 5) register spawning machines
        foreach (var spawningMachine in spawningMachinesInRoom)
        {
            spawningMachine.InitializeWaypointService(waypointService);
            spawningMachine.InitializeSpawner(enemiesSpawner);
            spawningWorkerManager?.RegisterMachine(spawningMachine);
        }

        foreach (var restingMachine in restingMachinesInRoom)
        {
            restingMachine.InitializeWaypointService(waypointService);
            machineSecurityManager?.RegisterRestingMachine(restingMachine);
        }
    }

    private void OnDestroy()
    {
        if (waypointService != null)
            waypointService.UnregisterRoomWaypoints(this);
        if (FactoryManager != null)
            FactoryManager.OnFactoryAlarmChanged -= HandleFactoryAlarmChanged;
    }

    // Exemple d’API pour fermer/ouvrir une porte (et notifier le service)
    public void SetWaypointStatus(RoomWaypoint waypoint, bool open)
    {
        waypoint.IsAvailable = open;
        waypointService.NotifyWaypointStatusChanged(waypoint, open);
    }

    public List<RoomWaypoint> GetWaypoints() => waypoints;
    private void Start()
    {
        if (roomProperties == null)
        {
            Debug.LogError($"RoomManager '{gameObject.name}' is missing a RoomProperties reference.");
            return;
        }

        if (FactoryManager != null)
        {
            FactoryManager.OnFactoryAlarmChanged += HandleFactoryAlarmChanged;
        }

        if (triggerZone != null)
        {
            triggerZone.onEnter.AddListener(OnPlayerEnterRoom);
            triggerZone.onExit.AddListener(OnPlayerExitRoom);

        }
    }

    private void HandleFactoryAlarmChanged(AlarmState newAlarmState)
    {
        Debug.Log($"{gameObject.name} RoomManager received new AlarmState: {newAlarmState}");
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
}