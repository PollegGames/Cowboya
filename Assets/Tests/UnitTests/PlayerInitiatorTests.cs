using NUnit.Framework;
using UnityEngine;

public class PlayerInitiatorTests
{
    private GameObject _gameObject;
    private PlayerInitiator _initiator;

    [SetUp]
    public void SetUp()
    {
        _gameObject = new GameObject();
        _initiator = _gameObject.AddComponent<PlayerInitiator>();
    }

    [Test]
    public void SetPlayerStartPosition_StoresPosition()
    {
        // TODO: Assert position storage
    }

    [Test]
    public void InitializePlayer_CreatesPlayer()
    {
        // TODO: Assert player creation
    }
}
