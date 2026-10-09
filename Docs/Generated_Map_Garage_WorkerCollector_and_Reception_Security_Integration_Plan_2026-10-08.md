# Generated Map Garage Worker Collector and Reception Security Integration Plan - 2026-10-08

## Status

Implementation-ready design. This document describes the changes needed to populate generated Garage and Reception rooms with their dedicated robots. It does not change runtime code or prefab assets.

## Goal

After a generated map has created and initialized all rooms:

- the primary generated Garage at POI slot 6 owns exactly one `WorkerCollector`;
- every generated `Reception` room owns exactly one `SecurityReception`;
- both robots run at the same time through their own existing primary-task logic;
- neither robot is counted as, spawned as, or dispatched like a normal generated Worker or SecurityGuard;
- repeated initialization cannot create duplicates;
- missing supporting rooms leave the affected robot in a safe, diagnosable state instead of causing an invalid movement task.

This population is derived from the generated rooms. It must not consume `RunMapConfigSO.workersCount` or `securityGuardsCount`.

## Existing Systems Reviewed

This plan is based on the current implementations and designs in:

- `Docs/Generated_Map_Room_Selection_Design_2026-10-08.md`
- `Docs/SecurityReception_Robot_Design_2026-10-08.md`
- `Docs/Level_2_Cube_Worker_and_Garage_Design_Review.md`
- `Docs/Worker_Collector_Logic_Correction_Plan.md`
- `Docs/RobotAI_NewArchitecture_MigrationGuide.md`
- `Assets/Scripts/Setup/SceneInitiator.cs`
- `Assets/Scripts/AI/EnemiesSpawner.cs`
- `Assets/Scripts/Factory/Managers/FactoryManager.cs`
- `Assets/Scripts/World/Rooms/RoomManager.cs`
- `Assets/Scripts/Robots/WorkerCollector/WorkerCollectorMissionService.cs`
- `Assets/Scripts/Robots/WorkerCollector/WorkerCollectorSpawnRestProvider.cs`
- the Garage, Resting, Conveyor, Reception, WorkerCollector, SecurityReception, and EnemyManager prefabs.

## Current Runtime Findings

### Generated-room selection

`GridFactory` and `POIRoomTypeSelector` already support `Reception`, `Garage`, `Conveyor`, and `Resting`, but the selector must be changed to the required fixed-slot contract:

- slot 1: guaranteed Resting;
- slot 2: guaranteed Security;
- slots 3-5: random Resting, Security, or Reception;
- slot 6: guaranteed Garage;
- slot 7: guaranteed Conveyor;
- slots 8+: fully random from every eligible generated-room type except Laboratory.

Therefore, every map with at least seven effective POIs has the complete Resting + Garage + Conveyor dependency set required by the Worker Collector. `poiCount` is never increased to reach this threshold.

Robot population must tolerate every one of these valid layouts.

### Reception security

Most of the required Reception flow already exists:

- `SceneInitiator.InitializeEnemies()` calls `SpawnSecurityReceptionGuards()` after `FactoryManager.Initialize()` and normal enemy spreading.
- `EnemiesSpawner.FindReceptionWorkWaypoints()` groups registered waypoints by Reception room and requires exactly one Work waypoint.
- `EnemiesSpawner` spawns one `SecurityReception` per resolved post and keeps it outside `spawnedSecurityGuards`.
- `RobotRole.SecurityReception` has distinct stationary combat policy.
- only the `SecurityGuard` role is registered with `MachineSecurityManager`.

This is the correct lifecycle and separation to preserve. The remaining work is validation and consolidation with the Garage-owned population pass, not a replacement AI controller.

### Garage Worker Collector

The Worker Collector's primary-task pipeline already exists:

```text
room capability registration
    -> WorkerCollectorMissionService
    -> RobotMemoryNew facts
    -> RobotBrainNew plan
    -> RobotHeartNew task ownership
    -> RobotTaskNew dispatch
    -> WorkerCollectorBodyController actions
```

The generated-compatible providers are currently distributed as follows:

- `ROOM_Conveyor`: `WorkerCollectorWhiteCubeSourceProvider`;
- `GarageCubes` nested in `ROOM_Garage`: `WorkerCollectorDropOffProvider`;
- `ROOM_resting`: `WorkerCollectorSpawnRestProvider`, including the WorkerCollector prefab and live-instance limit.

`WorkerCollectorMissionService.TryEnsureCollectors()` presently spawns a collector from the first registered Rest provider, and it stops if any live `WorkerCollectorBodyController` exists globally. This means:

1. a Garage does not own its Worker Collector;
2. creation is owned indirectly by the Rest provider instead of the fixed Garage-Conveyor pair;
3. multiple garages cannot each receive one collector;
4. collector identity is global rather than paired with a garage;
5. registration order can affect which provider supplies the robot.

These points are the primary implementation gap.

## Required Behavior

### Room-derived ownership

Use these ownership rules:

| Room | Dedicated robot | Count | Primary duty |
|---|---|---:|---|
| Reception | `SecurityReception` | one per room | hold the room's Reception Work post and attack only in range |
| Primary Garage (slot 6) | `WorkerCollector` | one for the fixed slot-6/slot-7 pair | collect white cubes from the paired Conveyor and deliver them to the primary Garage |

The fixed POI identities are authoritative. Extra Garage or Conveyor rooms selected randomly from slot 8 onward do not automatically create additional Worker Collectors. Normal enemy configuration remains authoritative only for roaming Workers, WorkerSpawners, SecurityGuards, and the Boss.

### Independent primary tasks

The two dedicated roles must coexist without task coupling:

- `SecurityReception` continues using its stationary guard task policy. It must not wait for or react to Worker Collector state.
- `WorkerCollector` continues using its collection mission facts and WorkerCollector task family. It must not react to reception-post or security-machine policy.
- spawning one dedicated role must not push tasks into the other robot.
- external systems may provide assignments or observations, but only Brain may plan and Heart may mutate the active task stack.

No shared "room robots controller" may directly drive movement, attacks, collection, or task transitions.

## Proposed Design

### 1. Add one post-room-initialization population phase

Introduce one explicit population call after all generated rooms have been instantiated, initialized, and registered with `WaypointService`.

Recommended order inside the generated scene path:

1. `MapManager.BuildFromConfig()` renders rooms.
2. `FactoryManager.Initialize()` initializes rooms, machines, providers, and registered waypoints.
3. `WaypointService.BuildAllNeighbors()` completes navigation data.
4. Spawn configured roaming enemies and spread them.
5. Run `SpawnDedicatedRoomRobots()`:
   - spawn one Reception defender per valid Reception post;
   - spawn one Garage Worker Collector for the primary slot-6 Garage provider;
   - initialize navigation and seed each robot's role-specific assignment;
   - report invalid or incomplete room capabilities once.

This should replace the isolated generated-scene call to `SpawnSecurityReceptionGuards()` with an orchestration method that invokes the existing Reception behavior plus the new Garage behavior. Static-scene behavior may keep its current path until intentionally migrated, but shared resolver code should be reusable.

### 2. Make the Garage the Worker Collector spawn owner

Add a Garage-scoped spawn capability, recommended name:

`WorkerCollectorGarageSpawnProvider`

Place it on `ROOM_Garage` or its `GarageCubes` capability root. It owns:

- the `WorkerCollector` prefab reference;
- a Garage-authored spawn point;
- the Garage's `WorkerCollectorDropOffProvider`;
- `maximumLiveCollectors`, default 1;
- the collectors it spawned;
- an idempotent `TrySpawnCollector()` operation.

Because slots 8+ may randomly create additional Garage rooms, generation must preserve which Garage came from fixed POI slot 6. Store the one-based POI selection slot (or an explicit `IsPrimaryWorkerCollectorGarage` flag derived from it) in generated cell/room metadata. Do not infer the primary Garage from scene enumeration order or position.

The robot should initially spawn at the guaranteed slot-1 Resting-room spawn marker. Garage ownership must still be bound before activation so the robot's first mission targets the primary slot-6 Garage and paired slot-7 Conveyor.

Do not silently reuse `WorkerCollectorSpawnRestProvider` as the Garage owner. Its name, serialized data, and current responsibility state that Rest owns creation. Retain it as a rest capability, or split it safely into:

- Garage spawn ownership; and
- optional Rest destination capability.

Existing Level 2/static wiring must be migrated or supported through a compatibility path so it does not spawn a second collector.

### 3. Make assignments Garage-specific

Extend mission assignment so each spawned collector is permanently associated with its owning `WorkerCollectorDropOffProvider`.

`WorkerCollectorMissionService` must request an assignment for a specific Garage instead of calling the current global `FindGarage()` for every collector. Recommended API direction:

```csharp
public static bool RequestAssignment(
    WorkerCollectorBodyController collector,
    WorkerCollectorDropOffProvider assignedGarage);
```

The assignment service then selects:

- the collector's owning Garage as `DropOff`;
- a compatible active Conveyor source;
- an optional Rest provider.

The owning Garage must not change between collection cycles unless that Garage is destroyed or disabled and failover is explicitly designed later.

For multiple garages, assign sources deterministically. Recommended first version:

1. gather active sources in stable room/grid order;
2. select the nearest reachable source to the Garage approach waypoint;
3. break equal-distance ties by stable room/grid order;
4. allow multiple collectors to share a source because cube claiming already prevents both from owning one cube.

Do not use `FindObjectsByType` enumeration order as a gameplay decision.

### 4. Use the guaranteed Resting room

A seven-or-more-POI generated map always has the Resting room at slot 1, the Garage at slot 6, and the Conveyor at slot 7. The Worker Collector must use a Resting-room provider for its rest phase; it must not rest inside the Garage.

If slots 8+ create additional Resting rooms, select the nearest reachable Rest provider to the owning Garage, with stable grid order as the tie-breaker.

### 5. Safe behavior for incomplete room combinations

Generated room selection does not guarantee all Worker Collector dependencies. Required behavior:

| Available rooms | Result |
|---|---|
| Reception | one SecurityReception runs normally |
| At least seven POIs | guaranteed Resting + Garage + Conveyor; the Worker Collector runs the complete collect/deliver/rest loop |
| Fewer than seven POIs | no complete Garage-Conveyor pair and no generated Worker Collector |
| Resting without Garage | no WorkerCollector is spawned by Rest alone |
| additional random Garages from slot 8+ | no additional collector unless a future design adds another explicit paired-room contract |
| multiple Receptions | one SecurityReception for each valid Reception Work post |

Log each missing dependency once per room/configuration change. Do not emit a warning every frame while the collector retries.

### 6. Preserve Reception role isolation

Keep the existing Reception rules:

- exactly one Work waypoint registered in each Reception room;
- one `SecurityReception` at that waypoint;
- no entry in `spawnedSecurityGuards`;
- no `MachineSecurityManager.RegisterGuard()` call;
- no Security/Rest destination selection;
- no chase task;
- `AttackTarget` only while the player is in attack range;
- return to holding the same post after combat.

Remove scene-name-specific behavior from the general generated-map path. The current `Level_3` faint-lock special case may remain for that authored static scene, but it must never activate merely because a generated Reception uses the same prefab.

### 7. Idempotency and lifecycle

Both room robot spawners must be safe to call more than once.

Use room/provider identity, not position-distance checks alone, as the authoritative duplicate key:

- Reception key: `RoomManager` or its unique Work waypoint;
- Garage key: `WorkerCollectorDropOffProvider`.

When a robot is destroyed or permanently pooled, clear the ownership record through an explicit lifecycle callback. Whether a dedicated robot respawns is a separate product decision; the first implementation should not automatically replace a killed robot during the same room lifetime unless current gameplay explicitly requires it.

Static registries in `WorkerCollectorMissionService` must remove disabled/destroyed providers and must be cleared safely across scene changes. A provider from a previous scene must never participate in a new generated map.

## Prefab Changes

### `ROOM_Garage.prefab`

- Add `WorkerCollectorGarageSpawnProvider`.
- Add a clearly named `WorkerCollectorSpawnPoint` positioned on valid walkable space.
- Add a Garage fallback rest/wait marker if the existing garage wait point is unsuitable.
- Assign the existing `WorkerCollectorDropOffProvider` explicitly.
- Assign `WorkerCollector.prefab`.
- Ensure the Garage approach waypoint is serialized or deterministically resolved.

### `ROOM_Conveyor.prefab`

- Keep `WorkerCollectorWhiteCubeSourceProvider`.
- Serialize its approach waypoint instead of relying on an ambiguous first-child fallback.
- Verify spawned white cubes receive `WhiteCubeCargo` and remain claimable in generated rooms.

### `ROOM_resting.prefab`

- Retain rest destination data.
- Stop it from independently creating a Worker Collector after Garage ownership is enabled.
- If backward compatibility is temporarily required, gate old spawning behind an explicit legacy/static-scene option that defaults off for generated maps.

### `ROOM_Reception.prefab`

- Keep exactly one Work waypoint beside `FutureDesk`.
- Ensure it is in `RoomManager.waypoints`, so generated initialization registers it.

### Manager prefabs

- Keep `SecurityReception.prefab` assigned on the active enemy manager/spawner.
- Prefer the Garage provider to own the WorkerCollector prefab reference; do not add Worker Collectors to normal enemy count arrays.

## Code Responsibility Changes

### `SceneInitiator`

- Call one dedicated-room population phase after generated factory and navigation initialization.
- Do not calculate per-room robot counts itself.

### `EnemiesSpawner` or a focused `DedicatedRoomRobotSpawner`

- Preserve Reception resolution and spawning.
- Resolve the initialized Garage whose generated metadata identifies POI slot 6 and invoke only its idempotent spawn method.
- Keep dedicated robots separate from normal enemy collections and configured counts.
- Return a small spawn summary for diagnostics and tests.

A focused spawner is preferable if `EnemiesSpawner` would otherwise acquire Worker Collector mission policy. The orchestration layer should only find room capabilities and request creation; mission choice remains in `WorkerCollectorMissionService`.

### `WorkerCollectorMissionService`

- Replace the global "any live collector" creation gate.
- Stop spawning robots from Rest registration.
- accept or discover the collector's owning Garage;
- create deterministic source/rest assignments for that Garage;
- preserve claims, reservations, Memory observations, and retry behavior;
- expose failure reasons that identify the affected Garage.

### `WorkerCollectorGarageSpawnProvider`

- own spawn identity and maximum-live count for one Garage;
- instantiate and initialize the Worker Collector role/pipeline;
- initialize `RobotBodyController` navigation before enabling the Heart;
- bind the owning Garage before the first `WorkerCollectorFindCube` task runs;
- keep task decisions out of the provider.

## Initialization Timing Constraint

The Worker Collector Heart can start its default task synchronously from `OnEnable`. Therefore, the owning Garage and navigation service must be installed while the clone is inactive, before enabling the robot.

Required creation order:

```text
instantiate inactive
    -> configure RobotRole.WorkerCollector
    -> bind owning Garage
    -> align body with Garage spawn marker
    -> initialize waypoint navigation
    -> seed Memory/last waypoint if required
    -> activate
    -> Heart begins primary task
```

The Reception defender follows the same inactive-configure-activate discipline, with its Reception post bound before activation.

## Diagnostics

Emit transition or population logs, not per-frame spam. Minimum useful messages:

```text
DedicatedRoomPopulation receptions=<count> garages=<count>
SecurityReceptionSpawned room=<name> post=<name>
WorkerCollectorSpawned garage=<name> spawn=<name>
WorkerCollectorAssigned garage=<name> source=<name> rest=<name-or-garage-fallback>
WorkerCollectorStandby garage=<name> reason=NoSource
DedicatedRoomRobotSkipped room=<name> reason=<missing/duplicate capability>
```

## Automated Test Plan

Add or extend Edit Mode tests for:

1. **Reception population**
   - one defender per Reception room;
   - repeated population produces no duplicate;
   - missing or duplicate Work posts fail clearly;
   - normal SecurityGuard count and registration are unchanged.

2. **Garage population**
   - one Worker Collector for the primary slot-6 Garage and slot-7 Conveyor pair;
   - extra random Garage rooms from slot 8+ do not create extra collectors;
   - repeated population produces no duplicate;
   - a Conveyor or Rest room without a Garage creates none.

3. **Garage ownership**
   - each collector assignment uses its owning Garage;
   - the collector never swaps away from the primary Garage during an ordinary cycle;
   - source selection is deterministic for equal seeds/layouts.

4. **Dependency behavior**
   - seven POIs provide Resting, Garage, and Conveyor at slots 1, 6, and 7;
   - six POIs never create a generated Worker Collector because the pair is incomplete;
   - eight-or-more POIs keep the fixed first seven slots and randomize only later slots.

5. **Pipeline isolation**
   - Worker Collector tasks remain in the WorkerCollector family;
   - SecurityReception never receives chase, rest, or machine-reactivation tasks;
   - spawning either role does not alter the other's task stack.

6. **Initialization order**
   - navigation and owning provider are available before Worker Collector activation;
   - Reception post is assigned before SecurityReception activation.

7. **Scene cleanup**
   - destroyed/disabled providers are removed from static registries;
   - loading a second scene cannot assign a provider from the previous scene.

Run the complete suite with:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/run-editmode-tests.ps1
```

## Manual Acceptance Matrix

Generate and play maps containing these combinations:

1. Five POIs: only Resting/Security/Reception rooms; no Garage Worker Collector.
2. Six POIs: Garage is fixed at slot 6, but no Worker Collector is spawned until its Conveyor pair exists.
3. Seven POIs: Resting at 1, Security at 2, Garage at 6, Conveyor at 7; one Worker Collector completes collection, delivery, batch, and Resting-room cycles.
4. Eight or more POIs: the first seven rules remain fixed and every later slot is random from the complete non-Laboratory pool.
5. A map whose slots 3-5 include Reception: one SecurityReception per Reception runs independently while the Worker Collector performs its primary loop.
6. Regenerate the same seeded map twice: room population and deterministic source/rest pairing match.

For every case, also verify room rotation does not break world-space spawn points, approach waypoints, arm reach, delivery, or Reception post placement.

## Implementation Sequence

1. Add tests that expose the current Rest-owned/global-singleton Worker Collector behavior.
2. Add the Garage spawn provider and authored Garage markers.
3. Bind a collector to an owning Garage before activation.
4. Refactor mission assignment to accept that Garage and select source/rest deterministically.
5. Disable generated-map spawning from `WorkerCollectorSpawnRestProvider` while preserving rest capability.
6. Add the combined post-room-initialization population pass.
7. Preserve and validate existing Reception spawn behavior through that pass.
8. Add incomplete-layout and multi-room tests.
9. Run all Edit Mode tests.
10. Complete the manual acceptance matrix in Unity.

## Acceptance Criteria

The feature is complete when:

- every initialized generated Reception has exactly one `SecurityReception` at its own registered Work post;
- the fixed slot-6 Garage and slot-7 Conveyor pair has exactly one `WorkerCollector`;
- neither dedicated robot changes normal enemy configuration counts;
- Reception security and Garage collection execute concurrently through separate Memory -> Brain -> Heart -> Task policies;
- additional random Garage or Conveyor rooms from slot 8+ do not change the primary pair's collector ownership;
- slot 1 always supplies the Worker Collector's Resting room;
- slots 6 and 7 always supply Garage and Conveyor as a pair on maps with at least seven effective POIs;
- repeated initialization and provider registration do not duplicate robots;
- scene changes do not retain stale provider assignments;
- generated-room rotations preserve valid spawns and navigation;
- the complete Edit Mode suite and manual acceptance matrix pass.
