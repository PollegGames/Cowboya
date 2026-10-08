using System;
using System.Collections.Generic;
using UnityEngine;

public enum StaticRouteDirection
{
    Bidirectional,
    ForwardOnly
}

[Serializable]
public sealed class StaticWaypointRoute
{
    [SerializeField] private string routeName;
    [SerializeField] private StaticRouteDirection direction = StaticRouteDirection.Bidirectional;
    [SerializeField] private List<RoomWaypoint> waypoints = new List<RoomWaypoint>();

    public string RouteName => routeName;
    public StaticRouteDirection Direction => direction;
    public IReadOnlyList<RoomWaypoint> Waypoints => waypoints;

    public StaticWaypointRoute(string routeName, StaticRouteDirection direction, IEnumerable<RoomWaypoint> waypoints)
    {
        this.routeName = routeName;
        this.direction = direction;
        this.waypoints = waypoints != null
            ? new List<RoomWaypoint>(waypoints)
            : new List<RoomWaypoint>();
    }
}

public readonly struct StaticWaypointConnection
{
    public StaticWaypointConnection(RoomWaypoint from, RoomWaypoint to)
    {
        From = from;
        To = to;
    }

    public RoomWaypoint From { get; }
    public RoomWaypoint To { get; }
}

/// <summary>
/// Stores the explicitly authored navigation topology for one static level.
/// </summary>
[DisallowMultipleComponent]
public sealed class StaticLevelPath : MonoBehaviour
{
    [SerializeField] private List<StaticWaypointRoute> routes = new List<StaticWaypointRoute>();
    [SerializeField] private RoomWaypoint levelStart;
    [SerializeField] private RoomWaypoint levelEnd;
    [SerializeField] private bool drawDebugGraph = true;
    [SerializeField, Min(0.01f)] private float nodeRadius = 0.25f;

    public IReadOnlyList<StaticWaypointRoute> Routes => routes;
    public RoomWaypoint LevelStart => levelStart;
    public RoomWaypoint LevelEnd => levelEnd;
    public bool IsNavigationReady { get; private set; }

    /// <summary>Finds the enabled static navigation authoring components in loaded scenes.</summary>
    public static List<StaticLevelPath> FindEnabledInScene()
    {
        var result = new List<StaticLevelPath>();
        StaticLevelPath[] paths = Resources.FindObjectsOfTypeAll<StaticLevelPath>();
        foreach (StaticLevelPath path in paths)
        {
            // isActiveAndEnabled is still false while another component on the same
            // active object is executing Awake, and Scene.isLoaded is also false during
            // the initial load pass. A valid scene excludes prefab assets while still
            // allowing bootstrap to discover authored components during that window.
            if (path != null && path.enabled && path.gameObject.activeInHierarchy
                && path.gameObject.scene.IsValid())
                result.Add(path);
        }
        return result;
    }

    /// <summary>Returns each configured waypoint once, using object identity.</summary>
    public List<RoomWaypoint> GetDistinctWaypoints()
    {
        var result = new List<RoomWaypoint>();
        var instanceIds = new HashSet<int>();
        foreach (StaticWaypointRoute route in routes)
        {
            if (route?.Waypoints == null)
                continue;

            foreach (RoomWaypoint waypoint in route.Waypoints)
            {
                if (waypoint != null && instanceIds.Add(waypoint.GetInstanceID()))
                    result.Add(waypoint);
            }
        }
        return result;
    }

    /// <summary>Builds the deduplicated directed edges represented by all valid route segments.</summary>
    public List<StaticWaypointConnection> GetDirectedConnections()
    {
        var result = new List<StaticWaypointConnection>();
        var keys = new HashSet<DirectedConnectionKey>();
        foreach (StaticWaypointRoute route in routes)
        {
            if (route?.Waypoints == null)
                continue;

            for (int i = 0; i < route.Waypoints.Count - 1; i++)
            {
                RoomWaypoint from = route.Waypoints[i];
                RoomWaypoint to = route.Waypoints[i + 1];
                AddConnection(from, to, keys, result);
                if (route.Direction == StaticRouteDirection.Bidirectional)
                    AddConnection(to, from, keys, result);
            }
        }
        return result;
    }

    /// <summary>Validates route authoring and returns errors and non-blocking warnings.</summary>
    public bool ValidateRoutes(out List<string> errors, out List<string> warnings)
    {
        errors = new List<string>();
        warnings = new List<string>();
        var identities = new Dictionary<string, RoomWaypoint>();
        var connections = new HashSet<DirectedConnectionKey>();
        bool hasValidRoute = false;

        if (routes == null || routes.Count == 0)
        {
            errors.Add("Static mode has no authored routes.");
            errors.Add("Static mode has no valid route.");
            return false;
        }

        for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
        {
            StaticWaypointRoute route = routes[routeIndex];
            string label = route != null && !string.IsNullOrWhiteSpace(route.RouteName)
                ? route.RouteName
                : $"Route {routeIndex}";
            if (route == null || route.Waypoints == null || route.Waypoints.Count < 2)
            {
                errors.Add($"{label} must contain at least two waypoints.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(route.RouteName))
                warnings.Add($"Route {routeIndex} has an empty name.");

            bool routeIsValid = true;
            var routeWaypoints = new HashSet<int>();
            for (int i = 0; i < route.Waypoints.Count; i++)
            {
                RoomWaypoint waypoint = route.Waypoints[i];
                if (waypoint == null)
                {
                    errors.Add($"{label} contains a null waypoint at index {i}.");
                    routeIsValid = false;
                    continue;
                }

                if (waypoint.parentRoom == null)
                {
                    errors.Add($"Waypoint '{waypoint.name}' in {label} has no owning RoomManager.");
                    routeIsValid = false;
                }

                string identity = GetEffectiveIdentity(waypoint);
                if (identities.TryGetValue(identity, out RoomWaypoint existing)
                    && existing != waypoint)
                {
                    errors.Add($"Waypoints '{existing.name}' and '{waypoint.name}' share identity '{identity}'.");
                    routeIsValid = false;
                }
                else
                {
                    identities[identity] = waypoint;
                }

                if (!routeWaypoints.Add(waypoint.GetInstanceID()))
                    warnings.Add($"{label} repeats waypoint '{waypoint.name}'.");

                if (i >= route.Waypoints.Count - 1)
                    continue;

                RoomWaypoint next = route.Waypoints[i + 1];
                if (next == waypoint && next != null)
                {
                    errors.Add($"{label} repeats '{waypoint.name}' in consecutive positions {i} and {i + 1}.");
                    routeIsValid = false;
                    continue;
                }

                var key = new DirectedConnectionKey(waypoint, next);
                if (waypoint != null && next != null && !connections.Add(key))
                    warnings.Add($"{label} repeats connection '{waypoint.name}' -> '{next.name}'.");
            }

            hasValidRoute |= routeIsValid;
        }

        if (!hasValidRoute)
            errors.Add("Static mode has no valid route.");

        return errors.Count == 0;
    }

    /// <summary>Validates, registers, builds, and verifies this static graph.</summary>
    public bool TryInitializeNavigation(IWaypointService waypointService)
    {
        IsNavigationReady = false;
        if (waypointService is not WaypointService concreteService)
        {
            Debug.LogError("Static navigation requires a WaypointService instance.", this);
            return false;
        }

        bool isValid = ValidateRoutes(out List<string> errors, out List<string> warnings);
        foreach (string warning in warnings)
            Debug.LogWarning($"[{nameof(StaticLevelPath)}] {warning}", this);
        foreach (string error in errors)
            Debug.LogError($"[{nameof(StaticLevelPath)}] {error}", this);
        if (!isValid || !concreteService.ConfigureGraph(WaypointGraphMode.StaticExplicit, this))
            return false;

        var groupedWaypoints = new Dictionary<RoomManager, List<RoomWaypoint>>();
        foreach (RoomWaypoint waypoint in GetDistinctWaypoints())
        {
            if (waypoint == null || waypoint.parentRoom == null)
                continue;
            if (!groupedWaypoints.TryGetValue(waypoint.parentRoom, out List<RoomWaypoint> roomWaypoints))
            {
                roomWaypoints = new List<RoomWaypoint>();
                groupedWaypoints.Add(waypoint.parentRoom, roomWaypoints);
            }
            roomWaypoints.Add(waypoint);
        }

        foreach (KeyValuePair<RoomManager, List<RoomWaypoint>> group in groupedWaypoints)
            waypointService.RegisterRoomWaypoints(group.Key, group.Value);

        waypointService.BuildAllNeighbors();
        IsNavigationReady = VerifyRuntimeGraph(concreteService);
        return IsNavigationReady;
    }

    private bool VerifyRuntimeGraph(WaypointService waypointService)
    {
        if (waypointService.GraphMode != WaypointGraphMode.StaticExplicit)
        {
            Debug.LogError("Waypoint service did not retain StaticExplicit graph mode.", this);
            return false;
        }

        var registeredIds = new HashSet<int>();
        foreach (RoomWaypoint waypoint in waypointService.GetAllWaypoints())
        {
            if (waypoint != null)
                registeredIds.Add(waypoint.GetInstanceID());
        }

        foreach (RoomWaypoint waypoint in GetDistinctWaypoints())
        {
            if (waypoint != null && !registeredIds.Contains(waypoint.GetInstanceID()))
            {
                Debug.LogError($"Waypoint '{waypoint.name}' was not registered with the static service.", this);
                return false;
            }
        }

        foreach (StaticWaypointConnection connection in GetDirectedConnections())
        {
            if (connection.From == null || connection.To == null
                || !connection.From.Neighbors.Contains(connection.To))
            {
                Debug.LogError("An authored static connection was not built in the requested direction.", this);
                return false;
            }
        }

        if (levelStart != null && levelEnd != null
            && waypointService.FindWorldPath(levelStart, levelEnd).Count == 0)
        {
            Debug.LogError($"No path exists from level start '{levelStart.name}' to level end '{levelEnd.name}'.", this);
            return false;
        }

        return true;
    }

    private static void AddConnection(
        RoomWaypoint from,
        RoomWaypoint to,
        HashSet<DirectedConnectionKey> keys,
        List<StaticWaypointConnection> result)
    {
        if (from == null || to == null || from == to)
            return;

        var key = new DirectedConnectionKey(from, to);
        if (keys.Add(key))
            result.Add(new StaticWaypointConnection(from, to));
    }

    private static string GetEffectiveIdentity(RoomWaypoint waypoint)
    {
        return waypoint.WorldPos.ToString("F2") + "_" + waypoint.type;
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugGraph || routes == null)
            return;

        var usageCounts = new Dictionary<int, int>();
        foreach (StaticWaypointRoute route in routes)
        {
            if (route?.Waypoints == null)
                continue;
            foreach (RoomWaypoint waypoint in route.Waypoints)
            {
                if (waypoint == null)
                    continue;
                int id = waypoint.GetInstanceID();
                usageCounts[id] = usageCounts.TryGetValue(id, out int count) ? count + 1 : 1;
            }
        }

        foreach (StaticWaypointRoute route in routes)
        {
            if (route?.Waypoints == null)
                continue;

            Color routeColor = route.Direction == StaticRouteDirection.Bidirectional
                ? new Color(0.15f, 0.8f, 1f)
                : new Color(1f, 0.65f, 0.1f);
            for (int i = 0; i < route.Waypoints.Count; i++)
            {
                RoomWaypoint waypoint = route.Waypoints[i];
                if (waypoint == null)
                    continue;

                Gizmos.color = waypoint.parentRoom == null
                    ? Color.red
                    : usageCounts[waypoint.GetInstanceID()] > 1 ? Color.yellow : routeColor;
                Gizmos.DrawSphere(waypoint.WorldPos, nodeRadius);

                if (i >= route.Waypoints.Count - 1 || route.Waypoints[i + 1] == null)
                    continue;

                Vector3 from = waypoint.WorldPos;
                Vector3 to = route.Waypoints[i + 1].WorldPos;
                Gizmos.color = routeColor;
                Gizmos.DrawLine(from, to);
                DrawArrow(from, to, routeColor);
                if (route.Direction == StaticRouteDirection.Bidirectional)
                    DrawArrow(to, from, routeColor);
            }
        }
    }

    private static void DrawArrow(Vector3 from, Vector3 to, Color color)
    {
        Vector3 direction = to - from;
        if (direction.sqrMagnitude < 0.001f)
            return;

        Vector3 tip = Vector3.Lerp(from, to, 0.62f);
        Vector3 normalized = direction.normalized;
        Vector3 perpendicular = new Vector3(-normalized.y, normalized.x, 0f);
        float size = Mathf.Min(0.6f, direction.magnitude * 0.15f);
        Gizmos.color = color;
        Gizmos.DrawLine(tip, tip - normalized * size + perpendicular * size * 0.5f);
        Gizmos.DrawLine(tip, tip - normalized * size - perpendicular * size * 0.5f);
    }

    private readonly struct DirectedConnectionKey : IEquatable<DirectedConnectionKey>
    {
        private readonly int fromId;
        private readonly int toId;

        public DirectedConnectionKey(RoomWaypoint from, RoomWaypoint to)
        {
            fromId = from != null ? from.GetInstanceID() : 0;
            toId = to != null ? to.GetInstanceID() : 0;
        }

        public bool Equals(DirectedConnectionKey other) => fromId == other.fromId && toId == other.toId;
        public override bool Equals(object obj) => obj is DirectedConnectionKey other && Equals(other);
        public override int GetHashCode() => (fromId * 397) ^ toId;
    }
}
