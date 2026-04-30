using System.Collections.Generic;
using UnityEngine;

public class SecuritySlot : MonoBehaviour
{
    [SerializeField] private BaseMachine securityMachine;
    [SerializeField] private RoomWaypoint machineWaypoint;
    [SerializeField] private bool logSlotDecisions = true;

    private readonly Dictionary<int, int> colliderPresenceCount = new Dictionary<int, int>();
    private RoomWaypoint cachedMachineWaypoint;
    private string cachedMachineWaypointSource = "none";
    private float cachedMachineWaypointDistance = -1f;

    private void OnDisable()
    {
        colliderPresenceCount.Clear();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var brain = collision.GetComponentInParent<RobotBrainNew>();
        if (brain == null)
            return;

        var heart = brain.Heart;
        if (!TryTrackEnter(brain, heart))
            return;

        if (heart == null || heart.Role != RobotRole.SecurityGuard)
        {
            RobotEcosystemProbe.RecordSlotDecision(this, "SecuritySlot", "ignored_role", brain, securityMachine, heart != null ? heart.CurrentTask : null);
            if (logSlotDecisions)
                Debug.Log($"[SecuritySlot] Ignored {brain.name} role={heart?.Role}", this);
            return;
        }

        if (!IsIncomingGuardTargetingThisSlot(brain))
        {
            RecordTargetingDecision(brain, heart.CurrentTask, "rejected_task");
            if (logSlotDecisions)
                Debug.Log($"[SecuritySlot] Ignored {brain.name} task={(heart.CurrentTask != null ? heart.CurrentTask.Type.ToString() : "None")}", this);
            return;
        }

        RobotEcosystemProbe.RecordSlotDecision(this, "SecuritySlot", "attach_requested", brain, securityMachine, heart.CurrentTask);
        bool replacing = securityMachine is SecurityMachine machine
            && machine.CurrentGuard != null
            && !ReferenceEquals(machine.CurrentGuard, brain);

        securityMachine.AttachRobot(brain.gameObject);
        RobotEcosystemProbe.RecordSlotDecision(
            this,
            "SecuritySlot",
            replacing ? "replaced_current" : "attached_empty",
            brain,
            securityMachine,
            heart.CurrentTask);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        var brain = collision.GetComponentInParent<RobotBrainNew>();
        if (brain == null)
            return;

        if (!TryTrackExit(brain))
            return;

        RobotEcosystemProbe.RecordSlotDecision(this, "SecuritySlot", "exit_tracked", brain, securityMachine, brain.Heart != null ? brain.Heart.CurrentTask : null);
    }

    private bool TryTrackEnter(RobotBrainNew brain, RobotHeartNew heart)
    {
        int id = brain.GetInstanceID();
        if (colliderPresenceCount.TryGetValue(id, out int count))
        {
            colliderPresenceCount[id] = count + 1;
            RobotEcosystemProbe.RecordSlotDecision(this, "SecuritySlot", "attach_ignored_duplicate", brain, securityMachine, heart != null ? heart.CurrentTask : null);
            return false;
        }

        colliderPresenceCount[id] = 1;
        return true;
    }

    private bool TryTrackExit(RobotBrainNew brain)
    {
        int id = brain.GetInstanceID();
        if (!colliderPresenceCount.TryGetValue(id, out int count))
            return false;

        count--;
        if (count > 0)
        {
            colliderPresenceCount[id] = count;
            return false;
        }

        colliderPresenceCount.Remove(id);
        return true;
    }

    private bool IsIncomingGuardTargetingThisSlot(RobotBrainNew brain)
    {
        if (brain == null || securityMachine == null || brain.Heart == null)
            return false;

        RobotTask currentTask = brain.Heart.CurrentTask;
        if (currentTask == null)
            return false;

        if (currentTask.Type == RobotTaskType.ReactivateMachine)
        {
            bool accepted = ReferenceEquals(currentTask.Payload as BaseMachine, securityMachine);
            RecordTargetingDecision(brain, currentTask, accepted ? "accepted_reactivate_machine" : "rejected_reactivate_mismatch");
            return accepted;
        }

        if (currentTask.Type == RobotTaskType.GuardPost)
        {
            bool accepted = IsTaskOrMemoryTargetingThisMachine(currentTask, brain);
            RecordTargetingDecision(brain, currentTask, accepted ? "accepted_guard_post" : "rejected_guard_post_not_this_slot");
            return accepted;
        }

        if (currentTask.Type != RobotTaskType.GoToMachine)
            return false;

        bool goToAccepted = IsTaskOrMemoryTargetingThisMachine(currentTask, brain);
        RecordTargetingDecision(brain, currentTask, goToAccepted ? "accepted_go_to_machine" : "rejected_go_to_machine_not_this_slot");
        return goToAccepted;
    }

    private bool IsTaskOrMemoryTargetingThisMachine(RobotTask task, RobotBrainNew brain)
    {
        RoomWaypoint thisMachineWaypoint = ResolveMachineWaypoint();
        if (thisMachineWaypoint == null)
            return false;

        RoomWaypoint taskWaypoint = task != null ? task.Payload as RoomWaypoint : null;
        if (ReferenceEquals(taskWaypoint, thisMachineWaypoint))
            return true;

        RoomWaypoint bodyTarget = brain != null && brain.Body != null ? brain.Body.CurrentTarget : null;
        if (ReferenceEquals(bodyTarget, thisMachineWaypoint))
            return true;

        RoomWaypoint lastVisited = brain != null && brain.Memory != null ? brain.Memory.LastVisitedPoint : null;
        return task != null
            && task.Type == RobotTaskType.GuardPost
            && ReferenceEquals(lastVisited, thisMachineWaypoint);
    }

    private RoomWaypoint ResolveMachineWaypoint()
    {
        if (machineWaypoint != null)
        {
            cachedMachineWaypointSource = "serialized";
            cachedMachineWaypointDistance = securityMachine != null ? Vector2.Distance(securityMachine.transform.position, machineWaypoint.WorldPos) : -1f;
            return machineWaypoint;
        }

        if (cachedMachineWaypoint != null)
            return cachedMachineWaypoint;

        if (securityMachine == null)
            return null;

        cachedMachineWaypoint = securityMachine.GetComponent<RoomWaypoint>() ?? securityMachine.GetComponentInParent<RoomWaypoint>();
        if (cachedMachineWaypoint != null)
        {
            cachedMachineWaypointSource = "component";
            cachedMachineWaypointDistance = Vector2.Distance(securityMachine.transform.position, cachedMachineWaypoint.WorldPos);
            return cachedMachineWaypoint;
        }

        cachedMachineWaypoint = FindClosestSecurityWaypoint(out float bestDistance);
        cachedMachineWaypointSource = cachedMachineWaypoint != null ? "nearest_Security" : "none";
        cachedMachineWaypointDistance = cachedMachineWaypoint != null ? bestDistance : -1f;
        return cachedMachineWaypoint;
    }

    private RoomWaypoint FindClosestSecurityWaypoint(out float bestDistance)
    {
        bestDistance = float.MaxValue;
        RoomWaypoint best = null;
        if (securityMachine == null || securityMachine.WaypointService == null)
            return null;

        var waypoints = securityMachine.WaypointService.GetAllWaypoints();
        foreach (var waypoint in waypoints)
        {
            if (waypoint == null || waypoint.type != WaypointType.Security)
                continue;

            float distance = Vector2.Distance(securityMachine.transform.position, waypoint.WorldPos);
            if (distance < bestDistance)
            {
                best = waypoint;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void RecordTargetingDecision(RobotBrainNew brain, RobotTask currentTask, string outcome)
    {
        RoomWaypoint taskWaypoint = currentTask != null ? currentTask.Payload as RoomWaypoint : null;
        RoomWaypoint bodyTarget = brain != null && brain.Body != null ? brain.Body.CurrentTarget : null;
        RoomWaypoint memoryLastVisited = brain != null && brain.Memory != null ? brain.Memory.LastVisitedPoint : null;
        RoomWaypoint resolvedMachineWaypoint = ResolveMachineWaypoint();
        MachineType? desiredType = brain != null && brain.Memory != null
            ? brain.Memory.DesiredMachineType
            : null;

        string detail =
            "taskPayload=" + DescribeWaypoint(taskWaypoint)
            + " bodyTarget=" + DescribeWaypoint(bodyTarget)
            + " lastVisited=" + DescribeWaypoint(memoryLastVisited)
            + " desiredType=" + (desiredType.HasValue ? desiredType.Value.ToString() : "none")
            + " machineType=" + (securityMachine != null ? securityMachine.Type.ToString() : "none")
            + " machineWaypoint=" + DescribeWaypoint(resolvedMachineWaypoint)
            + " machineWaypointSource=" + cachedMachineWaypointSource
            + " machineWaypointDistance=" + cachedMachineWaypointDistance.ToString("F2")
            + " payloadMatchesMachine=" + WaypointsMatch(taskWaypoint, resolvedMachineWaypoint)
            + " bodyTargetMatchesMachine=" + WaypointsMatch(bodyTarget, resolvedMachineWaypoint)
            + " lastVisitedMatchesMachine=" + WaypointsMatch(memoryLastVisited, resolvedMachineWaypoint);

        RobotEcosystemProbe.RecordSlotDecisionDetail(
            this,
            "SecuritySlot",
            outcome,
            brain,
            securityMachine,
            currentTask,
            detail);
    }

    private static bool WaypointsMatch(RoomWaypoint left, RoomWaypoint right)
    {
        return left != null && right != null && ReferenceEquals(left, right);
    }

    private static string DescribeWaypoint(RoomWaypoint waypoint)
    {
        if (waypoint == null)
            return "none";

        return waypoint.type + "@" + waypoint.WorldPos.ToString("F2");
    }
}
