using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public sealed class NewPlayerMovementController : AnimatorBaseAgentController
{
    [Header("Input")]

    [Header("Input (IPlayerInput)")]
    [SerializeField] private MonoBehaviour inputSource; // must implement IPlayerInput
    private IPlayerInput input;
    public IPlayerInput Input;

    private Vector2 _move;

    protected override void Awake()
    {
        base.Awake();
        if (hipRb == null) hipRb = GetComponent<Rigidbody2D>();

        input = inputSource as IPlayerInput;
        if (input == null)
            Debug.LogError($"{nameof(NewPlayerMovementController)}: inputSource does not implement IPlayerInput");
    }

    protected override void Update()
    {
        // Read input
        _move = input != null ? input.Movement : Vector2.zero;

        // Horizontal + vertical intents
        SetMovement(_move.x);
        SetVerticalMovement(_move.y);

        // Animator flags (no physics)
        animator.SetBool("IsWalking", isMoving);
        animator.SetBool("IsVerticalWalking", isVerticalMoving);

        // Face where we go
        TryFlip(direction);
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
        // animator.SetFloat("VerticalDirection", verticalDirection);

        // Vector2 desired = new Vector2(hipRb.linearVelocity.x, verticalDirection * moveSpeed);
        // Vector2 delta = desired - hipRb.linearVelocity;
        // Vector2 force = delta * hipRb.mass / Time.fixedDeltaTime;
        // hipRb.AddForce(force);
    }

    // Optional: keep base Move as-is. Ensure it uses X velocity only.
}
