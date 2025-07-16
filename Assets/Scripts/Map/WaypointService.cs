using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum Axis { Horizontal, Vertical }
public enum Bidirection { Both, Forward }

public class WaypointService : MonoBehaviour, IWaypointService
{
    [Header("Dependencies")]
    [SerializeField] private MonoBehaviour registryBehaviour;
    [SerializeField] private MonoBehaviour pathFinderBehaviour;

    private IWaypointRegistry registry;
    private IPathFinder pathFinder;

    // Reservation data
    private readonly HashSet<RoomWaypoint> reservedWaypoints = new();
    private readonly Dictionary<RoomWaypoint, int> workUsageCounts = new();
    private readonly Dictionary<RoomWaypoint, int> securityUsageCounts = new();

    private readonly Dictionary<RoomWaypoint, int> workSpawnersUsageCounts = new();
    private readonly Dictionary<FactoryMachine, EnemyWorkerController> reservedMachines = new();

    // Listeners
    private readonly HashSet<IRobotNavigationListener> robots = new();

    private void Awake()
    {
        registry = registryBehaviour as IWaypointRegistry;
        pathFinder = pathFinderBehaviour as IPathFinder;
        if (registry == null) Debug.LogError("RegistryBehaviour must implement IWaypointRegistry");
        if (pathFinder == null) Debug.LogError("PathFinderBehaviour must implement IPathFinder");
    }

    #region Registration & Notification
    public void RegisterRoomWaypoints(RoomManager room, IEnumerable<RoomWaypoint> waypoints)
    {
        registry.RegisterRoomWaypoints(room, waypoints);
    }
    public void UnregisterRoomWaypoints(RoomManager room)
    {
        registry.UnregisterRoomWaypoints(room);
    }
    public void Subscribe(IRobotNavigationListener robot)
    {
        robots.Add(robot);
    }
    public void Unsubscribe(IRobotNavigationListener robot)
    {
        robots.Remove(robot);
    }
    public void NotifyWaypointStatusChanged(RoomWaypoint changed, bool isAvailable)
    {
        if (!isAvailable)
        {
            foreach (var r in robots)
                r.OnPathObsoleted(changed);
        }
    }
    #endregion

    #region Queries & Pathfinding
    public List<RoomWaypoint> GetActiveWaypoints() => registry.GetActiveWaypoints();
    public List<RoomWaypoint> GetAllWaypoints() => registry.GetAllWaypoints();
    public List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end) => pathFinder.FindWorldPath(start, end);
    public RoomWaypoint GetClosestWaypoint(Vector2 position)
        => GetActiveWaypoints().OrderBy(wp => Vector2.Distance(wp.WorldPos, position)).FirstOrDefault();
    public RoomWaypoint GetClosestInactiveWaypoint(Vector2 position)
        => GetAllWaypoints().OrderBy(wp => Vector2.Distance(wp.WorldPos, position)).FirstOrDefault();
    public RoomWaypoint GetEndPoint()
    {
        var ep = GetActiveWaypoints()
            .FirstOrDefault(wp => wp.parentRoom.roomProperties.usageType == UsageType.End && wp.type == WaypointType.Center);
        if (ep == null) Debug.LogWarning("No end point found.");
        return ep;
    }
    public RoomWaypoint GetStartPoint()
    {
        var sp = GetActiveWaypoints()
            .FirstOrDefault(wp => wp.parentRoom.roomProperties.usageType == UsageType.Start && wp.type == WaypointType.Center);
        if (sp == null) Debug.LogWarning("No start point found.");
        Debug.Log($"[WaypointService] Start Point: {sp?.WorldPos}");
        return sp;
    }
    public void UpdateClosestWaypointToPlayer(Vector2 playerPosition)
    {
        ClosestWaypointToPlayer = GetClosestWaypoint(playerPosition);
    }
    public List<RoomWaypoint> GetActiveWaypointsList() => GetActiveWaypoints(); // alias
    public void BuildAllNeighbors() => pathFinder.BuildAllNeighbors();
    public RoomWaypoint ClosestWaypointToPlayer { get; private set; }
    #endregion

    #region Reservation: Work, Rest, Security
    public RoomWaypoint GetLeastUsedFreeWorkPoint(RoomWaypoint exclude = null)
    {
        var works = registry.GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.Empty
                        && wp.type == WaypointType.Work
                        && wp.parentRoom.factorymMachinesInRoom
                            .Any(m => m.IsOn
                                    && m.CurrentWorker == null
                                    && !reservedMachines.ContainsKey(m))
                        && !reservedWaypoints.Contains(wp)
                        && wp != exclude)
            .ToList();

        if (!works.Any())
        {
            works = registry.GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.Empty
                        && wp.type == WaypointType.Work
                        && wp.parentRoom.factorymMachinesInRoom
                            .Any(m => m.IsOn
                                    && m.CurrentWorker != null)
                        && wp != exclude)
            .ToList();
        }

        if (!works.Any())
        {
            works = registry.GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.Empty
                        && wp.type == WaypointType.Work
                        && wp.parentRoom.factorymMachinesInRoom
                            .Any(m => m.IsOn)
                        && wp != exclude)
            .ToList();
        }

        if (works.Any())
        {
            var best = works
                .OrderBy(wp => workUsageCounts.TryGetValue(wp, out var c) ? c : 0)
                .First();
            workUsageCounts[best] = workUsageCounts.TryGetValue(best, out var count)
                ? count + 1
                : 1;
            reservedWaypoints.Add(best);
            Debug.Log($"[WaypointReservation] Assigned EMPTY WORK '{best.WorldPos}' (count={workUsageCounts[best]}).");
            return best;
        }

        return GetFirstRestPoint(exclude);
    }

    public RoomWaypoint GetWorkOrRestPoint(RoomWaypoint exclude = null)
    {
        var works = registry.GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.Empty
                && wp.type == WaypointType.Work
                && wp != exclude
                && !reservedWaypoints.Contains(wp))
            .ToList();

        if (works.Any())
        {
            var best = works
                .OrderBy(wp => workUsageCounts.TryGetValue(wp, out var c) ? c : 0)
                .First();
            workUsageCounts[best] = workUsageCounts.TryGetValue(best, out var count)
                ? count + 1
                : 1;
            reservedWaypoints.Add(best);
            Debug.Log($"[WaypointReservation] Assigned EMPTY WORK '{best.WorldPos}' (count={workUsageCounts[best]}).");
            return best;
        }

        return GetFirstRestPoint(exclude);
    }

    //Get the center point of a blocked room
    public RoomWaypoint GetBlockedRoomCenter(RoomWaypoint exclude = null)
    {
        var blockedRooms = registry.GetAllWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.Blocked
                && wp.type == WaypointType.Security
                && wp != exclude
                && !reservedWaypoints.Contains(wp))
            .ToList();

        if (blockedRooms.Any())
        {
            var best = blockedRooms
                .OrderBy(wp => workSpawnersUsageCounts.TryGetValue(wp, out var c) ? c : 0)
                .First();
            workSpawnersUsageCounts[best] = workSpawnersUsageCounts.TryGetValue(best, out var count)
                ? count + 1
                : 1;
            reservedWaypoints.Add(best);
            Debug.Log($"[WaypointReservation] Assigned BLOCKED ROOM CENTER '{best.WorldPos}' (count={workSpawnersUsageCounts[best]}).");
            return best;
        }

        return null;
    }
    
     public RoomWaypoint GetSecurityOrRestPoint(RoomWaypoint exclude = null)
    {
        var secs = registry.GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.POI
                && wp.type == WaypointType.Security
                && wp != exclude
                && !reservedWaypoints.Contains(wp))
            .ToList();

        if (secs.Any())
        {
            var best = secs
                .OrderBy(wp => securityUsageCounts.TryGetValue(wp, out var c) ? c : 0)
                .First();
            securityUsageCounts[best] = securityUsageCounts.TryGetValue(best, out var count)
                ? count + 1
                : 1;
            reservedWaypoints.Add(best);
            Debug.Log($"[WaypointReservation] Assigned SECURITY '{best.WorldPos}' (count={securityUsageCounts[best]}).");
            return best;
        }

        return GetFirstRestPoint(exclude);
    }
    public RoomWaypoint GetFirstRestPoint(RoomWaypoint exclude = null)
    {
        var allWaypoints = registry.GetActiveWaypoints();
        var restPoints = allWaypoints
            .Where(wp =>
                wp != null
             && wp.parentRoom != null
             && wp.parentRoom.roomProperties.usageType == UsageType.POI
             && wp.type == WaypointType.Rest)
            .ToList();

        // Next, only those with at least one active RestingMachine
        var withMachines = restPoints
            .Where(wp =>
            {
                var machines = wp.parentRoom.restingMachinesInRoom;
                if (machines == null) return false;                    // guard null list
                return machines.Any(m => m != null && m.IsOn);        // guard null elements
            })
            .Where(wp => wp != exclude)                              // still respect exclude
            .ToList();
        if (withMachines.Any())
        {
            var chosen = withMachines.First();
            reservedWaypoints.Add(chosen);
            Debug.Log($"[WaypointReservation] Assigned REST POINT '{chosen.WorldPos}' (with machines)");
            return chosen;
        }

        // Try purely waypoint‐based first (no machine check)
        var unreserved = restPoints
            .Where(wp => !reservedWaypoints.Contains(wp) && wp != exclude)
            .ToList();
        if (unreserved.Any())
        {
            var chosen = unreserved.First();
            reservedWaypoints.Add(chosen);
            Debug.Log($"[WaypointReservation] Assigned REST POINT '{chosen.WorldPos}' (pure list)");
            return chosen;
        }

        return null;
    }

    public RoomWaypoint GetFirstFreeSecurityPoint()
    {
        var secs = registry.GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.POI
                         && wp.type == WaypointType.Security
                         && !reservedWaypoints.Contains(wp))
            .ToList();
        if (!secs.Any())
        {
            var restPoint = GetFirstRestPoint();
            if(restPoint == null)
            {
                Debug.LogWarning("[WaypointService] No free security points or rest points available.");
                return null;
            }
        }
        var target = secs.Any()
            ? secs.OrderBy(wp => securityUsageCounts.TryGetValue(wp, out var c) ? c : 0).First()
            : GetFirstRestPoint();
        securityUsageCounts[target] = securityUsageCounts.TryGetValue(target, out var sc) ? sc + 1 : 1;
        reservedWaypoints.Add(target);
        return target;
    }

    public void ReleasePOI(RoomWaypoint poi)
    {
        if (poi == null) return;
        reservedWaypoints.Remove(poi);
        if (workUsageCounts.TryGetValue(poi, out var wc) && wc > 0) workUsageCounts[poi] = wc - 1;
        if (securityUsageCounts.TryGetValue(poi, out var sc) && sc > 0) securityUsageCounts[poi] = sc - 1;
        OnPOIReleased?.Invoke(poi);
    }
    #endregion

    #region Machine Reservation
    public FactoryMachine ReserveFreeMachine(RoomManager room, EnemyWorkerController worker)
    {
        if (room == null) return null;
        var machine = room.factorymMachinesInRoom.FirstOrDefault(m => m.IsOn && m.CurrentWorker == null && !reservedMachines.ContainsKey(m));
        if (machine != null) reservedMachines[machine] = worker;
        return machine;
    }

    public void ReleaseMachine(FactoryMachine machine)
    {
        if (machine != null) reservedMachines.Remove(machine);
    }

    public bool IsMachineReserved(FactoryMachine machine)
    {
        return machine != null && reservedMachines.ContainsKey(machine);
    }
    #endregion

    public event Action<RoomWaypoint> OnPOIReleased;
}
