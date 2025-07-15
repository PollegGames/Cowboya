using System;
using System.Collections;
using UnityEngine;

public class SpawningMachine : BaseMachine
{
    [SerializeField] private Material materialOn;
    [SerializeField] private Material materialOff;

    private MeshRenderer meshRenderer;

    public event Action<SpawningMachine, bool> OnMachineStateChanged;

    [Header("Spawning Settings")]
    [SerializeField] private float spawnInterval = 30f;
    [SerializeField] private FactoryAlarmStatus factoryAlarmStatus;

    private Coroutine spawnCoroutine;

    private EnemyWorkerController currentWorker;

    public bool HasWorker => currentWorker != null;
    public EnemyWorkerController CurrentWorker => currentWorker;

    private IEnemiesSpawner enemiesSpawner;
    protected override void Awake()
    {
        base.Awake();

        if (factoryAlarmStatus == null)
            Debug.LogError("SpawningMachine: FactoryAlarmStatus not assigned.");
        if (enemiesSpawner == null)
            Debug.LogError("SpawningMachine: EnemiesSpawner not assigned.");
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
    }

    public void Initialize(IEnemiesSpawner enemiesSpawner)
    {
        if (enemiesSpawner == null)
        {
            Debug.LogError("SpawningMachine: EnemiesSpawner reference is missing.");
            return;
        }

        this.enemiesSpawner = enemiesSpawner;
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
        SendWorkerToRest(currentWorker);
        base.PowerOff();
        ApplyMaterial();
        OnMachineStateChanged?.Invoke(this, false);
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
            SendWorkerToRest(newWorker);
            return;
        }

        if (currentWorker == null)
        {
            currentWorker = newWorker;
        }
        SetWorkerToSpawn(currentWorker);
        base.AttachRobot(robot);
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
    private void SendWorkerToRest(EnemyWorkerController worker)
    {
        if (worker == null) return;
        worker.stateMachine.ChangeState(
            new Worker_GoingToRestStation(worker, worker.stateMachine, worker.waypointService));
    }

    public override void ReleaseRobot()
    {
        SendWorkerToRest(currentWorker);
        isOccupied = false;
        base.ReleaseRobot();
    }

    private void TryStartSpawning()
    {
        if (spawnCoroutine == null && isOn && factoryAlarmStatus.CurrentAlarmState == AlarmState.Wanted)
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
            SpawnEnemy();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnEnemy()
    {
        if (!isOn || factoryAlarmStatus.CurrentAlarmState != AlarmState.Wanted)
            return;

        GameObject enemyGO = enemiesSpawner.CreateAngGetFollowerGuard();
        var spawnPos = transform.position;
        var lastVisitedPoint = waypointService.GetClosestWaypoint(spawnPos);
        enemyGO.transform.position = lastVisitedPoint.WorldPos;
        var ec = enemyGO.GetComponent<EnemyController>();
        if (ec != null)
        {
            ec.SetFollowerState(factoryAlarmStatus);
            ec.memory.SetLastVisitedPoint(lastVisitedPoint);
        }

        // 2) turn it on
        enemyGO.SetActive(true);
        Debug.Log("[SpawningMachine] Enemy spawned and sent to Follower state.");
    }
}
