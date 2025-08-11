using NUnit.Framework;
using UnityEngine;
using System.Reflection;

public class BatteryPickupTests
{
    private class DummyPlayerMovementController : PlayerMovementController
    {
        void Awake() { }
        void OnEnable() { }
        void Update() { }
    }

    [Test]
    public void Battery_AddsHealthAndAttaches()
    {
        var playerGO = new GameObject("player");
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

        var hand = new GameObject("hand").transform;
        hand.SetParent(playerGO.transform);

        var batteryGO = new GameObject("battery");
        batteryGO.AddComponent<Rigidbody2D>();
        batteryGO.AddComponent<TargetJoint2D>();
        var battery = batteryGO.AddComponent<BatteryPickup>();

        battery.OnGrab(hand);

        Assert.AreEqual(60f, playerState.Stats.CurrentHealth);
        Assert.IsTrue(battery.CanBeGrabbed(inventory));
        Assert.AreEqual(battery, inventory.GetItem(PickupType.Battery));

        var otherInvGO = new GameObject("otherInv");
        var otherInventory = otherInvGO.AddComponent<Inventory>();
        otherInventory.SetItem(PickupType.Battery, new GameObject("otherBatt").AddComponent<BatteryPickup>());
        Assert.IsFalse(battery.CanBeGrabbed(otherInventory));

        inventory.DropItem(PickupType.Battery);
        Assert.IsFalse(inventory.HasItem(PickupType.Battery));
    }
}
