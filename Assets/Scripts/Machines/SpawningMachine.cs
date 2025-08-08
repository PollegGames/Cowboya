using System;
using System.Collections;
using UnityEngine;

public class SpawningMachine : BaseMachine
{
    [SerializeField] private Material materialOn;
    [SerializeField] private Material materialOff;

    private MeshRenderer meshRenderer;

    public event Action<SpawningMachine, bool> OnMachineStateChanged;
    public event Action<SpawningMachine, EnemyWorkerController> OnMachineTurningOff;

    [Header("Spawning Settings")]
    [SerializeField] private float spawnInterval = 30f;
    [SerializeField] private FactoryAlarmStatus factoryAlarmStatus;

    private Coroutine spawnCoroutine;

    private EnemyWorkerController currentWorker;

    public bool HasWorker => currentWorker != null;
    public EnemyWorkerController CurrentWorker => currentWorker;

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

        if (securityManager != null)
            securityManager.OnSecurityMachineTurnedOff -= HandleSecurityMachineTurnedOff;

        StopSpawning();
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
        if (securityManager != null)
            securityManager.OnSecurityMachineTurnedOff += HandleSecurityMachineTurnedOff;
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
        var newWorker = robot.GetComponent<EnemyWorkerController>();
        if (newWorker == null) return;

        if (!isOn)
        {
            SendWorkerToStart(newWorker);
            return;
        }

        if (currentWorker == null)
        {
            currentWorker = newWorker;
            SetWorkerToSpawn(currentWorker);
            base.AttachRobot(robot);
        }
    }

    private void SetWorkerToSpawn(EnemyWorkerController worker)
    {
        if (worker == null) return;

        worker.stateMachine.ChangeState(
            new Worker_Spawning(worker, worker.stateMachine, worker.waypointService));
    }

    /// <summary>
    /// Helper to send a worker to the rest station state.
    /// </summary>
    private void SendWorkerToStart(EnemyWorkerController worker)
    {
        if (worker == null) return;
        worker.stateMachine.ChangeState(
            new Worker_GoingToRestStation(worker, worker.stateMachine, worker.waypointService));
    }

    public override void ReleaseRobot()
    {
        SendWorkerToStart(currentWorker);
        isOccupied = false;
        base.ReleaseRobot();
        currentWorker = null;
    }

    private void TryStartSpawning()
    {
        if (spawnCoroutine == null && isOn && factoryAlarmStatus.CurrentAlarmState == AlarmState.Wanted
        && currentWorker != null)
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
        if (!isOn || factoryAlarmStatus.CurrentAlarmState != AlarmState.Wanted)
            return;
        var spawnPos = trigger.transform.position;
        var lastVisitedPoint = waypointService.GetClosestWaypoint(spawnPos, includeUnavailable: true);
        enemiesSpawner.CreateAndSpawnFollowerGuard(lastVisitedPoint, factoryAlarmStatus);

        Debug.Log("[SpawningMachine] Enemy spawned and sent to Follower state.");
    }

    private void HandleSecurityMachineTurnedOff(SecurityMachine machine)
    {
        if (!isOn || enemiesSpawner == null)
            return;

        var spawnPos = trigger.transform.position;
        var lastVisitedPoint = waypointService.GetClosestWaypoint(spawnPos, includeUnavailable: true);
        enemiesSpawner.CreateAndSpawnSecurityGuard(lastVisitedPoint, machine);

    }
}
