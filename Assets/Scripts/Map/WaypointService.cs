using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum Axis { Horizontal, Vertical }
public enum Bidirection { Both, Forward }

public class WaypointService : MonoBehaviour, IWaypointService
{
    [SerializeField] private MonoBehaviour registryBehaviour;
    [SerializeField] private MonoBehaviour pathFinderBehaviour;
    [SerializeField] private MonoBehaviour reservationServiceBehaviour;

    private IWaypointRegistry registry;
    private IPathFinder pathFinder;
    private IPOIReservationService reservationService;
    private readonly HashSet<IRobotNavigationListener> robots = new();

    private void Awake()
    {
        registry = registryBehaviour as IWaypointRegistry;
        pathFinder = pathFinderBehaviour as IPathFinder;
        reservationService = reservationServiceBehaviour as IPOIReservationService;
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

    public void Subscribe(IRobotNavigationListener robot) => robots.Add(robot);
    public void Unsubscribe(IRobotNavigationListener robot) => robots.Remove(robot);

    public void NotifyWaypointStatusChanged(RoomWaypoint changed, bool isAvailable)
    {
        if (!isAvailable)
            foreach (var robot in robots)
                robot.OnPathObsoleted(changed);
    }
    #endregion

    #region Waypoint Queries & Pathfinding
    public List<RoomWaypoint> GetActiveWaypoints() =>
        registry.GetActiveWaypoints();

    private List<RoomWaypoint> GetAllWaypoints() =>
        registry.GetAllWaypoints();

    public List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end)
    {
        return pathFinder.FindWorldPath(start, end);
    }

    public void BuildAllNeighbors()
    {
        pathFinder.BuildAllNeighbors();
    }
    #endregion

    #region Work & Rest Assignment
    public RoomWaypoint GetLeastUsedFreeWorkPoint(RoomWaypoint exclude = null) =>
        reservationService.GetLeastUsedFreeWorkPoint(exclude);

    public RoomWaypoint GetWorkOrRestPoint(RoomWaypoint exclude = null) =>
        reservationService.GetWorkOrRestPoint(exclude);

    public RoomWaypoint GetFirstRestPoint(RoomWaypoint exclude = null) =>
        reservationService.GetFirstRestPoint(exclude);

    public void ReleasePOI(RoomWaypoint poi)
    {
        reservationService.ReleasePOI(poi);
        OnPOIReleased?.Invoke(poi);
    }
    #endregion

    #region Security Assignment
    public RoomWaypoint GetFirstFreeSecurityPoint() =>
        reservationService.GetFirstFreeSecurityPoint();

    #endregion

    #region Utility
    public RoomWaypoint GetClosestWaypoint(Vector2 position)
    {
        return GetActiveWaypoints()
            .OrderBy(wp => Vector2.Distance(wp.WorldPos, position))
            .FirstOrDefault();
    }

    public void UpdateClosestWaypointToPlayer(Vector2 playerPosition)
    {
        ClosestWaypointToPlayer = GetClosestWaypoint(playerPosition);
    }

    public RoomWaypoint GetEndPoint()
    {
        var endPoints = GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.End
            && wp.type == WaypointType.Center)
            .ToList();

        if (endPoints.Count == 0)
        {
            Debug.LogWarning("[WaypointService] No end points found.");
            return null;
        }

        // Return the first end point found
        return endPoints[0];
    }

    public RoomWaypoint GetStartPoint()
    {
        var startPoint = GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.Start
            && wp.type == WaypointType.Center)
            .ToList();

        if (startPoint.Count == 0)
        {
            Debug.LogWarning("[WaypointService] No end points found.");
            return null;
        }

        // Return the first end point found
        return startPoint[0];
    }

    public event Action<RoomWaypoint> OnPOIReleased;


    public RoomWaypoint ClosestWaypointToPlayer { get; private set; }
    #endregion
}
