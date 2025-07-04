using UnityEngine;

public class WorkerSlot : MonoBehaviour
{
    [SerializeField] private FactoryMachine factoryMachine;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var enemy = collision.GetComponentInParent<EnemyController>();
        if (enemy == null) return;
        Debug.Log($"[WorkerSlot] {enemy.name} entered the slot.");
        if (enemy.workerState == WorkerState.ReadyToWork)
        {
            factoryMachine.OnWorkerReady(enemy);
        }
    }
}
