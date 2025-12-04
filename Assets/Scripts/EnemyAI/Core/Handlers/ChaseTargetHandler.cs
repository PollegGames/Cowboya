using UnityEngine;

/// <summary>
/// Moves toward a target transform or last known player position.
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
                brain.Body.SetDestination(waypoint);
                return;
            }
            brain.Body.SetDestination(target.position);
            return;
        }

        if (brain.Memory != null && brain.Memory.LastKnownPlayerPosition != Vector3.zero)
        {
            brain.Body.SetDestination(brain.Memory.LastKnownPlayerPosition);
        }
    }
}
