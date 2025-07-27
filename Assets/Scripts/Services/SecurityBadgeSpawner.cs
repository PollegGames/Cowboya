using UnityEngine;

public class SecurityBadgeSpawner : MonoBehaviour
{
    [SerializeField] private SecurityBadgePickup badgePrefab;

    [Header("Spring Joint Settings")]
    [Tooltip("How springy the joint is.")]
    [SerializeField] private float frequency = 5f;
    [Tooltip("How much the joint resists oscillation.")]
    [SerializeField] private float dampingRatio = 0.8f;
    [Tooltip("Maximum force the joint can apply.")]
    [SerializeField] private float maxForce = 1000f;

    public SecurityBadgePickup SpawnBadge(Transform parent)
    {
        if (badgePrefab == null)
        {
            Debug.LogWarning("SecurityBadgeSpawner: badgePrefab is null!");
            return null;
        }

        // 1) Instantiate badge at the parent's transform and parent it
        var badge = Instantiate(
            badgePrefab,
            parent.position,
            parent.rotation,
            parent
        );

        // 2) Ensure both badge and parent have Rigidbody2D
        var badgeRb = badge.GetComponent<Rigidbody2D>();
        if (badgeRb == null)
        {
            Debug.LogError("SecurityBadgeSpawner: badgePrefab needs a Rigidbody2D!");
            return badge;
        }

        var parentRb = parent.GetComponent<Rigidbody2D>();
        if (parentRb == null)
        {
            Debug.LogError($"SecurityBadgeSpawner: Parent '{parent.name}' has no Rigidbody2D.");
            return badge;
        }

        // 3) Add and configure a SpringJoint2D on the badge
        var spring = badge.gameObject.AddComponent<SpringJoint2D>();
        spring.connectedBody = parentRb;
        spring.autoConfigureDistance = false;
        spring.distance = 0f;                  // keep them exactly together
        spring.frequency = frequency;          // spring strength
        spring.dampingRatio = dampingRatio;    // damping
        spring.enableCollision = false;        // badge won't collide back into parent

        // Unfortunately SpringJoint2D has no maxForce setting,
        // so if you need that you could clamp badgeRb.velocity in Update()

        return badge;
    }
}
