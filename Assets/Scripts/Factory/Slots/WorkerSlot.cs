using System.Collections.Generic;
using UnityEngine;

public class WorkerSlot : MonoBehaviour
{
    [SerializeField] private BaseMachine machine;
    [SerializeField] private RoomWaypoint machineWaypoint;
    [SerializeField] private bool logSlotDecisions = false;
    [SerializeField] private bool logIgnoredTasks = false;

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

        if (heart == null || heart.Role != RobotRole.Worker)
        {
            RobotEcosystemProbe.RecordSlotDecision(this, "WorkerSlot", "ignored_role", brain, machine, heart != null ? heart.CurrentTask : null);
            if (logSlotDecisions)
                Debug.Log($"[WorkerSlot] Ignored {brain.name} role={heart?.Role}", this);
            return;
        }

        bool incomingTargetsThisSlot = IsIncomingWorkerTargetingThisSlot(brain);
        if (!incomingTargetsThisSlot)
        {
            RobotEcosystemProbe.RecordSlotDecision(this, "WorkerSlot", "rejected_task", brain, machine, heart.CurrentTask);
            if (logSlotDecisions && logIgnoredTasks)
                Debug.Log($"[WorkerSlot] Ignored {brain.name} task={(heart.CurrentTask != null ? heart.CurrentTask.Type.ToString() : "None")}", this);
            return;
        }

        if (machine == null)
            return;

        if (!machine.IsOn)
        {
            HandlePoweredOffMachineArrival(brain, heart.CurrentTask);
            return;
        }

        RobotEcosystemProbe.RecordSlotDecision(this, "WorkerSlot", "attach_requested", brain, machine, heart.CurrentTask);

        if (TryAttachOrReplace(brain))
            return;

        if (logSlotDecisions)
            Debug.Log($"[WorkerSlot] Attach rejected {brain.name} machine={machine.name}", this);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        var brain = collision.GetComponentInParent<RobotBrainNew>();
        if (brain == null)
            return;

        if (!TryTrackExit(brain))
            return;

        if (machine == null)
            return;

        RobotEcosystemProbe.RecordSlotDecision(
            this,
            "WorkerSlot",
            "exit_tracked",
            brain,
            machine,
            brain.Heart != null ? brain.Heart.CurrentTask : null);
    }

    private bool TryAttachOrReplace(RobotBrainNew incoming)
    {
        if (machine is FactoryMachine factoryMachine)
        {
            if (factoryMachine.CurrentWorker == null)
                return factoryMachine.TryAttachWorker(incoming, "enter_attach");
            if (ReferenceEquals(factoryMachine.CurrentWorker, incoming))
                return true;
            return factoryMachine.TryReplaceWorker(incoming, "enter_replace");
        }

        if (machine is RestingMachine restingMachine)
        {
            if (restingMachine.CurrentWorker == null)
                return restingMachine.TryAttachWorker(incoming, "enter_attach");
            if (ReferenceEquals(restingMachine.CurrentWorker, incoming))
                return true;
            return restingMachine.TryReplaceWorker(incoming, "enter_replace");
        }

        return machine.TryAttachWorker(incoming, "enter_attach");
    }

    private void HandlePoweredOffMachineArrival(RobotBrainNew brain, RobotTask currentTask)
    {
        RecordTargetingDecision(brain, currentTask, "rejected_machine_off_replan");

        if (brain != null && brain.Memory != null)
        {
            RoomWaypoint machinePoint = ResolveMachineWaypoint();
            if (machinePoint != null)
                brain.Memory.SetRoomWaypointAvailability(machinePoint, false);
            else
                brain.Memory.SetMachineWaypointAvailability(machine, false);
            brain.Memory.ChangeConnectionToMachine(false);
            brain.Memory.SetDesiredMachineType(machine.Type);
            RobotEcosystemProbe.RecordBrainCall(
                brain,
                "WorkerSlot.HandlePoweredOffMachineArrival",
                "machine=" + machine.name
                + " localObservation=True"
                + " memoryUpdated=True"
                + " taskBlocked=True"
                + " currentTask=" + (currentTask != null ? currentTask.Type.ToString() : "None"));
        }

        brain?.Heart?.BlockCurrentTask();
    }

    private bool TryTrackEnter(RobotBrainNew brain, RobotHeartNew heart)
    {
        int id = brain.GetInstanceID();
        if (colliderPresenceCount.TryGetValue(id, out int count))
        {
            colliderPresenceCount[id] = count + 1;
            RobotEcosystemProbe.RecordSlotDecision(this, "WorkerSlot", "attach_ignored_duplicate", brain, machine, heart != null ? heart.CurrentTask : null);
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

    private bool IsIncomingWorkerTargetingThisSlot(RobotBrainNew brain)
    {
        if (brain == null || machine == null || brain.Heart == null)
            return false;

        RobotTask currentTask = brain.Heart.CurrentTask;
        if (currentTask == null)
            return false;

        if (currentTask.Type == RobotTaskType.WorkAtMachine)
        {
            if (IsWorkAtMachineTaskTargetingThisMachine(currentTask, brain))
            {
                RecordTargetingDecision(brain, currentTask, "accepted_work_at_machine_exact");
                return true;
            }

            RecordTargetingDecision(brain, currentTask, "rejected_work_at_machine_not_this_slot");
            return false;
        }

        if (currentTask.Type != RobotTaskType.GoToMachine)
            return false;

        if (IsGoToMachineTaskTargetingThisMachine(currentTask, brain))
        {
            RecordTargetingDecision(brain, currentTask, "accepted_exact_go_to_machine");
            return true;
        }

        // Fallback when destination payload is temporarily missing: keep legacy type match.
        bool wouldAcceptByDesiredType = brain.Memory != null
            && brain.Memory.DesiredMachineType.HasValue
            && brain.Memory.DesiredMachineType.Value == machine.Type;

        if (wouldAcceptByDesiredType)
            RecordTargetingDecision(brain, currentTask, "rejected_desired_type_fallback_removed");

        return false;
    }

    private bool IsGoToMachineTaskTargetingThisMachine(RobotTask task, RobotBrainNew brain)
    {
        if (task == null || machine == null)
            return false;

        RoomWaypoint targetWaypoint = task.Payload as RoomWaypoint;
        if (targetWaypoint == null && brain != null && brain.Body != null)
            targetWaypoint = brain.Body.CurrentTarget;
        if (targetWaypoint == null)
            return false;

        RoomWaypoint thisMachineWaypoint = ResolveMachineWaypoint();
        return thisMachineWaypoint != null && ReferenceEquals(targetWaypoint, thisMachineWaypoint);
    }

    private bool IsWorkAtMachineTaskTargetingThisMachine(RobotTask task, RobotBrainNew brain)
    {
        if (task == null || machine == null)
            return false;

        RoomWaypoint thisMachineWaypoint = ResolveMachineWaypoint();
        if (thisMachineWaypoint == null)
            return false;

        RoomWaypoint taskWaypoint = task.Payload as RoomWaypoint;
        if (ReferenceEquals(taskWaypoint, thisMachineWaypoint))
            return true;

        RoomWaypoint lastVisited = brain != null && brain.Memory != null ? brain.Memory.LastVisitedPoint : null;
        return ReferenceEquals(lastVisited, thisMachineWaypoint);
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
            + " machineType=" + (machine != null ? machine.Type.ToString() : "none")
            + " machineWaypoint=" + DescribeWaypoint(resolvedMachineWaypoint)
            + " machineWaypointSource=" + cachedMachineWaypointSource
            + " machineWaypointDistance=" + cachedMachineWaypointDistance.ToString("F2")
            + " payloadMatchesMachine=" + WaypointsMatch(taskWaypoint, resolvedMachineWaypoint)
            + " bodyTargetMatchesMachine=" + WaypointsMatch(bodyTarget, resolvedMachineWaypoint)
            + " lastVisitedMatchesMachine=" + WaypointsMatch(memoryLastVisited, resolvedMachineWaypoint);

        RobotEcosystemProbe.RecordSlotDecisionDetail(
            this,
            "WorkerSlot",
            outcome,
            brain,
            machine,
            currentTask,
            detail);
    }

    private RoomWaypoint ResolveMachineWaypoint()
    {
        if (machineWaypoint != null)
        {
            cachedMachineWaypointSource = "serialized";
            cachedMachineWaypointDistance = machine != null ? Vector2.Distance(machine.transform.position, machineWaypoint.WorldPos) : -1f;
            return machineWaypoint;
        }

        if (cachedMachineWaypoint != null)
            return cachedMachineWaypoint;

        if (machine == null)
            return null;

        cachedMachineWaypoint = machine.GetComponent<RoomWaypoint>() ?? machine.GetComponentInParent<RoomWaypoint>();
        if (cachedMachineWaypoint != null)
        {
            cachedMachineWaypointSource = "component";
            cachedMachineWaypointDistance = Vector2.Distance(machine.transform.position, cachedMachineWaypoint.WorldPos);
            return cachedMachineWaypoint;
        }

        WaypointType? targetType = MachineWaypointResolver.MapMachineTypeToWaypointType(machine.Type);
        if (!targetType.HasValue)
            return null;

        RoomWaypoint best = FindClosestMachineWaypoint(targetType.Value, out float bestDistance);

        cachedMachineWaypoint = best;
        cachedMachineWaypointSource = best != null ? "nearest_" + targetType.Value : "none";
        cachedMachineWaypointDistance = best != null ? bestDistance : -1f;
        return cachedMachineWaypoint;
    }

    private RoomWaypoint FindClosestMachineWaypoint(WaypointType targetType, out float bestDistance)
    {
        bestDistance = float.MaxValue;
        RoomWaypoint best = null;
        if (machine == null || machine.WaypointService == null)
            return null;

        var waypoints = machine.WaypointService.GetAllWaypoints();
        foreach (var waypoint in waypoints)
        {
            if (waypoint == null || waypoint.type != targetType)
                continue;

            float distance = Vector2.Distance(machine.transform.position, waypoint.WorldPos);
            if (distance < bestDistance)
            {
                best = waypoint;
                bestDistance = distance;
            }
        }

        return best;
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
