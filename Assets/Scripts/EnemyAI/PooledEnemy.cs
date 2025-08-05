using System;
using System.Collections.Generic;
using UnityEngine;

public class PooledEnemy : MonoBehaviour, IPooledObject
{
    [SerializeField] private BodyJointLimiter bodyJointLimiter;
    [SerializeField] private LegJointLimiter legJointLimiter;
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyStateMachine stateMachine;
    [SerializeField] private RobotStateController robotStateController;
    [SerializeField] private JointBreaker jointBreaker;

    private Transform[] cachedTransforms;
    private Vector3[] defaultPositions;
    private Quaternion[] defaultRotations;

    private class JointInfo
    {
        public GameObject owner;
        public Type type;
        public Rigidbody2D connectedBody;
        public Vector2 anchor;
        public Vector2 connectedAnchor;
        public float breakForce;
        public float breakTorque;
        public bool enableCollision;
        public bool autoConfigure;
    }

    private readonly List<JointInfo> joints = new();

    private void Awake()
    {
        if (bodyJointLimiter == null)
            bodyJointLimiter = GetComponent<BodyJointLimiter>();
        if (legJointLimiter == null)
            legJointLimiter = GetComponent<LegJointLimiter>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (stateMachine == null)
            stateMachine = GetComponent<EnemyStateMachine>();
        if (robotStateController == null)
            robotStateController = GetComponent<RobotStateController>();
        if (jointBreaker == null)
            jointBreaker = GetComponent<JointBreaker>();

        cachedTransforms = GetComponentsInChildren<Transform>(true);
        defaultPositions = new Vector3[cachedTransforms.Length];
        defaultRotations = new Quaternion[cachedTransforms.Length];
        for (int i = 0; i < cachedTransforms.Length; i++)
        {
            defaultPositions[i] = cachedTransforms[i].localPosition;
            defaultRotations[i] = cachedTransforms[i].localRotation;
        }

        foreach (var joint in GetComponentsInChildren<Joint2D>(true))
        {
            joints.Add(new JointInfo
            {
                owner = joint.gameObject,
                type = joint.GetType(),
                connectedBody = joint.connectedBody,
                anchor = joint.anchor,
                connectedAnchor = joint.connectedAnchor,
                breakForce = joint.breakForce,
                breakTorque = joint.breakTorque,
                enableCollision = joint.enableCollision,
                autoConfigure = joint.autoConfigureConnectedAnchor
            });
        }
    }

    public void OnReleaseToPool()
    {
        foreach (var rb in GetComponentsInChildren<Rigidbody2D>())
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (bodyJointLimiter != null)
            bodyJointLimiter.enabled = true;
        if (legJointLimiter != null)
            legJointLimiter.enabled = true;

        for (int i = 0; i < cachedTransforms.Length; i++)
        {
            cachedTransforms[i].localPosition = defaultPositions[i];
            cachedTransforms[i].localRotation = defaultRotations[i];
        }
    }

    public void OnAcquireFromPool()
    {
        foreach (var info in joints)
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
            joint.breakForce = info.breakForce;
            joint.breakTorque = info.breakTorque;
            joint.enableCollision = info.enableCollision;
            joint.autoConfigureConnectedAnchor = info.autoConfigure;
        }

        if (jointBreaker != null)
        {
            jointBreaker.hingeJoints.Clear();
            jointBreaker.hingeJoints.AddRange(GetComponentsInChildren<HingeJoint2D>());
            jointBreaker.fixedJoints.Clear();
            jointBreaker.fixedJoints.AddRange(GetComponentsInChildren<FixedJoint2D>());
            jointBreaker.ikSolvers.Clear();
            jointBreaker.ikSolvers.AddRange(GetComponentsInChildren<Hinge2DIkSolver>());
            foreach (var solver in jointBreaker.ikSolvers)
                if (solver != null) solver.enabled = true;
        }

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        stateMachine?.ChangeState(null);
        robotStateController?.UpdateState(RobotState.Alive);
    }
}
