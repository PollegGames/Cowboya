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
        bool incomingTargetsThisSlot = IsIncomingWorkerTargetingThisSlot(brain);
        if (!TryTrackEnter(brain, heart))
            return;

        if (heart == null || heart.Role != RobotRole.Worker)
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

    private bool TryAttachOrReplace(RobotBrainNew incoming)
    {
        if (machine is RestingMachine restingMachine)
        {
            if (restingMachine.CurrentWorker == null)
                return restingMachine.TryAttachWorker(incoming, "enter_attach");
            if (ReferenceEquals(restingMachine.CurrentWorker, incoming))
                return true;
            return restingMachine.TryReplaceWorker(incoming, "enter_replace");
        }

        if (machine is FactoryMachine factoryMachine)
        {
            if (factoryMachine.CurrentWorker == null)
                return factoryMachine.TryAttachWorker(incoming, "enter_attach");
            if (ReferenceEquals(factoryMachine.CurrentWorker, incoming))
                return true;
            return factoryMachine.TryReplaceWorker(incoming, "enter_replace");
        }

        return machine.TryAttachWorker(incoming, "enter_attach");
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

    private bool IsIncomingWorkerTargetingThisSlot(RobotBrainNew brain)
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

        return brain.Memory.DesiredMachineType.HasValue
            && brain.Memory.DesiredMachineType.Value == machine.Type;
    }
}
