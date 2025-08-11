using NUnit.Framework;
using UnityEngine;
public class SceneInitiatorTests
{
    private GameObject _gameObject;
    private SceneInitiator _initiator;

    [SetUp]
    public void SetUp()
    {
        _gameObject = new GameObject();
        _initiator = _gameObject.AddComponent<SceneInitiator>();
    }

    [Test]
    public void Start_InitializesScene()
    {
        Assert.DoesNotThrow(() => _initiator.Construct(null, null, null, null, null, null, null, null, null, null, null));
    }
}
