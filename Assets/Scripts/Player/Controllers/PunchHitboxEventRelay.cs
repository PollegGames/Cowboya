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
    [SerializeField] private AttackRequestController attackRequestController;

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

        if (attackRequestController == null)
        {
            attackRequestController = GetComponent<AttackRequestController>();
            if (attackRequestController == null)
            {
                attackRequestController = GetComponentInParent<AttackRequestController>();
            }
        }
    }

    private void OnEnable()
    {
        if (punchAnimator == null)
        {
            punchAnimator = GetComponent<PlayerPunchAnimator>();
            if (punchAnimator == null)
            {
                punchAnimator = GetComponentInChildren<PlayerPunchAnimator>();
            }
        }

        if (punchAnimator != null)
        {
            punchAnimator.PunchCompleted += HandlePunchCompleted;
        }

        if (attackRequestController == null)
        {
            attackRequestController = GetComponent<AttackRequestController>();
            if (attackRequestController == null)
            {
                attackRequestController = GetComponentInParent<AttackRequestController>();
            }
        }

        if (attackRequestController != null)
        {
            attackRequestController.AttackAborted += HandleAttackAborted;
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
    /// Activates the hitbox that matches the provided <paramref name="request"/>.
    /// </summary>
    /// <param name="request">Details about the attack that should arm a hitbox.</param>
    public void ActivateHitboxForRequest(AttackRequest request)
    {
        ActivateHitbox(ResolveHitboxForSector(request.Sector, ResolveFacingDirection()));
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

        hitbox.Activate();
    }

    private void DeactivateHitbox(AttackHitbox hitbox)
    {
        hitbox?.Deactivate();
    }

    private AttackHitbox ResolveHitboxForCurrentSector()
    {
        AttackSector sector = punchAnimator != null ? punchAnimator.CurrentAttackSector : AttackSector.Right;
        return ResolveHitboxForSector(sector, ResolveFacingDirection());
    }

    private AttackHitbox ResolveHitboxForSector(AttackSector sector, bool facingRight)
    {
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

    private bool ResolveFacingDirection()
    {
        if (punchAnimator != null)
        {
            return punchAnimator.IsCurrentlyFacingRight;
        }

        if (robotStateController != null)
        {
            return robotStateController.transform.localScale.x >= 0f;
        }

        return transform.lossyScale.x >= 0f;
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

    /// <summary>
    /// Ensures that no punch hitboxes remain active.
    /// </summary>
    public void ForceDeactivateAllHitboxes()
    {
        LogActiveHitboxes();
        DeactivateAllHitboxes();
    }

    private void OnDisable()
    {
        if (punchAnimator != null)
        {
            punchAnimator.PunchCompleted -= HandlePunchCompleted;
        }

        if (attackRequestController != null)
        {
            attackRequestController.AttackAborted -= HandleAttackAborted;
        }

        DeactivateAllHitboxes();
    }

    private void HandleAttackAborted()
    {
        ForceDeactivateAllHitboxes();
    }

    private void HandlePunchCompleted()
    {
        ForceDeactivateAllHitboxes();
    }

    private void LogActiveHitboxes()
    {
        LogHitboxIfActive(leftArmHitbox);
        LogHitboxIfActive(rightArmHitbox);
        LogHitboxIfActive(upHitbox);
        LogHitboxIfActive(downHitbox);
    }

    private void LogHitboxIfActive(AttackHitbox hitbox)
    {
        if (hitbox != null && hitbox.IsActive)
        {
            Debug.LogWarning($"[PunchHitboxEventRelay] Hitbox '{hitbox.name}' remained active during forced deactivation.");
        }
    }
}
