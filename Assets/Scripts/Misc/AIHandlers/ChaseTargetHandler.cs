using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Handles chase target behavior.
/// </summary>
[CreateAssetMenu(menuName = "RobotAI/Handlers/ChaseTarget", fileName = "ChaseTargetHandler")]
public class ChaseTargetHandler : ScriptableRobotTaskHandler
{
    public override void Execute(RobotBrain brain, object payload)
    {
        if (brain == null || brain.Body == null)
            return;

        if (brain.Heart != null && brain.Heart.Role == RobotRole.Follower)
        {
            Vector3 targetPos = Vector3.zero;
            if (brain.Memory != null && brain.Memory.LastKnownPlayerPosition != Vector3.zero)
                targetPos = brain.Memory.LastKnownPlayerPosition;
            else if (brain.WaypointService != null && brain.WaypointService.ClosestWaypointToPlayer != null)
                targetPos = brain.WaypointService.ClosestWaypointToPlayer.WorldPos;

            var closest = GetClosestWaypointSameFloor(brain.WaypointService, targetPos, 5f, includeUnavailable: true);
            if (closest != null)
            {
                LogFollowerChase(brain, $"closestWaypointToPlayer={closest.name} pos={closest.WorldPos}");
                brain.Body.SetDestination(closest, includeUnavailable: true);
                return;
            }
        }

        LogFollowerChase(brain, $"payloadType={(payload != null ? payload.GetType().Name : "null")} payload={payload}");

        Transform target = payload as Transform;
        if (target != null)
        {
            var waypoint = target.GetComponent<RoomWaypoint>();
            if (waypoint != null)
            {
                LogFollowerChase(brain, $"targetWaypoint={waypoint.name} pos={waypoint.WorldPos}");
                brain.Body.SetDestination(waypoint, includeUnavailable: true);
                return;
            }
            LogFollowerChase(brain, $"targetTransform={target.name} pos={target.position}");
            brain.Body.SetDestination(target.position, includeUnavailable: true);
            return;
        }

        if (payload is RoomWaypoint roomWaypoint && roomWaypoint != null)
        {
            LogFollowerChase(brain, $"payloadWaypoint={roomWaypoint.name} pos={roomWaypoint.WorldPos}");
            brain.Body.SetDestination(roomWaypoint, includeUnavailable: true);
            return;
        }

        if (payload is Vector3 payloadPos)
        {
            LogFollowerChase(brain, $"payloadPos={payloadPos}");
            brain.Body.SetDestination(payloadPos, includeUnavailable: true);
            return;
        }

        if (brain.Memory != null && brain.Memory.LastKnownPlayerPosition != Vector3.zero)
        {
            LogFollowerChase(brain, $"memoryPos={brain.Memory.LastKnownPlayerPosition}");
            brain.Body.SetDestination(brain.Memory.LastKnownPlayerPosition, includeUnavailable: true);
        }
    }

    private void LogFollowerChase(RobotBrain brain, string message)
    {
        if (brain == null || brain.Heart == null || brain.Heart.Role != RobotRole.Follower)
            return;
        Debug.Log($"[Follower][ChaseTarget] {message}", brain);
    }

    private static RoomWaypoint GetClosestWaypointSameFloor(
        IWaypointService service,
        Vector2 targetPosition,
        float maxYDelta,
        bool includeUnavailable)
    {
        if (service == null || targetPosition == Vector2.zero)
            return null;

        List<RoomWaypoint> source = includeUnavailable
            ? service.GetAllWaypoints()
            : service.GetActiveWaypoints();
        if (source == null || source.Count == 0)
            return null;

        var sameFloor = source
            .Where(wp => wp != null && Mathf.Abs(wp.WorldPos.y - targetPosition.y) <= maxYDelta)
            .ToList();

        var candidates = sameFloor.Count > 0 ? sameFloor : source;
        return candidates
            .OrderBy(wp => Vector2.Distance(wp.WorldPos, targetPosition))
            .FirstOrDefault();
    }
}
