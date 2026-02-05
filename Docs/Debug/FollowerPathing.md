# Follower Pathing Debug Notes

Goal: capture why the Follower does not draw gizmo paths and moves only one direction while other robots can move in the grid of waypoints.

## Code logs added

The path follower logs were removed. Logging is now focused on follower logic only.

Files:
- `Assets/Scripts/AI/EnemiesSpawner.cs`
- `Assets/Scripts/Misc/AIHandlers/ChaseTargetHandler.cs`
- `Assets/Scripts/Robots/Body/RobotBodyController.cs`

Logs for follower spawn:
- `[Follower][Spawn] spawnPos=... type=... parentRoom=... isAvailable=... worldPos=...`
- `[Follower][Spawn] neighbors count=... list=...`
- `[Follower][Spawn] target=AlarmLastPlayerPos ...`
- `[Follower][Spawn] target=ClosestWaypointToPlayer ...`
- `[Follower][Spawn] target=Patrol ...`

Logs for follower chase handling:
- `[Follower][ChaseTarget] payloadType=...`
- `[Follower][ChaseTarget] targetWaypoint=... pos=...`
- `[Follower][ChaseTarget] targetTransform=... pos=...`
- `[Follower][ChaseTarget] payloadWaypoint=... pos=...`
- `[Follower][ChaseTarget] payloadPos=...`
- `[Follower][ChaseTarget] memoryPos=...`

Logs for follower body wiring:
- `[Follower][Body] Awake bodyReference=... hipRb=... attackController=...`
- `[Follower][Body] Initialize waypointQueries=... waypointNotifier=... pathFollower=... bodyReference=...`
- `[Follower][Body] SetDestination worldPosition but waypointQueries is null`
- `[Follower][Body] SetDestination worldPosition=... includeUnavailable=... closestWaypoint=... type=... parentRoom=... dist=...`
- `[Follower][Body] ClosestWaypoint neighbors count=... list=...`
- `[Follower][Body] PathState pathCount=... waypointCount=... pathIndex=... target=... lastAttempted=...`
- `[Follower][Body] HasArrivedAtDestination in Update`
- `[Follower][Body] HasArrivedAtDestination in FixedUpdate`
- `[Follower][Body] StopMovement (clearing path)`
- `[Follower][Gizmos] draw pathCount=... waypointCount=... pathIndex=... target=... arrived=... bodyPos=...`

## How to use

1) Run the scene.
2) Look for the logs above for the Follower.
3) Confirm what payload the follower gets for chase.

## What to check

- If the follower always gets `Patrol` instead of `ChasePlayer`, the alarm/target data is not set.
- If payload type is `RoomWaypoint` but the follower still moves one direction, check the waypoint grid around its spawn.
- If payload is `Vector3` but pathing is wrong, check `ClosestWaypointToPlayer` and `LastPlayerPosition`.
- If spawn/closest neighbors are empty, the graph is disconnected or not built for that waypoint type.
- If `PathState` shows `pathCount=0` or `waypointCount=0`, no path was built.

## Notes

If you need path-building logs again later, we can add them back behind a define.
