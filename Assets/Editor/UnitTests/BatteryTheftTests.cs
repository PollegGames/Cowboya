using NUnit.Framework;
using UnityEngine;
using System.Reflection;

public class BatteryTheftTests
{
    private class DummyPlayerMovementController : PlayerMovementController
    {
        void Awake() { }
        void OnEnable() { }
        void Update() { }
    }

    [Test]
    public void StealingBattery_ReducesEnemyHealth()
    {
        var enemyGO = new GameObject("enemy");
        enemyGO.AddComponent<EnergyBot>();
        enemyGO.AddComponent<HealthBot>();
        var enemyState = enemyGO.AddComponent<RobotStateController>();
        enemyState.Stats = new RobotStats();
        enemyState.Stats.MaxHealth = 100f;
        enemyState.Stats.CurrentHealth = 50f;
        enemyGO.AddComponent<EnemyStateMachine>();
        var enemy = enemyGO.AddComponent<EnemyController>();
        enemy.EnemyStatus = EnemyStatus.Idle;

        var batteryGO = new GameObject("battery");
        batteryGO.transform.SetParent(enemyGO.transform);
        batteryGO.AddComponent<Rigidbody2D>();
        batteryGO.AddComponent<TargetJoint2D>();
        var battery = batteryGO.AddComponent<BatteryPickup>();

        var playerGO = new GameObject("player");
        playerGO.AddComponent<EnergyBot>();
        playerGO.AddComponent<HealthBot>();
        var playerState = playerGO.AddComponent<RobotStateController>();
        playerState.Stats = new RobotStats();
        playerState.Stats.MaxHealth = 100f;
        playerState.Stats.CurrentHealth = 40f;
        var playerRb = playerGO.AddComponent<Rigidbody2D>();
        var player = playerGO.AddComponent<DummyPlayerMovementController>();
        typeof(PlayerMovementController)
            .GetField("bodyReference", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(player, playerRb);
        var hand = new GameObject("hand").transform;
        hand.SetParent(playerGO.transform);

        battery.OnGrab(hand);

        Assert.AreEqual(50f - 10f, enemyState.Stats.CurrentHealth);
        Assert.AreEqual(40f + 10f, playerState.Stats.CurrentHealth);
    }
}
