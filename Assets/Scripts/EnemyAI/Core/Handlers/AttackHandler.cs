using UnityEngine;

/// <summary>
/// Triggers the robot attack controller toward a target position/transform.
/// </summary>
[CreateAssetMenu(menuName = "RobotAI/Handlers/Attack", fileName = "AttackHandler")]
public class AttackHandler : ScriptableRobotTaskHandler
{
    public override void Execute(RobotBrain brain, object payload)
    {
        if (brain == null)
            return;

        var attackController = brain.GetComponent<RobotAttackController>();
        if (attackController == null)
        {
            Debug.LogWarning($"[AttackHandler] No RobotAttackController on {brain.name}");
            return;
        }

        Vector2 targetPosition;
        if (payload is Transform t && t != null)
            targetPosition = t.position;
        else if (payload is Vector3 v3)
            targetPosition = v3;
        else if (payload is Vector2 v2)
            targetPosition = v2;
        else
            targetPosition = brain.transform.position;

        attackController.TryAttack(targetPosition);
    }
}
