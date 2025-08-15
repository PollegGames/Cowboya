using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles horizontal movement and jumping for the Player6 prefab.
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class NewPlayerMovementController : MonoBehaviour, ILookDirectionProvider
{
    private const float DefaultMoveSpeed = 5f;
    private const float DefaultJumpForce = 12f;
    private const float MinGroundNormalY = 0.5f;
    private const string MoveActionName = "Move";
    private const string JumpActionName = "Jump";

    [Header("Movement")]
    [SerializeField]
    private float moveSpeed = DefaultMoveSpeed;

    [SerializeField]
    private float jumpForce = DefaultJumpForce;

    [SerializeField]
    private LayerMask groundMask;

    private Rigidbody2D rb;
    private Animator animator;
    private IPlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    private float moveInput;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int GroundedParam = Animator.StringToHash("Grounded");

        private Vector2 lookDirection = Vector2.right;
    public Vector2 LookDirection => lookDirection;
    /// <summary>
    /// Indicates whether the player is currently grounded.
    /// </summary>
    public bool IsGrounded { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        playerInput = playerInput as IPlayerInput;

        moveAction = playerInput.actions[MoveActionName];
        jumpAction = playerInput.actions[JumpActionName];
    }

    private void Update()
    {        
        if (playerInput != null)
        {
            horizontalInput = input.Movement.x;
            verticalInput = input.Movement.y;

            if (Mathf.Abs(horizontalInput) > 0.1f)
                lookDirection = new Vector2(Mathf.Sign(horizontalInput), 0f);
        }
        HandleJump();

        animator.SetFloat(SpeedParam, Mathf.Abs(moveInput));
        animator.SetBool(GroundedParam, IsGrounded);
    }

    private void FixedUpdate()
    {
        Vector2 velocity = rb.linearVelocity;
        velocity.x = moveInput * moveSpeed;
        rb.linearVelocity = velocity;
    }

    private void HandleJump()
    {
        if (jumpAction.triggered && IsGrounded)
        {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            IsGrounded = false;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsGroundLayer(collision.gameObject.layer))
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y > MinGroundNormalY)
                {
                    IsGrounded = true;
                    break;
                }
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (IsGroundLayer(collision.gameObject.layer))
        {
            IsGrounded = false;
        }
    }

    private bool IsGroundLayer(int layer)
    {
        return (groundMask & (1 << layer)) != 0;
    }
}
