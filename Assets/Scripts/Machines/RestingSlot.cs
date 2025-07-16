using UnityEngine;

public class RestingSlot : MonoBehaviour
{
    [SerializeField] private BaseMachine machine;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var enemy = collision.GetComponentInParent<EnemyWorkerController>();
        if (enemy == null) return;
        if (enemy.workerState == WorkerStatus.ReadyToRest)
        {
            machine.AttachRobot(enemy.gameObject);
        }
    }
}
