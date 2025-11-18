using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    [SerializeField] public int damage = 10;
    [SerializeField] private Vector2 pushForce = new Vector2(5f, 2f);
    [SerializeField] private SpriteRenderer hitboxIndicator;
    [SerializeField] private Color activeColor = Color.red;
    [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0f);

    public int DamageCost = 5;
    private bool isActive = false;
    private RobotStateController attacker;

    private void Awake()
    {
        attacker = GetComponentInParent<RobotStateController>();
        if (hitboxIndicator == null)
            hitboxIndicator = GetComponent<SpriteRenderer>();
        if (hitboxIndicator == null)
            hitboxIndicator = GetComponentInChildren<SpriteRenderer>(true);
        UpdateIndicator(false);
    }

    public void Activate()
    {
        isActive = true;
        UpdateIndicator(true);
    }

    public void Deactivate()
    {
        isActive = false;
        UpdateIndicator(false);
    }

    /// <summary>
    /// Indicates whether the hitbox is currently active.
    /// </summary>
    public bool IsActive => isActive;

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
        UpdateIndicator(false);
    }

    private void UpdateIndicator(bool active)
    {
        if (hitboxIndicator != null)
            hitboxIndicator.color = active ? activeColor : inactiveColor;
    }
}
