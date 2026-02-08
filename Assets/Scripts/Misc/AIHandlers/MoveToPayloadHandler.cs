using UnityEngine;

/// <summary>
/// Handles move to payload behavior.
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
            if (target != null) brain.Body.SetDestination(target);
            else brain.Body.SetDestination(machine.transform.position);
        }
        else if (payload is Vector3 v3)
        {
            brain.Body.SetDestination(v3);
        }
        else if (payload is Vector2 v2)
        {
            brain.Body.SetDestination(v2);
        }
    }
}
