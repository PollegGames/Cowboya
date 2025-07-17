using UnityEngine;

public interface IPlayerInput
{
    Vector2 Movement { get; }
    bool JumpPressed { get; }
    bool PrimaryAttack { get; }

    // Grab events for left hand
    bool LeftGrabDown { get; }
    bool LeftGrabHeld { get; }
    bool LeftGrabUp { get; }

    // Grab events for right hand
    bool RightGrabDown { get; }
    bool RightGrabHeld { get; }
    bool RightGrabUp { get; }
}
