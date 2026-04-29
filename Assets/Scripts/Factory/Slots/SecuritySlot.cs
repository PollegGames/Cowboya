using UnityEngine;

public class SecuritySlot : MonoBehaviour
{
    [SerializeField] private BaseMachine securityMachine;
    [SerializeField] private bool logSlotDecisions = true;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var brain = collision.GetComponentInParent<RobotBrainNew>();
        if (brain == null) return;
        var heart = brain.Heart;
        if (heart == null || heart.Role != RobotRole.SecurityGuard)
        {
            RobotEcosystemProbe.RecordSlotDecision(this, "SecuritySlot", "ignored_role", brain, securityMachine, heart != null ? heart.CurrentTask : null);
            if (logSlotDecisions)
                Debug.Log($"[SecuritySlot] Ignored {brain.name} role={heart?.Role}", this);
            return;
        }
        if (heart.CurrentTask != null && heart.CurrentTask.Type == RobotTaskType.ReactivateMachine)
        {
            var targetMachine = heart.CurrentTask.Payload as BaseMachine;
            if (targetMachine != null && securityMachine != null && !ReferenceEquals(targetMachine, securityMachine))
            {
                RobotEcosystemProbe.RecordSlotDecision(this, "SecuritySlot", "rejected_target_mismatch", brain, securityMachine, heart.CurrentTask);
                if (logSlotDecisions)
                    Debug.Log($"[SecuritySlot] Ignored {brain.name} reactivating {targetMachine.name}", this);
                return;
            }
        }
        securityMachine.AttachRobot(brain.gameObject);
        RobotEcosystemProbe.RecordSlotDecision(this, "SecuritySlot", "attached", brain, securityMachine, heart.CurrentTask);
    }
}

