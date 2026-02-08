using System;
using System.Collections;
using UnityEngine;

public class SpawningMachine : BaseMachine
{
    [SerializeField] private Material materialOn;
    [SerializeField] private Material materialOff;

    private MeshRenderer meshRenderer;

    public event Action<SpawningMachine, bool> OnMachineStateChanged;
    public event Action<SpawningMachine, RobotBrain> OnMachineTurningOff;

    [Header("Spawning Settings")]
    [SerializeField] private float spawnInterval = 30f;
    [SerializeField] private FactoryAlarmStatus factoryAlarmStatus;

    private Coroutine spawnCoroutine;

    private RobotBrain currentWorker;
    private RobotStateController currentWorkerState;

    public bool HasWorker => currentWorker != null;
    public RobotBrain CurrentWorker => currentWorker;

    private IEnemiesSpawner enemiesSpawner;
    private MachineSecurityManager securityManager;
    protected override void Awake()
    {
        base.Awake();
        meshRenderer = GetComponent<MeshRenderer>();
        ApplyMaterial();
    }

    private void OnEnable()
    {
        if (factoryAlarmStatus != null)
            factoryAlarmStatus.OnAlarmStateChanged += HandleAlarmChanged;

        TryStartSpawning();
    }

    private void OnDisable()
    {
        if (factoryAlarmStatus != null)
            factoryAlarmStatus.OnAlarmStateChanged -= HandleAlarmChanged;

        StopSpawning();
        UnsubscribeFromWorkerState();
    }

    private void Update()
    {
        if (factoryAlarmStatus.CurrentAlarmState == AlarmState.Wanted)
            TryStartSpawning();
    }
    public void InitializeSpawner(IEnemiesSpawner enemiesSpawner)
    {
        if (enemiesSpawner == null)
        {
            Debug.LogError("SpawningMachine: EnemiesSpawner reference is missing.");
            return;
        }

        this.enemiesSpawner = enemiesSpawner;
    }

    public void InitializeSecurityManager(MachineSecurityManager manager)
    {
        securityManager = manager;
    }

    public override void PowerOn()
    {
        base.PowerOn();
        TryStartSpawning();
        ApplyMaterial();
        OnMachineStateChanged?.Invoke(this, true);
    }

    public override void PowerOff()
    {
        if (!isOn) return;

        StopSpawning();
        UnsubscribeFromWorkerState();
        SendWorkerToStart(currentWorker);
        OnMachineTurningOff?.Invoke(this, currentWorker);
        base.PowerOff();
        ApplyMaterial();
        OnMachineStateChanged?.Invoke(this, false);
        currentWorker = null;
    }


    private void ApplyMaterial()
    {
        if (meshRenderer == null) return;
        if (materialOn == null || materialOff == null) return;
        meshRenderer.material = isOn ? materialOn : materialOff;
    }

    private void HandleAlarmChanged(AlarmState state)
    {
        if (state == AlarmState.Wanted && isOn)
            TryStartSpawning();
        else
            StopSpawning();
    }

    /// <summary>
    /// Called when a worker arrives at this machine.
    /// Sends workers to the appropriate state based on machine status and type.
    /// </summary>
    public override void AttachRobot(GameObject robot)
    {
        var newWorker = robot.GetComponent<RobotBrain>();
        if (newWorker == null) return;

        if (!isOn)
        {
            SendWorkerToStart(newWorker);
            return;
        }

        if (currentWorker == null)
        {
            currentWorker = newWorker;
            SubscribeToWorkerState(currentWorker);
            SetWorkerToSpawn(currentWorker);
            base.AttachRobot(robot);
            TryStartSpawning();
        }
    }

    private void SetWorkerToSpawn(RobotBrain worker)
    {
        if (worker == null) return;
        worker.OnMachineStateChanged(this, true);
    }

    /// <summary>
    /// Helper to send a worker to the rest station state.
    /// </summary>
    private void SendWorkerToStart(RobotBrain worker)
    {
        if (worker == null) return;
        worker.OnMachineStateChanged(this, false);
    }

    public override void ReleaseRobot()
    {
        SendWorkerToStart(currentWorker);
        isOccupied = false;
        base.ReleaseRobot();
        UnsubscribeFromWorkerState();
        currentWorker = null;
    }

    private void TryStartSpawning()
    {
        if (spawnCoroutine == null && isOn && factoryAlarmStatus.CurrentAlarmState == AlarmState.Wanted
        && HasAliveWorker())
        {
            spawnCoroutine = StartCoroutine(SpawnLoop());
        }
    }

    private void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            SpawnFollower();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnFollower()
    {
        if (!isOn || factoryAlarmStatus.CurrentAlarmState != AlarmState.Wanted || !HasAliveWorker())
            return;
        var spawnPos = trigger.transform.position;
        var lastVisitedPoint = waypointService.GetClosestWaypoint(spawnPos, includeUnavailable: true);
        enemiesSpawner.CreateAndSpawnFollowerGuard(lastVisitedPoint, factoryAlarmStatus);

    }

    private void SubscribeToWorkerState(RobotBrain worker)
    {
        UnsubscribeFromWorkerState();
        if (worker == null)
            return;
        currentWorkerState = worker.GetComponent<RobotStateController>();
        if (currentWorkerState != null)
            currentWorkerState.OnStateChanged += HandleWorkerStateChanged;
    }

    private void UnsubscribeFromWorkerState()
    {
        if (currentWorkerState != null)
            currentWorkerState.OnStateChanged -= HandleWorkerStateChanged;
        currentWorkerState = null;
    }

    private void HandleWorkerStateChanged(RobotState newState)
    {
        if (newState != RobotState.Dead)
            return;

        StopSpawning();
        base.ReleaseRobot();
        currentWorker = null;
        UnsubscribeFromWorkerState();
    }

    private bool HasAliveWorker()
    {
        return currentWorker != null && currentWorkerState != null && currentWorkerState.CurrentState == RobotState.Alive;
    }
}
