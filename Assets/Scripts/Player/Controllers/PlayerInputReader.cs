using UnityEngine;

public class PlayerInputReader : MonoBehaviour, IPlayerInput
{
    public Vector2 Movement { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool PrimaryAttack { get; private set; }
    public bool LeftGrabPressed { get; private set; }
    public bool RightGrabPressed { get; private set; }

    void Update()
    {
        Movement = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        JumpPressed = Input.GetButton("Jump");
        PrimaryAttack = Input.GetMouseButton(0);
        LeftGrabPressed = Input.GetKeyDown(KeyCode.Q);
        RightGrabPressed = Input.GetKeyDown(KeyCode.E);
    }
}
