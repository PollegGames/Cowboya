using UnityEngine;

[RequireComponent(typeof(Animator))]
public sealed class NewPlayerMovementController : MonoBehaviour, ILookDirectionProvider
{
    private const float DefaultMoveSpeed = 5f;
    private const float DefaultJumpForce = 12f;
    private const float MinGroundNormalY = 0.5f;
    private const string JumpActionName = "Jump";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = DefaultMoveSpeed;
    [SerializeField] private float jumpForce = DefaultJumpForce;
    [SerializeField] private LayerMask groundMask;

    [Header("Physics targets")]
    [SerializeField] private Rigidbody2D body;      // hips/torso RB
    [SerializeField] private Transform groundProbe; // empty under feet
    [SerializeField] private float groundProbeRadius = 0.06f;

    private Animator animator;

    // Input
    private IPlayerInput input;

    private float moveInput;
    private float previousSpeed;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int GroundedParam = Animator.StringToHash("Grounded");

    private Vector2 lookDirection = Vector2.right;
    public Vector2 LookDirection => lookDirection;

    public bool IsGrounded { get; private set; }

    [Header("Visual flip + poles")]
    [SerializeField] private SpriteRenderer[] sprites; // optional assign
    [SerializeField] private PoleMirror2D poleMirror;  // optional
    [SerializeField] private LegJointLimiter legJointLimiter; // optional

    private bool facingLeft = false; // single source of truth

    private void Awake()
    {
        if (!body) Debug.LogError("Assign a Rigidbody2D to 'body'.");
        animator = GetComponent<Animator>();
        input = GetComponent<IPlayerInput>();

        if (sprites == null || sprites.Length == 0)
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void Start()
    {
        SetFacing(facingLeft); // apply initial pose
    }

    private void Update()
    {
        if (input != null)
        {
            moveInput = input.Movement.x;

            if (Mathf.Abs(moveInput) > 0.01f)
                SetFacing(moveInput < 0f);

            if (input.JumpPressed)
                HandleJump();
        }

        float currentSpeed = Mathf.Abs(moveInput);
        bool startedWalking = previousSpeed <= 0.01f && currentSpeed > 0.01f;

        if (startedWalking && facingLeft)
            animator.Play("Walk", 0, 0.5f);

        animator.SetFloat(SpeedParam, currentSpeed);
        animator.SetBool(GroundedParam, IsGrounded);

        lookDirection = facingLeft ? Vector2.left : Vector2.right;
        previousSpeed = currentSpeed;
    }

    private void FixedUpdate()
    {
        var v = body.linearVelocity;   // use .velocity if needed
        v.x = moveInput * moveSpeed;
        body.linearVelocity = v;

        if (groundProbe)
            IsGrounded = Physics2D.OverlapCircle(groundProbe.position, groundProbeRadius, groundMask);
    }

    private void HandleJump()
    {
        if (!IsGrounded) return;
        body.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        IsGrounded = false;
    }

    private void SetFacing(bool left)
    {
        if (facingLeft == left) return;
        facingLeft = left;

        // visuals
        if (sprites != null)
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i]) sprites[i].flipX = left;

        // poles
        if (poleMirror) poleMirror.SetFacing(!left);              // expects isRight
        if (legJointLimiter) legJointLimiter.SetLegRotationLimits(!left); // expects facingRight


    }
}
