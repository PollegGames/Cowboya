using System.Linq;
using UnityEngine;

public class LegJointLimiter : MonoBehaviour
{
    [Header("Hinge joints des jambes")]
    public HingeJoint2D leftLegJoint;
    public HingeJoint2D rightLegJoint;

    private float minLeft = 0;
    private float maxLeft = 180;
    private float minRight = 0;
    private float maxRight = 180;
    private bool facingRight = true;

    private void Awake()
    {
        RefreshJoints();
        SetLegRotationLimits(true);
    }

    public void SetLegRotationLimits(bool goingRight)
    {
        facingRight = goingRight;

        if (facingRight)
        {
            ApplyJoint(rightLegJoint, minRight, maxRight );
            ApplyJoint(leftLegJoint, minLeft, maxLeft);
        }
        else
        {
            ApplyJoint(rightLegJoint, -minLeft, -maxLeft);
            ApplyJoint(leftLegJoint, -minRight, -maxRight);
        }
    }

    private void ApplyJoint(HingeJoint2D joint, float lower, float upper)
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
