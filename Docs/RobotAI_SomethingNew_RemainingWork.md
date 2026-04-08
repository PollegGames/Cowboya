# RobotAI `*New` Refactor - Remaining Work

## Scope
This note tracks missing pieces in the ongoing refactor of classes ending with `New` (`RobotBrainNew`, `RobotMemoryNew`, `RobotMemoryStateNew`, etc.).

## Current Gap 1 - Populate `AllAvailableWaypoints`

### Problem
`RobotBrainNew.FindByPriority(...)` relies on `snapshot.AllAvailableWaypoints`, but this dictionary is initialized empty and is not populated by runtime wiring yet.

### Proposed approach
Populate waypoint availability when each enemy robot is created/spawned.

### Integration point
`Assets/Scripts/AI/EnemiesSpawner.cs`

At robot initialization time:
1. Query all relevant waypoints from `IWaypointService` (initially the same map for all robots).
2. For each waypoint, call `RobotMemoryNew.SetRoomWaypointAvailability(waypoint, waypoint.IsAvailable)`.
3. Keep this as the robot's initial internal navigation map.

### Working hypothesis
At spawn time, all robots can share the same initial waypoint availability model for moving in the factory.

## Current Gap 2 - Keep the internal map updated

### Problem
Even if the map is initialized, it can become stale (machine off, blocked paths, etc.).

### Proposed first-step update strategy
Update waypoint availability lazily when trying to connect/reactivate a machine:
1. Robot attempts to connect to a machine.
2. If machine is OFF/unavailable, mark the corresponding machine waypoint as unavailable in memory.
3. Call `SetRoomWaypointAvailability(targetWaypoint, false)`.

This provides immediate practical value with minimal wiring complexity.

## Suggested implementation order
1. Add spawn-time population in `EnemiesSpawner` for all `RobotMemoryNew` instances.
2. Add update-on-failed-machine-connection in machine interaction handlers.
3. Validate that `RobotBrainNew` switches to fallback waypoint types (e.g. `Work -> Rest -> Center`).

## Validation checklist
- `AllAvailableWaypoints` is non-empty after enemy spawn.
- Worker/Security/Spawner robots receive `GoToMachine` tasks from valid waypoints when possible.
- If a target machine is OFF, the related waypoint becomes unavailable in robot memory.
- Brain replans to a fallback waypoint/task instead of repeatedly targeting the same unavailable machine.

## Notes
- This is intentionally a first implementation pass.
- A later pass can add global synchronization with room/waypoint status events if needed.
