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
    [SerializeField] private EnergyBot energyBot;
    [SerializeField] private PlayerBrain playerBrain;
    [SerializeField] private Animator animator;

    [Header("Animator Parameters")]
    [SerializeField] private string directionParameter = "Direction";
    [SerializeField] private string verticalDirectionParameter = "VerticalDirection";
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string walkBoolParameter = "IsWalking";
    [SerializeField] private string jumpBoolParameter = "IsJumping";
    [SerializeField] private string crouchBoolParameter = "IsCrouching";

    [Header("Movement Settings")]
    [SerializeField, Min(0f)] private float jumpAnimationDuration = 0.4f;
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

        if (energyBot == null)
        {
            energyBot = GetComponent<EnergyBot>();
        }

        if (playerBrain == null)
        {
            playerBrain = GetComponent<PlayerBrain>();
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
        bool walking = magnitude > 0f;
        float directionValue = 0f;

        if (walking)
        {
            directionValue = horizontalInput >= 0f ? 1f : -1f;
            SpendEnergy(EnergyAction.Walk, Time.deltaTime);
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

        UpdateVerticalDirectionFromState();
    }

    /// <summary>
    /// Triggers a jump state on the animator.
    /// </summary>
    public void Jump()
    {
        if (isJumping || !HasEnergyFor(EnergyAction.Jump))
        {
            return;
        }

        if (!SpendEnergy(EnergyAction.Jump))
        {
            return;
        }

        isJumping = true;
        SetAnimatorBool(jumpBoolParameter, true);
        UpdateVerticalDirectionFromState();
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
        UpdateVerticalDirectionFromState();
        OnJumpEnded?.Invoke();
    }

    /// <summary>
    /// Starts a crouch pose.
    /// </summary>
    public void Crouch()
    {
        if (isCrouching || !HasEnergyFor(EnergyAction.Crouch))
        {
            return;
        }

        if (!SpendEnergy(EnergyAction.Crouch))
        {
            return;
        }

        isCrouching = true;
        SetAnimatorBool(crouchBoolParameter, true);
        UpdateVerticalDirectionFromState();
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
        UpdateVerticalDirectionFromState();
        OnCrouchEnded?.Invoke();
    }

    /// <summary>
    /// Stores the desired facing direction. Actual sprite flipping is handled elsewhere.
    /// </summary>
    public void SetFacingDirection(bool isRight)
    {
        facingRight = isRight;
    }

    private void SetAnimatorBool(string parameter, bool value)
    {
        if (animator == null || string.IsNullOrEmpty(parameter))
        {
            return;
        }

        animator.SetBool(parameter, value);
    }

    private void UpdateVerticalDirectionFromState()
    {
        if (animator == null || string.IsNullOrEmpty(verticalDirectionParameter))
        {
            return;
        }

        int verticalValue = 0;

        if (isJumping)
        {
            verticalValue = 1;
        }
        else if (isCrouching)
        {
            verticalValue = -1;
        }

        animator.SetFloat(verticalDirectionParameter, verticalValue);
    }

    private bool HasEnergyFor(EnergyAction action)
    {
        if (energyBot != null)
            return energyBot.HasEnergyForAction(action);

        if (robotBehaviour != null)
            return robotBehaviour.CanPerformEnergy(action);

        return true;
    }

    private bool SpendEnergy(EnergyAction action, float deltaTime = 0f)
    {
        if (playerBrain != null)
            return playerBrain.TrySpendEnergy(action, deltaTime);

        if (energyBot != null)
            return energyBot.TryConsume(action, deltaTime);

        return true;
    }

}
