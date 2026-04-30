using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public sealed class RestingMachine : BaseMachine
{
    [Header("Visuals")]
    [SerializeField] private Material materialOn;
    [SerializeField] private Material materialOff;

    [Header("Behavior")]
    [Min(0f)]
    [SerializeField] private float sendBackToWorkDelay = 6f;

    [Header("Debug")]
    [SerializeField] private bool logRestingMachine = false;

    private MeshRenderer meshRenderer;
    private Coroutine restCountdownCo;
    private RobotBrainNew currentWorker;

    public RobotBrainNew CurrentWorker => currentWorker;
    public event Action<RestingMachine, RobotBrainNew> OnWorkerAttached;
    protected override void Awake()
    {
        base.Awake();
        meshRenderer = GetComponent<MeshRenderer>();
        RefreshVisual();
    }

    public override void PowerOn()
    {
        if (isOn) return;

        base.PowerOn();
        RefreshVisual();

        // If a worker is already attached while powering on, start the timer.
        if (currentWorker != null)
            StartRestCountdown(currentWorker);
    }

    public override void PowerOff()
    {
        if (!isOn) return;

        CancelRestCountdown();
        var releasedWorker = currentWorker;
        Debug.Log(
            $"[RestingMachine] PowerOff begin machine={name} worker={(releasedWorker != null ? releasedWorker.name : "none")} occupied={isOccupied}",
            this);

        base.PowerOff();

        if (isOccupied)
            ReleaseRobot();

        if (releasedWorker != null)
            NotifyWorkerReleased(releasedWorker, this, "power_off");

        RefreshVisual();

        ClearWorkerSlot();
    }

    public override void AttachRobot(GameObject robot)
    {
        if (!TryGetWorker(robot, out var worker))
            return;

        LogAttachAttempt(worker);

        // Already same worker while on -> no-op.
        if (isOn && ReferenceEquals(currentWorker, worker))
            return;

        if (!CanAcceptWorker(worker))
        {
            LogReject(worker);
            return;
        }

        SetWorkerSlot(worker);

        OnWorkerAttached?.Invoke(this, currentWorker);
        StartRestCountdown(currentWorker);

        base.AttachRobot(robot);
    }

    public override void ReleaseRobot()
    {
        if (!isOccupied && currentWorker == null)
            return;

        CancelRestCountdown();
        base.ReleaseRobot();
        ClearWorkerSlot();
    }

    public void ReleaseWorker(RobotBrainNew worker)
    {
        if (worker == null || !ReferenceEquals(worker, currentWorker))
            return;

        if (logRestingMachine)
            Debug.Log($"[RestingMachine] ReleaseWorker worker={worker.name}", this);

        ReleaseRobot();
    }

    public override bool TryAttachWorker(RobotBrainNew worker, string reason)
    {
        _ = reason;
        if (worker == null || !isOn)
            return false;
        if (ReferenceEquals(currentWorker, worker))
            return false;
        if (currentWorker != null)
            return false;
        if (!CanAcceptWorker(worker))
            return false;

        AttachRobot(worker.gameObject);
        NotifyWorkerAttached(worker, this);
        return true;
    }

    public override bool TryReplaceWorker(RobotBrainNew incoming, string reason)
    {
        if (incoming == null || !isOn)
            return false;
        if (ReferenceEquals(currentWorker, incoming))
            return false;
        if (currentWorker == null)
            return TryAttachWorker(incoming, reason);

        var previous = currentWorker;
        ReplaceWorkerInPlace(incoming);
        NotifyWorkerAttached(incoming, this);
        NotifyWorkerReleased(previous, this, "replaced");
        return true;
    }

    public override bool TryReleaseWorker(RobotBrainNew worker, string reason)
    {
        if (worker == null || !ReferenceEquals(currentWorker, worker))
            return false;

        ReleaseWorker(worker);
        NotifyWorkerReleased(worker, this, reason);
        return true;
    }

    public bool CanAcceptWorker(RobotBrainNew worker)
    {
        if (worker == null || !isOn) return false;
        if (worker.Heart == null) return false;
        if (worker.Heart.Role != RobotRole.Worker && worker.Heart.Role != RobotRole.SecurityGuard) return false;
        if (currentWorker != null && !ReferenceEquals(currentWorker, worker)) return false;
        return true;
    }

    private static bool TryGetWorker(GameObject robot, out RobotBrainNew worker)
    {
        worker = null;
        if (robot == null) return false;
        return robot.TryGetComponent(out worker) && worker != null;
    }

    private void SetWorkerSlot(RobotBrainNew worker)
    {
        currentWorker = worker;
        isOccupied = worker != null;
    }

    private void ReplaceWorkerInPlace(RobotBrainNew incoming)
    {
        if (incoming == null)
            return;

        SetWorkerSlot(incoming);
        base.AttachRobot(incoming.gameObject);
        OnWorkerAttached?.Invoke(this, currentWorker);
        StartRestCountdown(currentWorker);
    }

    private void ClearWorkerSlot()
    {
        currentWorker = null;
        isOccupied = false;
    }

    private void RefreshVisual()
    {
        if (meshRenderer == null) return;
        if (materialOn == null || materialOff == null) return;

        // material instantiates; use sharedMaterial to avoid per-instance allocations.
        meshRenderer.sharedMaterial = isOn ? materialOn : materialOff;
    }

    private void StartRestCountdown(RobotBrainNew worker)
    {
        CancelRestCountdown();
        if (worker == null) return;

        restCountdownCo = StartCoroutine(RestCountdown(worker));
    }

    private void CancelRestCountdown()
    {
        if (restCountdownCo == null) return;
        StopCoroutine(restCountdownCo);
        restCountdownCo = null;
    }

    private IEnumerator RestCountdown(RobotBrainNew worker)
    {
        var elapsed = 0f;

        while (elapsed < sendBackToWorkDelay)
        {
            if (!IsCountdownStillValid(worker))
            {
                restCountdownCo = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!IsCountdownStillValid(worker))
        {
            restCountdownCo = null;
            yield break;
        }

        TryReleaseWorker(worker, "rest_done");
        restCountdownCo = null;
    }

    private bool IsCountdownStillValid(RobotBrainNew worker)
        => isOn && worker != null && ReferenceEquals(worker, currentWorker);

    private void LogAttachAttempt(RobotBrainNew worker)
    {
        if (!logRestingMachine) return;

        Debug.Log(
            $"[RestingMachine] AttachRobot worker={worker.name} isOn={isOn} currentWorker={(currentWorker != null ? currentWorker.name : "null")}",
            this
        );
    }

    private void LogReject(RobotBrainNew worker)
    {
        if (!logRestingMachine) return;

        Debug.Log(
            $"[RestingMachine] Rejecting worker={worker.name} isOn={isOn} occupied={(currentWorker != null)}",
            this
        );
    }
}



