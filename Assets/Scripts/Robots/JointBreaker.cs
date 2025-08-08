using System.Collections.Generic;
using UnityEngine;

public class JointBreaker : MonoBehaviour
{
    [Header("HingeJoint2D references")]
    public List<HingeJoint2D> hingeJoints = new List<HingeJoint2D>();

    [Header("FixedJoint2D references")]
    public List<FixedJoint2D> fixedJoints = new List<FixedJoint2D>();

    [Header("IK Solvers (Hinge2DIkSolver)")]
    public List<Hinge2DIkSolver> ikSolvers = new List<Hinge2DIkSolver>();

    private class JointInfo
    {
        public GameObject owner;
        public System.Type type;
        public Rigidbody2D connectedBody;
        public Vector2 anchor;
        public Vector2 connectedAnchor;
        public float breakForce;
        public float breakTorque;
        public bool enableCollision;
        public bool autoConfigure;
        public bool enabled;
    }

    private readonly List<JointInfo> jointInfos = new();

    private void Awake()
    {
        hingeJoints.AddRange(GetComponentsInChildren<HingeJoint2D>());
        fixedJoints.AddRange(GetComponentsInChildren<FixedJoint2D>());
        ikSolvers.AddRange(GetComponentsInChildren<Hinge2DIkSolver>());

        foreach (var joint in GetComponentsInChildren<Joint2D>(true))
        {
            jointInfos.Add(new JointInfo
            {
                owner = joint.gameObject,
                type = joint.GetType(),
                connectedBody = joint.connectedBody,
                anchor = joint.anchor,
                connectedAnchor = joint.connectedAnchor,
                breakForce = joint.breakForce,
                breakTorque = joint.breakTorque,
                enableCollision = joint.enableCollision,
                autoConfigure = joint.autoConfigureConnectedAnchor,
                enabled = joint.enabled,
            });
        }
    }

    public void BreakAll()
    {
        foreach (var hj in hingeJoints)
            if (hj != null) hj.breakForce = 0f;

        foreach (var fj in fixedJoints)
            if (fj != null) fj.breakForce = 0f;

        DisableAllSolvers();
    }

    public void DestroyAll()
    {
        foreach (var hj in hingeJoints)
            if (hj != null) Destroy(hj);

        foreach (var fj in fixedJoints)
            if (fj != null) Destroy(fj);

        hingeJoints.Clear();
        fixedJoints.Clear();

        DisableAllSolvers();
    }

    public void DisableAllSolvers()
    {
        foreach (var solver in ikSolvers)
            if (solver != null) solver.enabled = false;
    }

    /// <summary>
    /// Restores all cached joints and re-enables any IK solvers.
    /// </summary>
    public void RestoreAll()
    {
        foreach (var info in jointInfos)
        {
            var component = info.owner.GetComponent(info.type);
            Joint2D joint = component as Joint2D;
            if (joint == null)
                joint = info.owner.AddComponent(info.type) as Joint2D;
            if (joint == null)
                continue;
            joint.connectedBody = info.connectedBody;
            joint.anchor = info.anchor;
            joint.connectedAnchor = info.connectedAnchor;
            joint.autoConfigureConnectedAnchor = info.autoConfigure;
            joint.breakForce = info.breakForce;
            joint.breakTorque = info.breakTorque;
            joint.enableCollision = info.enableCollision;
            joint.enabled = info.enabled;
            joint.enabled = true;
        }

        hingeJoints.Clear();
        hingeJoints.AddRange(GetComponentsInChildren<HingeJoint2D>());
        fixedJoints.Clear();
        fixedJoints.AddRange(GetComponentsInChildren<FixedJoint2D>());
        ikSolvers.Clear();
        ikSolvers.AddRange(GetComponentsInChildren<Hinge2DIkSolver>());

        foreach (var solver in ikSolvers)
            if (solver != null) solver.enabled = true;
    }
}
