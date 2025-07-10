using UnityEngine;

public class SecuritySlot : MonoBehaviour
{
    [SerializeField] private SecurityMachine securityMachine;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var guard = collision.GetComponentInParent<EnemyController>();
        if (guard == null) return;
        securityMachine.OnSecurityGuardReady(guard);
    }
}
