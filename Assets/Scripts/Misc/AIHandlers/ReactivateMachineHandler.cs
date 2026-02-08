using System.Collections;
using UnityEngine;

/// <summary>
/// Moves to a machine and triggers the reactivation routine.
/// </summary>
[CreateAssetMenu(menuName = "RobotAI/Handlers/ReactivateMachine", fileName = "ReactivateMachineHandler")]
public class ReactivateMachineHandler : ScriptableRobotTaskHandler
{
    public override void Execute(RobotBrain brain, object payload)
    {
        if (brain == null || brain.Body == null)
            return;

        if (payload is BaseMachine machine && machine != null)
        {
            var expectedWaypoint = machine.GetComponent<RoomWaypoint>();
            if (expectedWaypoint != null)
                brain.Body.SetDestination(expectedWaypoint);
            else
                brain.Body.SetDestination(machine.transform.position);

            brain.StartReactivateRoutine(ReactivateAndReturnRoutine(brain, machine, expectedWaypoint));
        }
        else if (payload is RoomWaypoint waypoint && waypoint != null)
        {
            brain.Body.SetDestination(waypoint);
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

    private IEnumerator ReactivateAndReturnRoutine(
        RobotBrain brain,
        BaseMachine machine,
        RoomWaypoint expectedWaypoint)
    {
        if (brain == null)
            yield break;

        float start = Time.time;
        bool useTimeout = brain.ReactivateArrivalTimeoutSeconds > 0f;
        if (brain.IsSecurityGuard)
            useTimeout = false;

        bool reached = false;
        while (brain.Body != null
            && !reached
            && (!useTimeout || Time.time - start < brain.ReactivateArrivalTimeoutSeconds))
        {
            reached = brain.HasArrivedAtExpectedMachine(machine, expectedWaypoint);
            yield return null;
        }

        if (!reached)
            reached = brain.HasArrivedAtExpectedMachine(machine, expectedWaypoint);
        if (brain.Body != null && reached && machine != null && !machine.IsOn)
        {
            machine.PowerOn();
        }

        brain.CompleteReactivateTask(machine, reached);
        brain.EndReactivateRoutine();
    }
}
