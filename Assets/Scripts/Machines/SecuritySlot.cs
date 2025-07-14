using UnityEngine;

public class SecuritySlot : MonoBehaviour
{
    [SerializeField] private BaseMachine securityMachine;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var guard = collision.GetComponentInParent<EnemyController>();
        if (guard == null) return;
        if (guard.EnemyStatus == EnemyStatus.Idle)
        {
            securityMachine.AttachRobot(guard.gameObject);
        }
    }
}
