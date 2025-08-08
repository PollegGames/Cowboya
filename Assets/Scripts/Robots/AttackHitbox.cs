using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    [SerializeField] public int damage = 10;
    [SerializeField] private Vector2 pushForce = new Vector2(5f, 2f);

    public int DamageCost = 5;
    private bool isActive = false;
    private RobotStateController attacker;

    private void Awake()
    {
        attacker = GetComponentInParent<RobotStateController>();
    }

    public void Activate() => isActive = true;
    public void Deactivate() => isActive = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;

        RobotStateController target = other.GetComponentInParent<RobotStateController>();
        if (target == null || target == attacker)
            return; // Ignore self or objects without RobotStateController


        // Apply damage
        target.Health.TakeDamage(damage);

        // Apply physical push
        Rigidbody2D rb = other.attachedRigidbody;
        if (rb != null)
        {
            // Use the direction from attacker to target (horizontal only if needed)
            Vector2 attackerPos = attacker.transform.position;
            Vector2 targetPos = rb.position;
            Vector2 direction = (targetPos - attackerPos).normalized;
            // Option: Only push horizontally (ignorer Y si tu veux)
            direction.y = 0f;

            Vector2 forceToApply = new Vector2(direction.x * pushForce.x, pushForce.y);
            rb.AddForce(forceToApply, ForceMode2D.Impulse);
        }

        isActive = false;
    }
}
