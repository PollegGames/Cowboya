using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WaypointReservationService : MonoBehaviour, IPOIReservationService
{
    [SerializeField] private MonoBehaviour registryBehaviour;
    private IWaypointRegistry registry;

    private readonly HashSet<RoomWaypoint> _reservedWaypoints = new();
    private readonly Dictionary<RoomWaypoint, int> _workUsageCounts = new();
    private readonly Dictionary<RoomWaypoint, int> _securityUsageCounts = new();

    private void Awake()
    {
        registry = registryBehaviour as IWaypointRegistry;
    }

    public RoomWaypoint GetLeastUsedFreeWorkPoint(RoomWaypoint exclude = null)
    {
        var works = registry.GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.Empty
                         && wp.type == WaypointType.Work
                         && wp != exclude
                         && !_reservedWaypoints.Contains(wp))
            .ToList();
        if (!works.Any())
        {
            works = registry.GetActiveWaypoints()
                .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.Empty
                            && wp.type == WaypointType.Work
                         && wp != exclude)
                .ToList();
        }

        if (!works.Any()) return null;

        var best = works.OrderBy(wp => _workUsageCounts.TryGetValue(wp, out var c) ? c : 0).First();
        _workUsageCounts[best] = _workUsageCounts.TryGetValue(best, out var count) ? count + 1 : 1;
        _reservedWaypoints.Add(best);
        Debug.Log($"[WaypointReservation] Assigned EMPTY WORK '{best.name}' (count={_workUsageCounts[best]}).");
        return best;
    }

    public RoomWaypoint GetWorkOrRestPoint(RoomWaypoint exclude = null)
    {
        var works = registry.GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.Empty
                         && wp.type == WaypointType.Center
                            && wp != exclude
                         && !_reservedWaypoints.Contains(wp))
            .ToList();

        if (works.Any())
        {
            var best = works.OrderBy(wp => _workUsageCounts.TryGetValue(wp, out var c) ? c : 0).First();
            _workUsageCounts[best] = _workUsageCounts.TryGetValue(best, out var count) ? count + 1 : 1;
            _reservedWaypoints.Add(best);
            Debug.Log($"[WaypointReservation] Assigned EMPTY WORK '{best.name}' (count={_workUsageCounts[best]}).");
            return best;
        }

        var rest = GetFirstRestPoint(exclude);
        if (rest != null)
        {
            _reservedWaypoints.Add(rest);
            Debug.Log($"[WaypointReservation] Assigned REST '{rest.name}'.");
        }
        return rest;
    }

    public RoomWaypoint GetFirstRestPoint(RoomWaypoint exclude = null)
    {
        return registry.GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.POI
                         && wp.type == WaypointType.Rest
                         && wp != exclude)
            .FirstOrDefault();
    }

    public RoomWaypoint GetFirstFreeSecurityPoint()
    {
        var security = registry.GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.POI
                         && wp.type == WaypointType.Security
                         && !_reservedWaypoints.Contains(wp))
            .ToList();
        if (!security.Any())
        {
            return GetFirstRestPoint();
        }

        var best = security.OrderBy(wp => _securityUsageCounts.TryGetValue(wp, out var c) ? c : 0).First();
        _securityUsageCounts[best] = _securityUsageCounts.TryGetValue(best, out var count) ? count + 1 : 1;
        _reservedWaypoints.Add(best);
        Debug.Log($"[WaypointReservation] Assigned SECURITY point '{best.name}' (count={_securityUsageCounts[best]}).");
        return best;
    }

    public void ReleasePOI(RoomWaypoint poi)
    {
        if (poi == null) return;
        _reservedWaypoints.Remove(poi);
        if (_workUsageCounts.TryGetValue(poi, out var wc) && wc > 0) _workUsageCounts[poi] = wc - 1;
        if (_securityUsageCounts.TryGetValue(poi, out var sc) && sc > 0) _securityUsageCounts[poi] = sc - 1;
        Debug.Log($"[WaypointReservation] Released POI '{poi.name}'.");
    }
}
