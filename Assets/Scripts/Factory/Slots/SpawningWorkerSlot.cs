using UnityEngine;

public class SpawningWorkerSlot : MonoBehaviour
{
    [SerializeField] private BaseMachine machine;
    [SerializeField] private bool logSlotDecisions = true;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var brain = collision.GetComponentInParent<RobotBrain>();
        if (brain == null) return;
        var heart = brain.Heart;
        if (heart == null || heart.Role != RobotRole.WorkerSpawner)
        {
            if (logSlotDecisions)
                Debug.Log($"[SpawningWorkerSlot] Ignored {brain.name} role={heart?.Role}", this);
            return;
        }
        machine.AttachRobot(brain.gameObject);
    }
}
