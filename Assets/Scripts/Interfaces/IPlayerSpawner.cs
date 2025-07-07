using UnityEngine;

public interface IPlayerSpawner
{
    GameObject playerInstance { get; }
    RobotStateController playerRobotBehaviour { get; }
    Transform playerHeadTransform { get; }
    void SetPlayerStartPosition(Vector3 startPosition);
    void InitializePlayer();
}
