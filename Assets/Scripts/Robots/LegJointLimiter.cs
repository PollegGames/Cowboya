using System.Linq;
using UnityEngine;

public class LegJointLimiter : MonoBehaviour
{
    [Header("Hinge joints des jambes")]
    public HingeJoint2D leftLegJoint;
    public HingeJoint2D rightLegJoint;

    private float minLeft;
    private float maxLeft;
    private float motorSpeedLeft;
    private float minRight;
    private float maxRight;
    private float motorSpeedRight;
    private bool cached;
    private bool facingRight = true;

    private void Awake()
    {
        RefreshJoints();
        SetLegRotationLimits(true);
    }

    public void SetLegRotationLimits(bool goingRight)
    {
        if (!cached)
            CacheJointLimits();

        if (goingRight == facingRight)
            return;

        facingRight = goingRight;

        if (facingRight)
        {
            ApplyJoint(rightLegJoint, minRight, maxRight, motorSpeedRight);
            ApplyJoint(leftLegJoint, minLeft, maxLeft, motorSpeedLeft);
        }
        else
        {
            ApplyJoint(rightLegJoint, minLeft, maxLeft, motorSpeedLeft);
            ApplyJoint(leftLegJoint, minRight, maxRight, motorSpeedRight);
        }
    }

    private void ApplyJoint(HingeJoint2D joint, float lower, float upper, float speed)
    {
        if (joint == null)
            return;
        var limits = joint.limits;
        limits.min = lower;
        limits.max = upper;
        joint.limits = limits;
        joint.useLimits = true;
        var motor = joint.motor;
        motor.motorSpeed = speed;
        joint.motor = motor;
        joint.useMotor = true;
    }

    /// <summary>
    /// Reacquires leg hinge joint references after joints have been restored.
    /// </summary>
    public void RefreshJoints()
    {
        leftLegJoint = FindJoint("LeftLowLeg");
        rightLegJoint = FindJoint("RightLowLeg");
        CacheJointLimits();
    }

    private void CacheJointLimits()
    {
        if (leftLegJoint == null || rightLegJoint == null)
            return;

        var leftLimits = leftLegJoint.limits;
        minLeft = leftLimits.min;
        maxLeft = leftLimits.max;
        motorSpeedLeft = leftLegJoint.motor.motorSpeed;

        var rightLimits = rightLegJoint.limits;
        minRight = rightLimits.min;
        maxRight = rightLimits.max;
        motorSpeedRight = rightLegJoint.motor.motorSpeed;

        cached = true;
    }

    private HingeJoint2D FindJoint(string name)
    {
        return GetComponentsInChildren<HingeJoint2D>(true)
            .FirstOrDefault(j => j.name == name);
    }
}
