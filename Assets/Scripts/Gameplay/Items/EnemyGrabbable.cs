using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyGrabbable : MonoBehaviour, IGrabbable, IGrabContextReceiver
{
    [Header("Physics")]
    [SerializeField, Range(5f, 15f)] private float frequency = 10f;
    [SerializeField, Range(0f, 1f)] private float dampingRatio = 0.9f;
    [SerializeField, Min(0f)] private float maxForce = 2500f;
    [SerializeField, Min(0f)] private float releaseVelocityLimit = 12f;

    [Header("Robot Systems")]
    [SerializeField] private Behaviour[] extraBehavioursToPause;
    [SerializeField] private bool resetIntentOnRelease = true;

    private readonly List<PausedBehaviour> pausedBehaviours = new List<PausedBehaviour>();
    private readonly List<Rigidbody2D> rotationFrozenBodies = new List<Rigidbody2D>();
    private RobotStateController stateController;
    private RobotBodyController bodyController;
    private RobotHeartNew heart;
    private RobotBrainNew brain;
    private RobotAttackController attackController;
    private EnemyArmTargetController armTargetController;
    private Collider2D sourceCollider;
    private Rigidbody2D activeBody;
    private TargetJoint2D activeJoint;
    private bool grabbed;
    private bool jointWasCreated;
    private bool activeJointWasEnabled;

    private struct PausedBehaviour
    {
        public Behaviour Behaviour;
        public bool WasEnabled;
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        if (grabbed)
        {
            ReleaseWithoutThrow();
        }
    }

    public bool CanBeGrabbed(Inventory inventory)
    {
        _ = inventory;

        if (grabbed)
        {
            return false;
        }

        CacheReferences();

        return ResolveActiveBody() != null
            && (stateController == null || stateController.CurrentState == RobotState.Alive);
    }

    public void OnGrab(Transform grabParent)
    {
        if (grabParent == null)
        {
            Debug.LogWarning($"{nameof(EnemyGrabbable)} received a null grab parent.", this);
            return;
        }

        CacheReferences();
        activeBody = ResolveActiveBody();

        if (activeBody == null)
        {
            Debug.LogWarning($"{nameof(EnemyGrabbable)} on {name} has no rigidbody to grab.", this);
            return;
        }

        grabbed = true;
        StopRobotActions();
        PauseRobotBehaviours();
        ReleaseFrozenRotations();
        EnableJoint(grabParent.position);
    }

    public void OnAttract(Vector2 attractPoint)
    {
        if (!grabbed || activeJoint == null)
        {
            return;
        }

        activeJoint.target = attractPoint;
    }

    public void OnRelease(Vector2 throwForce)
    {
        if (!grabbed)
        {
            return;
        }

        DisableJoint();
        ApplyThrow(throwForce);
        RestoreFrozenRotations();
        ResumeRobotBehaviours();
        RestartRobotIntent();
        DestroyCreatedJoint();
        ClearGrabContext();
        grabbed = false;
    }

    public void SetGrabContext(Collider2D sourceCollider, Vector2 grabOrigin)
    {
        _ = grabOrigin;
        this.sourceCollider = sourceCollider;
    }

    private void CacheReferences()
    {
        if (stateController == null)
            stateController = GetComponent<RobotStateController>();
        if (bodyController == null)
            bodyController = GetComponent<RobotBodyController>();
        if (heart == null)
            heart = GetComponent<RobotHeartNew>();
        if (brain == null)
            brain = GetComponent<RobotBrainNew>();
        if (attackController == null)
            attackController = GetComponent<RobotAttackController>();
        if (armTargetController == null)
            armTargetController = GetComponentInChildren<EnemyArmTargetController>(true);
    }

    private Rigidbody2D ResolveActiveBody()
    {
        if (sourceCollider != null)
        {
            Rigidbody2D sourceBody = sourceCollider.attachedRigidbody;
            if (sourceBody != null && sourceBody.transform.IsChildOf(transform))
            {
                return sourceBody;
            }
        }

        return GetComponentInChildren<Rigidbody2D>();
    }

    private void StopRobotActions()
    {
        bodyController?.StopMovement();
        bodyController?.SetMovement(0f);
        bodyController?.SetVerticalMovement(0f);
        attackController?.StopAttacking();
        armTargetController?.SetAttackRequested(false);
    }

    private void PauseRobotBehaviours()
    {
        pausedBehaviours.Clear();
        PauseBehaviour(armTargetController);
        PauseBehaviour(attackController);
        PauseBehaviour(bodyController);
        PauseBehaviour(heart);
        PauseBehaviour(brain);

        if (extraBehavioursToPause == null)
        {
            return;
        }

        for (int i = 0; i < extraBehavioursToPause.Length; i++)
        {
            PauseBehaviour(extraBehavioursToPause[i]);
        }
    }

    private void PauseBehaviour(Behaviour behaviour)
    {
        if (behaviour == null || behaviour == this)
        {
            return;
        }

        for (int i = 0; i < pausedBehaviours.Count; i++)
        {
            if (pausedBehaviours[i].Behaviour == behaviour)
            {
                return;
            }
        }

        pausedBehaviours.Add(new PausedBehaviour
        {
            Behaviour = behaviour,
            WasEnabled = behaviour.enabled
        });

        behaviour.enabled = false;
    }

    private void ResumeRobotBehaviours()
    {
        for (int i = 0; i < pausedBehaviours.Count; i++)
        {
            PausedBehaviour paused = pausedBehaviours[i];
            if (paused.Behaviour != null)
            {
                paused.Behaviour.enabled = paused.WasEnabled;
            }
        }

        pausedBehaviours.Clear();
    }

    private void RestartRobotIntent()
    {
        if (!resetIntentOnRelease || heart == null || !heart.isActiveAndEnabled)
        {
            return;
        }

        heart.ResetIntentStack();

        if (brain != null && brain.isActiveAndEnabled && brain.TryGetCurrentPlan(out _, out RobotTask plannedTask) && plannedTask != null)
        {
            heart.QueueTask(plannedTask);
        }
    }

    private void EnableJoint(Vector2 target)
    {
        EnsureJoint();
        if (activeJoint == null || activeBody == null)
        {
            return;
        }

        activeBody.simulated = true;
        activeJoint.target = target;
        activeJoint.enabled = true;
    }

    private void EnsureJoint()
    {
        if (activeBody == null)
        {
            return;
        }

        activeJoint = activeBody.GetComponent<TargetJoint2D>();
        jointWasCreated = false;

        if (activeJoint == null)
        {
            activeJoint = activeBody.gameObject.AddComponent<TargetJoint2D>();
            jointWasCreated = true;
        }

        activeJointWasEnabled = activeJoint.enabled;
        ConfigureJoint(true);
    }

    private void ConfigureJoint(bool keepCreatedJoint)
    {
        if (activeJoint == null)
        {
            return;
        }

        activeJoint.autoConfigureTarget = false;
        activeJoint.frequency = frequency;
        activeJoint.dampingRatio = dampingRatio;
        activeJoint.maxForce = maxForce;

        if (!keepCreatedJoint || !grabbed)
        {
            activeJoint.enabled = false;
        }
    }

    private void DisableJoint()
    {
        if (activeJoint == null)
        {
            return;
        }

        activeJoint.enabled = !jointWasCreated && activeJointWasEnabled;
    }

    private void ApplyThrow(Vector2 throwForce)
    {
        if (activeBody == null)
        {
            return;
        }

        if (throwForce != Vector2.zero)
        {
            activeBody.AddForce(throwForce, ForceMode2D.Impulse);
        }

        if (releaseVelocityLimit <= 0f || activeBody.linearVelocity.sqrMagnitude <= releaseVelocityLimit * releaseVelocityLimit)
        {
            return;
        }

        activeBody.linearVelocity = activeBody.linearVelocity.normalized * releaseVelocityLimit;
    }

    private void ReleaseWithoutThrow()
    {
        DisableJoint();
        RestoreFrozenRotations();
        ResumeRobotBehaviours();
        grabbed = false;

        if (jointWasCreated && activeJoint != null)
        {
            DestroyCreatedJoint();
        }

        ClearGrabContext();
    }

    private void ReleaseFrozenRotations()
    {
        rotationFrozenBodies.Clear();

        Rigidbody2D[] bodies = GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D body = bodies[i];
            if (body == null || !body.freezeRotation)
            {
                continue;
            }

            rotationFrozenBodies.Add(body);
            body.freezeRotation = false;
        }
    }

    private void RestoreFrozenRotations()
    {
        for (int i = 0; i < rotationFrozenBodies.Count; i++)
        {
            Rigidbody2D body = rotationFrozenBodies[i];
            if (body != null)
            {
                body.freezeRotation = true;
            }
        }

        rotationFrozenBodies.Clear();
    }

    private void DestroyCreatedJoint()
    {
        if (!jointWasCreated || activeJoint == null)
        {
            jointWasCreated = false;
            return;
        }

        Destroy(activeJoint);
        activeJoint = null;
        jointWasCreated = false;
    }

    private void ClearGrabContext()
    {
        sourceCollider = null;
        activeBody = null;
        activeJoint = null;
        activeJointWasEnabled = false;
    }
}
