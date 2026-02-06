# Follower Behavior (Chase + Lift Pathing)

## Goals
- Follower uses the same pathing system as other robots.
- Chase is handled through task handlers (no `Update()` polling).
- Follower prefers the closest waypoint to the player on the same floor.
- Detect zone pauses movement; attack logic stays unchanged elsewhere.

## Key Decisions
1. **Task-driven chase refresh**
   - A coroutine in `RobotBrain` re-executes the `ChasePlayer` handler at a fixed interval.
   - It only refreshes when there is no active path.
   - This avoids jitter from repeated path resets.
2. **Same-floor constraint for follower chase**
   - The follower selects the closest waypoint within a Y-band (default `5f`).
   - If no waypoint matches the band, it falls back to the full set.
3. **Lift pathing**
   - Lift waypoints are connected by `WaypointPathFinder` using `LiftGoingUp` and `LiftGoingDown`.
   - Choosing a same-floor waypoint ensures the path will use lift connectors when needed.

## Current Flow
1. **Spawn**
   - Spawner pushes a `ChasePlayer` task (payload may be last known player position).
2. **Chase**
   - `ChaseTargetHandler` handles followers by choosing:
     - `LastKnownPlayerPosition` when available, otherwise
     - `ClosestWaypointToPlayer`.
   - The handler then picks the closest waypoint on the same floor and calls `SetDestination`.
3. **Pause**
   - If `PlayerInAttackZone` is true, movement is paused by clearing the path.

## Config Knobs
- `RobotBrain.followerChaseRefreshSeconds` controls refresh interval.
- Same-floor Y-band is `5f` in `ChaseTargetHandler`.

## Key Files
- `Assets/Scripts/Robots/RobotBrain.cs`
- `Assets/Scripts/Misc/AIHandlers/ChaseTargetHandler.cs`
- `Assets/Scripts/World/Waypoints/WaypointPathFinder.cs`
- `Assets/Scripts/Robots/Body/RobotBodyController.cs`

