using UnityEngine;
using UnityEngine.InputSystem;

public class InputSystem_Actions
{
    public InputActionAsset asset { get; }
    public InputActionMap Player { get; }
    public InputAction Move { get; }
    public InputAction Jump { get; }
    public InputAction Attack { get; }
    public InputAction Interact { get; }

    public InputSystem_Actions()
    {
        asset = ScriptableObject.CreateInstance<InputActionAsset>();
        Player = new InputActionMap("Player");

        Move = Player.AddAction("Move", InputActionType.Value);
        Jump = Player.AddAction("Jump", InputActionType.Button);
        Attack = Player.AddAction("Attack", InputActionType.Button);
        Interact = Player.AddAction("Interact", InputActionType.Button);

        asset.AddActionMap(Player);
    }

    public void Enable() => asset.Enable();
    public void Disable() => asset.Disable();
}
