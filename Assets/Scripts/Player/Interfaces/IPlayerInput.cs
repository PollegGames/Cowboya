using UnityEngine;

public interface IPlayerInput
{
    Vector2 Movement { get; }
    bool JumpPressed { get; }
    bool PrimaryAttack { get; }
    bool LeftGrabPressed { get; }
    bool RightGrabPressed { get; }
}
