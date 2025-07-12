using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class RestingMachine : BaseMachine
{
    [SerializeField] private Material materialOn;
    [SerializeField] private Material materialOff;

    private MeshRenderer meshRenderer;
    private EnemyWorkerController currentWorker;
    private IWaypointService waypointService;

    public void Initialize(IWaypointService service)
    {
        waypointService = service;
    }

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
    }

    public override void PowerOff()
    {
        if (!isOn) return;
        SendCurrentWorkerToWork();
        base.PowerOff();
        ApplyMaterial();
    }

    public override void AttachRobot(GameObject robot)
    {
        var worker = robot.GetComponent<EnemyWorkerController>();
        if (worker == null) return;

        if (!isOn)
        {
            SendWorkerToWork(worker);
            return;
        }

        SendWorkerToWork(currentWorker);
        SendWorkerToRest(worker);
        currentWorker = worker;
        isOccupied = true;
        OnRobotAssigned?.Invoke(this);
    }

    public override void ReleaseRobot()
    {
        SendCurrentWorkerToWork();
        isOccupied = false;
        base.ReleaseRobot();
    }

    private void SendWorkerToRest(EnemyWorkerController worker)
    {
        if (worker == null) return;
        worker.stateMachine.ChangeState(
            new Worker_Resting(worker, worker.stateMachine, worker.waypointService));
    }

    private void SendWorkerToWork(EnemyWorkerController worker)
    {
        if (worker == null) return;
        worker.stateMachine.ChangeState(
            new Worker_GoingToLeastWorkedStation(worker, worker.stateMachine, worker.waypointService));
    }

    private void SendCurrentWorkerToWork()
    {
        SendWorkerToWork(currentWorker);
        currentWorker = null;
    }
}
