using UnityEngine;

/// <summary>
/// Moves a worker spawner to its reserved spawning machine then
/// switches to <see cref="Worker_Spawning"/>.
/// </summary>
public class Worker_GoingToSpawningMachine : WorkerState
{
    private RoomWaypoint targetPoint;
    private bool hasArrived;

    public Worker_GoingToSpawningMachine(EnemyWorkerController enemy,
                                         WorkerStateMachine machine,
                                         IWaypointService waypointService)
        : base(enemy, machine, waypointService)
    {
    }

    public override void EnterState()
    {
        enemy.workerState = WorkerStatus.GoingToSpawningMachine;
        targetPoint = waypointService.GetBlockedRoomSecuritySpawning(enemy.memory.LastVisitedPoint);
        enemy.SetDestination(targetPoint, includeUnavailable: true);
        hasArrived = false;
    }

    public override void UpdateState()
    {
        if (hasArrived) return;

        if (enemy.HasArrivedAtDestination())
        {
            hasArrived = true;
            enemy.SetMovement(0f);
            enemy.SetVerticalMovement(0f);
            enemy.memory.SetLastVisitedPoint(targetPoint);

            enemy.workerState = WorkerStatus.ReadyToSpawnFollowers;
        }
    }

    public override void ExitState() { }
}
