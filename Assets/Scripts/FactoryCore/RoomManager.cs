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
                spawningMachine.InitializeSpawner(enemiesSpawner);
                spawningMachine.InitializeSecurityManager(machineSecurityManager);
                spawningWorkerManager?.RegisterMachine(spawningMachine);
            }

            foreach (var restingMachine in restingMachinesInRoom)
            {
                if (restingMachine == null)
                    continue;
                restingMachine.InitializeWaypointService(waypointService);
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

        if (factoryManager != null)
            factoryManager.OnFactoryAlarmChanged += HandleFactoryAlarmChanged;
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