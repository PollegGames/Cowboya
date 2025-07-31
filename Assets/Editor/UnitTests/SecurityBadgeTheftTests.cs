using NUnit.Framework;
using UnityEngine;
using System.Reflection;

public class SecurityBadgeTheftTests
{
    private class DummyPlayerMovementController : PlayerMovementController
    {
        // Override lifecycle methods to avoid base behaviour
        void Awake() { }
        void OnEnable() { }
        void Update() { }
    }

    [Test]
    public void StealingBadge_StartsChaseOnlyOnce()
    {
        // Enemy with required components
        var enemyGO = new GameObject("enemy");
        var enemy = enemyGO.AddComponent<EnemyController>();
        enemyGO.AddComponent<EnemyStateMachine>();
        enemyGO.AddComponent<RobotStateController>();
        enemy.EnemyStatus = EnemyStatus.Idle;

        // Badge attached to enemy
        var badgeGO = new GameObject("badge");
        badgeGO.transform.SetParent(enemyGO.transform);
        badgeGO.AddComponent<Rigidbody2D>();
        badgeGO.AddComponent<TargetJoint2D>();
        var badge = badgeGO.AddComponent<SecurityBadgePickup>();

        // Player hand with dummy player
        var playerGO = new GameObject("player");
        var playerBody = playerGO.AddComponent<Rigidbody2D>();
        var player = playerGO.AddComponent<DummyPlayerMovementController>();
        typeof(PlayerMovementController)
            .GetField("bodyReference", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(player, playerBody);
        var hand = new GameObject("hand").transform;
        hand.SetParent(playerGO.transform);

        int chaseCalls = 0;
        Application.LogCallback handler = (c, s, t) => { if (c.Contains("badge stolen")) chaseCalls++; };
        Application.logMessageReceived += handler;

        badge.OnGrab(hand);
        Assert.AreEqual(EnemyStatus.Following, enemy.EnemyStatus);

        badge.OnGrab(hand); // grab again should not trigger chase again
        badge.OnGrab(hand);

        Application.logMessageReceived -= handler;

        Assert.AreEqual(1, chaseCalls);
    }
}
