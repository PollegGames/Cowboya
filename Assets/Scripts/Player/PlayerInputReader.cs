using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour, IPlayerInput
{
    [SerializeField, Range(0f, 1f)] private float leftStickCrouchPressThreshold = 0.6f;
    [SerializeField, Range(0f, 1f)] private float leftStickCrouchReleaseThreshold = 0.45f;

    public Vector2 Movement { get; private set; }
    public Vector2 Aim { get; private set; }
    public bool AimIsScreenPosition { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool JumpDown { get; private set; }
    public bool CrouchHeld { get; private set; }

    public bool LeftGrabDown { get; private set; }
    public bool LeftGrabHeld { get; private set; }
    public bool LeftGrabUp { get; private set; }
    public uint LeftGrabPressSequence { get; private set; }

    public bool RightGrabDown { get; private set; }
    public bool RightGrabHeld { get; private set; }
    public bool RightGrabUp { get; private set; }
    public uint RightGrabPressSequence { get; private set; }

    public bool LeftAttackDown { get; private set; }
    public bool LeftAttackHeld { get; private set; }
    public bool LeftAttackUp { get; private set; }
    public uint LeftAttackPressSequence { get; private set; }

    public bool RightAttackDown { get; private set; }
    public bool RightAttackHeld { get; private set; }
    public bool RightAttackUp { get; private set; }
    public uint RightAttackPressSequence { get; private set; }

    private InputSystem_Actions controls;
    private uint pressSequence;
    private bool keyboardCrouchHeld;
    private bool gamepadCrouchHeld;

    private void Awake()
    {
        controls = new InputSystem_Actions();

        controls.Player.Move.performed += ctx =>
        {
            Movement = ctx.ReadValue<Vector2>();
            if (ctx.control.device is Gamepad)
                UpdateLeftStickCrouch(Movement.y);
        };
        controls.Player.Move.canceled += ctx =>
        {
            Movement = Vector2.zero;
            if (ctx.control.device is Gamepad)
            {
                gamepadCrouchHeld = false;
                RefreshCrouchHeld();
            }
        };
        controls.Player.Look.performed += ctx =>
        {
            AimIsScreenPosition = false;
            Aim = ctx.ReadValue<Vector2>();
        };
        controls.Player.Look.canceled += ctx =>
        {
            Aim = Vector2.zero;
            AimIsScreenPosition = false;
        };
        controls.Player.MouseAim.performed += ctx =>
        {
            AimIsScreenPosition = true;
            Aim = ctx.ReadValue<Vector2>();
        };
        controls.Player.Jump.started += ctx =>
        {
            JumpPressed = true;
            JumpDown = true;
        };
        controls.Player.Jump.canceled += ctx => JumpPressed = false;
        controls.Player.Crouch.started += ctx =>
        {
            keyboardCrouchHeld = true;
            RefreshCrouchHeld();
        };
        controls.Player.Crouch.canceled += ctx =>
        {
            keyboardCrouchHeld = false;
            RefreshCrouchHeld();
        };

        BindButton(controls.Player.LeftGrab,
            () => { LeftGrabDown = true; LeftGrabHeld = true; LeftGrabPressSequence = NextSequence(); },
            () => { LeftGrabHeld = false; LeftGrabUp = true; });
        BindButton(controls.Player.RightGrab,
            () => { RightGrabDown = true; RightGrabHeld = true; RightGrabPressSequence = NextSequence(); },
            () => { RightGrabHeld = false; RightGrabUp = true; });
        BindButton(controls.Player.LeftAttack,
            () => { LeftAttackDown = true; LeftAttackHeld = true; LeftAttackPressSequence = NextSequence(); },
            () => { LeftAttackHeld = false; LeftAttackUp = true; });
        BindButton(controls.Player.RightAttack,
            () => { RightAttackDown = true; RightAttackHeld = true; RightAttackPressSequence = NextSequence(); },
            () => { RightAttackHeld = false; RightAttackUp = true; });
    }

    private static void BindButton(InputAction action, System.Action pressed, System.Action released)
    {
        action.started += ctx => pressed();
        action.canceled += ctx => released();
    }

    private uint NextSequence()
    {
        pressSequence++;
        return pressSequence;
    }

    private void UpdateLeftStickCrouch(float verticalMovement)
    {
        gamepadCrouchHeld = PlayerCrouchInputResolver.Resolve(
            gamepadCrouchHeld,
            verticalMovement,
            leftStickCrouchPressThreshold,
            leftStickCrouchReleaseThreshold);
        RefreshCrouchHeld();
    }

    private void RefreshCrouchHeld()
    {
        CrouchHeld = keyboardCrouchHeld || gamepadCrouchHeld;
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    private void LateUpdate()
    {
        JumpDown = false;
        LeftGrabDown = RightGrabDown = false;
        LeftGrabUp = RightGrabUp = false;
        LeftAttackDown = RightAttackDown = false;
        LeftAttackUp = RightAttackUp = false;
    }
}

public static class PlayerCrouchInputResolver
{
    /// <summary>
    /// Resolves left-stick crouch state with separate press and release thresholds.
    /// </summary>
    public static bool Resolve(
        bool wasHeld,
        float verticalMovement,
        float pressThreshold,
        float releaseThreshold)
    {
        pressThreshold = Mathf.Clamp01(pressThreshold);
        releaseThreshold = Mathf.Min(Mathf.Clamp01(releaseThreshold), pressThreshold);

        return wasHeld
            ? verticalMovement <= -releaseThreshold
            : verticalMovement <= -pressThreshold;
    }
}
