using UnityEngine;

public class WorkerSlot : MonoBehaviour
{
    [SerializeField] private BaseMachine machine;
    [SerializeField] private bool logSlotDecisions = false;
    [SerializeField] private bool logIgnoredTasks = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var brain = collision.GetComponentInParent<RobotBrain>();
        if (brain == null) return;
        var heart = brain.Heart;
        if (heart == null || heart.Role != RobotRole.Worker)
        {
            if (logSlotDecisions)
                Debug.Log($"[WorkerSlot] Ignored {brain.name} role={heart?.Role}", this);
            return;
        }
        if (!CanAttachWorker(brain))
            return;
        machine.AttachRobot(brain.gameObject);
    }

    private bool CanAttachWorker(RobotBrain brain)
    {
        if (machine is FactoryMachine factoryMachine && !factoryMachine.CanAcceptWorker(brain))
            return false;

        if (!brain.CanUseMachineSlot(machine, RobotTaskType.WorkAtMachine))
        {
            var heart = brain.Heart;
            if (logSlotDecisions && logIgnoredTasks)
                Debug.Log($"[WorkerSlot] Ignored {brain.name} task={(heart?.CurrentTask != null ? heart.CurrentTask.Type.ToString() : "None")}", this);
            return false;
        }

        return true;
    }
}
