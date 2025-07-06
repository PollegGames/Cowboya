using UnityEngine;

public class PlayerInputReader : MonoBehaviour, IPlayerInput
{
    public Vector2 Movement { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool PrimaryAttack { get; private set; }

    void Update()
    {
        Movement = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        JumpPressed = Input.GetButton("Jump");
        PrimaryAttack = Input.GetMouseButton(0);
    }
}
