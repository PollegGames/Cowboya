using UnityEngine;

/// <summary>
/// Creates security badge pickups and attaches them to a specified parent with
/// target joint physics.
/// </summary>
public class SecurityBadgeSpawner : MonoBehaviour
{
    [SerializeField] private SecurityBadgePickup badgePrefab;

    /// <summary>
    /// Spawns a security badge and attaches it to the provided parent transform.
    /// </summary>
    public SecurityBadgePickup SpawnBadge(Transform parent)
    {
        return SpawnBadge(parent, Vector3.zero);
    }

    /// <summary>
    /// Spawns a security badge and attaches it at a local-space offset from its parent.
    /// </summary>
    public SecurityBadgePickup SpawnBadge(Transform parent, Vector3 localOffset)
    {
        if (badgePrefab == null)
        {
            Debug.LogWarning("SecurityBadgeSpawner: badgePrefab is null!");
            return null;
        }

        // Instantiate in world space first so the robot body's scale does not
        // enlarge or shrink the badge when it becomes a child.
        var badge = Instantiate(
            badgePrefab,
            parent.position,
            parent.rotation
        );
        badge.transform.SetParent(parent, worldPositionStays: true);

        // 2) Ensure the badge has a Rigidbody2D
        var badgeRb = badge.GetComponent<Rigidbody2D>();
        if (badgeRb == null)
        {
            Debug.LogError("SecurityBadgeSpawner: badgePrefab needs a Rigidbody2D!");
            Destroy(badge.gameObject);
            return null;
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
        badge.SetFollowTarget(parent, localOffset);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var joint = badge.GetComponent<TargetJoint2D>();
        Debug.Log(
            $"[SecurityBadgeSpawner] Spawned badge '{badge.name}' parent='{parent.name}' " +
            $"local={badge.transform.localPosition} world={badge.transform.position} " +
            $"bodyType={badgeRb.bodyType} jointTarget={(joint != null ? joint.target.ToString() : "none")}",
            badge);
#endif

        return badge;
    }
}
