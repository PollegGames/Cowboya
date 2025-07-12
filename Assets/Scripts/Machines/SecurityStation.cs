using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class SecurityStation : BaseMachine
{
    [SerializeField] private Material materialOn;
    [SerializeField] private Material materialOff;

    private MeshRenderer meshRenderer;
    private EnemyController currentGuard;
    private IWaypointService waypointService;

    public EnemyController CurrentGuard => currentGuard;

    public void Initialize(IWaypointService service)
    {
        waypointService = service;
    }

    protected override void Awake()
    {
        base.Awake();
        meshRenderer = GetComponent<MeshRenderer>();
        ApplyMaterial();
    }

    private void ApplyMaterial()
    {
        if (meshRenderer == null) return;
        if (materialOn == null || materialOff == null) return;
        meshRenderer.material = isOn ? materialOn : materialOff;
    }

    public override void PowerOn()
    {
        base.PowerOn();
        ApplyMaterial();
    }

    public override void PowerOff()
    {
        if (!isOn) return;
        SendCurrentGuardToRest();
        base.PowerOff();
        ApplyMaterial();
    }

    public override void AttachRobot(GameObject robot)
    {
        var guard = robot.GetComponent<EnemyController>();
        if (guard == null) return;

        if (!isOn)
        {
            SendGuardToRest(guard);
            return;
        }

        SendGuardToRest(currentGuard);
        SetGuardToCheck(guard);
        currentGuard = guard;
        isOccupied = true;
        OnRobotAssigned?.Invoke(this);
    }

    public override void ReleaseRobot()
    {
        SendCurrentGuardToRest();
        isOccupied = false;
        base.ReleaseRobot();
    }

    private void SendGuardToRest(EnemyController guard)
    {
        if (guard == null) return;
        var sm = guard.GetComponent<EnemyStateMachine>();
        sm?.ChangeState(new Enemy_SecurityGuardRest(guard, sm, waypointService));
    }

    private void SetGuardToCheck(EnemyController guard)
    {
        if (guard == null) return;
        var sm = guard.GetComponent<EnemyStateMachine>();
        sm?.ChangeState(new Enemy_SecurityCheck(guard, sm, waypointService));
    }

    private void SendCurrentGuardToRest()
    {
        SendGuardToRest(currentGuard);
        currentGuard = null;
    }
}
