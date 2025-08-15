using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public sealed class NewPlayerMovementController : AnimatorBaseAgentController
{
    [Header("Input")]
    [SerializeField] private string moveActionName = "Move"; // Vector2

    [SerializeField] private PoleMirror2D poleMirror2D;

    private PlayerInput _playerInput;
    private InputAction _moveAction;
    private Vector2 _move;

    protected override void Awake()
    {
        base.Awake();
        _playerInput = GetComponent<PlayerInput>();
        if (this.hipRb == null) hipRb = GetComponent<Rigidbody2D>();

        _moveAction = _playerInput.actions[moveActionName];
    }

    private void OnEnable()
    {
        _moveAction?.Enable();
    }

    private void OnDisable()
    {
        _moveAction?.Disable();
    }

    protected override void Update()
    {
        TryFlip(direction);
        poleMirror2D.SetFacing(direction > 0);
        // Read input once per frame
        _move = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;

        // Set directions
        SetMovement(_move.x);
        SetVerticalMovement(_move.y);

        // Animator flags only (no physics here)
        animator.SetBool("IsWalking", isMoving);
        animator.SetBool("IsVerticalWalking", isVerticalMoving);

        // Face where we go  
    }

    private void FixedUpdate()
    {
        if (isMoving) Move();
        if (isVerticalMoving) MoveVertical();
    }

    public override void SetMovement(float dir)
    {
        base.SetMovement(dir);
        isMoving = !Mathf.Approximately(direction, 0f);
    }

    public override void SetVerticalMovement(float dir)
    {
        verticalDirection = Mathf.Clamp(dir, -1f, 1f);
        isVerticalMoving = !Mathf.Approximately(verticalDirection, 0f);
    }

    // Use Y for vertical motion
    protected override void MoveVertical()
    {
        animator.SetFloat("VerticalDirection", verticalDirection);

        Vector2 desired = new Vector2(hipRb.linearVelocity.x, verticalDirection * moveSpeed);
        Vector2 delta = desired - hipRb.linearVelocity;
        Vector2 force = delta * hipRb.mass / Time.fixedDeltaTime;
        hipRb.AddForce(force);
    }

    // Optional: keep base Move as-is. Ensure it uses X velocity only.
}
