using UnityEngine;

/// <summary>
/// Handles horizontal movement and jumping for the Player6 prefab.
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class NewPlayerMovementController : MonoBehaviour, ILookDirectionProvider
{
    private const float DefaultMoveSpeed = 5f;
    private const float DefaultJumpForce = 12f;
    private const float MinGroundNormalY = 0.5f;

    [Header("Movement")]
    [SerializeField]
    private float moveSpeed = DefaultMoveSpeed;

    [SerializeField]
    private float jumpForce = DefaultJumpForce;

    [SerializeField]
    private LayerMask groundMask;

    private Rigidbody2D rb;
    private Animator animator;
    [SerializeField]
    private MonoBehaviour inputSource;
    private IPlayerInput input;
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
        input = inputSource as IPlayerInput;
        if (input == null)
        {
            Debug.LogError("NewPlayerMovementController: inputSource does not implement IPlayerInput");
        }
    }

    private void Update()
    {        
        if (input != null)
        {
            moveInput = input.Movement.x;
            if (Mathf.Abs(moveInput) > 0.1f)
            {
                lookDirection = new Vector2(Mathf.Sign(moveInput), 0f);
            }

            if (input.JumpPressed)
            {
                HandleJump();
            }
        }

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
        if (IsGrounded)
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
