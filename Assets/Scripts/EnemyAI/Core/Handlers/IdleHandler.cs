using UnityEngine;

[CreateAssetMenu(menuName = "RobotAI/Handlers/Idle", fileName = "IdleHandler")]
public class IdleHandler : ScriptableRobotTaskHandler
{
    public override void Execute(RobotBrain brain, object payload)
    {
        if (brain == null || brain.Body == null)
            return;

        // Clear any residual movement so the robot truly idles.
        brain.Body.StopMovement();
    }
}
