using UnityEngine;

public class SecuritySlot : MonoBehaviour
{
    [SerializeField] private BaseMachine securityMachine;
    [SerializeField] private bool logSlotDecisions = true;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var brain = collision.GetComponentInParent<RobotBrain>();
        if (brain == null) return;
        var heart = brain.Heart;
        if (heart == null || heart.Role != RobotRole.SecurityGuard)
        {
            if (logSlotDecisions)
                Debug.Log($"[SecuritySlot] Ignored {brain.name} role={heart?.Role}", this);
            return;
        }
        if (heart.CurrentTask != null && heart.CurrentTask.Type == RobotTaskType.ReactivateMachine)
        {
            var targetMachine = heart.CurrentTask.Payload as BaseMachine;
            if (targetMachine != null && securityMachine != null && !ReferenceEquals(targetMachine, securityMachine))
            {
                if (logSlotDecisions)
                    Debug.Log($"[SecuritySlot] Ignored {brain.name} reactivating {targetMachine.name}", this);
                return;
            }
        }
        securityMachine.AttachRobot(brain.gameObject);
    }
}
