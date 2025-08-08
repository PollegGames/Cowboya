using System.Linq;
using UnityEngine;

public class LegJointLimiter : MonoBehaviour
{
    [Header("Hinge joints des jambes")]
    public HingeJoint2D leftLegJoint;
    public HingeJoint2D rightLegJoint;

    private void Awake()
    {
        RefreshJoints();
    }

    public void SetLegRotationLimits(bool goingRight)
    {
        if (goingRight)
        {
            // Facing right: -180 to 0 for both legs
            SetJointLimits(rightLegJoint, -180f, 0);
            SetJointLimits(leftLegJoint, -180f, 0);
        }
        else
        {
            // Facing left: 0 to 180 for both legs
            SetJointLimits(rightLegJoint, 0f, 180);
            SetJointLimits(leftLegJoint, 0f, 180);
        }
    }

    private void SetJointLimits(HingeJoint2D joint, float lower, float upper)
    {
        if (joint == null)
            return;
        var limits = joint.limits;
        limits.min = lower;
        limits.max = upper;
        joint.limits = limits;
        joint.useLimits = true;
    }

    /// <summary>
    /// Reacquires leg hinge joint references after joints have been restored.
    /// </summary>
    public void RefreshJoints()
    {
        leftLegJoint = FindJoint("LeftLowLeg");
        rightLegJoint = FindJoint("RightLowLeg");
    }

    private HingeJoint2D FindJoint(string name)
    {
        return GetComponentsInChildren<HingeJoint2D>(true)
            .FirstOrDefault(j => j.name == name);
    }
}
