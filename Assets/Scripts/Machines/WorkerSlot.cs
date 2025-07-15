using UnityEngine;

public class WorkerSlot : MonoBehaviour
{
    [SerializeField] private BaseMachine machine;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var enemy = collision.GetComponentInParent<EnemyWorkerController>();
        if (enemy == null) return;
        if (enemy.workerState == WorkerStatus.ReadyToWork ||
            enemy.workerState == WorkerStatus.ReadyToRest ||
            enemy.workerState == WorkerStatus.Resting)
        {
            machine.AttachRobot(enemy.gameObject);
        }
    }
}
