using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Streamlined locomotion controller that only drives animator parameters.
/// </summary>
public class RobotLocomotionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RobotStateController robotBehaviour;
    [SerializeField] private Animator animator;

    [Header("Animator Parameters")]
    [SerializeField] private string directionParameter = "Direction";
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string walkBoolParameter = "IsWalking";
    [SerializeField] private string jumpBoolParameter = "IsJumping";
    [SerializeField] private string crouchBoolParameter = "IsCrouching";

    [Header("Movement Settings")]
    [SerializeField, Min(0f)] private float inputWalkThreshold = 0.2f;
    [SerializeField, Min(0f)] private float jumpAnimationDuration = 0.4f;
    [SerializeField] private float energyCostPerJump = 3f;
    [SerializeField] private float energyCostPerCrouch = 1f;
    [SerializeField] public bool isPlayerControlled = false;

    public event Action OnJumpStarted;
    public event Action OnJumpEnded;
    public event Action OnCrouchStarted;
    public event Action OnCrouchEnded;

    private bool isJumping;
    private bool isCrouching;
    private bool facingRight = true;
    private Coroutine jumpRoutine;

    private void Awake()
    {
        if (robotBehaviour == null)
        {
            robotBehaviour = GetComponent<RobotStateController>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    /// <summary>
    /// Updates the animator with the current horizontal input.
    /// </summary>
    public void HandleMovement(float horizontalInput, bool flipped)
    {
        facingRight = !flipped;

        float magnitude = Mathf.Abs(horizontalInput);
        bool walking = magnitude > inputWalkThreshold;
        float directionValue = 0f;

        if (walking)
        {
            directionValue = horizontalInput >= 0f ? 1f : -1f;
        }

        ApplyAnimatorMovement(directionValue, walking);
    }

    private void ApplyAnimatorMovement(float directionValue, bool walking)
    {
        if (animator == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(directionParameter))
        {
            animator.SetFloat(directionParameter, directionValue);
        }

        if (!string.IsNullOrEmpty(speedParameter))
        {
            animator.SetFloat(speedParameter, walking ? 1f : 0f);
        }

        if (!string.IsNullOrEmpty(walkBoolParameter))
        {
            animator.SetBool(walkBoolParameter, walking);
        }

        if (!string.IsNullOrEmpty(crouchBoolParameter) && !walking && !isCrouching)
        {
            animator.SetBool(crouchBoolParameter, false);
        }
    }

    /// <summary>
    /// Triggers a jump state on the animator.
    /// </summary>
    public void Jump()
    {
        if (isJumping || !CanPerformEnergy(energyCostPerJump))
        {
            return;
        }

        ConsumeEnergy(energyCostPerJump);
        isJumping = true;
        SetAnimatorBool(jumpBoolParameter, true);
        OnJumpStarted?.Invoke();

        if (jumpRoutine != null)
        {
            StopCoroutine(jumpRoutine);
        }

        if (jumpAnimationDuration > 0f)
        {
            jumpRoutine = StartCoroutine(ResetJumpAfterDelay(jumpAnimationDuration));
        }
    }

    private IEnumerator ResetJumpAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        CompleteJump();
    }

    /// <summary>
    /// Resets the jump state, typically invoked by an animation event.
    /// </summary>
    public void CompleteJump()
    {
        if (!isJumping)
        {
            return;
        }

        isJumping = false;
        SetAnimatorBool(jumpBoolParameter, false);
        OnJumpEnded?.Invoke();
    }

    /// <summary>
    /// Starts a crouch pose.
    /// </summary>
    public void Crouch()
    {
        if (isCrouching || !CanPerformEnergy(energyCostPerCrouch))
        {
            return;
        }

        ConsumeEnergy(energyCostPerCrouch);
        isCrouching = true;
        SetAnimatorBool(crouchBoolParameter, true);
        OnCrouchStarted?.Invoke();
    }

    /// <summary>
    /// Stops crouching.
    /// </summary>
    public void Uncrouch()
    {
        if (!isCrouching)
        {
            return;
        }

        isCrouching = false;
        SetAnimatorBool(crouchBoolParameter, false);
        OnCrouchEnded?.Invoke();
    }

    /// <summary>
    /// Stores the desired facing direction. Actual sprite flipping is handled elsewhere.
    /// </summary>
    public void SetFacingDirection(bool isRight)
    {
        facingRight = isRight;
    }

    private bool CanPerformEnergy(float energyCost)
    {
        if (robotBehaviour == null || energyCost <= 0f)
        {
            return true;
        }

        return robotBehaviour.CanPerformEnergy(energyCost);
    }

    private void ConsumeEnergy(float energyCost)
    {
        if (robotBehaviour == null || energyCost <= 0f)
        {
            return;
        }

        robotBehaviour.ConsumeEnergy(energyCost);
    }

    private void SetAnimatorBool(string parameter, bool value)
    {
        if (animator == null || string.IsNullOrEmpty(parameter))
        {
            return;
        }

        animator.SetBool(parameter, value);
    }
}
