using System.Collections.Generic;
using UnityEngine;

public class RestingSlot : MonoBehaviour
{
    [SerializeField] private BaseMachine machine;
    [SerializeField] private bool logSlotDecisions = true;

    private readonly Dictionary<int, int> colliderPresenceCount = new Dictionary<int, int>();

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
        bool incomingTargetsThisSlot = IsIncomingRobotTargetingThisSlot(brain);
        if (!TryTrackEnter(brain, heart))
            return;

        if (!CanUseRestSlot(heart))
        {
            RobotEcosystemProbe.RecordSlotDecision(this, "RestingSlot", "ignored_role", brain, machine, heart != null ? heart.CurrentTask : null);
            if (logSlotDecisions)
                Debug.Log($"[RestingSlot] Ignored {brain.name} role={heart?.Role}", this);
            return;
        }

        if (!incomingTargetsThisSlot)
        {
            RobotEcosystemProbe.RecordSlotDecision(this, "RestingSlot", "rejected_task", brain, machine, heart.CurrentTask);
            if (logSlotDecisions)
                Debug.Log($"[RestingSlot] Ignored {brain.name} task={(heart.CurrentTask != null ? heart.CurrentTask.Type.ToString() : "None")}", this);
            return;
        }

        if (machine == null)
            return;

        if (!machine.IsOn)
        {
            HandlePoweredOffMachineArrival(brain, heart.CurrentTask);
            return;
        }

        RobotEcosystemProbe.RecordSlotDecision(this, "RestingSlot", "attach_requested", brain, machine, heart.CurrentTask);

        if (TryAttachOrReplace(brain))
            return;

        if (logSlotDecisions)
            Debug.Log($"[RestingSlot] Attach rejected {brain.name} machine={machine.name}", this);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        var brain = collision.GetComponentInParent<RobotBrainNew>();
        if (brain == null)
            return;

        if (!TryTrackExit(brain))
            return;

        RobotEcosystemProbe.RecordSlotDecision(this, "RestingSlot", "exit_tracked", brain, machine, brain.Heart != null ? brain.Heart.CurrentTask : null);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        var brain = collision.GetComponentInParent<RobotBrainNew>();
        if (brain == null || brain.Heart == null || !CanUseRestSlot(brain.Heart))
            return;
        if (machine == null || !machine.IsOn || machine.IsOccupied)
            return;
        if (!IsIncomingRobotTargetingThisSlot(brain))
            return;

        TryAttachOrReplace(brain);
    }

    private bool TryAttachOrReplace(RobotBrainNew incoming)
    {
        if (machine == null || incoming == null)
            return false;

        RobotBrainNew current = ResolveCurrentRobot();
        if (current == null)
        {
            bool attached = machine.TryAttachWorker(incoming, "enter_attach");
            RobotEcosystemProbe.RecordSlotDecision(
                this,
                "RestingSlot",
                attached ? "attached_empty" : "attach_rejected_empty",
                incoming,
                machine,
                incoming.Heart != null ? incoming.Heart.CurrentTask : null);
            return attached;
        }

        if (ReferenceEquals(current, incoming))
            return true;

        RobotEcosystemProbe.RecordSlotDecision(
            this,
            "RestingSlot",
            "queued_occupied",
            incoming,
            machine,
            incoming.Heart != null ? incoming.Heart.CurrentTask : null);
        return false;
    }

    private void HandlePoweredOffMachineArrival(RobotBrainNew brain, RobotTask currentTask)
    {
        RobotEcosystemProbe.RecordSlotDecision(this, "RestingSlot", "rejected_machine_off_replan", brain, machine, currentTask);

        if (brain != null && brain.Memory != null)
        {
            RoomWaypoint machinePoint = MachineWaypointResolver.Resolve(machine);
            if (machinePoint != null)
                brain.Memory.SetRoomWaypointAvailability(machinePoint, false);
            else
                brain.Memory.SetMachineWaypointAvailability(machine, false);
            brain.Memory.ChangeConnectionToMachine(false);
            brain.Memory.SetDesiredMachineType(machine.Type);
            RobotEcosystemProbe.RecordBrainCall(
                brain,
                "RestingSlot.HandlePoweredOffMachineArrival",
                "machine=" + machine.name
                + " localObservation=True"
                + " memoryUpdated=True"
                + " taskBlocked=False"
                + " currentTask=" + (currentTask != null ? currentTask.Type.ToString() : "None"));
        }
    }

    private bool TryTrackEnter(RobotBrainNew brain, RobotHeartNew heart)
    {
        int id = brain.GetInstanceID();
        if (colliderPresenceCount.TryGetValue(id, out int count))
        {
            colliderPresenceCount[id] = count + 1;
            RobotEcosystemProbe.RecordSlotDecision(this, "RestingSlot", "attach_ignored_duplicate", brain, machine, heart != null ? heart.CurrentTask : null);
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

    private bool IsIncomingRobotTargetingThisSlot(RobotBrainNew brain)
    {
        if (brain == null || machine == null || brain.Heart == null)
            return false;

        RobotTask currentTask = brain.Heart.CurrentTask;
        if (currentTask == null)
            return false;

        if (currentTask.Type == RobotTaskType.Rest)
            return true;

        if (currentTask.Type != RobotTaskType.GoToMachine || brain.Memory == null)
            return false;

        RoomWaypoint targetWaypoint = currentTask.Payload as RoomWaypoint;
        if (targetWaypoint == null && brain.Body != null)
            targetWaypoint = brain.Body.CurrentTarget;

        RoomWaypoint machinePoint = MachineWaypointResolver.Resolve(machine);
        return targetWaypoint != null
            && machinePoint != null
            && ReferenceEquals(targetWaypoint, machinePoint);
    }

    private static bool CanUseRestSlot(RobotHeartNew heart)
    {
        if (heart == null)
            return false;

        return heart.Role == RobotRole.Worker
            || heart.Role == RobotRole.SecurityGuard;
    }

    private RobotBrainNew ResolveCurrentRobot()
    {
        if (machine is RestingMachine restingMachine)
            return restingMachine.CurrentWorker;

        if (machine is FactoryMachine factoryMachine)
            return factoryMachine.CurrentWorker;

        return null;
    }
}
