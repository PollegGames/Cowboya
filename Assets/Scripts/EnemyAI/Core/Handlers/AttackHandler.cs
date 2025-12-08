using UnityEngine;

/// <summary>
/// Triggers the robot attack controller toward a target position/transform.
/// </summary>
[CreateAssetMenu(menuName = "RobotAI/Handlers/Attack", fileName = "AttackHandler")]
public class AttackHandler : ScriptableRobotTaskHandler
{
    public override void Execute(RobotBrain brain, object payload)
    {
        if (brain == null || brain.Body == null)
            return;

        Transform target = payload as Transform;
        if (target == null)
        {
            if (payload is Component component)
                target = component.transform;
            else if (payload is GameObject go)
                target = go.transform;
        }

        if (target == null)
        {
            Debug.LogWarning($"[AttackHandler] Missing target payload for {brain.name}");
            return;
        }

        if (brain.Body.AttackController == null)
        {
            Debug.LogWarning($"[AttackHandler] No RobotAttackController on {brain.name}");
            return;
        }

        brain.Body.AttackController.TryStartAttack(target);
    }

    private string DescribePayload(object payload)
    {
        return payload switch
        {
            null => "null",
            Transform t => t.name,
            Component c => c.name,
            Vector3 v3 => v3.ToString(),
            Vector2 v2 => v2.ToString(),
            _ => payload.ToString()
        };
    }
}
