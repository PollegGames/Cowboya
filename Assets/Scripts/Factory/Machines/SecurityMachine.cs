using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class SecurityMachine : BaseMachine
{
    [SerializeField] private Material materialOn;
    [SerializeField] private Material materialOff;

    private MeshRenderer meshRenderer;
    private RobotBrainNew currentGuardBrain;
    private MachineSecurityManager securityManager;

    public RobotBrainNew CurrentGuard => currentGuardBrain;

    /// <summary>
    /// Connects this security machine to the manager that owns guard dispatch decisions.
    /// </summary>
    public void InitializeSecurityManager(MachineSecurityManager manager)
    {
        securityManager = manager;
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
        var guardBrain = robot.GetComponent<RobotBrainNew>();
        if (guardBrain == null) return;

        if (!isOn)
        {
            RobotEcosystemProbe.RecordSlotDecision(
                this,
                "SecurityMachine",
                "attach_rejected_power_off",
                guardBrain,
                this,
                guardBrain.Heart != null ? guardBrain.Heart.CurrentTask : null);
            SendGuardToRest(guardBrain);
            return;
        }

        if (ReferenceEquals(currentGuardBrain, guardBrain))
        {
            RobotEcosystemProbe.RecordSlotDecision(
                this,
                "SecurityMachine",
                "attach_ignored_same_guard",
                guardBrain,
                this,
                guardBrain.Heart != null ? guardBrain.Heart.CurrentTask : null);
            return;
        }

        RobotBrainNew previousGuard = currentGuardBrain;
        SendGuardToRest(currentGuardBrain);
        SetGuardToSecurityPost(guardBrain);
        currentGuardBrain = guardBrain;
        base.AttachRobot(robot);

        RobotEcosystemProbe.RecordSlotDecision(
            this,
            "SecurityMachine",
            previousGuard != null ? "replaced_current" : "attached_empty",
            guardBrain,
            this,
            guardBrain.Heart != null ? guardBrain.Heart.CurrentTask : null);

        securityManager?.HandleGuardConnectedToSecurityMachine(this, guardBrain);
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
        if (guard.Memory != null)
            guard.Memory.SetDesiredMachineType(MachineType.RestStation);
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



