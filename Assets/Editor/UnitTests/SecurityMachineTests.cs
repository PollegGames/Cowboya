using NUnit.Framework;
using UnityEngine;

public class SecurityMachineTests
{
    private GameObject _machineGO;
    private SecurityMachine _machine;
    private EnemyController _guard;
    private EnemyStateMachine _stateMachine;
    private WaypointService _waypointService;

    [SetUp]
    public void SetUp()
    {
        _machineGO = new GameObject();
        _machine = _machineGO.AddComponent<SecurityMachine>();
        _waypointService = _machineGO.AddComponent<WaypointService>();
        _machine.InitializeWaypointService(_waypointService);

        var guardGO = new GameObject();
        _guard = guardGO.AddComponent<EnemyController>();
        _stateMachine = guardGO.AddComponent<EnemyStateMachine>();
        _guard.EnemyStatus = EnemyStatus.Idle;
    }

    [Test]
    public void PowerOff_SendsGuardToRest()
    {
        _machine.AttachRobot(_guard.gameObject);
        _machine.PowerOff();
        Assert.AreEqual(EnemyStatus.GoingToRest, _guard.EnemyStatus);
    }
}
