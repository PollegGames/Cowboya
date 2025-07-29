using UnityEngine;

/// <summary>
/// Base class for agents (player, enemies, allies) that handles movement, facing, and jumping using physical components.
/// Uses RobotLocomotionController, FacingController, and LegJointLimiter instead of Animator.
/// </summary>
public abstract class PhysicsBaseAgentController : MonoBehaviour, IMover
{
    [Header("Movement & Facing Modules")]
    [SerializeField] protected RobotLocomotionController locomotion;
    [SerializeField] protected FacingController facing;
    [SerializeField] protected LegJointLimiter legJointLimiter;
    [SerializeField] protected BodyJointLimiter bodyJointLimiter;

    protected float direction = 0f;
    protected float verticalDirection = 0f;
    protected bool flipped = false;

    protected virtual void Awake()
    {
        if (locomotion == null)
            locomotion = GetComponent<RobotLocomotionController>();
        if (facing == null)
            facing = GetComponent<FacingController>();
        if (legJointLimiter == null)
            legJointLimiter = GetComponent<LegJointLimiter>();
    }

    /// <summary>
    /// Sets horizontal movement input and flips the character if needed.
    /// </summary>
    public virtual void SetMovement(float input)
    {
        direction = Mathf.Clamp(input, -1f, 1f);
        locomotion.HandleMovement(direction);
        TryFlip(direction);
    }

    
    /// <summary>
    /// Sets the vertical movement direction.
    /// </summary>
    public virtual void SetVerticalMovement(float direction)
    {
        this.verticalDirection = Mathf.Clamp(direction, -1f, 1f);
    }

    /// <summary>
    /// Triggers a jump using locomotion system.
    /// </summary>
    public virtual void TryJump()
    {
        locomotion.Jump();
    }

    /// <summary>
    /// Handles character flipping and updates facing direction and leg limits.
    /// </summary>
    protected virtual void TryFlip(float input)
    {
        if (Mathf.Abs(input) > 0.1f)
        {
            bool movingLeft = input < 0f;
            if (movingLeft != flipped)
            {
                flipped = movingLeft;
                ApplyFacingDirection();
            }
        }
    }

    /// <summary>
    /// Applies the current facing direction to all related modules.
    /// </summary>
    protected virtual void ApplyFacingDirection()
    {
        locomotion.SetFacingDirection(!flipped);       // true = facing right
        facing.SetLegFacing(!flipped);                 // true = facing right
        legJointLimiter.SetLegRotationLimits(flipped); // true = going left
        bodyJointLimiter.SetBodyRotationLimits(flipped);
    }
}
