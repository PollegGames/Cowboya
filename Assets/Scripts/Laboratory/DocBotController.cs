using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime behaviour states for the non-combat laboratory scientist.
/// </summary>
public enum DocBotActivity {
    Work = 0,
    CowardTemporary = 1,
    CowardForVisit = 2,
    Dead = 3
}

/// <summary>
/// Coordinates DocBot's health, fear, exchange, and future machine-work hooks
/// without enrolling the scientist in the worker combat/task brain.
/// </summary>
[DisallowMultipleComponent]
public sealed class DocBotController : MonoBehaviour {
    [Header("Robot")]
    [SerializeField] private RobotStateController stateController;
    [SerializeField] private HealthBot healthBot;
    [SerializeField] private JointBreaker jointBreaker;
    [SerializeField] private EnemyGrabbable enemyGrabbable;
    [SerializeField] private DocBotItemHolder itemHolder;
    [SerializeField] private DocBotHandReachController handReach;

    [Header("Behaviour")]
    [SerializeField, Min(1f)] private float maximumHealth = 20f;
    [SerializeField, Min(0f)] private float temporaryFearDuration = 10f;

    private LaboratoryProgress laboratoryProgress;
    private DocBotActivity currentActivity = DocBotActivity.Work;
    private float temporaryFearUntil;
    private Transform activeWorkAnchor;
    private bool initializedForVisit;
    private bool eventsSubscribed;

    public event Action<DocBotActivity> OnActivityChanged;
    public event Action<DocBotController> OnScientistDied;

    public RobotStateController StateController => stateController;
    public DocBotItemHolder ItemHolder => itemHolder;
    public DocBotActivity CurrentActivity => currentActivity;
    public bool IsWorkingAtMachine => activeWorkAnchor != null;
    public Transform ActiveWorkAnchor => activeWorkAnchor;
    public bool IsAlive => stateController != null
        && stateController.CurrentState == RobotState.Alive;
    public bool CanAcceptJunk => initializedForVisit
        && IsAlive
        && currentActivity == DocBotActivity.Work
        && !IsWorkingAtMachine
        && (enemyGrabbable == null || !enemyGrabbable.IsGrabbed)
        && itemHolder != null
        && !itemHolder.HasHeldJunk
        && laboratoryProgress != null
        && laboratoryProgress.HasActiveVisit
        && !laboratoryProgress.AcceptedJunkThisVisit;

    /// <summary>
    /// Assigns all root and interaction references created by the prefab builder.
    /// </summary>
    public void Configure(
        RobotStateController state,
        HealthBot health,
        JointBreaker breaker,
        EnemyGrabbable grabbable,
        DocBotItemHolder holder,
        DocBotHandReachController reach) {
        UnsubscribeEvents();
        stateController = state;
        healthBot = health;
        jointBreaker = breaker;
        enemyGrabbable = grabbable;
        itemHolder = holder;
        handReach = reach;
        EnsureBaselineStats();
        SubscribeEvents();
    }

    private void Awake() {
        CacheReferences();
        EnsureBaselineStats();
    }

    private void OnEnable() {
        CacheReferences();
        SubscribeEvents();
    }

    private void OnDisable() {
        UnsubscribeEvents();
    }

    private void Update() {
        if (currentActivity != DocBotActivity.CowardTemporary
            || (enemyGrabbable != null && enemyGrabbable.IsGrabbed)
            || Time.time < temporaryFearUntil) {
            return;
        }

        SetActivity(DocBotActivity.Work);
    }

    /// <summary>
    /// Connects this scene instance to the authoritative state of one visit.
    /// </summary>
    public void InitializeForVisit(LaboratoryProgress progress) {
        laboratoryProgress = progress;
        initializedForVisit = progress != null && progress.HasActiveVisit;
        activeWorkAnchor = null;
        temporaryFearUntil = 0f;

        if (!IsAlive) {
            SetActivity(DocBotActivity.Dead);
            laboratoryProgress?.TryMarkScientistDead();
            return;
        }

        DocBotActivity initialActivity = progress != null
            && progress.CurrentVisitDisposition == LaboratoryScientistDisposition.CowardForVisit
            ? DocBotActivity.CowardForVisit
            : DocBotActivity.Work;
        SetActivity(initialActivity);
    }

    /// <summary>
    /// Commits an already completed physical Junk transfer to visit progression.
    /// </summary>
    public bool TryCommitAcceptedJunk(JunkVariant variant) {
        return initializedForVisit
            && IsAlive
            && currentActivity == DocBotActivity.Work
            && !IsWorkingAtMachine
            && laboratoryProgress != null
            && laboratoryProgress.HasActiveVisit
            && !laboratoryProgress.AcceptedJunkThisVisit
            && laboratoryProgress.TryAcceptJunk(variant);
    }

    /// <summary>
    /// Reserves DocBot for a future machine behaviour. Movement and animation may
    /// be authored independently around the supplied anchor.
    /// </summary>
    public bool TryBeginMachineWork(Transform workAnchor) {
        if (workAnchor == null || !CanAcceptMachineWork()) {
            return false;
        }

        activeWorkAnchor = workAnchor;
        return true;
    }

    /// <summary>
    /// Releases the current machine assignment without changing visit disposition.
    /// </summary>
    public void EndMachineWork(Transform workAnchor = null) {
        if (workAnchor != null && workAnchor != activeWorkAnchor) {
            return;
        }

        activeWorkAnchor = null;
    }

    private bool CanAcceptMachineWork() {
        return initializedForVisit
            && IsAlive
            && currentActivity == DocBotActivity.Work
            && activeWorkAnchor == null
            && (enemyGrabbable == null || !enemyGrabbable.IsGrabbed);
    }

    private void HandleHealthChanged(float delta) {
        if (delta < 0f && IsAlive) {
            TriggerTemporaryFear();
        }
    }

    private void HandleGrabStarted(EnemyGrabbable grabbable) {
        _ = grabbable;
        TriggerTemporaryFear();
    }

    private void HandleGrabEnded(EnemyGrabbable grabbable) {
        _ = grabbable;
        if (IsAlive && currentActivity == DocBotActivity.CowardTemporary) {
            temporaryFearUntil = Time.time + temporaryFearDuration;
        }
    }

    private void TriggerTemporaryFear() {
        if (!IsAlive || currentActivity == DocBotActivity.CowardForVisit) {
            return;
        }

        EndMachineWork();
        temporaryFearUntil = Time.time + temporaryFearDuration;
        SetActivity(DocBotActivity.CowardTemporary);
    }

    private void HandleRobotStateChanged(RobotState state) {
        if (state != RobotState.Dead) {
            return;
        }

        EndMachineWork();
        handReach?.CancelReach();
        itemHolder?.ReleaseItemsForDeath();
        laboratoryProgress?.TryMarkScientistDead();
        SetActivity(DocBotActivity.Dead);
        OnScientistDied?.Invoke(this);
    }

    private void SetActivity(DocBotActivity activity) {
        if (currentActivity == activity) {
            return;
        }

        currentActivity = activity;
        OnActivityChanged?.Invoke(activity);
    }

    private void CacheReferences() {
        if (stateController == null) {
            stateController = GetComponent<RobotStateController>();
        }

        if (healthBot == null) {
            healthBot = GetComponent<HealthBot>();
        }

        if (jointBreaker == null) {
            jointBreaker = GetComponent<JointBreaker>();
        }

        if (enemyGrabbable == null) {
            enemyGrabbable = GetComponent<EnemyGrabbable>();
        }

        if (itemHolder == null) {
            itemHolder = GetComponent<DocBotItemHolder>();
        }

        if (handReach == null) {
            handReach = GetComponent<DocBotHandReachController>();
        }
    }

    private void EnsureBaselineStats() {
        if (stateController == null) {
            return;
        }

        RobotStats stats = new RobotStats(
            maximumHealth,
            maximumHealth,
            0f,
            0f,
            1f,
            0f,
            new List<Module>(),
            new List<Attack>()) {
            RobotName = "DocBot"
        };
        stateController.Stats = stats;
    }

    private void SubscribeEvents() {
        if (eventsSubscribed) {
            return;
        }

        if (stateController != null) {
            stateController.OnStateChanged += HandleRobotStateChanged;
        }

        if (healthBot != null) {
            healthBot.OnHealthChanged += HandleHealthChanged;
        }

        if (enemyGrabbable != null) {
            enemyGrabbable.OnGrabStarted += HandleGrabStarted;
            enemyGrabbable.OnGrabEnded += HandleGrabEnded;
        }

        eventsSubscribed = true;
    }

    private void UnsubscribeEvents() {
        if (!eventsSubscribed) {
            return;
        }

        if (stateController != null) {
            stateController.OnStateChanged -= HandleRobotStateChanged;
        }

        if (healthBot != null) {
            healthBot.OnHealthChanged -= HandleHealthChanged;
        }

        if (enemyGrabbable != null) {
            enemyGrabbable.OnGrabStarted -= HandleGrabStarted;
            enemyGrabbable.OnGrabEnded -= HandleGrabEnded;
        }

        eventsSubscribed = false;
    }
}
