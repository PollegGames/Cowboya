using UnityEngine;

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

    void Update()
    {
        Movement      = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        JumpPressed   = Input.GetButton("Jump");
        PrimaryAttack = Input.GetMouseButton(0);

        // Left grab on RMB:
        LeftGrabDown  = Input.GetMouseButtonDown(1);
        LeftGrabHeld  = Input.GetMouseButton   (1);
        LeftGrabUp    = Input.GetMouseButtonUp  (1);

        // Right grab—if you really want the same button, map it too.
        RightGrabDown = LeftGrabDown;
        RightGrabHeld = LeftGrabHeld;
        RightGrabUp   = LeftGrabUp;
    }
}
