using UnityEngine;

/// <summary>
/// Base class for horizontal and vertical movement.
/// Used by the player, enemies and allies.
/// </summary>
public abstract class AnimatorBaseAgentController : MonoBehaviour, IMover, ILookDirectionProvider
{
    [Header("Movement Settings")]
    [SerializeField] protected float moveSpeed = 3f;

    [Header("Animator")]
    [SerializeField] protected Animator animator;

    [Header("Body Reference")]
    [SerializeField] protected Transform bodyReference;
    [SerializeField] protected Rigidbody2D hipRb;

    [Header("Flip Settings")]
    [SerializeField] private SpriteRenderer[] sprites; // optional assign
    private bool flipped = false;
    private Vector2 lookDirection = Vector2.right;

    /// <summary>
    /// Gets the current look direction.
    /// </summary>
    public Vector2 LookDirection => lookDirection;

    protected bool isMoving;
    protected bool isVerticalMoving;
    protected float direction;         // Horizontal (-1, 0, 1)
    protected float verticalDirection; // Vertical   (-1, 0, 1)
    [Header("Movement & Facing Modules")]
    [SerializeField] protected LegJointLimiter legJointLimiter;
    protected virtual void Awake()
    {
        if (legJointLimiter == null)
            legJointLimiter = GetComponent<LegJointLimiter>();
        if (sprites == null || sprites.Length == 0)
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
        Debug.Log("AnimatorBaseAgentController: Awake called, components initialized.");
    }


    protected virtual void Update()
    {
        if (isMoving)
        {
            Move();
        }
        else
        {
            animator.SetBool("IsWalking", false);
        }

        if (isVerticalMoving)
        {
            MoveVertical();
        }
        else
        {
            animator.SetBool("IsVerticalWalking", false);
        }

    }

    /// <summary>
    /// Sets the horizontal movement direction.
    /// </summary>
    public virtual void SetMovement(float direction)
    {
        this.direction = Mathf.Clamp(direction, -1f, 1f);
        isMoving = direction != 0;
        if (!Mathf.Approximately(this.direction, 0f))
            lookDirection = new Vector2(Mathf.Sign(this.direction), 0f);
    }

    /// <summary>
    /// Sets the vertical movement direction.
    /// </summary>
    public virtual void SetVerticalMovement(float direction)
    {
        this.verticalDirection = Mathf.Clamp(direction, -1f, 1f);
    }

    /// <summary>
    /// Handles horizontal physical movement
    /// </summary>
    protected virtual void Move()
    {
        animator.SetBool("IsWalking", true);
        animator.SetFloat("Direction", direction);

        Vector2 desiredVelocity = new Vector2(direction * moveSpeed, hipRb.linearVelocity.y);
        Vector2 velocityChange = desiredVelocity - hipRb.linearVelocity;
        Vector2 force = velocityChange * hipRb.mass / Time.fixedDeltaTime;
        hipRb.AddForce(force);
    }

    /// <summary>
    /// Handles vertical physical movement.
    /// </summary>
    protected virtual void MoveVertical()
    {
        animator.SetBool("IsVerticalWalking", true);
        animator.SetFloat("VerticalDirection", verticalDirection);

        Vector2 desiredVelocity = new Vector2(verticalDirection * 1f, hipRb.linearVelocity.y);
        Vector2 velocityChange = desiredVelocity - hipRb.linearVelocity;
        Vector2 force = velocityChange * hipRb.mass / Time.fixedDeltaTime;
        hipRb.AddForce(force);
    }

    protected virtual void ApplyFacingDirection()
    {
        if (legJointLimiter != null)
            legJointLimiter.SetLegRotationLimits(flipped);
    }

    protected virtual void TryFlip(float direction)
    {
        ApplyFacingDirection();
        if ((direction > 0 && flipped) || (direction < 0 && !flipped))
        {
            flipped = !flipped;
            if (sprites != null)
                for (int i = 0; i < sprites.Length; i++)
                    if (sprites[i]) sprites[i].flipX = flipped;
        }
    }
}
