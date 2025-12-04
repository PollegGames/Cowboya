using UnityEngine;
using System;

[RequireComponent(typeof(MeshRenderer))]
public class SecurityMachine : BaseMachine
{
    [SerializeField] private Material materialOn;
    [SerializeField] private Material materialOff;

    private MeshRenderer meshRenderer;
    private RobotBrain currentGuardBrain;

    public RobotBrain CurrentGuard => currentGuardBrain;

    public event Action<SecurityMachine, bool> OnMachineStateChanged;
    public event Action<SecurityMachine, RobotBrain> OnMachineTurningOff;

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
        var guard = currentGuardBrain;
        OnMachineTurningOff?.Invoke(this, guard);
        SendCurrentGuardToRest();
        base.PowerOff();
        ApplyMaterial();
        OnMachineStateChanged?.Invoke(this, false);
    }

    public override void AttachRobot(GameObject robot)
    {
        var guardBrain = robot.GetComponent<RobotBrain>();
        if (guardBrain == null) return;

        if (!isOn)
        {
            SendGuardToRest(guardBrain);
            return;
        }

        SendGuardToRest(currentGuardBrain);
        SetGuardToSecurityPost(guardBrain);
        currentGuardBrain = guardBrain;
        base.AttachRobot(robot);
    }

    public override void ReleaseRobot()
    {
        SendCurrentGuardToRest();
        isOccupied = false;
        base.ReleaseRobot();
    }

    private void SendGuardToRest(RobotBrain guard)
    {
        if (guard == null) return;
        guard.OnMachineStateChanged(this, false);
    }

    private void SetGuardToSecurityPost(RobotBrain guard)
    {
        if (guard == null) return;
        guard.OnMachineStateChanged(this, true);
    }

    private void SendCurrentGuardToRest()
    {
        SendGuardToRest(currentGuardBrain);
        currentGuardBrain = null;
    }
}
