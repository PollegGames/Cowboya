using UnityEngine;

[CreateAssetMenu(menuName = "RobotAI/Handlers/Idle", fileName = "IdleHandler")]
public class IdleHandler : ScriptableRobotTaskHandler
{
    public override void Execute(RobotBrain brain, object payload)
    {
        // No-op; could play idle animation or reset movement.
    }
}
