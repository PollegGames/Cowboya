using UnityEngine;

public class WorkerSlot : MonoBehaviour
{
    [SerializeField] private FactoryMachine factoryMachine;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var enemy = collision.GetComponentInParent<EnemyWorkerController>();
        if (enemy == null) return;
        if (enemy.workerState == WorkerStatus.ReadyToWork || enemy.workerState == WorkerStatus.Resting)
        {
            factoryMachine.OnWorkerReady(enemy);
        }
    }
}
