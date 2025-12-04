using UnityEngine;

/// <summary>
/// Moves the body toward a waypoint or machine-linked waypoint.
/// </summary>
[CreateAssetMenu(menuName = "RobotAI/Handlers/MoveToPayload", fileName = "MoveToPayloadHandler")]
public class MoveToPayloadHandler : ScriptableRobotTaskHandler
{
    public override void Execute(RobotBrain brain, object payload)
    {
        if (brain == null || brain.Body == null)
            return;

        if (payload is RoomWaypoint waypoint && waypoint != null)
        {
            brain.Body.SetDestination(waypoint);
        }
        else if (payload is BaseMachine machine && machine != null)
        {
            var target = machine.GetComponent<RoomWaypoint>();
            if (target != null)
                brain.Body.SetDestination(target);
        }
    }
}
