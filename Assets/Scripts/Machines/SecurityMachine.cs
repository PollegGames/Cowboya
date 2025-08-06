using UnityEngine;
using System;

[RequireComponent(typeof(MeshRenderer))]
public class SecurityMachine : BaseMachine
{
    [SerializeField] private Material materialOn;
    [SerializeField] private Material materialOff;

    private MeshRenderer meshRenderer;
    private EnemyController currentGuard;

    public EnemyController CurrentGuard => currentGuard;

    public event Action<SecurityMachine, bool> OnMachineStateChanged;
    public event Action<SecurityMachine, EnemyController> OnMachineTurningOff;

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
        OnMachineStateChanged?.Invoke(this, true);
    }

    public override void PowerOff()
    {
        if (!isOn) return;
        var guard = currentGuard;
        OnMachineTurningOff?.Invoke(this, guard);
        SendCurrentGuardToRest();
        base.PowerOff();
        ApplyMaterial();
        OnMachineStateChanged?.Invoke(this, false);
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
        SetGuardToSecurityPost(guard);
        currentGuard = guard;
        base.AttachRobot(robot);
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
        sm?.ChangeState(new Enemy_GoingToRest(guard, sm, waypointService));
    }

    private void SetGuardToSecurityPost(EnemyController guard)
    {
        if (guard == null) return;
        var sm = guard.GetComponent<EnemyStateMachine>();
        sm?.ChangeState(new Enemy_CheckingSecurity(guard, sm, waypointService));
    }

    private void SendCurrentGuardToRest()
    {
        SendGuardToRest(currentGuard);
        currentGuard = null;
    }
}
