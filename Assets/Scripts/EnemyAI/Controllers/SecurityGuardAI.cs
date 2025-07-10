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
        if (securityManager != null)
            securityManager.OnMachineTurnedOff += HandleMachineTurnedOff;
    }

    private void OnDestroy()
    {
        if (securityManager != null)
            securityManager.OnMachineTurnedOff -= HandleMachineTurnedOff;
    }

    private void HandleMachineTurnedOff(FactoryMachine machine)
    {
        if (controller == null || stateMachine == null || waypointService == null)
            return;
        stateMachine.ChangeState(new Enemy_ReactivateMachine(controller, stateMachine, waypointService, machine));
    }
}

