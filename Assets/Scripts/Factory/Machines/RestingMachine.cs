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
    [SerializeField] private float sendBackToWorkDelay = 3f;

    [Header("Debug")]
    [SerializeField] private bool logRestingMachine = false;

    private MeshRenderer meshRenderer;
    private Coroutine restCountdownCo;
    private RobotBrain currentWorker;

    public RobotBrain CurrentWorker => currentWorker;

    public event Action<RestingMachine, bool> OnMachineStateChanged;
    public event Action<RestingMachine, RobotBrain> OnMachineTurningOff;
    public event Action<RestingMachine, RobotBrain> OnWorkerAttached;
    public event Action<RestingMachine, RobotBrain> OnRestTimerCompleted;
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
        RaiseStateChanged(true);

        // If a worker is already attached while powering on, start the timer.
        if (currentWorker != null)
            StartRestCountdown(currentWorker);
    }

    public override void PowerOff()
    {
        if (!isOn) return;

        CancelRestCountdown();

        // Preserve old behavior: worker still visible to listeners at this point.
        OnMachineTurningOff?.Invoke(this, currentWorker);

        if (isOccupied)
            base.ReleaseRobot();

        base.PowerOff();
        RefreshVisual();

        // Preserve old behavior: CurrentWorker still readable here.
        RaiseStateChanged(false);

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
        CancelRestCountdown();
        base.ReleaseRobot();
        ClearWorkerSlot();
    }

    public void ReleaseWorker(RobotBrain worker)
    {
        if (worker == null || !ReferenceEquals(worker, currentWorker))
            return;

        if (logRestingMachine)
            Debug.Log($"[RestingMachine] ReleaseWorker worker={worker.name}", this);

        ReleaseRobot();
    }

    public bool CanAcceptWorker(RobotBrain worker)
    {
        if (worker == null || !isOn) return false;
        if (currentWorker != null && !ReferenceEquals(currentWorker, worker)) return false;
        return true;
    }

    private static bool TryGetWorker(GameObject robot, out RobotBrain worker)
    {
        worker = null;
        if (robot == null) return false;
        return robot.TryGetComponent(out worker) && worker != null;
    }

    private void SetWorkerSlot(RobotBrain worker)
    {
        currentWorker = worker;
        isOccupied = worker != null;
    }

    private void ClearWorkerSlot()
    {
        currentWorker = null;
        isOccupied = false;
    }

    private void RaiseStateChanged(bool on) => OnMachineStateChanged?.Invoke(this, on);

    private void RefreshVisual()
    {
        if (meshRenderer == null) return;
        if (materialOn == null || materialOff == null) return;

        // material instantiates; use sharedMaterial to avoid per-instance allocations.
        meshRenderer.sharedMaterial = isOn ? materialOn : materialOff;
    }

    private void StartRestCountdown(RobotBrain worker)
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

    private IEnumerator RestCountdown(RobotBrain worker)
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

        OnRestTimerCompleted?.Invoke(this, worker);
        restCountdownCo = null;
    }

    private bool IsCountdownStillValid(RobotBrain worker)
        => isOn && worker != null && ReferenceEquals(worker, currentWorker);

    private void LogAttachAttempt(RobotBrain worker)
    {
        if (!logRestingMachine) return;

        Debug.Log(
            $"[RestingMachine] AttachRobot worker={worker.name} isOn={isOn} currentWorker={(currentWorker != null ? currentWorker.name : "null")}",
            this
        );
    }

    private void LogReject(RobotBrain worker)
    {
        if (!logRestingMachine) return;

        Debug.Log(
            $"[RestingMachine] Rejecting worker={worker.name} isOn={isOn} occupied={(currentWorker != null)}",
            this
        );
    }
}
