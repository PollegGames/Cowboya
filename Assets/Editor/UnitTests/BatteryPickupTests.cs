using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

public class BatteryPickupTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    private class DummyPlayerMovementController : PlayerMovementController
    {
        void Awake() { }
        void OnEnable() { }
        void Update() { }
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
                Object.DestroyImmediate(createdObject);
        }
        createdObjects.Clear();
    }

    private GameObject CreateGameObject(string objectName)
    {
        GameObject createdObject = new GameObject(objectName);
        createdObjects.Add(createdObject);
        return createdObject;
    }

    [Test]
    public void Battery_AddsHealthAndAttaches()
    {
        var playerGO = CreateGameObject("player");
        playerGO.AddComponent<EnergyBot>();
        playerGO.AddComponent<HealthBot>();
        var playerState = playerGO.AddComponent<RobotStateController>();
        playerState.Stats = new RobotStats();
        playerState.Stats.MaxHealth = 100f;
        playerState.Stats.CurrentHealth = 50f;

        var playerRb = playerGO.AddComponent<Rigidbody2D>();
        var player = playerGO.AddComponent<DummyPlayerMovementController>();
        var inventory = playerGO.AddComponent<Inventory>();
        typeof(PlayerMovementController)
            .GetField("bodyReference", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(player, playerRb);

        var hand = CreateGameObject("hand").transform;
        hand.SetParent(playerGO.transform);

        var batteryGO = CreateGameObject("battery");
        batteryGO.AddComponent<Rigidbody2D>();
        batteryGO.AddComponent<TargetJoint2D>();
        var battery = batteryGO.AddComponent<BatteryPickup>();

        battery.OnGrab(hand);

        Assert.AreEqual(60f, playerState.Stats.CurrentHealth);
        Assert.IsTrue(battery.CanBeGrabbed(inventory));
        Assert.AreEqual(battery, inventory.GetItem(PickupType.Battery));

        var otherInvGO = CreateGameObject("otherInv");
        var otherInventory = otherInvGO.AddComponent<Inventory>();
        otherInventory.SetItem(PickupType.Battery, CreateGameObject("otherBatt").AddComponent<BatteryPickup>());
        Assert.IsFalse(battery.CanBeGrabbed(otherInventory));

        inventory.DropItem(PickupType.Battery);
        Assert.IsFalse(inventory.HasItem(PickupType.Battery));
    }

    [Test]
    public void Battery_DisablesPhysicsWhileGrabbed()
    {
        var batteryGO = CreateGameObject("battery");
        var rb = batteryGO.AddComponent<Rigidbody2D>();
        rb.gravityScale = 2f;
        batteryGO.AddComponent<TargetJoint2D>();
        var battery = batteryGO.AddComponent<BatteryPickup>();
        var hand = CreateGameObject("hand").transform;

        battery.OnGrab(hand);

        Assert.AreEqual(RigidbodyType2D.Kinematic, rb.bodyType);
        Assert.AreEqual(0f, rb.gravityScale);

        battery.OnRelease(Vector2.zero);

        Assert.AreEqual(RigidbodyType2D.Dynamic, rb.bodyType);
        Assert.AreEqual(2f, rb.gravityScale);
    }
}
