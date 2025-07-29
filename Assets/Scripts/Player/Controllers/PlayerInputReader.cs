using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour, IPlayerInput
{
    public Vector2 Movement  { get; private set; }
    public bool JumpPressed   { get; private set; }
    public bool PrimaryAttack { get; private set; }

    public bool LeftGrabDown  { get; private set; }
    public bool LeftGrabHeld  { get; private set; }
    public bool LeftGrabUp    { get; private set; }

    public bool RightGrabDown { get; private set; }
    public bool RightGrabHeld { get; private set; }
    public bool RightGrabUp   { get; private set; }

    private InputSystem_Actions controls;

    private void Awake()
    {
        controls = new InputSystem_Actions();

        controls.Player.Move.performed += ctx => Movement = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => Movement = Vector2.zero;
        controls.Player.Jump.started += ctx => JumpPressed = true;
        controls.Player.Jump.canceled += ctx => JumpPressed = false;
        controls.Player.Attack.started += ctx => PrimaryAttack = true;
        controls.Player.Attack.canceled += ctx => PrimaryAttack = false;

        controls.Player.Interact.started += ctx =>
        {
            LeftGrabDown = RightGrabDown = true;
            LeftGrabHeld = RightGrabHeld = true;
        };
        controls.Player.Interact.canceled += ctx =>
        {
            LeftGrabHeld = RightGrabHeld = false;
            LeftGrabUp = RightGrabUp = true;
        };
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    private void LateUpdate()
    {
        LeftGrabDown = RightGrabDown = false;
        LeftGrabUp = RightGrabUp = false;
    }
}
