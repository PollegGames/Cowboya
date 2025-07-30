using UnityEngine;

/// <summary>
/// Creates security badge pickups and attaches them to a specified parent with
/// target joint physics.
/// </summary>
public class SecurityBadgeSpawner : MonoBehaviour
{
    [SerializeField] private SecurityBadgePickup badgePrefab;

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

        // 2) Ensure the badge has a Rigidbody2D
        var badgeRb = badge.GetComponent<Rigidbody2D>();
        if (badgeRb == null)
        {
            Debug.LogError("SecurityBadgeSpawner: badgePrefab needs a Rigidbody2D!");
            return badge;
        }

        // // 3) Ensure the badge has a TargetJoint2D. If the prefab already
        // // includes one (likely via RequireComponent on SecurityBadgePickup)
        // // reuse it instead of adding a duplicate.
        // var joint = badge.GetComponent<TargetJoint2D>();
        // if (joint == null)
        //     joint = badge.gameObject.AddComponent<TargetJoint2D>();
        // joint.autoConfigureTarget = false;
        // joint.target = parent.position;
        // joint.frequency = frequency;          // spring strength
        // joint.dampingRatio = dampingRatio;    // damping
        // joint.maxForce = maxForce;

        // Make the badge follow the parent transform
        badge.SetFollowTarget(parent);

        return badge;
    }
}
