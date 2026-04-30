using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WaypointPathFollower : IRobotNavigationListener
{
    private readonly Transform body;
    private readonly IMover mover;
    private readonly IWaypointQueries waypointQueries;
    private readonly MovementMonitor monitor;

    private List<Vector3> currentPath;
    private List<RoomWaypoint> currentWaypoints;
    private int pathIndex;
    private Vector3? finalTarget;
    private bool currentIncludeUnavailable;

    private readonly float arrivalX;
    private readonly float arrivalY;
    private readonly float deadZoneX;
    private readonly float deadZoneY;

    private bool withinX;
    private bool withinY;

    private RoomWaypoint lastAttemptedWaypoint;

    public event Action OnStuck;

    public int CurrentPathCount => currentPath?.Count ?? 0;
    public int CurrentWaypointsCount => currentWaypoints?.Count ?? 0;
    public int PathIndex => pathIndex;
    public RoomWaypoint LastAttemptedWaypoint => lastAttemptedWaypoint;
    public RoomWaypoint CurrentTarget =>
        currentWaypoints != null && currentWaypoints.Count > 0 ? currentWaypoints[^1] : null;

    public WaypointPathFollower(
        Transform body,
        IMover mover,
        IWaypointQueries waypointQueries,
        float arrivalThresholdX = 2f,
        float arrivalThresholdY = 2f,
        float deadZoneX = 5f,
        float deadZoneY = 5f)
    {
        this.body = body;
        this.mover = mover;
        this.waypointQueries = waypointQueries;
        this.arrivalX = arrivalThresholdX;
        this.arrivalY = arrivalThresholdY;
        this.deadZoneX = deadZoneX;
        this.deadZoneY = deadZoneY;
        monitor = new MovementMonitor();
    }

    public void Update(float deltaTime)
    {
        HandleMovement(deltaTime);
    }

    private void HandleMovement(float deltaTime)
    {
        if (currentPath == null || pathIndex >= currentPath.Count)
            return;

        Vector3 target = currentPath[pathIndex];
        Vector3 currentPos = body.position;
        float dx = target.x - currentPos.x;
        float dy = target.y - currentPos.y;

        bool nearX = Mathf.Abs(dx) <= arrivalX;
        bool nearY = Mathf.Abs(dy) <= arrivalY;

        if (nearX && nearY)
        {
            pathIndex++;
            withinX = withinY = false;

            if (pathIndex >= currentPath.Count)
                monitor.Reset(currentPos);
            return;
        }

        withinX = UpdateAxis(withinX, dx, arrivalX, deadZoneX);
        withinY = UpdateAxis(withinY, dy, arrivalY, deadZoneY);

        mover.SetMovement(withinX ? 0f : Mathf.Sign(dx));
        mover.SetVerticalMovement(withinY ? 0f : Mathf.Sign(dy));

        MovementStatus status = monitor.Update(deltaTime, currentPos);
        if (status == MovementStatus.Stuck)
        {
            OnStuck?.Invoke();
        }
        else if (status == MovementStatus.ShouldAttemptRecovery && currentWaypoints?.Count > 0)
        {
            SetDestination(currentWaypoints[^1], finalTarget, currentIncludeUnavailable);
        }
    }

    private bool UpdateAxis(bool within, float delta, float threshold, float dead)
    {
        if (!within && Mathf.Abs(delta) <= threshold) return true;
        if (within && Mathf.Abs(delta) > dead) return false;
        return within;
    }

    public void SetDestination(RoomWaypoint target, bool includeUnavailable = false)
    {
        SetDestination(target, null, includeUnavailable);
    }

    public void SetDestination(RoomWaypoint target, Vector3? finalPosition, bool includeUnavailable = false)
    {
        if (includeUnavailable && waypointQueries is IWaypointService svc)
            svc.BuildAllNeighbors(true);

        finalTarget = finalPosition;
        currentIncludeUnavailable = includeUnavailable;

        RoomWaypoint start = GetClosestWaypoint(target, includeUnavailable);
        if (start == target)
        {
            if (finalPosition.HasValue && ShouldAppendFinalPosition(finalPosition.Value, start.WorldPos))
            {
                lastAttemptedWaypoint = start;
                currentWaypoints = new List<RoomWaypoint> { start };
                currentPath = new List<Vector3> { start.WorldPos, finalPosition.Value };
                pathIndex = 1;
                LogPathDecision("WaypointPathFollower.SetDestination", start, target, "alreadyAtWaypoint finalPosition=" + finalPosition.Value.ToString("F2") + " pathCount=" + currentPath.Count);
                return;
            }

            lastAttemptedWaypoint = start;
            currentWaypoints = new List<RoomWaypoint> { start };
            currentPath = new List<Vector3> { start.WorldPos };
            pathIndex = currentPath.Count;
            mover.SetMovement(0f);
            mover.SetVerticalMovement(0f);
            LogPathDecision("WaypointPathFollower.SetDestination", start, target, "alreadyAtWaypoint pathCount=" + currentPath.Count);
            return;
        }
        if (start == null)
        {
            Debug.LogWarning("Start is null, no pathfinding");
            LogPathDecision("WaypointPathFollower.NoPath", null, target, "reason=start_null includeUnavailable=" + includeUnavailable);
            ClearPath();
            return;
        }
        if (target == null)
        {
            Debug.LogWarning("Target is null, no pathfinding");
            LogPathDecision("WaypointPathFollower.NoPath", start, null, "reason=target_null includeUnavailable=" + includeUnavailable);
            ClearPath();
            return;
        }

        lastAttemptedWaypoint = start;

        var raw = waypointQueries.FindWorldPath(start, target);
        if (raw == null || raw.Count == 0)
        {
            Debug.LogError($"No path from {start.name} to {target.name}.");
            LogPathDecision("WaypointPathFollower.NoPath", start, target, "reason=find_world_path_empty includeUnavailable=" + includeUnavailable);
            ClearPath();
            return;
        }

        if (raw[0] != start) raw.Insert(0, start);
        if (raw[^1] != target) raw.Add(target);

        currentWaypoints = raw;
        currentPath = raw.Select(wp => wp.WorldPos).ToList();
        if (finalPosition.HasValue && currentPath.Count > 0)
        {
            Vector3 last = currentPath[^1];
            if (ShouldAppendFinalPosition(finalPosition.Value, last))
                currentPath.Add(finalPosition.Value);
        }
        pathIndex = 1;
        LogPathDecision(
            "WaypointPathFollower.SetDestination",
            start,
            target,
            "pathCount=" + currentPath.Count
                + " waypointCount=" + currentWaypoints.Count
                + " finalPosition=" + (finalPosition.HasValue ? finalPosition.Value.ToString("F2") : "none")
                + " includeUnavailable=" + includeUnavailable);
    }

    public bool HasArrived =>
        currentPath != null && currentPath.Count > 0 && pathIndex >= currentPath.Count;

    public void OnPathObsoleted(RoomWaypoint blockedWaypoint)
    {
        Debug.Log($"Path to {blockedWaypoint.name} is blocked. Recalculating...");
    }

    public RoomWaypoint GetClosestWaypoint(RoomWaypoint exclude = null, bool includeUnavailable = false)
    {
        var agentY = body.position.y;

        var source = includeUnavailable ? waypointQueries.GetAllWaypoints() : waypointQueries.GetActiveWaypoints();
        var sameFloorCandidates = source
            .Where(wp => wp != exclude && Mathf.Abs(wp.WorldPos.y - agentY) < 5f)
            .OrderBy(wp => Vector2.Distance(body.position, wp.WorldPos))
            .ToList();

        // Even when includeUnavailable=true, prefer local floor starts first.
        // Cross-floor nearest should only be used as a fallback when no local candidate exists.
        var candidates = sameFloorCandidates.Count > 0
            ? sameFloorCandidates
            : source.Where(wp => wp != exclude)
                .OrderBy(wp => Vector2.Distance(body.position, wp.WorldPos))
                .ToList();

        foreach (var wp in candidates)
        {
            if (lastAttemptedWaypoint == null || !wp.Equals(lastAttemptedWaypoint))
            {
                lastAttemptedWaypoint = wp;
                return wp;
            }
        }

        return candidates.FirstOrDefault();
    }

    public void DrawGizmos()
    {
        if (currentPath == null || currentPath.Count <= pathIndex)
            return;

        Gizmos.color = Color.magenta;
        Vector3 prev = body.position;
        for (int i = pathIndex; i < currentPath.Count; i++)
        {
            Vector3 point = currentPath[i];
            Gizmos.DrawLine(prev, point);
            Gizmos.DrawSphere(point, 0.1f);
            prev = point;
        }
    }

    public void ClearPath()
    {
        currentPath = null;
        currentWaypoints = null;
        pathIndex = 0;
        finalTarget = null;
        currentIncludeUnavailable = false;
        monitor.Reset(body.position);
        mover.SetMovement(0f);
        mover.SetVerticalMovement(0f);
    }

    private bool ShouldAppendFinalPosition(Vector3 finalPosition, Vector3 lastWaypointPosition)
    {
        return Mathf.Abs(finalPosition.x - lastWaypointPosition.x) > arrivalX
            || Mathf.Abs(finalPosition.y - lastWaypointPosition.y) > arrivalY;
    }

    private void LogPathDecision(string eventName, RoomWaypoint start, RoomWaypoint target, string detail)
    {
        if (mover is MonoBehaviour owner)
            RobotEcosystemProbe.RecordWaypointDecision(owner, eventName, start, target, detail);
    }
}
