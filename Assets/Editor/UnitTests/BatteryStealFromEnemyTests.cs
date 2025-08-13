using NUnit.Framework;
using UnityEngine;
using System.Reflection;

public class BatteryStealFromEnemyTests
{
    private class DummyInput : MonoBehaviour, IPlayerInput
    {
        public Vector2 Movement => Vector2.zero;
        public bool JumpPressed => false;
        public bool PrimaryAttack => false;
        public bool LeftGrabDown  { get; private set; }
        public bool LeftGrabHeld  { get; private set; }
        public bool LeftGrabUp    { get; private set; }
        public bool RightGrabDown => LeftGrabDown;
        public bool RightGrabHeld => LeftGrabHeld;
        public bool RightGrabUp   => LeftGrabUp;
        public void PressGrab()
        {
            LeftGrabDown = true;
            LeftGrabHeld = true;
        }
        public void NextFrame()
        {
            LeftGrabDown = false;
            LeftGrabUp = false;
        }
    }

    private class DummyPlayerMovementController : PlayerMovementController
    {
        void Awake() { }
        void OnEnable() { }
        void Update() { }
    }

    [Test]
    public void StealBattery_TransfersInventoryAndDamagesEnemy()
    {
        // Enemy with battery
        var enemyGO = new GameObject("enemy");
        enemyGO.AddComponent<EnergyBot>();
        enemyGO.AddComponent<HealthBot>();
        var enemyState = enemyGO.AddComponent<RobotStateController>();
        enemyState.Stats = new RobotStats();
        enemyState.Stats.MaxHealth = 100f;
        enemyState.Stats.CurrentHealth = 50f;
        var enemyInventory = enemyGO.AddComponent<Inventory>();
        var enemy = enemyGO.AddComponent<EnemyWorkerController>();
        typeof(EnemyWorkerController)
            .GetField("robotBehaviour", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(enemy, enemyState);
        typeof(EnemyWorkerController)
            .GetField("inventory", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(enemy, enemyInventory);

        var batteryGO = new GameObject("battery");
        batteryGO.transform.SetParent(enemyGO.transform);
        batteryGO.AddComponent<Rigidbody2D>();
        batteryGO.AddComponent<TargetJoint2D>();
        batteryGO.AddComponent<CircleCollider2D>();
        var battery = batteryGO.AddComponent<BatteryPickup>();
        battery.AssignInventory(enemyInventory);
        enemyInventory.SetItem(PickupType.Battery, battery);

        // Player with grab system
        var playerGO = new GameObject("player");
        playerGO.AddComponent<EnergyBot>();
        playerGO.AddComponent<HealthBot>();
        var playerState = playerGO.AddComponent<RobotStateController>();
        playerState.Stats = new RobotStats();
        playerState.Stats.MaxHealth = 100f;
        playerState.Stats.CurrentHealth = 40f;
        var playerRb = playerGO.AddComponent<Rigidbody2D>();
        var player = playerGO.AddComponent<DummyPlayerMovementController>();
        var playerInventory = playerGO.AddComponent<Inventory>();
        typeof(PlayerMovementController)
            .GetField("bodyReference", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(player, playerRb);

        var handObj = new GameObject("hand");
        handObj.transform.SetParent(playerGO.transform);
        var hand = handObj.AddComponent<GrabHandAttractor>();
        hand.detectionRadius = 1f;
        int layer = 8;
        hand.detectionLayer = 1 << layer;
        batteryGO.layer = layer;
        batteryGO.transform.position = handObj.transform.position;

        var grabSystem = playerGO.AddComponent<GrabSystem>();
        grabSystem.leftHand = hand;
        var inputObj = new GameObject("input");
        var input = inputObj.AddComponent<DummyInput>();
        typeof(GrabSystem)
            .GetField("inputSource", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(grabSystem, input);
        typeof(GrabSystem)
            .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(grabSystem, null);

        Assert.IsTrue(enemyInventory.HasItem(PickupType.Battery));
        Assert.IsFalse(playerInventory.HasItem(PickupType.Battery));

        input.PressGrab();
        // grabSystem.Update();
        input.NextFrame();

        Assert.AreEqual(battery, playerInventory.GetItem(PickupType.Battery));
        Assert.IsFalse(enemyInventory.HasItem(PickupType.Battery));

        var gain = (float)typeof(BatteryPickup)
            .GetField("healthGain", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(battery);

        Assert.AreEqual(40f + gain, playerState.Stats.CurrentHealth);
        Assert.AreEqual(50f - gain, enemyState.Stats.CurrentHealth);
    }
}
