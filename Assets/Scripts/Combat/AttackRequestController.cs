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
    [SerializeField] private Transform aimOrigin;

    [Header("Animator Parameters")]
    [SerializeField] private string punchDirectionParameter = "PunchDirection";
    [SerializeField] private string punchTriggerParameter = "PunchTrigger";
    [SerializeField] private string punchAimXParameter = "PunchAimX";
    [SerializeField] private string punchAimYParameter = "PunchAimY";
    [SerializeField] private int idleDirectionValue = -1;

    private int punchDirectionHash;
    private int punchTriggerHash;
    private int punchAimXHash;
    private int punchAimYHash;
    private bool hashesInitialized;
    private bool hasPunchAimXParameter;
    private bool hasPunchAimYParameter;
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

        if (aimOrigin == null)
        {
            aimOrigin = transform;
        }

        if (animator != null)
        {
            punchDirectionHash = Animator.StringToHash(punchDirectionParameter);
            punchTriggerHash = Animator.StringToHash(punchTriggerParameter);
            hasPunchAimXParameter = TryResolveFloatParameter(punchAimXParameter, out punchAimXHash);
            hasPunchAimYParameter = TryResolveFloatParameter(punchAimYParameter, out punchAimYHash);
            hashesInitialized = true;
            ResetAimParameters();
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
        AbortActiveAttack();
    }

    /// <summary>
    /// Attempts to execute the provided <paramref name="request"/> on the robot.
    /// </summary>
    /// <param name="request">Details of the requested attack.</param>
    /// <returns>True when the request was accepted.</returns>
    public bool TryHandleAttack(AttackRequest request)
    {
        if (ShouldBlockNewRequest())
        {
            return false;
        }

        if (!CanTriggerAnimation())
        {
            return false;
        }

        if (!ConsumeEnergy(request))
        {
            AbortActiveAttack();
            return false;
        }

        TriggerAnimation(request);
        if (ShouldTrackAttackProgress())
        {
            attackInProgress = true;
        }
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
            ResetAimParameters();
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

    private bool ShouldBlockNewRequest()
    {
        if (!ShouldTrackAttackProgress())
        {
            return false;
        }

        return attackInProgress;
    }

    private bool ShouldTrackAttackProgress()
    {
        return punchAnimator != null;
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

        Vector2 aimDirection = ResolveAimDirection(request);
        ApplyAimParameters(aimDirection);

        int directionValue = ResolveDirection(request.Sector);
        animator.SetInteger(punchDirectionHash, directionValue);
        animator.ResetTrigger(punchTriggerHash);
        animator.SetTrigger(punchTriggerHash);
    }

    private Vector2 ResolveAimDirection(AttackRequest request)
    {
        Vector2 origin = aimOrigin != null ? (Vector2)aimOrigin.position : (Vector2)transform.position;
        Vector2 delta = request.TargetPosition - origin;

        if (delta.sqrMagnitude > 0.0001f)
        {
            return delta.normalized;
        }

        return ResolveFallbackDirection(request.Sector);
    }

    private Vector2 ResolveFallbackDirection(AttackSector sector)
    {
        switch (sector)
        {
            case AttackSector.Up:
                return Vector2.up;
            case AttackSector.Down:
                return Vector2.down;
            case AttackSector.Left:
                return Vector2.left;
            case AttackSector.Right:
            default:
                return Vector2.right;
        }
    }

    private Vector2 ResolveDefaultAimDirection()
    {
        if (punchAnimator != null)
        {
            return punchAnimator.IsCurrentlyFacingRight ? Vector2.right : Vector2.left;
        }

        float scaleX = transform != null ? transform.lossyScale.x : 1f;
        if (Mathf.Approximately(scaleX, 0f))
        {
            return Vector2.right;
        }

        return scaleX >= 0f ? Vector2.right : Vector2.left;
    }

    private void ApplyAimParameters(Vector2 aimDirection)
    {
        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            aimDirection = ResolveDefaultAimDirection();
        }

        if (animator != null)
        {
            if (hasPunchAimXParameter)
            {
                animator.SetFloat(punchAimXHash, aimDirection.x);
            }

            if (hasPunchAimYParameter)
            {
                animator.SetFloat(punchAimYHash, aimDirection.y);
            }
        }
    }

    private void ResetAimParameters()
    {
        if (animator == null || !hashesInitialized)
        {
            return;
        }

        Vector2 resolvedDefault = ResolveDefaultAimDirection();

        if (hasPunchAimXParameter)
        {
            animator.SetFloat(punchAimXHash, resolvedDefault.x);
        }

        if (hasPunchAimYParameter)
        {
            animator.SetFloat(punchAimYHash, resolvedDefault.y);
        }

        animator.SetInteger(punchDirectionHash, idleDirectionValue);
    }

    private bool TryResolveFloatParameter(string parameterName, out int hash)
    {
        hash = 0;
        if (animator == null || string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Float &&
                string.Equals(parameter.name, parameterName, StringComparison.Ordinal))
            {
                hash = Animator.StringToHash(parameterName);
                return true;
            }
        }

        return false;
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
