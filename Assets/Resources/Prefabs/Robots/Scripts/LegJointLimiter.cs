using UnityEngine;

public class LegJointLimiter : MonoBehaviour
{
    [Header("Hinge joints des jambes")]
    public HingeJoint2D leftLegJoint;
    public HingeJoint2D rightLegJoint;

    public void SetLegRotationLimits(bool goingRight)
    {
        if (goingRight)
        {
            // Vers la droite : -90 à +90 pour les deux jambes
            SetJointLimits(rightLegJoint, -180f, 0);
            SetJointLimits(leftLegJoint, -180f, 0);
        }
        else
        {
            // Vers la gauche : 0 à 180 pour les deux jambes
            SetJointLimits(rightLegJoint, 0f, 180);
            SetJointLimits(leftLegJoint, 0f, 180);
        }
    }

    private void SetJointLimits(HingeJoint2D joint, float lower, float upper)
    {
        var limits = joint.limits;
        limits.min = lower;
        limits.max = upper;
        joint.limits = limits;
        joint.useLimits = true;
    }
}
