using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class RestingMachine : BaseMachine
{
    [SerializeField] private Material materialOn;
    [SerializeField] private Material materialOff;
    [SerializeField] private float sendBackToWorkDelay = 3f;
    private Coroutine restCountdownCo;

    private MeshRenderer meshRenderer;
    private RobotBrain currentWorker;

    public event Action<RestingMachine, bool> OnMachineStateChanged;
    public event Action<RestingMachine, RobotBrain> OnMachineTurningOff;
    protected override void Awake()
    {
        base.Awake();
        meshRenderer = GetComponent<MeshRenderer>();
        ApplyMaterial();
    }

    private void ApplyMaterial()
    {
        if (meshRenderer == null) return;
        if (materialOn == null || materialOff == null) return;
        meshRenderer.material = isOn ? materialOn : materialOff;
    }

    public override void PowerOn()
    {
        base.PowerOn();
        ApplyMaterial();
        OnMachineStateChanged?.Invoke(this, true);
        // If a worker is already attached, (re)start their rest countdown.
        if (currentWorker != null)
        {
            SendWorkerToRest(currentWorker);
            StartRestCountdown(currentWorker);
        }
    }

    public override void PowerOff()
    {
        if (!isOn) return;
        CancelRestCountdown();

        SendCurrentWorkerToWork();
        OnMachineTurningOff?.Invoke(this, currentWorker);
        base.PowerOff();
        ApplyMaterial();
        OnMachineStateChanged?.Invoke(this, false);
        currentWorker = null;
    }

    public override void AttachRobot(GameObject robot)
    {
        var worker = robot.GetComponent<RobotBrain>();
        if (worker == null) return;

        if (!isOn)
        {
            SendWorkerToWork(worker);
            return;
        }
        // If already occupied, push the current one back to work
        if (currentWorker != null && currentWorker != worker)
        {
            CancelRestCountdown();
            SendWorkerToWork(currentWorker);
        }

        // Accept the new worker and start their rest
        currentWorker = worker;
        isOccupied = true;

        SendWorkerToRest(currentWorker);
        StartRestCountdown(currentWorker);


        base.AttachRobot(robot);
    }

    public override void ReleaseRobot()
    {
        CancelRestCountdown();
        SendCurrentWorkerToWork();
        isOccupied = false;
        base.ReleaseRobot();
        currentWorker = null;
    }

    private void SendWorkerToRest(RobotBrain worker)
    {
        if (worker == null) return;
        worker.OnMachineStateChanged(this, false);
    }

    private void SendWorkerToWork(RobotBrain worker)
    {
        if (worker == null) return;
        object payload = null;
        if (waypointService != null)
        {
            payload = waypointService.GetLeastUsedFreeWorkPoint();
            if (payload == null)
                payload = waypointService.GetWorkOrRestPoint();
            if (payload == null)
                payload = waypointService.GetStartPoint();
        }

        // Free the slot when dispatching the current worker so a new one can rest.
        if (worker == currentWorker)
        {
            CancelRestCountdown();
            currentWorker = null;
            isOccupied = false;
        }

        if (payload == null)
            payload = transform.position;
        worker.OnMachineStateChanged(payload, true);
    }

    private void SendCurrentWorkerToWork()
    {
        SendWorkerToWork(currentWorker);
    }


    private void StartRestCountdown(RobotBrain worker)
    {
        CancelRestCountdown();
        restCountdownCo = StartCoroutine(RestCountdown(worker));
    }

    private void CancelRestCountdown()
    {
        if (restCountdownCo != null)
        {
            StopCoroutine(restCountdownCo);
            restCountdownCo = null;
        }
    }
    private IEnumerator RestCountdown(RobotBrain worker)
    {
        float t = 0f;
        while (t < sendBackToWorkDelay)
        {
            // Abort if machine turns off or worker changes
            if (!isOn || worker == null || worker != currentWorker)
                yield break;

            t += Time.deltaTime;
            yield return null;
        }

        // Still valid? Send back to work and free the slot.
        if (isOn && worker != null && worker == currentWorker)
        {
            SendWorkerToWork(worker);
            currentWorker = null;
            isOccupied = false;
            restCountdownCo = null;
        }
    }
}
