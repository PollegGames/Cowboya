using UnityEngine;

/// <summary>
/// Bridges punch animation events to <see cref="AttackHitbox"/> instances.
/// </summary>
[DisallowMultipleComponent]
public sealed class PunchHitboxEventRelay : MonoBehaviour
{
    [Header("Hitboxes")]
    [SerializeField] private AttackHitbox leftArmHitbox;
    [SerializeField] private AttackHitbox rightArmHitbox;
    [SerializeField] private AttackHitbox upHitbox;
    [SerializeField] private AttackHitbox downHitbox;

    [Header("Dependencies")]
    [SerializeField] private RobotStateController robotStateController;
    [SerializeField] private PlayerPunchAnimator punchAnimator;

    private void Awake()
    {
        if (robotStateController == null)
        {
            robotStateController = GetComponentInParent<RobotStateController>();
        }

        if (punchAnimator == null)
        {
            punchAnimator = GetComponent<PlayerPunchAnimator>();
            if (punchAnimator == null)
            {
                punchAnimator = GetComponentInChildren<PlayerPunchAnimator>();
            }
        }
    }

    /// <summary>
    /// Activates the hitbox associated with the currently playing punch.
    /// </summary>
    public void ActivatePrimaryHitbox()
    {
        ActivateHitbox(ResolveHitboxForCurrentSector());
    }

    /// <summary>
    /// Deactivates the hitbox associated with the currently playing punch.
    /// </summary>
    public void DeactivatePrimaryHitbox()
    {
        DeactivateHitbox(ResolveHitboxForCurrentSector());
    }

    /// <summary>
    /// Activates the configured left arm hitbox.
    /// </summary>
    public void ActivateLeftHitbox()
    {
        ActivateHitbox(leftArmHitbox);
    }

    /// <summary>
    /// Deactivates the configured left arm hitbox.
    /// </summary>
    public void DeactivateLeftHitbox()
    {
        DeactivateHitbox(leftArmHitbox);
    }

    /// <summary>
    /// Activates the configured right arm hitbox.
    /// </summary>
    public void ActivateRightHitbox()
    {
        ActivateHitbox(rightArmHitbox);
    }

    /// <summary>
    /// Deactivates the configured right arm hitbox.
    /// </summary>
    public void DeactivateRightHitbox()
    {
        DeactivateHitbox(rightArmHitbox);
    }

    /// <summary>
    /// Activates the configured upward punch hitbox.
    /// </summary>
    public void ActivateUpHitbox()
    {
        ActivateHitbox(upHitbox);
    }

    /// <summary>
    /// Deactivates the configured upward punch hitbox.
    /// </summary>
    public void DeactivateUpHitbox()
    {
        DeactivateHitbox(upHitbox);
    }

    /// <summary>
    /// Activates the configured downward punch hitbox.
    /// </summary>
    public void ActivateDownHitbox()
    {
        ActivateHitbox(downHitbox);
    }

    /// <summary>
    /// Deactivates the configured downward punch hitbox.
    /// </summary>
    public void DeactivateDownHitbox()
    {
        DeactivateHitbox(downHitbox);
    }

    private void ActivateHitbox(AttackHitbox hitbox)
    {
        if (hitbox == null)
        {
            return;
        }

        if (robotStateController != null)
        {
            if (!robotStateController.PerformAttackByEnergy(hitbox.DamageCost))
            {
                punchAnimator?.AbortActivePunch();
                DeactivateAllHitboxes();
                return;
            }
        }

        hitbox.Activate();
    }

    private void DeactivateHitbox(AttackHitbox hitbox)
    {
        hitbox?.Deactivate();
    }

    private AttackHitbox ResolveHitboxForCurrentSector()
    {
        AttackSector sector = punchAnimator != null ? punchAnimator.CurrentAttackSector : AttackSector.Right;
        bool facingRight = punchAnimator != null
            ? punchAnimator.IsCurrentlyFacingRight
            : (robotStateController != null
                ? robotStateController.transform.localScale.x >= 0f
                : transform.lossyScale.x >= 0f);

        switch (sector)
        {
            case AttackSector.Up:
                return upHitbox != null ? upHitbox : SelectHorizontalHitbox(facingRight);
            case AttackSector.Down:
                return downHitbox != null ? downHitbox : SelectHorizontalHitbox(facingRight);
            case AttackSector.Left:
                return facingRight ? leftArmHitbox : rightArmHitbox;
            case AttackSector.Right:
            default:
                return facingRight ? rightArmHitbox : leftArmHitbox;
        }
    }

    private AttackHitbox SelectHorizontalHitbox(bool facingRight)
    {
        return facingRight ? (rightArmHitbox != null ? rightArmHitbox : leftArmHitbox) : (leftArmHitbox != null ? leftArmHitbox : rightArmHitbox);
    }

    private void DeactivateAllHitboxes()
    {
        leftArmHitbox?.Deactivate();
        rightArmHitbox?.Deactivate();
        upHitbox?.Deactivate();
        downHitbox?.Deactivate();
    }

    private void OnDisable()
    {
        DeactivateAllHitboxes();
    }
}
