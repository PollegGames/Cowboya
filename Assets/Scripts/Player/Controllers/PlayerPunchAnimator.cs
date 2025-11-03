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
    [SerializeField] private string punchDirectionParameter = "PunchDirection";
    [SerializeField] private string punchTriggerParameter = "PunchTrigger";
    [SerializeField] private int idleDirectionValue = -1;
    [SerializeField] private int punchLayerIndex = 0;

    private static readonly int forwardValue = 0;
    private static readonly int upValue = 1;
    private static readonly int downValue = 2;
    private static readonly int backValue = 3;

    private const string PunchStateTag = "Punch";

    private int punchDirectionHash;
    private int punchTriggerHash;
    private Coroutine punchRoutine;
    private AttackSector currentAttackSector = AttackSector.Right;

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

        punchDirectionHash = Animator.StringToHash(punchDirectionParameter);
        punchTriggerHash = Animator.StringToHash(punchTriggerParameter);
        animator?.SetInteger(punchDirectionHash, idleDirectionValue);
    }

    /// <summary>
    /// Receives attack requests and pushes the matching punch state.
    /// </summary>
    /// <param name="request">The attack request emitted by the player attack controller.</param>
    public void HandleAttackRequest(AttackRequest request)
    {
        if (animator == null)
        {
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
        animator.SetInteger(punchDirectionHash, idleDirectionValue);
        currentAttackSector = AttackSector.Right;
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

        animator.SetInteger(punchDirectionHash, idleDirectionValue);
        punchRoutine = null;
        currentAttackSector = AttackSector.Right;
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
            animator.SetInteger(punchDirectionHash, idleDirectionValue);
        }

        currentAttackSector = AttackSector.Right;
    }
}
