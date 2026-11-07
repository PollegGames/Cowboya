using System;
using System.Collections.Generic;
using CowBoya.Robots;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyGrabbable : MonoBehaviour, IGrabbable, IGrabContextReceiver,
    IGrabControllerDetachReceiver
{
    [Header("Physics")]
    [SerializeField, Range(5f, 15f)] private float frequency = 10f;
    [SerializeField, Range(0f, 1f)] private float dampingRatio = 0.9f;
    [SerializeField, Min(0f)] private float maxForce = 2500f;
    [SerializeField, Min(0f), Tooltip("Caps grab force relative to the complete robot mass so light robots do not snap toward the hand.")]
    private float maximumGrabAcceleration = 70f;
    [SerializeField, Min(0f)] private float releaseVelocityLimit = 12f;

    [SerializeField, Min(0f), Tooltip("Seconds the released robot stays limp before its puppet binder resumes.")]
    private float releaseRagdollSeconds = 0.4f;

    [Header("Holder Collisions")]
    [SerializeField, Tooltip("While held, the robot does not collide with the body of whoever holds it. "
        + "Without this, pushing the held robot against your own body propels the holder.")]
    private bool ignoreHolderCollisions = true;
    [SerializeField, Min(0f), Tooltip("Seconds after release before collisions with the former holder come back.")]
    private float holderCollisionRestoreDelay = 0.35f;
    [SerializeField, Min(0f), Tooltip("Longest wait for the two bodies to separate before collisions are restored anyway.")]
    private float holderCollisionRestoreTimeout = 2f;

    [Header("Robot Systems")]
    [SerializeField] private Behaviour[] extraBehavioursToPause;
    [SerializeField] private bool resetIntentOnRelease = true;

    private readonly List<PausedBehaviour> pausedBehaviours = new List<PausedBehaviour>();
    private readonly List<PausedBehaviour> pendingReleaseRestores = new List<PausedBehaviour>();
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
    private int releaseRestoreFrame = -1;
    private readonly List<(Collider2D Own, Collider2D Holder)> ignoredHolderPairs = new List<(Collider2D, Collider2D)>();
    private float holderCollisionRestoreTime = -1f;
    private float holderCollisionRestoreDeadline = -1f;

    public event Action<EnemyGrabbable> OnGrabStarted;
    public event Action<EnemyGrabbable> OnGrabEnded;

    public bool IsGrabbed => grabbed;

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
        RestorePendingReleaseBehaviours();
        RestoreHolderCollisions();

        if (grabbed)
        {
            EndGrab(
                applyThrow: false,
                throwForce: Vector2.zero,
                restoreRobotIntent: false,
                delayReleasePhysicsRestore: false);
        }
    }

    private void Update()
    {
        if (releaseRestoreFrame >= 0 && Time.frameCount >= releaseRestoreFrame)
        {
            RestorePendingReleaseBehaviours();
        }

        if (!grabbed && holderCollisionRestoreTime >= 0f && Time.time >= holderCollisionRestoreTime)
        {
            // Wait until the bodies are apart so restoring collisions does not pop them.
            if (Time.time < holderCollisionRestoreDeadline && AnyIgnoredPairOverlaps())
                holderCollisionRestoreTime = Time.time + 0.1f;
            else
                RestoreHolderCollisions();
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
            && (stateController == null || stateController.CurrentState != RobotState.Dead);
    }

    public void OnGrab(Transform grabParent)
    {
        if (grabbed)
        {
            return;
        }

        if (grabParent == null)
        {
            Debug.LogWarning($"{nameof(EnemyGrabbable)} received a null grab parent.", this);
            return;
        }

        CacheReferences();
        RestorePendingReleaseBehaviours();
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
        IgnoreHolderCollisions(grabParent);
        EnableJoint(grabParent.position);
        OnGrabStarted?.Invoke(this);
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
        EndGrab(
            applyThrow: true,
            throwForce: throwForce,
            restoreRobotIntent: true,
            delayReleasePhysicsRestore: true);
    }

    /// <summary>
    /// Ends the current grip without applying throw force or invoking OnRelease.
    /// </summary>
    public void OnDetachedFromGrabController()
    {
        EndGrab(
            applyThrow: false,
            throwForce: Vector2.zero,
            restoreRobotIntent: true,
            delayReleasePhysicsRestore: true);
    }

    public void SetGrabContext(Collider2D sourceCollider, Vector2 grabOrigin)
    {
        _ = grabOrigin;
        this.sourceCollider = sourceCollider;
    }

    /// <summary>
    /// Assigns additional robot-specific behaviours that must pause while held.
    /// </summary>
    public void ConfigureExtraBehaviours(params Behaviour[] behaviours) {
        extraBehavioursToPause = behaviours ?? new Behaviour[0];
    }

    /// <summary>
    /// Returns whether a behaviour is part of this grabbable's authored pause set.
    /// </summary>
    public bool PausesBehaviour(Behaviour behaviour) {
        if (behaviour == null || extraBehavioursToPause == null)
        {
            return false;
        }

        for (int i = 0; i < extraBehavioursToPause.Length; i++)
        {
            if (extraBehavioursToPause[i] == behaviour)
            {
                return true;
            }
        }

        return false;
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
        PausePuppetBinders();

        if (extraBehavioursToPause == null)
        {
            return;
        }

        for (int i = 0; i < extraBehavioursToPause.Length; i++)
        {
            PauseBehaviour(extraBehavioursToPause[i]);
        }
    }

    private void PausePuppetBinders()
    {
        SimplePuppetBinder[] binders = GetComponentsInChildren<SimplePuppetBinder>(true);
        for (int i = 0; i < binders.Length; i++)
        {
            PauseBehaviour(binders[i]);
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

    private void ResumeRobotBehaviours(bool delayReleasePhysicsRestore)
    {
        for (int i = 0; i < pausedBehaviours.Count; i++)
        {
            PausedBehaviour paused = pausedBehaviours[i];
            if (delayReleasePhysicsRestore && IsReleasePhysicsDriver(paused.Behaviour))
            {
                pendingReleaseRestores.Add(paused);
                continue;
            }

            if (paused.Behaviour != null)
            {
                paused.Behaviour.enabled = paused.WasEnabled;
            }
        }

        pausedBehaviours.Clear();

        if (pendingReleaseRestores.Count > 0)
        {
            // Stay limp a little after release; the binder then blends back in (see SimplePuppetBinder recovery).
            int ragdollFrames = Mathf.Max(1, Mathf.CeilToInt(releaseRagdollSeconds / Mathf.Max(Time.deltaTime, 0.001f)));
            releaseRestoreFrame = Time.frameCount + ragdollFrames;
        }
    }

    private static bool IsReleasePhysicsDriver(Behaviour behaviour)
    {
        return behaviour is SimplePuppetBinder
            || behaviour is RobotBodyController
            || behaviour is CollectorRobotBodyController;
    }

    private void RestorePendingReleaseBehaviours()
    {
        bool robotIsDead = stateController != null
            && stateController.CurrentState == RobotState.Dead;

        for (int i = 0; i < pendingReleaseRestores.Count; i++)
        {
            PausedBehaviour pending = pendingReleaseRestores[i];
            if (pending.Behaviour != null
                && pending.Behaviour is SimplePuppetBinder)
            {
                pending.Behaviour.enabled = robotIsDead ? false : pending.WasEnabled;
            }
        }

        for (int i = 0; i < pendingReleaseRestores.Count; i++)
        {
            PausedBehaviour pending = pendingReleaseRestores[i];
            if (pending.Behaviour != null
                && !(pending.Behaviour is SimplePuppetBinder))
            {
                pending.Behaviour.enabled = pending.WasEnabled;
            }
        }

        pendingReleaseRestores.Clear();
        releaseRestoreFrame = -1;
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
        activeJoint.maxForce = CalculateMaximumJointForce();

        if (!keepCreatedJoint || !grabbed)
        {
            activeJoint.enabled = false;
        }
    }

    private float CalculateMaximumJointForce()
    {
        if (maxForce <= 0f || maximumGrabAcceleration <= 0f)
        {
            return 0f;
        }

        float totalDynamicMass = 0f;
        Rigidbody2D[] bodies = GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D body = bodies[i];
            if (body != null && body.simulated && body.bodyType == RigidbodyType2D.Dynamic)
            {
                totalDynamicMass += body.mass;
            }
        }

        if (totalDynamicMass <= 0f && activeBody != null)
        {
            totalDynamicMass = activeBody.mass;
        }

        return Mathf.Min(maxForce, totalDynamicMass * maximumGrabAcceleration);
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

    private void EndGrab(
        bool applyThrow,
        Vector2 throwForce,
        bool restoreRobotIntent,
        bool delayReleasePhysicsRestore)
    {
        if (!grabbed)
        {
            return;
        }

        grabbed = false;
        DisableJoint();
        if (applyThrow)
        {
            ApplyThrow(throwForce);
        }

        RestoreFrozenRotations();
        ResumeRobotBehaviours(delayReleasePhysicsRestore);
        ScheduleHolderCollisionRestore(delayReleasePhysicsRestore);

        if (stateController != null && stateController.CurrentState == RobotState.Dead)
        {
            stateController.ReapplyDeathState();
        }
        else if (restoreRobotIntent
            && (stateController == null || stateController.CurrentState == RobotState.Alive))
        {
            RestartRobotIntent();
        }

        if (jointWasCreated && activeJoint != null)
        {
            DestroyCreatedJoint();
        }

        ClearGrabContext();
        OnGrabEnded?.Invoke(this);
    }

    private void IgnoreHolderCollisions(Transform grabParent)
    {
        RestoreHolderCollisions();
        if (!ignoreHolderCollisions || grabParent == null)
        {
            return;
        }

        CowboyGrabController holder = grabParent.GetComponentInParent<CowboyGrabController>();
        Transform holderRoot = holder != null ? holder.transform.root : grabParent.root;
        if (holderRoot == null || holderRoot == transform.root)
        {
            return;
        }

        Collider2D[] ownColliders = GetComponentsInChildren<Collider2D>(true);
        Collider2D[] holderColliders = holderRoot.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D own in ownColliders)
        {
            if (own == null || own.isTrigger)
                continue;

            foreach (Collider2D other in holderColliders)
            {
                if (other == null || other.isTrigger || Physics2D.GetIgnoreCollision(own, other))
                    continue;

                Physics2D.IgnoreCollision(own, other, true);
                ignoredHolderPairs.Add((own, other));
            }
        }
    }

    private void ScheduleHolderCollisionRestore(bool delayed)
    {
        if (ignoredHolderPairs.Count == 0)
        {
            return;
        }

        if (!delayed || holderCollisionRestoreDelay <= 0f)
        {
            RestoreHolderCollisions();
            return;
        }

        holderCollisionRestoreTime = Time.time + holderCollisionRestoreDelay;
        holderCollisionRestoreDeadline = Time.time + Mathf.Max(holderCollisionRestoreDelay, holderCollisionRestoreTimeout);
    }

    private bool AnyIgnoredPairOverlaps()
    {
        foreach ((Collider2D own, Collider2D other) in ignoredHolderPairs)
        {
            if (own != null && other != null && own.isActiveAndEnabled && other.isActiveAndEnabled
                && own.Distance(other).isOverlapped)
            {
                return true;
            }
        }

        return false;
    }

    private void RestoreHolderCollisions()
    {
        foreach ((Collider2D own, Collider2D other) in ignoredHolderPairs)
        {
            if (own != null && other != null)
                Physics2D.IgnoreCollision(own, other, false);
        }

        ignoredHolderPairs.Clear();
        holderCollisionRestoreTime = -1f;
        holderCollisionRestoreDeadline = -1f;
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

        if (Application.isPlaying)
        {
            Destroy(activeJoint);
        }
        else
        {
            DestroyImmediate(activeJoint);
        }
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
