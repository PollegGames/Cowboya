using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour, IPlayerInput
{
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

    private void Awake()
    {
        controls = new InputSystem_Actions();

        controls.Player.Move.performed += ctx => Movement = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => Movement = Vector2.zero;
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
        controls.Player.Crouch.started += ctx => CrouchHeld = true;
        controls.Player.Crouch.canceled += ctx => CrouchHeld = false;

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
