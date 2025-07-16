using UnityEngine;

public class SpawningWorkerSlot : MonoBehaviour
{
    [SerializeField] private BaseMachine machine;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var enemy = collision.GetComponentInParent<EnemyWorkerController>();
        if (enemy == null) return;
        if (enemy.workerState == WorkerStatus.ReadyToSpawnFollowers
            || enemy.workerState == WorkerStatus.Spawning)
        {
            machine.AttachRobot(enemy.gameObject);
        }
    }
}
