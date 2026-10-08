# Security Reception Robot Design - 2026-10-08

## Goal

Create one `SecurityReception` robot for each reception room. It reuses the SecurityGuard presentation and close-range attack behavior, but its permanent primary duty is defending the reception desk.

The robot must:

- spawn at the reception room's `WorkWaypoint`, beside `FutureDesk`;
- remain anchored at that position during normal behavior;
- detect and attack the player when the player enters attack range;
- never chase the player away from the reception post;
- never respond to powered-off factory machines;
- return to an idle/guarding state at the same post after an attack;
- continue to use the SecurityGuard prefab's health, damage, animation, perception, attack, death, drops, and other compatible presentation settings unless a reception-specific override is required.

## Current Project Findings

### Existing SecurityGuard behavior

The existing `SecurityGuard` role is not stationary:

- `RobotBrainNew` plans `AttackTarget` in attack range and `ChasePlayer` in detection range.
- Outside combat it selects Security/Rest destinations.
- `MachineSecurityManager` registers and dispatches SecurityGuards to reactivate powered-off machines.
- `EnemiesSpawner` spreads SecurityGuards across Security/Rest waypoints and includes them in the normal guard collection.

Therefore, merely copying the prefab while retaining the unmodified `SecurityGuard` role would not meet the reception behavior. The copy could leave its post to chase, rest, use a security machine, or reactivate another machine.

### Reception room setup

`Assets/Resources/Prefabs/Map/ROOM_Reception.prefab` contains:

- a `FutureDesk` prefab instance;
- a child GameObject named `WorkWaypoint` with `RoomWaypoint.type = Work`;
- a `RoomManager.waypoints` list that currently contains only the left door, right door, and center waypoints.

The reception `WorkWaypoint` is therefore present but is not currently registered through the room's serialized waypoint list. This must be corrected before relying on waypoint-service lookup or tests.

### Existing local changes

At design time, the worktree already contains user changes in `Assets/Scenes/Level_3.unity` and a staged `Assets/Resources/Prefabs/Robots/SecurityReception.meta`. Implementation must preserve these changes and inspect them in the Unity Editor before modifying overlapping assets.

## Proposed Design

### 1. Add a distinct robot role

Add `SecurityReception` as a new `RobotRole` value appended after all existing numeric values. Do not renumber existing roles because tests and serialized assets rely on their current integer values.

A distinct role makes the policy explicit and prevents accidental registration with `MachineSecurityManager`. It also avoids treating the reception defender as a normal roaming SecurityGuard while allowing shared enemy systems to opt in deliberately.

Expected role policy:

| Situation | Task |
|---|---|
| Player in attack range | `AttackTarget` |
| Player only in detection range | stationary guard/idle task; do not chase |
| No player nearby | stationary guard/idle task |
| Machine powers off | ignore |
| Attack ends or player leaves | stop movement and remain at the assigned reception post |

`AttackTarget` is suitable because `RobotTaskNew.HandleAttackTarget` already calls `StopMovement()` before starting the existing attack controller.

### 2. Keep a permanent reception-post assignment

Add a small, focused reception-defender component or equivalent explicit spawn assignment that stores the assigned `RoomWaypoint` and enforces the stationary contract.

The spawn position is the reception room's authored `WorkWaypoint`; it should not be selected from the global pool of ordinary Work waypoints. Lookup must be scoped to a `RoomManager` whose `RoomProperties` identifies it as Reception, then select that room's `WaypointType.Work` waypoint.

The stationary contract should not continuously teleport a living robot every frame. The robot should be placed at the post during spawn, have movement stopped for its guard and attack tasks, and never receive a movement task. A small drift tolerance may be used as a defensive correction only if physics can displace the robot.

### 3. Create a copied prefab

Create:

`Assets/Resources/Prefabs/Robots/SecurityReception/SecurityReception.prefab`

Copy `SecurityGuard.prefab` through Unity's asset APIs or Prefab workflow so all nested references and `.meta` identity are valid. Rename the root/presentation objects where useful, assign the new role/spawn policy, and retain the existing perception and attack-zone configuration.

Do not modify the original SecurityGuard prefab's behavior.

The copied prefab should retain the existing enemy combat stack, including `FollowPlayerTriggerHandler`, `RobotBrainNew`, `RobotHeartNew`, `RobotBodyController`, `RobotAttackController`, health/death handling, and compatible colliders. Its reception-specific behavior should come from role policy and post assignment, not from disabling the whole AI pipeline.

### 4. Add explicit reception spawning

Extend `EnemiesSpawner` and `IEnemiesSpawner` with reception-defender creation/spawn support and a separately serialized `securityReceptionPrefab` reference.

Recommended lifecycle:

1. Rooms and their waypoints are initialized and registered.
2. Find every Reception room.
3. Resolve exactly one registered `WorkWaypoint` in each reception room.
4. Spawn one `SecurityReception` at each resolved waypoint.
5. Initialize the normal robot pipeline with role `SecurityReception` and seed its last visited/assigned post.
6. Do not add it to the normal `spawnedSecurityGuards` dispatch collection and do not register it with `MachineSecurityManager`.

The reception-defender count should be derived from reception rooms, not from `RunMapConfigSO.enemiesCount`. This gives each reception one defender without reducing or increasing the configured roaming SecurityGuard count.

For a hand-built static scene, use the same room-scoped resolution after static room initialization. Do not use `StaticLevelSpawnPoint`; that component marks the player spawn location.

### 5. Preserve shared enemy behavior deliberately

Update role checks only where reception behavior should match an enemy guard:

- perception must allow the attack zone to generate `CanAttack`;
- death, damage, animation, and enemy grabbable behavior should remain compatible;
- badge/drop behavior requires a product decision during implementation: default recommendation is to match SecurityGuard unless that would change progression balance;
- victory targets should remain based on configured roaming guards unless the design explicitly requires killing reception defenders too.

Do not include `SecurityReception` in:

- security-machine slot admission;
- resting-machine admission;
- machine reactivation dispatch;
- global Security/Rest waypoint selection;
- player chase planning.

## Implementation Sequence

1. Open the existing modified `Level_3` and reception-related assets in Unity and confirm the user's staged changes before editing overlapping files.
2. Add the reception `WorkWaypoint` to `ROOM_Reception`'s `RoomManager.waypoints` list and verify it is positioned beside `FutureDesk`.
3. Append `SecurityReception` to `RobotRole` without changing existing enum values.
4. Add stationary decision behavior in `RobotHeartNew`/`RobotBrainNew`: attack only when `CanAttack`; otherwise hold the post, even when `PlayerDetected` or in danger.
5. Include the new role in attack-zone perception eligibility.
6. Add explicit per-reception spawn lookup and initialization without machine-security registration.
7. Copy and configure `SecurityReception.prefab`; assign it on `EnemyManager.prefab` or the active spawner configuration.
8. Add tests, run Edit Mode tests, then perform the manual Unity acceptance pass.

## Automated Test Plan

Add focused Edit Mode tests for:

1. **Role value stability**
   - Existing `RobotRole` numeric values remain unchanged.
   - `SecurityReception` has a new unique appended value.

2. **Stationary planning**
   - With no perception flags, the planned task is stationary.
   - With `PlayerDetected` but not `CanAttack`, the task remains stationary and is not `ChasePlayer`.
   - With `CanAttack` and a player Transform, the task is `AttackTarget` with that Transform payload.
   - A pending machine-reactivation memory entry does not produce `ReactivateMachine` for this role.

3. **Attack task behavior**
   - Executing `AttackTarget` stops locomotion and invokes the existing attack controller.
   - Leaving attack range returns to the stationary task without setting a destination.

4. **Reception waypoint resolution**
   - A Reception room's Work waypoint is selected.
   - A Work waypoint in another room is not selected.
   - Missing or duplicate reception Work waypoints produce a clear warning/error and do not spawn at an arbitrary global point.

5. **Spawn behavior**
   - Exactly one defender is created per reception room.
   - It spawns at the resolved Work waypoint.
   - Its role is `SecurityReception`.
   - It is not registered as a machine-reactivation SecurityGuard.

6. **Prefab validation**
   - The prefab loads successfully.
   - Required brain, heart, memory, body, attack, perception, health, and collision components are present.
   - The spawner prefab reference is assigned.

Run all Edit Mode tests with:

```bash
unity -runTests -testPlatform EditMode -projectPath "$(pwd)" -quit
```

If the `unity` executable is not available on `PATH`, run the equivalent command with the installed Unity Editor executable and record the version/path used.

## Manual Acceptance Test

In a scene containing a Reception room:

1. Start play mode and confirm exactly one `SecurityReception` appears at the `WorkWaypoint` beside `FutureDesk`.
2. Wait without approaching; confirm it does not walk to Security, Rest, Start, another Work point, or a powered-off machine.
3. Enter detection range but remain outside attack range; confirm it does not chase.
4. Enter attack range; confirm it attacks with the same attack behavior as SecurityGuard while remaining at the reception post.
5. Leave attack range; confirm it stops attacking and remains at/returns to the post without chasing.
6. Power off a factory machine; confirm the reception defender ignores it while normal SecurityGuards still react.
7. Damage and kill the defender; confirm health, death, collision, drops/badge, and pooling behavior match the chosen design.
8. Repeat in a rotated/generated Reception room and verify the spawn uses the waypoint's world position correctly.

## Acceptance Criteria

The feature is complete when:

- every Reception room spawns exactly one SecurityReception at its authored Work waypoint;
- the robot never receives chase, rest, security-post, or machine-reactivation movement during normal play;
- it attacks the player with the existing SecurityGuard combat behavior only while the player is in attack range;
- it remains at the reception post before, during, and after combat within the agreed drift tolerance;
- existing SecurityGuards continue their current roaming/reactivation behavior unchanged;
- automated Edit Mode tests pass;
- the manual acceptance test passes without new errors or missing-reference warnings.

