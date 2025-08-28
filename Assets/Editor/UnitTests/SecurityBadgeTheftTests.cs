using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

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

    private class DummyWaypointQueries : IWaypointQueries
    {
        public List<RoomWaypoint> GetAllWaypoints() => new();
        public List<RoomWaypoint> GetActiveWaypoints() => new();
        public List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end) => new();
        public RoomWaypoint GetClosestWaypoint(Vector2 position, bool includeUnavailable = false) => null;
        public RoomWaypoint GetEndPoint() => null;
        public RoomWaypoint GetStartPoint() => null;
        public void UpdateClosestWaypointToPlayer(Vector2 playerPosition) { }
        public RoomWaypoint ClosestWaypointToPlayer => null;
    }

    private class DummyWaypointNotifier : IWaypointNotifier
    {
        public void Subscribe(IRobotNavigationListener robot) { }
        public void Unsubscribe(IRobotNavigationListener robot) { }
        public void NotifyWaypointStatusChanged(RoomWaypoint changed, bool isAvailable) { }
    }

    private class DummyRespawnService : IRobotRespawnService
    {
        public void RespawnWorker() { }
        public void RespawnBoss() { }
    }

    [Test]
    public void Initialize_SpawnsNewBadgeAfterSteal()
    {
        // Enemy setup
        var enemyGO = new GameObject("enemy");
        enemyGO.AddComponent<EnemyStateMachine>();
        enemyGO.AddComponent<RobotStateController>();
        var enemy = enemyGO.AddComponent<EnemyController>();
        typeof(EnemyController)
            .GetField("bodyReference", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(enemy, enemyGO.transform);

        // Badge prefab and spawner
        var badgePrefabGO = new GameObject("badgePrefab");
        badgePrefabGO.AddComponent<Rigidbody2D>();
        badgePrefabGO.AddComponent<TargetJoint2D>();
        var badgePrefab = badgePrefabGO.AddComponent<SecurityBadgePickup>();

        var spawnerGO = new GameObject("spawner");
        var spawner = spawnerGO.AddComponent<SecurityBadgeSpawner>();
        typeof(SecurityBadgeSpawner)
            .GetField("badgePrefab", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(spawner, badgePrefab);

        var dropContainer = new GameObject("drop").transform;

        // Stub services
        var queries = new DummyWaypointQueries();
        var notifier = new DummyWaypointNotifier();
        var respawn = new DummyRespawnService();

        enemy.Initialize(queries, notifier, respawn, dropContainer, spawner, true);
        var enemyInventory = enemyGO.GetComponent<Inventory>();
        var firstBadge = enemyInventory.GetItem(PickupType.SecurityBadge) as SecurityBadgePickup;
        Assert.IsNotNull(firstBadge);

        // Player to steal the badge
        var playerGO = new GameObject("player");
        var playerInv = playerGO.AddComponent<Inventory>();
        var playerBody = playerGO.AddComponent<Rigidbody2D>();
        var player = playerGO.AddComponent<DummyPlayerMovementController>();
        typeof(PlayerMovementController)
            .GetField("bodyReference", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(player, playerBody);
        var hand = new GameObject("hand").transform;
        hand.SetParent(playerGO.transform);

        firstBadge.OnGrab(hand);

        Assert.IsNull(enemyInventory.GetItem(PickupType.SecurityBadge));

        enemy.Initialize(queries, notifier, respawn, dropContainer, spawner, true);
        var secondBadge = enemyInventory.GetItem(PickupType.SecurityBadge) as SecurityBadgePickup;
        Assert.IsNotNull(secondBadge);
        Assert.AreNotSame(firstBadge, secondBadge);
    }
}
