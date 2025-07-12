using UnityEngine;

/// <summary>
/// Component specific to security guards to react to machine shutdown events.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class SecurityGuardAI : MonoBehaviour
{
    private EnemyController controller;
    private EnemyStateMachine stateMachine;
    private IWaypointService waypointService;
    private MachineSecurityManager securityManager;

    private void Awake()
    {
        controller = GetComponent<EnemyController>();
        stateMachine = GetComponent<EnemyStateMachine>();
    }

    public void Initialize(IWaypointService service, MachineSecurityManager manager)
    {
        waypointService = service;
        securityManager = manager;
        securityManager?.RegisterGuard(this);
    }

    private void OnDestroy()
    {
        securityManager?.UnregisterGuard(this);
    }

    public void ReactivateMachine(FactoryMachine machine)
    {
        if (controller == null || stateMachine == null || waypointService == null)
            return;
        var returnPoint = controller.memory.LastVisitedPoint;
        stateMachine.ChangeState(new Enemy_ReactivateMachine(
            controller, stateMachine, waypointService, machine, returnPoint));
    }
}

