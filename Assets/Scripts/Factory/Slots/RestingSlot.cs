using UnityEngine;

public class RestingSlot : MonoBehaviour
{
    [SerializeField] private BaseMachine machine;
    [SerializeField] private bool logSlotDecisions = true;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var brain = collision.GetComponentInParent<RobotBrain>();
        if (brain == null) return;
        var heart = brain.Heart;
        if (heart == null || heart.Role != RobotRole.Worker)
        {
            if (logSlotDecisions)
                Debug.Log($"[RestingSlot] Ignored {brain.name} role={heart?.Role}", this);
            return;
        }

        if (!CanAttachWorker(brain))
            return;

        machine.AttachRobot(brain.gameObject);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        var brain = collision.GetComponentInParent<RobotBrain>();
        if (brain == null) return;

        if (machine is RestingMachine restingMachine && restingMachine.CurrentWorker == brain)
        {
            if (logSlotDecisions)
                Debug.Log($"[RestingSlot] Release {brain.name} on exit", this);
            restingMachine.ReleaseWorker(brain);
        }
    }

    private bool CanAttachWorker(RobotBrain brain)
    {
        if (machine is RestingMachine restingMachine && !restingMachine.CanAcceptWorker(brain))
        {
            if (logSlotDecisions)
                Debug.Log($"[RestingSlot] Waiting {brain.name} (machine unavailable/occupied)", this);
            return false;
        }

        if (!brain.CanUseMachineSlot(machine, RobotTaskType.Rest))
        {
            var heart = brain.Heart;
            if (logSlotDecisions)
                Debug.Log($"[RestingSlot] Ignored {brain.name} task={(heart?.CurrentTask != null ? heart.CurrentTask.Type.ToString() : "None")}", this);
            return false;
        }

        return true;
    }
}
