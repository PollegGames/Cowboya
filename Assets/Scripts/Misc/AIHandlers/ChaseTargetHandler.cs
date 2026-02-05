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

        Transform target = payload as Transform;
        if (target != null)
        {
            var waypoint = target.GetComponent<RoomWaypoint>();
            if (waypoint != null)
            {
                brain.Body.SetDestination(waypoint, includeUnavailable: true);
                return;
            }
            brain.Body.SetDestination(target.position, includeUnavailable: true);
            return;
        }

        if (payload is RoomWaypoint roomWaypoint && roomWaypoint != null)
        {
            brain.Body.SetDestination(roomWaypoint, includeUnavailable: true);
            return;
        }

        if (payload is Vector3 targetPos)
        {
            brain.Body.SetDestination(targetPos, includeUnavailable: true);
            return;
        }

        if (brain.Memory != null && brain.Memory.LastKnownPlayerPosition != Vector3.zero)
        {
            brain.Body.SetDestination(brain.Memory.LastKnownPlayerPosition, includeUnavailable: true);
        }
    }
}
