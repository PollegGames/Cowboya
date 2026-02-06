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
        securityMachine.AttachRobot(brain.gameObject);
    }
}
