using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class SecurityMachine : BaseMachine
{
    [SerializeField] private Material materialOn;
    [SerializeField] private Material materialOff;

    private MeshRenderer meshRenderer;
    private RobotBrainNew currentGuardBrain;

    public RobotBrainNew CurrentGuard => currentGuardBrain;

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
        var guardBrain = robot.GetComponent<RobotBrainNew>();
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

    public void VacateGuard(RobotBrainNew guard)
    {
        if (guard == null || currentGuardBrain != guard)
            return;

        currentGuardBrain = null;
        base.ReleaseRobot();
    }

    private void SendGuardToRest(RobotBrainNew guard)
    {
        if (guard == null) return;
        RobotDomainEventBus.PublishMachineStateDispatch(guard, null, false);
    }

    private void SetGuardToSecurityPost(RobotBrainNew guard)
    {
        if (guard == null) return;
        RobotDomainEventBus.PublishMachineStateDispatch(guard, this, true);
    }

    private void SendCurrentGuardToRest()
    {
        SendGuardToRest(currentGuardBrain);
        currentGuardBrain = null;
    }
}



