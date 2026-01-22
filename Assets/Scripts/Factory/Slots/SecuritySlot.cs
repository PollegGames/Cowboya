using UnityEngine;

public class SecuritySlot : MonoBehaviour
{
    [SerializeField] private BaseMachine securityMachine;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var brain = collision.GetComponentInParent<RobotBrain>();
        if (brain == null) return;
        securityMachine.AttachRobot(brain.gameObject);
    }
}
