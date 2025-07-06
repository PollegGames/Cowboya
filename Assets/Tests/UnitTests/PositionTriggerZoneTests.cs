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
        _zone = _gameObject.AddComponent<PositionTriggerZone>();
    }

    [Test]
    public void OnEnterZone_InvokesEvent()
    {
        // TODO: Assert onEnter invocation
    }

    [Test]
    public void OnExitZone_InvokesEvent()
    {
        // TODO: Assert onExit invocation
    }
}
