using UnityEngine;

public class RobotPoseController : MonoBehaviour
{
    [Header("Références aux deux cibles de bras")]
    public IKArcPoseDistributor leftArmFollower;
    public IKArcPoseDistributor rightArmFollower;

    [Header("Debug / Visual")]
    [Range(0f, 1f)] public float armT = 0.5f;

    /// <summary>
    /// Call this with an angle in degrees [0, 360]
    /// It converts it to a t ∈ [0, 1] and dispatches to both arms
    /// </summary>
    public void UpdateArmFromAngle(float angleDeg)
    {
        float normalizedAngle = (angleDeg + 360f) % 360f; // safety
        armT = normalizedAngle / 360f;

        leftArmFollower?.SetT(armT);
        rightArmFollower?.SetT(armT);
    }
}
