using NUnit.Framework;
using UnityEngine;

public class PositionTriggerZoneTests
{
    private GameObject _gameObject;
    private PositionTriggerZone _zone;

    [SetUp]
    public void SetUp()
    {
        _gameObject = new GameObject();
        _zone = _gameObject.AddComponent<TestZone>();
    }

    private class TestZone : PositionTriggerZone
    {
        public void InvokeEnter(Collider2D col) => base.OnEnterZone(col);
        public void InvokeExit() => base.OnExitZone();
    }

    [Test]
    public void OnEnterZone_InvokesEvent()
    {
        bool called = false;
        _zone.onEnter.AddListener(_ => called = true);
        (_zone as TestZone).InvokeEnter(null);
        Assert.IsTrue(called);
    }

    [Test]
    public void OnExitZone_InvokesEvent()
    {
        bool called = false;
        _zone.onExit.AddListener(() => called = true);
        (_zone as TestZone).InvokeExit();
        Assert.IsTrue(called);
    }
}
