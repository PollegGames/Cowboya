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

  [Header("Anim params")]
    [SerializeField] private string pIsWalking = "IsWalking";
    [SerializeField] private string pIsCrouching = "IsCrouching";
    [SerializeField] private string pIsJumping = "IsJumping";
    [SerializeField] private string pJumpTrigger = "Jump";

    [Header("Tuning")]
    [SerializeField] private float verticalDeadZone = 0.2f;

    private Vector2 _move;
    private bool jumpLatch; // prevents retrigger while holding up

    protected override void Awake()
    {
        base.Awake();
        if (!hipRb) hipRb = GetComponent<Rigidbody2D>();

        // prefer injected IPlayerInput, else fall back to keyboard/gamepad
        input = inputSource as IPlayerInput;
        if (input == null && inputSource != null)
            Debug.LogError($"{nameof(NewPlayerMovementController)}: inputSource does not implement IPlayerInput");
    }

    protected override void Update()
    {
        ReadInput();

        // horizontal
        SetMovement(_move.x);
        animator.SetBool(pIsWalking, Mathf.Abs(direction) > 0.01f);
        TryFlip(direction);

        // vertical → animations only
        HandleJumpCrouch(_move.y);
    }

    private void FixedUpdate()
    {
        if (Mathf.Abs(direction) > 0.01f) Move();   // keep base physics for X
        // never call MoveVertical() → no vertical forces
    }

    private void ReadInput()
    {
        if (input != null)
        {
            _move = Vector2.ClampMagnitude(input.Movement, 1f);
            return;
        }

        // Fallback: WASD/Arrows and gamepad left stick
        Vector2 k = Vector2.zero;
        var kb = Keyboard.current;
        if (kb != null)
        {
            k.x = (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? -1f : 0f)
                + (kb.dKey.isPressed || kb.rightArrowKey.isPressed ?  1f : 0f);
            k.y = (kb.sKey.isPressed || kb.downArrowKey.isPressed ? -1f : 0f)
                + (kb.wKey.isPressed || kb.upArrowKey.isPressed   ?  1f : 0f);
        }
        var gp = Gamepad.current != null ? Gamepad.current.leftStick.ReadValue() : Vector2.zero;

        // prefer the stronger input vector
        _move = (gp.sqrMagnitude > k.sqrMagnitude) ? gp : k;
        _move = Vector2.ClampMagnitude(_move, 1f);
    }

    private void HandleJumpCrouch(float y)
    {
        bool up = y >  verticalDeadZone;
        bool down = y < -verticalDeadZone;

        // Jump as animation flag
        if (up && !jumpLatch)
        {
            jumpLatch = true;
            animator.SetBool(pIsJumping, true);
            if (!string.IsNullOrEmpty(pJumpTrigger)) animator.SetTrigger(pJumpTrigger);
        }
        if (!up)
        {
            animator.SetBool(pIsJumping, false);
            jumpLatch = false;
        }

        // Crouch while held
        animator.SetBool(pIsCrouching, down);
    }

    // Call from landing animation event
    public void OnLanded() => animator.SetBool(pIsJumping, false);

    // Ensure vertical physics never run
    protected override void MoveVertical() { /* intentionally empty */ }
}
