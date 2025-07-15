using NUnit.Framework;
using UnityEngine;

public class SecurityGuardRestTests
{
    private GameObject _gameObject;
    private EnemyController _enemy;
    private EnemyStateMachine _stateMachine;
    private WaypointService _waypointService;

    [SetUp]
    public void SetUp()
    {
        _gameObject = new GameObject();
        _enemy = _gameObject.AddComponent<EnemyController>();
        _stateMachine = _gameObject.AddComponent<EnemyStateMachine>();
        _waypointService = _gameObject.AddComponent<WaypointService>();
    }

    [Test]
    public void NoSecurityPoints_FallbackToRestPoint()
    {
        var state = new Enemy_SecurityGuardRest(_enemy, _stateMachine, _waypointService);
        state.EnterState();
        Assert.AreEqual(EnemyStatus.Resting, _enemy.EnemyStatus);
    }

    [Test]
    public void NoRestPoints_FallbackToStartPoint()
    {
        var state = new Enemy_SecurityGuardRest(_enemy, _stateMachine, _waypointService);
        state.EnterState();
        Assert.AreEqual(EnemyStatus.Resting, _enemy.EnemyStatus);
    }
}
