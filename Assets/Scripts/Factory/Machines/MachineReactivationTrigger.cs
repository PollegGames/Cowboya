using UnityEngine;

[RequireComponent(typeof(PositionTriggerZone))]
public class MachineReactivationTrigger : MonoBehaviour
{
    [SerializeField] private BaseMachine machine;
    [SerializeField] private bool requireSecurityGuard = true;
    [SerializeField] private bool requireMatchingReactivateTask = true;
    [SerializeField] private bool completeReactivateTask = true;

    private PositionTriggerZone zone;

    private void Awake()
    {
        if (machine == null)
            machine = GetComponentInParent<BaseMachine>();
        if (zone == null)
            zone = GetComponent<PositionTriggerZone>();
    }

    private void OnEnable()
    {
        if (zone == null)
            zone = GetComponent<PositionTriggerZone>();
        if (zone != null)
            zone.onEnter.AddListener(HandleEnter);
    }

    private void OnDisable()
    {
        if (zone != null)
            zone.onEnter.RemoveListener(HandleEnter);
    }

    private void HandleEnter(Collider2D collider)
    {
        if (machine == null || machine.IsOn)
            return;

        var brain = collider != null ? collider.GetComponentInParent<RobotBrainNew>() : null;
        if (requireSecurityGuard && (brain == null || !brain.IsSecurityGuard))
            return;

        if (requireMatchingReactivateTask && !IsMatchingReactivateTask(brain))
            return;

        machine.PowerOn();

        if (completeReactivateTask && brain != null && brain.IsSecurityGuard)
            RobotDomainEventBus.PublishCompleteReactivateDispatch(brain, machine, reached: true);
    }

    private bool IsMatchingReactivateTask(RobotBrainNew brain)
    {
        if (brain == null || brain.Heart == null)
            return false;
        var task = brain.Heart.CurrentTask;
        if (task == null || task.Type != RobotTaskType.ReactivateMachine)
            return false;
        return ReferenceEquals(task.Payload, machine);
    }
}

