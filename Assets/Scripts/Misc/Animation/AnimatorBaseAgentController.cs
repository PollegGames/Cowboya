using UnityEngine;

/// <summary>
/// Base class for agents that use an Animator and Rigidbody2D for movement.
/// Movement and facing are driven through public setters so derived
/// classes can control the behaviour without player input assumptions.
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

    private Vector2 lookDirection = Vector2.right;

    public Transform BodyReference => bodyReference;

    /// <summary>
    /// Gets the current look direction.
    /// </summary>
    public Vector2 LookDirection => lookDirection;

    protected bool isMoving;
    protected bool isVerticalMoving;
    protected float direction;         // Horizontal (-1, 0, 1)
    protected float verticalDirection; // Vertical   (-1, 0, 1)

    private Transform movementRoot;
    private bool warnedMissingHipRb;

    protected virtual void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        hipRb = ResolveRigidbody();

        if (bodyReference == null)
            bodyReference = hipRb != null ? hipRb.transform : transform;
        movementRoot = bodyReference != null ? bodyReference : transform;
    }

    protected virtual void FixedUpdate()
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
        isMoving = !Mathf.Approximately(this.direction, 0f);
        if (isMoving)
            lookDirection = new Vector2(Mathf.Sign(this.direction), 0f);
    }

    /// <summary>
    /// Sets the vertical movement direction.
    /// </summary>
    public virtual void SetVerticalMovement(float direction)
    {
        verticalDirection = Mathf.Clamp(direction, -1f, 1f);
        isVerticalMoving = !Mathf.Approximately(verticalDirection, 0f);
    }

    /// <summary>
    /// Handles horizontal physical movement.
    /// </summary>
    protected virtual void Move()
    {
        animator.SetBool("IsWalking", true);
        animator.SetFloat("Direction", direction);

        if (hipRb != null)
        {
            Vector2 desiredVelocity = new Vector2(direction * moveSpeed, hipRb.linearVelocity.y);
            Vector2 velocityChange = desiredVelocity - hipRb.linearVelocity;
            Vector2 force = velocityChange * hipRb.mass / Time.fixedDeltaTime;
            hipRb.AddForce(force);
        }
        else
        {
            TranslateWithoutPhysics(new Vector2(direction * moveSpeed, 0f));
        }
    }

    /// <summary>
    /// Handles vertical physical movement.
    /// </summary>
    protected virtual void MoveVertical()
    {
        animator.SetBool("IsVerticalWalking", true);
        animator.SetFloat("VerticalDirection", verticalDirection);

        if (hipRb != null)
        {
            Vector2 desiredVelocity = new Vector2(verticalDirection * 1f, hipRb.linearVelocity.y);
            Vector2 velocityChange = desiredVelocity - hipRb.linearVelocity;
            Vector2 force = velocityChange * hipRb.mass / Time.fixedDeltaTime;
            hipRb.AddForce(force);
        }
        else
        {
            TranslateWithoutPhysics(new Vector2(0f, verticalDirection * 1f));
        }
    }

    protected virtual void TryFlip(float direction)
    {
    }

    private Rigidbody2D ResolveRigidbody()
    {
        if (hipRb == null || !hipRb)
        {
            var candidate = GetComponent<Rigidbody2D>();
            if (candidate == null)
                candidate = GetComponentInChildren<Rigidbody2D>();
            hipRb = candidate;
        }
        return hipRb;
    }

    private void TranslateWithoutPhysics(Vector2 velocity)
    {
        if (!warnedMissingHipRb)
        {
            Debug.LogWarning($"[AnimatorBaseAgentController] '{name}' has no Rigidbody2D assigned; using direct transform translation instead.", this);
            warnedMissingHipRb = true;
        }

        if (movementRoot == null || velocity == Vector2.zero)
            return;

        Vector3 delta = new Vector3(velocity.x, velocity.y, 0f) * Time.fixedDeltaTime;
        movementRoot.position += delta;
    }
}

