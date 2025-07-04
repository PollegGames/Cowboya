using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    [SerializeField] public int damage = 10;
    [SerializeField] private Vector2 pushForce = new Vector2(5f, 2f);

    public int DamageCost = 5;
    private bool isActive = false;
    private RobotBehaviour attacker;

    private void Awake()
    {
        attacker = GetComponentInParent<RobotBehaviour>();
    }

    public void Activate() => isActive = true;
    public void Deactivate() => isActive = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;

        RobotBehaviour target = other.GetComponentInParent<RobotBehaviour>();
        if (target == null || target == attacker)
            return; // Ne touche pas soi-même ou objet sans RobotBehaviour


        // Applique les dégâts
        target.Health.TakeDamage(damage);

        // Applique la poussée physique
        Rigidbody2D rb = other.attachedRigidbody;
        if (rb != null)
        {
            Vector2 direction = (rb.position - (Vector2)transform.position).normalized;
            Vector2 forceToApply = new Vector2(direction.x * pushForce.x, pushForce.y);
            rb.AddForce(forceToApply, ForceMode2D.Impulse);
        }

        isActive = false;
    }
}
