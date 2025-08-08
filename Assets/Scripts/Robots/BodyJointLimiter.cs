using System.Linq;
using UnityEngine;

public class BodyJointLimiter : MonoBehaviour
{
    [Header("Hinge joints des jambes")]
    [SerializeField] private HingeJoint2D bodyJoint;
    [SerializeField] private HingeJoint2D torsoJoint;
    [SerializeField] private HingeJoint2D lowTorsoJoint;

    private void Awake()
    {
        RefreshJoints();
    }

    public void SetBodyRotationLimits(bool goingRight)
    {
        if (goingRight)
        {
            SetJointLimits(bodyJoint, 0, 10);
            SetJointLimits(torsoJoint, 0, 10);
            SetJointLimits(lowTorsoJoint, 0, 10);
        }
        else
        {
            SetJointLimits(bodyJoint, -10, 0);
            SetJointLimits(torsoJoint,-10, 0);
            SetJointLimits(lowTorsoJoint, -10, 0);
        }
    }

    private void SetJointLimits(HingeJoint2D joint, float lower, float upper)
    {
        if(joint == null)
            return;
        var limits = joint.limits;
        limits.min = lower;
        limits.max = upper;
        joint.limits = limits;
        joint.useLimits = true;
    }

    /// <summary>
    /// Reacquires hinge joint references after joints have been restored.
    /// </summary>
    public void RefreshJoints()
    {
        bodyJoint = FindJoint("Body");
        torsoJoint = FindJoint("Torso");
        lowTorsoJoint = FindJoint("LowTorso");
    }

    private HingeJoint2D FindJoint(string name)
    {
        return GetComponentsInChildren<HingeJoint2D>(true)
            .FirstOrDefault(j => j.name == name);
    }
}
