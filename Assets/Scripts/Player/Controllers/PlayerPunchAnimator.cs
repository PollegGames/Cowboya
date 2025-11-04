using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Drives the punch states on the animator based on <see cref="AttackRequest"/> input.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class PlayerPunchAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovementController movementController;
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private Transform masterRigRoot;
    [SerializeField] private string punchDirectionParameter = "PunchDirection";
    [SerializeField] private string punchTriggerParameter = "PunchTrigger";
    [SerializeField] private string punchAimXParameter = "PunchAimX";
    [SerializeField] private string punchAimYParameter = "PunchAimY";
    [SerializeField] private int idleDirectionValue = -1;
    [SerializeField] private int punchLayerIndex = 0;

    private static readonly int forwardValue = 0;
    private static readonly int upValue = 1;
    private static readonly int downValue = 2;
    private static readonly int backValue = 3;

    private const string PunchStateTag = "Punch";

    private int punchDirectionHash;
    private int punchTriggerHash;
    private int punchAimXHash;
    private int punchAimYHash;
    private bool hasPunchAimXParameter;
    private bool hasPunchAimYParameter;
    private Coroutine punchRoutine;
    private AttackSector currentAttackSector = AttackSector.Right;
    private Quaternion masterRigBaseLocalRotation;

    public event Action PunchCompleted;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (movementController == null)
        {
            movementController = GetComponent<PlayerMovementController>();
        }

        if (aimOrigin == null)
        {
            aimOrigin = transform;
        }

        if (masterRigRoot != null)
        {
            masterRigBaseLocalRotation = masterRigRoot.localRotation;
        }

        CacheAnimatorParameters();
        ResetAimState();
    }

    private void OnEnable()
    {
        ResetAimState();
    }

    /// <summary>
    /// Receives attack requests and pushes the matching punch state.
    /// </summary>
    /// <param name="request">The attack request emitted by the player attack controller.</param>
    public void HandleAttackRequest(AttackRequest request)
    {
        Vector2 aimDirection = ResolveAimDirection(request);
        ApplyAimDirection(aimDirection);

        if (animator == null)
        {
            currentAttackSector = request.Sector;
            return;
        }

        currentAttackSector = request.Sector;
        int directionValue = ResolveDirection(request);
        animator.SetInteger(punchDirectionHash, directionValue);
        animator.ResetTrigger(punchTriggerHash);
        animator.SetTrigger(punchTriggerHash);

        if (punchRoutine != null)
        {
            StopCoroutine(punchRoutine);
        }

        punchRoutine = StartCoroutine(ResetPunchAfterCompletion());
    }

    private int ResolveDirection(AttackRequest request)
    {
        switch (request.Sector)
        {
            case AttackSector.Up:
                return upValue;
            case AttackSector.Down:
                return downValue;
            case AttackSector.Right:
                return IsFacingRight() ? forwardValue : backValue;
            case AttackSector.Left:
                return IsFacingRight() ? backValue : forwardValue;
            default:
                return forwardValue;
        }
    }

    private bool IsFacingRight()
    {
        if (movementController != null)
        {
            return movementController.LookDirection.x >= 0f;
        }

        return transform.localScale.x >= 0f;
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
        return IsFacingRight() ? Vector2.right : Vector2.left;
    }

    private void ApplyAimDirection(Vector2 aimDirection)
    {
        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            aimDirection = ResolveDefaultAimDirection();
        }

        if (masterRigRoot != null)
        {
            Vector3 target = new Vector3(aimDirection.x, aimDirection.y, 0f);
            Quaternion rotation = Quaternion.FromToRotation(Vector3.right, target.normalized);
            masterRigRoot.localRotation = masterRigBaseLocalRotation * rotation;
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

    private void ResetAimState()
    {
        Vector2 resolvedDefault = ResolveDefaultAimDirection();
        ApplyAimDirection(resolvedDefault);

        if (animator != null)
        {
            animator.SetInteger(punchDirectionHash, idleDirectionValue);
        }
    }

    private void CacheAnimatorParameters()
    {
        if (animator == null)
        {
            return;
        }

        punchDirectionHash = Animator.StringToHash(punchDirectionParameter);
        punchTriggerHash = Animator.StringToHash(punchTriggerParameter);

        hasPunchAimXParameter = TryResolveFloatParameter(punchAimXParameter, out punchAimXHash);
        hasPunchAimYParameter = TryResolveFloatParameter(punchAimYParameter, out punchAimYHash);

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

    public AttackSector CurrentAttackSector => currentAttackSector;

    public bool IsCurrentlyFacingRight => IsFacingRight();

    public void AbortActivePunch()
    {
        if (animator == null)
        {
            return;
        }

        if (punchRoutine != null)
        {
            StopCoroutine(punchRoutine);
            punchRoutine = null;
        }

        animator.ResetTrigger(punchTriggerHash);
        ResetAimState();
        currentAttackSector = AttackSector.Right;
        PunchCompleted?.Invoke();
    }

    private IEnumerator ResetPunchAfterCompletion()
    {
        yield return null;
        int maxLayerIndex = animator.layerCount - 1;
        if (maxLayerIndex < 0)
        {
            yield break;
        }

        int layerIndex = Mathf.Clamp(punchLayerIndex, 0, maxLayerIndex);

        while (animator.IsInTransition(layerIndex))
        {
            yield return null;
        }

        while (animator.GetCurrentAnimatorStateInfo(layerIndex).IsTag(PunchStateTag))
        {
            yield return null;

            while (animator.IsInTransition(layerIndex))
            {
                yield return null;
            }
        }

        ResetAimState();
        punchRoutine = null;
        currentAttackSector = AttackSector.Right;
        PunchCompleted?.Invoke();
    }

    private void OnDisable()
    {
        if (punchRoutine != null)
        {
            StopCoroutine(punchRoutine);
            punchRoutine = null;
        }

        if (animator != null)
        {
            animator.ResetTrigger(punchTriggerHash);
            ResetAimState();
        }

        currentAttackSector = AttackSector.Right;
        PunchCompleted?.Invoke();
    }
}
