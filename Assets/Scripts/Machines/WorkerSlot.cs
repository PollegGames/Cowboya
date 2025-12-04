using UnityEngine;

public class WorkerSlot : MonoBehaviour
{
    [SerializeField] private BaseMachine machine;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var brain = collision.GetComponentInParent<RobotBrain>();
        if (brain == null) return;
        machine.AttachRobot(brain.gameObject);
    }
}
