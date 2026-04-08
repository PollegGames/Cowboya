using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum Axis
{
    Horizontal,
    Vertical,
}

public enum Bidirection
{
    Both,
    Forward,
}

public class WaypointService : MonoBehaviour, IWaypointService
{
    [Header("Debug")]
    [SerializeField] private bool logWorkAssignments = false;
    [Header("Dependencies")]
    [SerializeField]
    private MonoBehaviour registryBehaviour;

    [SerializeField]
    private MonoBehaviour pathFinderBehaviour;

    private IWaypointRegistry registry;
    private IPathFinder pathFinder;

    // Reservation data
    private readonly HashSet<RoomWaypoint> reservedWaypoints = new();
    private readonly Dictionary<RoomWaypoint, int> workUsageCounts = new();
    private readonly Dictionary<RoomWaypoint, int> securityUsageCounts = new();

    private readonly Dictionary<RoomWaypoint, int> workSpawnersUsageCounts = new();
    private readonly Dictionary<FactoryMachine, RobotBrain> reservedMachines = new();

    // Listeners
    private readonly HashSet<IRobotNavigationListener> robots = new();

    private void Awake()
    {
        registry = registryBehaviour as IWaypointRegistry;
        pathFinder = pathFinderBehaviour as IPathFinder;
        if (registry == null)
            Debug.LogError("RegistryBehaviour must implement IWaypointRegistry");
        if (pathFinder == null)
            Debug.LogError("PathFinderBehaviour must implement IPathFinder");
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
    public List<RoomWaypoint> GetAllWaypoints() => registry.GetAllWaypoints();

    public List<RoomWaypoint> GetActiveWaypoints() => registry.GetActiveWaypoints();

    public List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end) =>
        pathFinder.FindWorldPath(start, end);

    public RoomWaypoint GetClosestWaypoint(Vector2 position, bool includeUnavailable = false)
    {
        var source = includeUnavailable ? GetAllWaypoints() : GetActiveWaypoints();
        return source.OrderBy(wp => Vector2.Distance(wp.WorldPos, position)).FirstOrDefault();
    }


    public RoomWaypoint GetEndPoint()
    {
        var ep = GetActiveWaypoints()
            .FirstOrDefault(wp =>
                wp.parentRoom.roomProperties.usageType == UsageType.End
                && wp.type == WaypointType.Center
            );
        if (ep == null)
            Debug.LogWarning("No end point found.");
        return ep;
    }

    public RoomWaypoint GetStartPoint()
    {
        var sp = GetActiveWaypoints()
            .FirstOrDefault(wp =>
                wp.parentRoom.roomProperties.usageType == UsageType.Start
                && wp.type == WaypointType.Center
            );
        if (sp == null)
            Debug.LogWarning("No start point found.");
        return sp;
    }

    public void UpdateClosestWaypointToPlayer(Vector2 playerPosition)
    {
        if (playerPosition != null)
        {
            var point = GetClosestWaypoint(playerPosition);
            if (point != null)
            {
                ClosestWaypointToPlayer = point;
            }
        }
    }

    public List<RoomWaypoint> GetActiveWaypointsList() => GetActiveWaypoints(); // alias

    public void BuildAllNeighbors(bool includeUnavailable = false)
    {
        if (pathFinder == null)
        {
            Debug.LogWarning($"{nameof(WaypointService)} is missing a path finder.");
            return;
        }

        pathFinder.BuildAllNeighbors(includeUnavailable);
    }

    public RoomWaypoint ClosestWaypointToPlayer { get; private set; }
    #endregion

    #region Reservation: Work, Rest, Security
    public RoomWaypoint GetLeastUsedFreeWorkPoint(RoomWaypoint exclude = null)
    {
        PruneReservationData();

        var works = registry
            .GetActiveWaypoints()
            .Where(wp =>
                wp.parentRoom.roomProperties.usageType == UsageType.Work
                && wp.type == WaypointType.Work
                && wp.parentRoom.factorymMachinesInRoom.Any(m =>
                    m.IsOn && m.CurrentWorker == null && !reservedMachines.ContainsKey(m)
                )
                && !reservedWaypoints.Contains(wp)
                && wp != exclude
            )
            .ToList();

        if (!works.Any())
        {
            works = registry
                .GetAllWaypoints()
                .Where(wp =>
                    wp.parentRoom.roomProperties.usageType == UsageType.Work
                    && wp.type == WaypointType.Work
                    && wp.parentRoom.factorymMachinesInRoom.Any(m =>
                        m.IsOn && m.CurrentWorker == null && !reservedMachines.ContainsKey(m)
                    )
                    && !reservedWaypoints.Contains(wp)
                    && wp != exclude
                )
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
            if (logWorkAssignments)
            {
                Debug.Log(
                    $"[WaypointReservation] Assigned FREE WORK '{best.WorldPos}' (count={workUsageCounts[best]})."
                );
            }
            return best;
        }

        if (logWorkAssignments)
            Debug.Log("[WaypointReservation] No FREE work points available.");
        return null;
    }

    public RoomWaypoint GetWorkOrRestPoint(RoomWaypoint exclude = null)
    {
        PruneReservationData();

        var works = registry
            .GetActiveWaypoints()
            .Where(wp =>
                wp.parentRoom.roomProperties.usageType == UsageType.Work
                && wp.type == WaypointType.Work
                && wp != exclude
                && !reservedWaypoints.Contains(wp)
                && wp.parentRoom.factorymMachinesInRoom.Any(m =>
                    m.IsOn && m.CurrentWorker == null && !reservedMachines.ContainsKey(m)
                )
            )
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
            if (logWorkAssignments)
            {
                Debug.Log(
                    $"[WaypointReservation] Assigned FREE WORK '{best.WorldPos}' (count={workUsageCounts[best]})."
                );
            }
            return best;
        }

        var restPoint = GetFirstRestPoint(exclude);
        if (restPoint == null)
        {
            restPoint = GetStartPoint();
        }
        if (logWorkAssignments)
            Debug.Log($"[WaypointReservation] No FREE work; fallback={(restPoint != null ? restPoint.name : "null")}");
        return restPoint;
    }

    public RoomWaypoint GetAnyOnWorkPoint(RoomWaypoint exclude = null)
    {
        PruneReservationData();

        var works = registry
            .GetActiveWaypoints()
            .Where(wp =>
                wp.parentRoom.roomProperties.usageType == UsageType.Work
                && wp.type == WaypointType.Work
                && wp != exclude
                && wp.parentRoom.factorymMachinesInRoom.Any(m => m.IsOn)
            )
            .ToList();

        if (!works.Any())
        {
            works = registry
                .GetAllWaypoints()
                .Where(wp =>
                    wp.parentRoom.roomProperties.usageType == UsageType.Work
                    && wp.type == WaypointType.Work
                    && wp != exclude
                    && wp.parentRoom.factorymMachinesInRoom.Any(m => m.IsOn)
                )
                .ToList();
        }

        if (!works.Any())
            return null;

        var best = works
            .OrderBy(wp => workUsageCounts.TryGetValue(wp, out var c) ? c : 0)
            .First();
        workUsageCounts[best] = workUsageCounts.TryGetValue(best, out var count)
            ? count + 1
            : 1;
        return best;
    }

    public FactoryMachine GetAnyOnFactoryMachine()
    {
        var machines = registry
            .GetActiveWaypoints()
            .Where(wp =>
                wp.parentRoom.roomProperties.usageType == UsageType.Work
                && wp.parentRoom.factorymMachinesInRoom.Any(m => m.IsOn)
            )
            .SelectMany(wp => wp.parentRoom.factorymMachinesInRoom)
            .Where(m => m.IsOn)
            .ToList();

        return machines.FirstOrDefault();
    }

    //Get the center point of a blocked room
    public RoomWaypoint GetBlockedRoomSecuritySpawning(RoomWaypoint exclude = null)
    {
        PruneReservationData();

        var blockedRooms = registry
            .GetAllWaypoints()
            .Where(wp =>
                wp.parentRoom.roomProperties.usageType == UsageType.Blocked
                && wp.type == WaypointType.Spawner
                && wp != exclude
                && !reservedWaypoints.Contains(wp)
            )
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
            Debug.Log(
                $"[WaypointReservation] Assigned BLOCKED ROOM CENTER '{best.WorldPos}' (count={workSpawnersUsageCounts[best]})."
            );
            return best;
        }

        return null;
    }

    public RoomWaypoint GetBlockedRoomCenter(RoomWaypoint exclude = null)
    {
        PruneReservationData();

        var blockedRooms = registry
            .GetAllWaypoints()
            .Where(wp =>
                wp.parentRoom.roomProperties.usageType == UsageType.Blocked
                && wp.type == WaypointType.Center
                && wp != exclude
                && !reservedWaypoints.Contains(wp)
            )
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
            Debug.Log(
                $"[WaypointReservation] Assigned BLOCKED ROOM CENTER '{best.WorldPos}' (count={workSpawnersUsageCounts[best]})."
            );
            return best;
        }
        Debug.LogWarning("[WaypointReservation] No blocked room center found.");
        blockedRooms = registry
            .GetAllWaypoints()
            .Where(wp =>
                wp.parentRoom.roomProperties.usageType == UsageType.Blocked
                && wp.type == WaypointType.Center
                && wp != exclude
            )
            .ToList();
        var blocked = blockedRooms.FirstOrDefault();
        if (blocked == null)
        {
            blocked = GetStartPoint();
        }
        return blocked;
    }

    public RoomWaypoint GetSecurityOrRestPoint(RoomWaypoint exclude = null)
    {
        PruneReservationData();

        var secs = registry
            .GetActiveWaypoints()
            .Where(wp =>
                wp.parentRoom.roomProperties.usageType == UsageType.POI
                && wp.type == WaypointType.Security
                && wp != exclude
                && !reservedWaypoints.Contains(wp)
            )
            .ToList();

        if (secs.Any())
        {
            var best = secs.OrderBy(wp => securityUsageCounts.TryGetValue(wp, out var c) ? c : 0)
                .First();
            securityUsageCounts[best] = securityUsageCounts.TryGetValue(best, out var count)
                ? count + 1
                : 1;
            reservedWaypoints.Add(best);
            Debug.Log(
                $"[WaypointReservation] Assigned SECURITY '{best.WorldPos}' (count={securityUsageCounts[best]})."
            );
            return best;
        }

        return GetFirstRestPoint(exclude);
    }

    public RoomWaypoint GetFirstRestPoint(RoomWaypoint exclude = null)
    {
        PruneReservationData();

        var allWaypoints = registry.GetActiveWaypoints();
        var restPoints = allWaypoints
            .Where(wp =>
                wp != null
                && wp.parentRoom != null
                && wp.parentRoom.roomProperties.usageType == UsageType.POI
                && wp.type == WaypointType.Rest
                && wp.parentRoom.restingMachinesInRoom.Any(m => m.IsOn)
            )
            .Where(wp => wp != exclude)
            .ToList();

        if (restPoints.Any())
        {
            var unreserved = restPoints.Where(wp => !reservedWaypoints.Contains(wp)).ToList();
            if (unreserved.Any())
            {
                var chosen = unreserved.First();
                reservedWaypoints.Add(chosen);
                return chosen;
            }

            // All rest points are already reserved; do not return an occupied/shared fallback.
            return null;
        }

        // If here, there were no restPoints where resting machines are ON. Log for debugging.
        Debug.Log("[WaypointService] No rest points with ON resting machines found; falling back to any rest point.");

        // Fallback to any rest point even if no resting machines are powered on.
        var fallbackRestPoints = allWaypoints
            .Where(wp =>
                wp != null
                && wp.parentRoom != null
                && wp.parentRoom.roomProperties.usageType == UsageType.POI
                && wp.type == WaypointType.Rest
            )
            .Where(wp => wp != exclude)
            .ToList();

        if (!fallbackRestPoints.Any())
            return null;

        var fallbackUnreserved = fallbackRestPoints.Where(wp => !reservedWaypoints.Contains(wp)).ToList();
        if (fallbackUnreserved.Any())
        {
            var chosen = fallbackUnreserved.First();
            reservedWaypoints.Add(chosen);
            return chosen;
        }

        return null;
    }

    public RoomWaypoint GetFirstFreeSecurityPoint()
    {
        PruneReservationData();

        var secs = registry
            .GetActiveWaypoints()
            .Where(wp =>
                wp.parentRoom.roomProperties.usageType == UsageType.POI
                && wp.type == WaypointType.Security
                && !reservedWaypoints.Contains(wp)
            )
            .ToList();
        if (!secs.Any())
        {
            var restPoint = GetFirstRestPoint();
            if (restPoint == null)
            {
                Debug.LogWarning(
                    "[WaypointService] No free security points or rest points available."
                );
                return null;
            }
        }
        var target = secs.Any()
            ? secs.OrderBy(wp => securityUsageCounts.TryGetValue(wp, out var c) ? c : 0).First()
            : GetFirstRestPoint();
        securityUsageCounts[target] = securityUsageCounts.TryGetValue(target, out var sc)
            ? sc + 1
            : 1;
        reservedWaypoints.Add(target);
        return target;
    }

    public bool IsPOIReserved(RoomWaypoint poi)
    {
        if (poi == null)
            return false;

        PruneReservationData();
        return reservedWaypoints.Contains(poi);
    }

    public void ReleaseInvalidReservations()
    {
        PruneReservationData();
    }

    public void ReleasePOI(RoomWaypoint poi)
    {
        if (poi == null)
            return;
        PruneReservationData();
        reservedWaypoints.Remove(poi);
        if (workUsageCounts.TryGetValue(poi, out var wc) && wc > 0)
            workUsageCounts[poi] = wc - 1;
        if (securityUsageCounts.TryGetValue(poi, out var sc) && sc > 0)
            securityUsageCounts[poi] = sc - 1;
        OnPOIReleased?.Invoke(poi);
    }
    #endregion

    private void PruneReservationData()
    {
        reservedWaypoints.RemoveWhere(wp => wp == null);

        var activeWaypoints = registry != null ? registry.GetAllWaypoints() : null;
        if (activeWaypoints == null || activeWaypoints.Count == 0)
            return;

        var activeSet = new HashSet<RoomWaypoint>(activeWaypoints);
        reservedWaypoints.RemoveWhere(wp => wp == null || !activeSet.Contains(wp));

        workUsageCounts.Keys.Where(k => k == null || !activeSet.Contains(k)).ToList()
            .ForEach(k => workUsageCounts.Remove(k));
        securityUsageCounts.Keys.Where(k => k == null || !activeSet.Contains(k)).ToList()
            .ForEach(k => securityUsageCounts.Remove(k));
        workSpawnersUsageCounts.Keys.Where(k => k == null || !activeSet.Contains(k)).ToList()
            .ForEach(k => workSpawnersUsageCounts.Remove(k));
    }

    #region Machine Reservation
    public FactoryMachine ReserveFreeMachine(RoomManager room, RobotBrain worker)
    {
        if (room == null)
            return null;
        var machine = room.factorymMachinesInRoom.FirstOrDefault(m =>
            m.IsOn && m.CurrentWorker == null && !reservedMachines.ContainsKey(m)
        );
        if (machine != null)
            reservedMachines[machine] = worker;
        return machine;
    }

    public bool ReserveMachine(FactoryMachine machine, RobotBrain worker)
    {
        if (machine == null || worker == null)
            return false;
        if (reservedMachines.ContainsKey(machine))
            return false;
        reservedMachines[machine] = worker;
        return true;
    }

    public void ReleaseMachine(FactoryMachine machine)
    {
        if (machine != null)
            reservedMachines.Remove(machine);
    }

    public bool IsMachineReserved(FactoryMachine machine)
    {
        return machine != null && reservedMachines.ContainsKey(machine);
    }

    public bool IsMachineReservedFor(FactoryMachine machine, RobotBrain worker)
    {
        if (machine == null || worker == null)
            return false;
        return reservedMachines.TryGetValue(machine, out var reserved) && reserved == worker;
    }
    #endregion

    public event Action<RoomWaypoint> OnPOIReleased;
}
