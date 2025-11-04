using System;
using UnityEngine;

/// <summary>
/// Handles <see cref="AttackRequest"/> objects for a robot by triggering the punch
/// animation, toggling hitboxes and consuming energy as needed.
/// </summary>
[DisallowMultipleComponent]
public sealed class AttackRequestController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RobotStateController robotStateController;
    [SerializeField] private PlayerPunchAnimator punchAnimator;
    [SerializeField] private PunchHitboxEventRelay hitboxRelay;
    [SerializeField] private Animator animator;

    [Header("Animator Parameters")]
    [SerializeField] private string punchDirectionParameter = "PunchDirection";
    [SerializeField] private string punchTriggerParameter = "PunchTrigger";
    [SerializeField] private int idleDirectionValue = -1;

    private int punchDirectionHash;
    private int punchTriggerHash;
    private bool hashesInitialized;
    private bool attackInProgress;

    /// <summary>
    /// Raised whenever an active punch is aborted or cancelled.
    /// </summary>
    public event Action AttackAborted;

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

        if (punchAnimator != null)
        {
            punchAnimator.PunchCompleted += NotifyPunchCompleted;
        }

        if (hitboxRelay == null)
        {
            hitboxRelay = GetComponent<PunchHitboxEventRelay>();
            if (hitboxRelay == null)
            {
                hitboxRelay = GetComponentInChildren<PunchHitboxEventRelay>();
            }
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        if (animator != null)
        {
            punchDirectionHash = Animator.StringToHash(punchDirectionParameter);
            punchTriggerHash = Animator.StringToHash(punchTriggerParameter);
            hashesInitialized = true;
        }
    }

    private void OnDestroy()
    {
        if (punchAnimator != null)
        {
            punchAnimator.PunchCompleted -= NotifyPunchCompleted;
        }
    }

    private void OnDisable()
    {
        attackInProgress = false;
        hitboxRelay?.ForceDeactivateAllHitboxes();
        AttackAborted?.Invoke();
    }

    /// <summary>
    /// Attempts to execute the provided <paramref name="request"/> on the robot.
    /// </summary>
    /// <param name="request">Details of the requested attack.</param>
    /// <returns>True when the request was accepted.</returns>
    public bool TryHandleAttack(AttackRequest request)
    {
        if (attackInProgress)
        {
            return false;
        }

        if (!CanTriggerAnimation())
        {
            return false;
        }

        if (!ConsumeEnergy(request))
        {
            hitboxRelay?.ForceDeactivateAllHitboxes();
            AbortActiveAttack();
            return false;
        }

        TriggerAnimation(request);
        attackInProgress = true;
        return true;
    }

    /// <summary>
    /// Executes the provided <paramref name="request"/> disregarding the return
    /// value from <see cref="TryHandleAttack"/> to match <see cref="Attack"/>
    /// delegates.
    /// </summary>
    /// <param name="request">Details of the requested attack.</param>
    public void HandleAttackRequest(AttackRequest request)
    {
        TryHandleAttack(request);
    }

    /// <summary>
    /// Stops any active punch and resets animator state.
    /// </summary>
    public void AbortActiveAttack()
    {
        if (punchAnimator != null)
        {
            punchAnimator.AbortActivePunch();
        }
        else if (animator != null && hashesInitialized)
        {
            animator.ResetTrigger(punchTriggerHash);
            animator.SetInteger(punchDirectionHash, idleDirectionValue);
        }

        hitboxRelay?.ForceDeactivateAllHitboxes();
        attackInProgress = false;
        AttackAborted?.Invoke();
    }

    /// <summary>
    /// Clears the active attack flag when the punch animation completes.
    /// </summary>
    public void NotifyPunchCompleted()
    {
        attackInProgress = false;
    }

    private bool ConsumeEnergy(AttackRequest request)
    {
        if (robotStateController == null)
        {
            return true;
        }

        float requiredEnergy = request.EnergyRequired;
        if (requiredEnergy <= 0f && robotStateController.Stats != null)
        {
            requiredEnergy = robotStateController.Stats.AttackEnergyCost;
        }

        if (requiredEnergy <= 0f)
        {
            return true;
        }

        return robotStateController.PerformAttackByEnergy(requiredEnergy);
    }

    private bool CanTriggerAnimation()
    {
        if (punchAnimator != null)
        {
            return true;
        }

        return animator != null && hashesInitialized;
    }

    private void TriggerAnimation(AttackRequest request)
    {
        if (punchAnimator != null)
        {
            punchAnimator.HandleAttackRequest(request);
            return;
        }

        if (animator == null || !hashesInitialized)
        {
            return;
        }

        int directionValue = ResolveDirection(request.Sector);
        animator.SetInteger(punchDirectionHash, directionValue);
        animator.ResetTrigger(punchTriggerHash);
        animator.SetTrigger(punchTriggerHash);
    }

    private int ResolveDirection(AttackSector sector)
    {
        switch (sector)
        {
            case AttackSector.Up:
                return 1;
            case AttackSector.Down:
                return 2;
            case AttackSector.Left:
                return 3;
            case AttackSector.Right:
            default:
                return 0;
        }
    }
}
