# Level 2 Worker Collector and Garage

Status: **Design proposal — awaiting review before implementation**  
Date: 2026-08-23  
Target scene: `Assets/Scenes/Level_2.unity`

> Correction notice (2026-09-03): the original pickup sequence in this document
> assigns and claims a specific conveyor cube before the worker reaches the conveyor.
> Runtime investigation showed that this makes the worker chase a moving-cube-derived
> final position and prevents the intended wait-at-conveyor behavior. The corrective
> implementation specification is
> [`Worker_Collector_Logic_Correction_Plan.md`](Worker_Collector_Logic_Correction_Plan.md).
> That document supersedes this document wherever the two disagree about source travel,
> cube selection, claiming, arm reach, and pickup recovery. The garage storage, batch,
> door, delivery, and rest requirements here remain valid.

## 1. Goal

Level 2 will contain a dedicated robot worker whose job is to move white cubes into a garage in batches of nine.

The intended loop is:

1. The worker spawns at the rest-room spawn point.
2. It finds an available white cube.
3. It walks to the cube and carries it with a visible hand/grab attachment.
4. It walks to the garage delivery point.
5. The garage accepts the cube into the first available position of a 3 × 3 grid.
6. The worker repeats until all nine physical positions are occupied.
7. The garage door closes, the nine cubes are removed, and the door opens again.
8. Only after that completed nine-cube processing cycle, the worker returns to the rest room.
9. After a configurable rest time, the worker starts another collection cycle.

The player may take a cube from the worker or from an open garage. A taken garage cube stops counting immediately.

## 2. Existing Project Features to Reuse

The current project already contains useful foundations:

- `Worker3.prefab` supplies the worker art, ragdoll, animation, locomotion, and robot body setup.
- `RobotBodyController` and the waypoint graph provide room-to-room navigation.
- `CubeNormal.prefab` is the current physical white cube.
- `CubePickup` and `IGrabbable` provide player grabbing and `OnGrabbed` / `OnReleased` notifications.
- Level 2 already references the resting, cube collector, conveyor, and garage room prefabs.
- `SpawnGarbageController` already demonstrates retracting a panel by scaling one local axis.
- `GarageCubeConveyorController` currently creates cubes in the garage. That behavior does not match the new garage intake loop and must not generate cubes while the new system is active.

The normal Worker AI alternates between factory work and rest machines. The Worker Collector must not be configured as `RobotRole.Worker`, because adding cube rules to that role would change every existing worker. It must also not reuse `RobotRole.Collector`, which already belongs to the flying junk Collector pipeline.

The new robot will use a new `RobotRole.WorkerCollector` and remain inside the established architecture:

```text
room/source/garage observations
    -> RobotMemoryNew / WorkerCollector memory facts
    -> RobotBrainNew / role-specific planning
    -> RobotHeartNew / task stack ownership
    -> RobotTaskNew / task execution
    -> RobotBodyController + WorkerCollector body actions
```

This is an extension of the current pipeline, following the existing flying Collector precedent. It is not a Level 2-only AI controller and does not replace the Memory, Brain, Heart, or Task layers.

### Architecture invariants

These rules are mandatory throughout implementation:

1. Existing `RobotRole.Worker` planning and task behavior remain unchanged.
2. Existing `RobotRole.Collector` flying-collector behavior remains unchanged.
3. `WorkerCollector` receives a new serialized enum value appended after existing roles; existing numeric values are never reordered.
4. Physical components report facts to Memory; they do not choose the next high-level task.
5. Brain is the only layer that translates Worker Collector facts into a planned task.
6. Heart remains the owner of the active task stack and task transitions.
7. `RobotTaskNew` dispatches Worker Collector tasks to its body contract.
8. `RobotBodyController` and the existing waypoint graph/path follower remain the navigation implementation.
9. No Worker Collector system modifies global pathfinding rules to reach a cube or garage. It supplies an existing waypoint plus an optional final world position.
10. Level 2 room objects expose reusable capabilities; robot logic must not search by scene name or hold Level 2-specific hard-coded coordinates.

## 3. Scope

### Included

- A separate `WorkerCollector` prefab based on the existing Worker visual/physics rig.
- White-cube identification, discovery, and temporary target claiming.
- Worker navigation, pickup, visible carrying, delivery, waiting, and resting.
- A garage intake with nine explicit slots arranged as three columns and three rows.
- An anchored, one-direction stretch/retract door animation.
- Player removal of a stored or carried cube.
- Correct count changes, close cancellation, processing, and reopening.
- Level 2 scene wiring.
- Reusable conveyor-source, garage-destination, and rest/spawn capability components suitable for generated maps.
- A distinct `WorkerCollector` role implemented through Memory → Brain → Heart → Task.
- Edit Mode tests and a Level 2 Play Mode smoke-test checklist.

### Not included

- Changes to player controls.
- Collecting colored upgrade cubes, junk, batteries, or dead robots.
- A rewrite or behavior change of the existing Worker or flying Collector roles.
- Multiple garages or multiple cube workers in the first version.
- Saving a partially filled garage across scene changes.
- New worker artwork. The first version is a prefab variant of `Worker3` and can receive unique art later.

## 4. Runtime Components

### 4.1 `WhiteCubeCargo`

A small marker/state component identifies a `CubePickup` as white cargo that this worker is allowed to collect. It avoids using the shared `CubeUpgrade` tag, which is also used by colored upgrade cubes.

Responsibilities:

- Reference the associated `CubePickup`.
- Expose whether the cube is available, claimed by a worker, carried, or stored.
- Allow one worker to claim a loose cube atomically.
- Release the claim if the cube is destroyed, taken by the player, or abandoned.
- Never prevent the player from grabbing the cube.

`CubeNormal.prefab` receives this marker. Upgrade cube prefabs do not.

### 4.2 Worker Collector pipeline extension

The feature extends the existing robot pillars rather than introducing a parallel controller.

`RobotMemorySnapshotNew` gains a `WorkerCollector` facts structure containing only observed state, such as:

- Current mission/source/garage/rest assignment.
- Claimed cube reference and whether the target remains available.
- Target approach reached.
- Cargo secured or lost.
- Garage approach reached, slot reserved, and delivery accepted.
- Batch completion observed.
- Rest destination reached and rest-until time.

`RobotMemoryNew` exposes narrow mutation methods for these observations and emits new `MemoryChangeType` values. It does not select destinations or tasks.

`RobotBrainNew` adds an isolated `case RobotRole.WorkerCollector` that maps the facts to Worker Collector tasks. Existing role cases are not modified beyond safely excluding the new role from generic machine planning.

`RobotHeartNew` adds a Worker Collector default task and resolves an `IWorkerCollectorTaskBody`. Heart continues to receive Brain plans, own the task stack, and enter/exit tasks.

`RobotTaskNew` adds a Worker Collector task family and invokes body commands. Proposed task types are:

- `WorkerCollectorStandby`
- `WorkerCollectorFindCube`
- `WorkerCollectorMoveToCube`
- `WorkerCollectorGrabCube`
- `WorkerCollectorMoveToGarage`
- `WorkerCollectorDepositCube`
- `WorkerCollectorWaitForBatch`
- `WorkerCollectorMoveToRest`
- `WorkerCollectorRest`

The exact task list may be reduced if two adjacent steps have identical lifecycle requirements, but no task controller may bypass Memory and push its own high-level routine.

### 4.3 `WorkerCollectorBodyController`

This component implements `IWorkerCollectorTaskBody` and owns physical collection actions. It is an actuator/observation source, not the high-level decision maker.

Responsibilities:

- Store a serialized carry anchor located at the worker hand/front.
- Use the existing `RobotBodyController` for all waypoint travel.
- Attach the cube through its existing `CubePickup` grab behavior so the player sees the same physical response.
- Keep the cube collision-safe while carried.
- Detect that another holder, especially the player, has reparented/grabbed the cube.
- Report approach, cargo, deposit, and loss observations into `RobotMemoryNew` through the Brain ingress API.
- Transfer the cube to the garage without destroying or cloning it.

Player theft is intentional. The worker does not fight the player for ownership; it clears its target and searches again.

### 4.4 `WorkerCollectorMissionService`

This reusable scene service matches available Worker Collectors with room capabilities. It replaces Level 2 hard-coded references and supports later generated layouts.

Responsibilities:

- Maintain registrations for active white-cube sources, garage destinations, and Worker Collector rest/spawn providers.
- Build an assignment containing stable provider references and their navigation approach points.
- Claim a loose white cube without changing the waypoint graph.
- Submit assignments through `RobotBrainNew` ingress so assignment facts enter Memory first.
- Release claims/reservations when robots, rooms, or targets are disabled.
- Allow generated rooms to register/unregister at runtime.

The first implementation supports one Worker Collector, but ownership and cleanup contracts must not assume there can only ever be one.

### 4.5 `GarageCubeStorage`

This component is the authoritative source of truth for the garage count and slots.

Responsibilities:

- Own exactly nine serialized slot transforms.
- Reserve an empty slot for an approaching worker, preventing delivery races.
- Accept only `WhiteCubeCargo` while the door is open.
- Place a delivered cube precisely at its reserved slot.
- Make a stored cube kinematic and stable while preserving `CubePickup` and its collider.
- Subscribe to the stored cube's grab/destruction notifications.
- Free the exact slot and decrement occupancy when the player takes a cube.
- Emit occupancy and full-state events.
- Remove only the cubes that remain registered when a valid full batch is processed.

The slot transforms, rather than physics stacking, define the 3 × 3 layout. This prevents the entire pile collapsing when the player removes a bottom cube.

Default fill order:

```text
6 7 8   top row
3 4 5   middle row
0 1 2   bottom row
```

The first empty slot in this order is used. If the player removes slot 1 while upper rows are occupied, slot 1 remains visibly empty and becomes the next delivery target; existing cubes do not rearrange.

### 4.6 `GarageDoorController`

This is a machine door, separate from the room-transition `DoorController`.

Responsibilities:

- Expose `Open`, `Closing`, `Closed`, and `Opening` states.
- Animate one selected local scale axis using the same smooth retract/stretch principle as `SpawnGarbageController`.
- Correct the panel position while scaling so one configured edge stays fixed. This makes the door move in one direction instead of shrinking toward its center.
- Expose configurable closed scale, open multiplier, anchored edge, open/close durations, and optional hold times.
- Enable the blocking collider when closed and disable it when open.
- Report animation completion to the garage coordinator.

The proposed default for the pictured garage is a vertical door whose top edge is fixed and whose bottom edge retracts upward. The anchor remains configurable in the prefab in case the final sprite orientation requires the opposite direction.

### 4.7 `GarageCubeProcessor`

This component coordinates storage and door timing; neither the worker nor the door decides whether a batch is valid.

Responsibilities:

- Keep the garage open while fewer than nine slots are occupied.
- Begin closing when occupancy reaches nine.
- Cancel/reverse closing if occupancy drops below nine before the door becomes fully closed.
- Recheck that all nine slots are occupied after the door is fully closed.
- Remove the registered nine cubes only after that successful closed-door check.
- Increment `CompletedBatchCount` and emit `OnBatchCompleted` once.
- Reopen the door and accept the next batch.

## 5. Worker State Machine

The following is the Brain-planned task sequence. Transitions occur because body/room observations update Memory; Heart then receives the next plan from Brain.

```text
WorkerCollectorStandby
    -> WorkerCollectorFindCube
    -> WorkerCollectorMoveToCube
    -> WorkerCollectorGrabCube
    -> WorkerCollectorMoveToGarage
    -> WorkerCollectorDepositCube
    -> WorkerCollectorFindCube          (garage has fewer than 9)
    -> WorkerCollectorWaitForBatch      (deposit filled slot 9)
    -> WorkerCollectorMoveToRest        (batch completion observed in Memory)
    -> WorkerCollectorRest
    -> WorkerCollectorFindCube
```

Recovery transitions:

- Target disappears or is grabbed before pickup: Memory records target unavailable, Brain plans `WorkerCollectorFindCube`.
- Carried cube is stolen/destroyed: body reports cargo lost to Memory, Brain plans recovery/search.
- Garage reservation is lost: request another slot or wait.
- Garage starts closing before this worker deposits: wait outside until it opens.
- Navigation becomes stuck: clear the target/claim, return to a safe waypoint, and retry after a short delay using the existing body recovery path.
- No cube exists: remain in `WorkerCollectorFindCube`/standby with a configurable assignment scan interval; do not begin the post-batch rest period.

The worker's “nine cubes completed” condition means the garage emitted `OnBatchCompleted`. It is not a lifetime count of nine delivery attempts. This distinction is required because the player can remove cubes.

## 6. Garage State Machine and Race Rules

```text
Open / accepting cubes
    occupancy becomes 9
Closing
    occupancy drops below 9 -> Opening -> Open
    door fully closes with occupancy 9 -> Processing
Processing
    unregister and despawn the 9 stored cubes
    emit one batch-completed event
Opening
    door fully opens -> Open / accepting cubes
```

Rules:

1. `OccupiedCount` is derived from registered slot contents, never from a separate delivery counter.
2. Reserved slots do not count as occupied.
3. A cube counts only after the garage successfully attaches it to a slot.
4. A grabbed or destroyed stored cube is unregistered once and immediately stops counting.
5. During `Closing`, stored cubes remain grabbable until the physical door prevents access.
6. If a cube is taken during `Closing`, processing is cancelled and the door opens.
7. At fully `Closed`, the processor validates all nine references again before removal.
8. Cubes delivered after a batch begins are rejected; they remain with their carrier.
9. The batch event fires after the nine cubes are removed, never merely when count first reaches nine.
10. A disabled/destroyed garage unsubscribes from every cube event and releases outstanding reservations.

## 7. Prefab and Scene Setup

### New prefab: `WorkerCollector.prefab`

Proposed hierarchy additions to the Worker3-based variant:

```text
WorkerCollector
├── existing Worker3 master/puppet rig
├── RobotMemoryNew
├── RobotBrainNew
├── RobotHeartNew (role = WorkerCollector)
├── RobotBodyController
├── WorkerCollectorBodyController
└── CubeCarryAnchor
```

The variant keeps the existing Memory, Brain, Heart, Body, animation, health, ragdoll, and player-grabbable robot behavior. It differs through the serialized `WorkerCollector` role and its specialized body actuator. There is exactly one active planning pipeline on the prefab.

### Garage hierarchy additions

```text
ROOM_Garage
└── CubeGarage
    ├── GarageCubeStorage
    ├── GarageCubeProcessor
    ├── DeliveryPoint
    ├── WorkerWaitPoint
    ├── Door
    │   ├── GarageDoorController
    │   ├── DoorPanel
    │   └── BlockingCollider
    └── Slots
        ├── Slot_00 ... Slot_02
        ├── Slot_03 ... Slot_05
        └── Slot_06 ... Slot_08
```

The nine slots will be authored explicitly in the prefab so their spacing can be tuned visually without code changes.

### Level 2 wiring

- Add a reusable Worker Collector spawn/rest provider to `ROOM_resting`.
- Add a reusable white-cube source provider to `ROOM_Conveyor`.
- Add a reusable garage destination provider to `ROOM_Garage`.
- Let these providers register with `WorkerCollectorMissionService`; do not assign Level 2 object references directly on the robot prefab.
- Ensure the conveyor source creates/contains `CubeNormal` objects with `WhiteCubeCargo`.
- Disable/remove the old garage-local automatic cube output for this scene if it is present.
- Keep the existing room door system unchanged; the new garage machine door is internal to `ROOM_Garage`.

### Generated-map compatibility

Level 2 is the first authored composition of reusable assets, not a special-case runtime path.

- `ROOM_resting.prefab`, `ROOM_Conveyor.prefab`, and `ROOM_Garage.prefab` own their respective provider components and approach transforms.
- When generated rooms are instantiated, providers register on enable and unregister on disable/destroy.
- The mission service selects compatible providers from the generated map and assigns their existing waypoints/final positions.
- The current `WaypointService`, `WaypointPathFinder`, `WaypointPathFollower`, `RoomWaypoint`, room links, and door/lift traversal logic are reused unchanged.
- A later map configuration may add `workerCollectorsCount`; Level 2 can initially use a serialized count of one without making the robot scene-specific.
- Ordinary workers continue to spawn from `workersCount` and use work/rest machine selection exactly as they do now.

## 8. Suggested Defaults

These are tuning values, not hard-coded rules:

| Setting | Proposed default |
|---|---:|
| Garage capacity | 9 |
| Grid | 3 columns × 3 rows |
| Cube scan interval | 0.5 s |
| Worker pickup distance | 0.6 world units |
| Worker delivery distance | 0.6 world units |
| Rest duration | 5 s |
| Door close duration | 0.6 s |
| Door closed/process hold | 0.5 s |
| Door open duration | 0.6 s |
| Door open scale multiplier | 0.05 |

Capacity remains validated as nine for Level 2 even if rows/columns are serialized for future tuning.

## 9. Planned File Changes

Proposed new runtime files:

- `Assets/Scripts/Gameplay/Items/WhiteCubeCargo.cs`
- `Assets/Scripts/Robots/WorkerCollector/WorkerCollectorMissionContracts.cs`
- `Assets/Scripts/Robots/WorkerCollector/WorkerCollectorBodyController.cs`
- `Assets/Scripts/Robots/WorkerCollector/WorkerCollectorMissionService.cs`
- `Assets/Scripts/Robots/WorkerCollector/WorkerCollectorRoomProviders.cs`
- `Assets/Scripts/Factory/Garage/GarageCubeStorage.cs`
- `Assets/Scripts/Factory/Garage/GarageDoorController.cs`
- `Assets/Scripts/Factory/Garage/GarageCubeProcessor.cs`

Existing pipeline files extended with isolated Worker Collector cases:

- `Assets/Scripts/Robots/State/RobotRole.cs`
- `Assets/Scripts/Robots/RobotMemorySnapshotNew.cs`
- `Assets/Scripts/Robots/RobotMemoryStateNew.cs`
- `Assets/Scripts/Robots/RobotMemoryNew.cs`
- `Assets/Scripts/Robots/RobotBrainNew.cs`
- `Assets/Scripts/Robots/RobotHeartNew.cs`
- `Assets/Scripts/Robots/Tasks/RobotTasks.cs`
- `Assets/Scripts/Robots/Tasks/RobotTaskStackNew.cs`
- `Assets/Scripts/Robots/Tasks/RobotTaskNew.cs`
- Worker spawning/bootstrap APIs used by `SceneInitiator` and generated runs.

Extensions must be additive, role-gated, and covered by regression tests proving that `RobotRole.Worker` produces the same plans/tasks as before.

Proposed prefab/scene changes:

- Add the white-cargo marker to `CubeNormal.prefab`.
- Create `Assets/Resources/Prefabs/Robots/WorkerCollector/WorkerCollector.prefab`.
- Add the garage machine hierarchy to `ROOM_Garage.prefab` or create a nested `CubeGarage.prefab` and place it there.
- Add reusable source/rest/garage providers to their room prefabs and compose them in `Level_2.unity`.
- Disable the incompatible `GarageCubeConveyorController` output in the new intake setup.

Proposed tests:

- `WhiteCubeCargoTests.cs`
- `WorkerCollectorMemoryTests.cs`
- `WorkerCollectorPipelineTests.cs`
- `WorkerCollectorBodyControllerTests.cs`
- `WorkerCollectorMissionServiceTests.cs`
- `GarageCubeStorageTests.cs`
- `GarageCubeProcessorTests.cs`
- `GarageDoorControllerTests.cs`
- `Level2WorkerCollectorPrefabTests.cs`

No existing scene or gameplay file should be changed during the documentation/review step.

## 10. Multi-Chat Implementation Plan

Implementation is intentionally divided into four separate chats. Each chat has one narrow deliverable, its own tests, and a manual Unity checkpoint. Work on the next chat begins only after the previous result has been tested and approved.

### Chat 1 — Garage open/close animation

Goal: implement and validate only the internal garage door animation.

Work included:

1. Add `GarageDoorController` with `Open`, `Closing`, `Closed`, and `Opening` states.
2. Animate one local scale axis while keeping the configured edge fixed.
3. Add public test controls or Inspector context actions for opening and closing.
4. Configure the door panel and blocking collider in the garage prefab/Level 2.
5. Add focused Edit Mode tests for endpoints, state changes, re-entrant commands, and collider state.

Explicitly deferred:

- Cube slots and cube counting.
- Cube despawning.
- Worker prefab or AI.
- Automatic door commands based on garage occupancy.

Manual approval checkpoint:

- Open Level 2, trigger Open and Close repeatedly, and confirm the panel stretches/retracts in the correct direction without drifting.
- Confirm the collider blocks only when the door is closed.
- Approve the animation speed, fixed edge, and final open/closed positions.

Chat 1 is complete only after this visual test is approved.

### Chat 2 — Duplicate and validate the `WorkerCollector` prefab

Goal: create a separate robot prefab named `WorkerCollector` without implementing collection behavior yet.

Work included:

1. Duplicate `Worker3` into `Assets/Resources/Prefabs/Robots/WorkerCollector/WorkerCollector.prefab`.
2. Preserve the existing worker art, master/puppet rig, colliders, joints, health, animation, and locomotion setup.
3. Keep `RobotMemoryNew`, `RobotBrainNew`, `RobotHeartNew`, and `RobotBodyController` on the duplicate.
4. Append `RobotRole.WorkerCollector` without changing any existing serialized role value.
5. Configure Heart to the `WorkerCollector` role with a harmless `WorkerCollectorStandby` default task.
6. Add an empty `CubeCarryAnchor` and `WorkerCollectorBodyController` shell at the intended hand/front position.
7. Place one temporary/manual instance in a safe Level 2 test position if needed for visual validation.
8. Add prefab/pipeline validation tests that protect required components, role, default task, and existing Worker role values.

Explicitly deferred:

- Finding, claiming, or grabbing cubes.
- Automatic spawning.
- Garage delivery and resting logic.

Manual approval checkpoint:

- Confirm the duplicated robot looks and behaves physically like `Worker3`.
- Confirm its scale, rig, colliders, animation, and player interaction are correct.
- Confirm the carry anchor is visually positioned correctly.
- Confirm the original `Worker3.prefab` was not changed by the duplication.
- Confirm the prefab contains one valid Memory → Brain → Heart → Task → Body pipeline and is safely idle.

Chat 2 is complete only after the duplicated prefab is approved.

### Chat 3 — Grab mechanism and conveyor cube collection

Goal: make a manually placed `WorkerCollector` identify and grab white cubes produced by the conveyor.

Work included:

1. Add `WhiteCubeCargo` identity and claim state to normal white cubes only.
2. Add Worker Collector mission/fact data to `RobotMemorySnapshotNew` and mutation events to Memory.
3. Add role-gated Worker Collector planning to Brain, default/task-stack handling to Heart, and task execution to `RobotTaskNew`.
4. Complete `WorkerCollectorBodyController` and connect it to `CubeCarryAnchor`.
5. Register the existing Level 2 conveyor as a reusable white-cube source provider.
6. Assign a conveyor cube through the mission service and Memory ingress.
7. Claim the cube, plan movement through Brain/Heart/Task, travel through the existing `RobotBodyController`, and attach it visibly.
8. Report safe observations when the target disappears or the player takes the target/carried cube so Brain replans.
9. Provide a temporary registered test drop-off/wait provider so pickup and transport can be evaluated without the final nine-cube garage cycle.
10. Add focused tests for the full pillar flow, white-only selection, exclusive claims, attachment, player theft, and cleanup.

Explicitly deferred:

- Automatic spawning of `WorkerCollector`.
- The complete 3 × 3 garage storage and batch processor.
- Returning to rest after nine cubes.
- The final continuous gameplay loop.

Manual approval checkpoint:

- Manually place/enable the `WorkerCollector`.
- Confirm it ignores colored upgrade cubes and waits for a white conveyor cube.
- Confirm it walks to, grabs, and visibly carries the white cube.
- Take the target before pickup and take a carried cube; confirm the worker recovers without freezing or duplicating it.
- Confirm the conveyor continues operating correctly after a cube is claimed.
- Confirm debug/probe output shows Memory observation → Brain plan → Heart task → body action rather than a direct controller transition.
- Confirm an ordinary Worker still selects and performs its normal work/rest tasks.

Chat 3 is complete only after collection and grabbing are approved.

### Chat 4 — Spawn and complete the Worker Collector loop

Goal: integrate the approved pieces into the complete Level 2 routine.

Work included:

1. Add a reusable Worker Collector spawn/rest provider to `ROOM_resting` and the corresponding spawner/bootstrap API.
2. Spawn exactly one collector for Level 2 and prevent duplicate live instances.
3. Add/configure reusable `GarageCubeStorage` and garage destination provider with nine explicit 3 × 3 slots in `ROOM_Garage`.
4. Add `GarageCubeProcessor` and connect it to the approved door controller.
5. Let `WorkerCollectorMissionService` discover the room providers and submit assignments through Memory ingress.
6. Connect collection, garage delivery, player-removal observations, full-batch processing, return-to-rest, rest delay, and resume behavior through the pipeline.
7. Disable/remove the incompatible garage-local automatic cube output in Level 2.
8. Add integration tests for spawning, the nine-cube loop, interrupted closing, despawning, resting, restarting, provider unregister cleanup, and ordinary Worker regression.
9. Run the full Edit Mode suite and the complete Level 2 smoke checklist.

Manual approval checkpoint:

- Confirm one worker spawns in the rest room.
- Observe a complete nine-cube collection and 3 × 3 fill.
- Remove stored cubes and confirm exact holes/count changes.
- Interrupt a closing cycle by taking a cube and confirm safe reopening.
- Complete a valid batch and confirm nine cubes despawn, the door reopens, the worker rests, and collection resumes after the delay.

Chat 4 completes the requested feature.

### Handoff rules between chats

At the end of every implementation chat:

1. Report exactly which files changed.
2. Report automated tests run and their results.
3. Give a short manual Unity test checklist for that checkpoint.
4. Record any Inspector values or scene references that still require tuning.
5. Do not begin the next chat until the user confirms the current checkpoint is correct.
6. If an earlier approved component needs redesign later, stop and explain why before changing it.

## 11. Test and Acceptance Plan

### Automated Edit Mode coverage

- Existing serialized `RobotRole` numeric values remain stable and `WorkerCollector` is appended.
- An ordinary `RobotRole.Worker` still produces its existing work/rest plans and tasks.
- The existing flying `RobotRole.Collector` still produces its existing Collector task family.
- Worker Collector observations update Memory before Brain plans a new task.
- Brain plans only Worker Collector tasks for the Worker Collector role.
- Heart owns the Worker Collector task stack and `RobotTaskNew` invokes the specialized body contract.
- Worker Collector movement continues through `RobotBodyController` and the existing waypoint service.
- Only marked white cubes are valid worker targets.
- Two claim attempts cannot own the same cube.
- A player may take a claimed/carried cube and the worker recovers.
- Delivery fills slots bottom row first.
- Nine accepted cubes produce `OccupiedCount == 9`.
- Taking slot 0, 4, or 8 produces `OccupiedCount == 8` and frees that same slot.
- Taking a cube during door closing cancels processing.
- A valid closed batch removes exactly nine registered cubes.
- One valid batch emits exactly one completion event.
- The garage reopens empty after processing.
- The worker rests only after the completion event, then resumes after the delay.
- Disable/destroy paths leave no claims, reservations, or event subscriptions behind.
- Dynamically enabled/disabled conveyor, garage, and rest providers register and unregister safely.

### Play Mode smoke checklist

1. Start Level 2 and confirm the worker appears in the rest room.
2. Confirm it selects only a white cube and visibly carries it.
3. Steal the carried cube and confirm the worker safely selects another.
4. Let it deposit several cubes and verify the intended 3 × 3 positions.
5. Remove a middle stored cube and confirm the count falls and that exact gap is refilled next.
6. Fill the ninth slot and confirm the door retract/stretch animation closes in one direction.
7. Steal the ninth cube while the door is closing and confirm the door reopens without processing.
8. Fill the garage again and allow it to close; confirm all nine cubes disappear, then the garage reopens.
9. Confirm the worker returns to rest only after that successful process.
10. Confirm it resumes collecting after the configured rest delay.

CLI test command from the project guidelines:

```bash
unity -runTests -testPlatform EditMode -projectPath "$(pwd)" -quit
```

## 12. Acceptance Criteria

The feature is complete when:

- The Worker Collector follows Memory → Brain → Heart → Task → Body for its complete routine.
- Existing Workers continue their work/rest behavior without new cube-collection branches.
- Existing waypoint/pathfinding behavior is unchanged.
- Rest, conveyor, and garage functionality lives on reusable room prefabs rather than Level 2-only object lookups.
- One dedicated worker reliably loops between rest, white-cube collection, garage delivery, and rest.
- It never deliberately targets a colored upgrade cube or another pickup type.
- Its carried cube is visible and can be stolen by the player without breaking the routine.
- The garage displays a stable 3 × 3 arrangement of up to nine real `CubePickup` objects.
- Every stored cube remains player-grabbable while physically accessible.
- Removing a cube immediately reduces the authoritative count and frees the correct slot.
- The door closes only for nine currently stored cubes, processes exactly those cubes, then reopens.
- An interrupted full state cannot incorrectly despawn eight or fewer cubes.
- The worker rests only after a successful nine-cube processing event.
- All new Edit Mode tests pass and the Level 2 smoke checklist passes.

## 13. Review Decisions

Please approve or change these points before implementation:

1. **Robot architecture:** required — add `RobotRole.WorkerCollector` and implement it through the existing Memory → Brain → Heart → Task → Body pipeline. Do not use a parallel routine controller.
2. **Compatibility:** required — do not change ordinary Worker decisions or existing waypoint/pathfinding behavior.
3. **Room reuse:** required — conveyor, garage, and rest/spawn capabilities belong to reusable room prefabs and register dynamically for later generated maps.
4. **Batch meaning:** recommended — the worker rests only after the garage successfully processes nine cubes, not after nine delivery attempts.
5. **Removed slot behavior:** recommended — preserve the exact hole and fill that hole next; do not rearrange remaining cubes.
6. **Door interruption:** recommended — if a cube is taken while closing, cancel and reopen.
7. **Door direction:** proposed — top edge fixed, bottom edge retracts upward.
8. **Worker appearance:** proposed — use a `Worker3` prefab variant initially, with no new artwork.
9. **White-cube source:** confirmed by the staged plan — use white cubes produced by the reusable conveyor room/provider.
10. **Stored cube physics:** recommended — kinematic in fixed slots until grabbed, then restored to dynamic physics.

Implementation should begin only after this document and the decisions above are approved.

## 14. Chat 4 Final Implementation Result and Level 2 Validation

Date reviewed: 2026-08-27.

This section records the actual state observed after the Chat 4 implementation. It replaces any assumption that Chat 4 is complete merely because the code and prefab wiring exist. The manual Level 2 acceptance checkpoint has not passed yet.

### 14.1 `approachWaypoint` usage

There are two separate source and destination uses of an approach waypoint:

- The **source approach waypoint belongs near the conveyor**, on reachable walking ground at the side from which the worker should approach a white cube. `WorkerCollectorWhiteCubeSourceProvider.approachWaypoint` is the last graph node on the route to the conveyor. `WorkerCollectorBodyController.BeginMoveToCube` then appends the claimed cube's position, adjusted for the carry-anchor offset, as a short final segment. It must not be placed near the garage.
- The **drop-off approach waypoint belongs near the garage entrance**, on reachable walking ground outside or immediately inside the entrance, depending on the approved door layout. `WorkerCollectorDropOffProvider.approachWaypoint` is the last graph node on the route to the garage. `WorkerCollectorBodyController.BeginMoveToDropOff` then appends `WorkerWaitPoint` as the short final segment.

The complete outward trip is therefore:

`worker current position -> static route nodes -> ConveyorApproach -> exact cube pickup position`

The complete delivery trip is:

`worker carrying cube -> static route nodes -> GarageApproach -> WorkerWaitPoint`

An approach waypoint is not a generic point shared by the conveyor and garage. Each capability needs its own explicitly assigned graph node. The direct line from an approach waypoint to its final position must be clear because the final segment does not calculate another obstacle-avoiding sub-path.

If an approach waypoint is not serialized, each provider attempts to resolve one. It prefers a `RoomWaypoint` whose type is `Work`, then falls back to the first waypoint it finds. This fallback is convenient but can select the wrong route anchor when a room contains multiple possible waypoints; the intended waypoint should therefore be explicitly configured in the prefab.

### 14.2 `WorkerWaitPoint` usage

Here, "final" means the final position of the **move-to-garage task**, not the final point of the complete collection loop.

`WorkerWaitPoint` is assigned to `WorkerCollectorDropOffProvider.waitPoint`. Its world position is exposed as `WaitPosition`. It occurs at this point in the process:

1. The worker claims and approaches a conveyor cube.
2. The worker grabs the cube.
3. The worker follows the waypoint graph to `GarageApproach`.
4. The worker moves over the short final segment from `GarageApproach` to `WorkerWaitPoint`.
5. Arrival is reported through Body -> Brain ingress -> Memory.
6. Brain selects `WorkerCollectorDepositCube`; the garage storage accepts and moves the cube into its reserved slot.
7. If the garage is not full, the worker leaves to find another cube.
8. If this was the ninth cube, the worker remains at `WorkerWaitPoint` during `WorkerCollectorWaitForBatch`. After successful processing it travels to the rest room.

Physically, `WorkerWaitPoint` should be a safe standing location beside the garage deposit area: close enough to make the hand-off believable, outside the 3 x 3 storage slots, outside the moving door/collider, and with a clear direct line from `GarageApproach`.

Despite its name, it is not currently a separate post-deposit waiting location. It acts as the delivery approach/standing position. The prefab also contains a `DeliveryPoint`, but the current provider/body path does not reference it. This is a naming and wiring mismatch that should be resolved before final acceptance: either use `DeliveryPoint` for delivery and reserve `WorkerWaitPoint` for batch waiting, or rename the existing point to describe its actual role.

### 14.3 Who spawns the Worker Collector

The collector is spawned dynamically; there is no manually placed Level 2 collector responsible for this flow.

1. The conveyor source, garage drop-off, and rest-room providers register with `WorkerCollectorMissionService` as their room prefabs become enabled.
2. `WorkerCollectorMissionService.TryEnsureCollectors` checks that a source and valid garage exist and that no live `WorkerCollectorBodyController` already exists.
3. The service calls `WorkerCollectorSpawnRestProvider.TrySpawnCollector` on the registered rest room.
4. The rest provider instantiates the configured `WorkerCollector.prefab` and limits its own live instances to one.

The supplied log confirms this chain was used: registration of `WorkerCollectorDropOffProvider` called `WorkerCollectorMissionService.TryEnsureCollectors`, which called `WorkerCollectorSpawnRestProvider.TrySpawnCollector`.

### 14.4 Confirmed spawn-rotation defect

The reported 90-degree rotation is real and its cause is identified:

- `ROOM_resting` has a root rotation of -90 degrees around X.
- `WorkerCollectorSpawnPoint` is a child of that rotated room and therefore inherits the room's world rotation.
- `WorkerCollectorSpawnRestProvider` currently instantiates the collector with `point.rotation`.

Consequently the collector inherits the room-space conversion rotation at spawn. The spawn implementation should use the collector prefab's intended world rotation (or an explicitly compensated spawn orientation) rather than blindly copying the room marker's inherited world rotation. This review records the defect only; no code or prefab correction was made during this documentation pass.

### 14.5 Memory -> Brain -> Heart -> Task architecture review

The implemented mission decision flow does follow the required architecture:

1. `WorkerCollectorMissionService` discovers and reserves external world capabilities, then submits the assignment through `RobotBrainNew.OnWorkerCollectorMissionAssigned`.
2. Brain ingress writes the assignment or body observation into `RobotMemoryNew` first.
3. Memory changes cause `RobotBrainNew` to select the next Worker Collector task.
4. Brain publishes that task to `RobotHeartNew`.
5. Heart owns and starts the task through `RobotTaskNew`.
6. `RobotTaskNew` invokes the specialized `IWorkerCollectorTaskBody` action.
7. The body reports results back through Brain ingress so that Memory is updated before the next plan.

Spawning and provider registration sit outside the robot's decision pipeline because they bootstrap the robot and expose world capabilities. They do not directly choose or execute the robot's collection behavior.

### 14.6 Result of the supplied Level 2 log

The supplied log proves only the beginning of the loop:

- One Worker Collector is spawned by the service/provider chain.
- Heart builds `WorkerCollectorStandby` as its safe default.
- Brain plans `WorkerCollectorFindCube`.
- Heart accepts and starts `WorkerCollectorFindCube`.

The log contains no later mission-assigned, move-to-cube, grab, move-to-garage, or deposit transition. There is also no exception or explicit error. Therefore the collector is stalled at the assignment boundary: `FindCube` is active, but `WorkerCollectorMissionService.RequestAssignment` has not produced an assignment visible in Memory.

The implementation retries `FindCube` every 0.5 seconds when assignment fails, but the current code does not log why an attempt failed. From this log alone it is not possible to choose conclusively between these assignment prerequisites:

- no currently claimable `WhiteCubeCargo` exists beneath the registered conveyor source's configured `cubeRoot`;
- the garage cannot currently accept/reserve a delivery slot;
- a required provider/reference exists but resolves to an unintended hierarchy object.

The runtime pipeline mode is not the likely blocker in this checkout: `NewShadow` is configured to drive gameplay. The next diagnostic pass should add focused assignment-failure reporting or inspect these three prerequisites live in the Unity Inspector. No such diagnostic or gameplay change was made during this documentation-only pass.

### 14.7 Acceptance status

Focused automated coverage for the Worker Collector/Garage implementation passed (25 tests). The broader Edit Mode run reached 229 passing tests with 29 failures belonging to existing unrelated fixtures. Automated tests do not replace the required Level 2 smoke test.

Current Chat 4 status: **implemented but not accepted**.

Remaining blockers for acceptance:

1. Correct the inherited 90-degree spawn rotation.
2. Identify and correct the failed assignment prerequisite after `WorkerCollectorFindCube` starts.
3. Clarify and wire the distinct delivery and waiting positions.
4. Run and pass the complete nine-cube Level 2 smoke checklist in Section 11.

### 14.8 Static-level navigation dependency

The current Level 2 failure is not only a question of where the approach points are placed. Static scene setup does not create or connect the navigation infrastructure used by `RobotBodyController`:

- `SceneBootstrapper` instantiates `WaypointService` only for `GeneratedMap` mode.
- `FactoryManager.InitializeStatic` explicitly keeps its waypoint-service reference null.
- `RoomManager.InitializeStatic` consequently does not register the room's waypoint list.
- The dynamically spawned Worker Collector is not initialized with an `IWaypointQueries`/`IWaypointNotifier` service.
- No static graph is built before `SetDestination` is requested.

Therefore valid-looking transforms in Level 2 are insufficient: the collector can receive a movement task while its Body has no usable navigation graph.

The proposed solution, constraints, graph-authoring workflow, and acceptance tests are specified separately in `Docs/Static_Level_Robot_Pathfinding_Design.md`. That design is intentionally opt-in for the four authored levels so the generated-map navigation flow remains unchanged.

### 14.9 Acceptance-blocker remediation

Implementation update: 2026-08-27.

The code and prefab blockers identified above have now been addressed:

1. The rest provider spawns with the Worker Collector prefab's intended world rotation instead of inheriting the rotated room marker's world rotation.
2. Mission assignment now waits until `RobotBodyController` has a usable navigation service. Dynamically spawned robots are initialized against the ready shared graph, preventing a move task from being silently issued before pathfinding exists.
3. Assignment failures expose a specific `WorkerCollectorAssignmentFailure` reason (`NavigationUnavailable`, `NoGarageSlot`, `NoWhiteCube`, or pipeline rejection) instead of remaining indistinguishable.
4. `DeliveryPoint` and `WorkerWaitPoint` are distinct provider references. The move-to-garage task targets `DeliveryPoint`; the batch-wait task moves to `WorkerWaitPoint`.
5. `Level_2.unity` contains an enabled `StaticLevelPath` route, so the static waypoint service can be created, registered, and supplied to robots through the shared initialization path.

The remaining acceptance action is the manual Play Mode smoke checklist in Section 11. Automated compilation succeeds, but the focused Unity test runner could not be launched from the CLI while this project was already open in another Unity Editor instance.

## 15. Right-Arm Cube Pickup Correction

Date reviewed: 2026-09-01.

This section supersedes the cube-approach and hand-attachment details in Sections 4.3, 7, and 14.1 where they conflict. The mission, navigation, and IK systems are present, but the current pickup geometry does not produce the intended right-arm reach.

### 15.1 Final diagnosis

The current pickup has four connected defects:

1. The collector has only a right-arm pickup action, but the approach calculation does not explicitly guarantee that the worker stops on the left side of the cube with the cube on its right.
2. `BeginMoveToCube` subtracts the resting solver-target offset from the cube position. This makes the worker walk until `RArm_Solver_Target` is already approximately on the cube, leaving no visible reach for the arm to perform.
3. The cube presentation uses an X/Z position convention while the worker, `LimbSolver2D`, and navigation operate in an X/Y plane. The current code mixes raw `Vector3` positions and implicit `Vector2` casts instead of applying one explicit conversion.
4. `RArm_Solver_Target` is both the IK command target and the effective carry parent. The target should be moved to command IK, but the secured cube should follow a hand/carry anchor aligned with `RHand_Effector`, not the invisible solver controller.

The supplied Level 2 log reaches `WorkerCollectorMoveToCube` but ends before the worker reaches the conveyor. It contains no `Body reached safe cube approach`, `WorkerCollectorGrabCube`, arm timeout, or grab-success event. The log therefore confirms assignment and navigation startup, while the defects above are established by the runtime geometry and prefab wiring.

### 15.2 One authoritative coordinate conversion

Worker Collector code must not independently decide whether a cube uses XY or XZ. The source/provider boundary converts the claimed cube position once and exposes a canonical worker-plane pickup point:

```csharp
Vector2 CubeToWorkerPlane(Vector3 cubePosition)
{
    return new Vector2(cubePosition.x, cubePosition.z);
}
```

For the Level 2 conveyor, this means:

```text
cube X -> worker/IK X
cube Z -> worker/IK Y
worker/IK Z -> preserved rendering depth
```

If the X/Z values are local to the rotated conveyor rather than world values, the provider must first resolve the cube through the configured conveyor reference frame. Callers still receive only the resulting canonical `Vector2`; they must not repeat the transform or swap.

All pickup calculations then use the same worker-plane values:

- source claim-radius checks;
- final body approach position;
- target-side validation;
- solver-target destination;
- hand-effector reach distance;
- diagnostics and tests.

When commanding the IK target, only its worker-plane X/Y values change. Its authored Z depth remains unchanged:

```csharp
Vector2 pickupPoint = source.GetWorkerPlanePickupPoint(cube);
Vector3 targetPosition = armSolverTarget.position;
targetPosition.x = pickupPoint.x;
targetPosition.y = pickupPoint.y;
armSolverTarget.position = targetPosition;
```

This contract must be validated against the live cube `Rigidbody2D`. If the live cube already moves in world XY and only appears as X/Z in the rotated parent's local Inspector values, the provider performs the reference-frame conversion rather than blindly swapping the cube's world Y and Z. There must still be exactly one authoritative conversion path.

### 15.3 Left-side body approach for the right arm

The worker must approach from the cube's left so the cube is on the right side of the worker:

```text
worker body ---- resting right-hand target ---- reach gap ---- cube
```

The final body position is derived from a desired resting target position, not from a target position already placed on the cube:

```csharp
Vector2 cubePoint = source.GetWorkerPlanePickupPoint(cube);
Vector2 desiredRestTarget = cubePoint + Vector2.left * armReachStartDistance;
Vector2 bodyDestination = desiredRestTarget - rightArmRestOffsetFromBody;
```

`rightArmRestOffsetFromBody` is captured in the same worker plane from the navigation body reference to `RArm_Solver_Target`. `armReachStartDistance` is serialized and must be:

- greater than `pickupDistance`, so the arm visibly moves before pickup;
- smaller than the usable right-arm IK reach;
- large enough that the worker body and cube colliders do not overlap.

Proposed initial tuning:

| Setting | Initial value |
|---|---:|
| Pickup success distance | 0.6 |
| Arm reach start distance | 1.2 |
| Minimum accepted start distance | 0.8 |
| Maximum accepted start distance | 1.6 |
| Arm reach timeout | 3.0 s |

The exact values are tuning data. The invariant is more important: at navigation arrival, the cube's worker-plane X must be greater than the worker/right-shoulder X and the hand must be outside `pickupDistance` but inside the arm's reachable range. If those conditions are false, the body must correct its position before publishing `TargetApproachReached`.

### 15.4 IK and grab sequence

The corrected physical sequence is:

```text
MoveToCube
    -> stop left of cube with a valid reach gap
    -> publish TargetApproachReached
GrabCube
    -> move RArm_Solver_Target toward canonical pickup point in worker XY
    -> LimbSolver2D moves the master-puppet right-arm chain
    -> observe RHand_Effector distance to canonical pickup point
    -> secure when effector distance <= pickupDistance
    -> attach/follow a dedicated carry anchor aligned under RHand_Effector
    -> hold the solver-target pose while carrying
```

`RHand_Effector` is an observation/output of the IK chain and must not be directly translated by `WorkerCollectorBodyController`. `RArm_Solver_Target` is the commanded transform. The carry anchor may be `RHand_Effector` itself or a dedicated child authored for cube alignment, but it must not be the solver target.

Reach distance is calculated after conversion, between two values in the same plane:

```csharp
Vector2 handPoint = new Vector2(armEffector.position.x, armEffector.position.y);
float reachDistance = Vector2.Distance(handPoint, pickupPoint);
```

For comparisons, the implementation may use squared distance to avoid a square root, but it must not compare worker XY directly with an unconverted cube XZ `Vector3`.

### 15.5 Facing and IK-side requirements

Before reaching:

- the worker must be positioned left of the cube;
- its visual facing must be right;
- the right-arm `LimbSolver2D` flip/orientation must match a target on the right;
- locomotion stopping must not leave the animator or rig visibly facing left after a leftward trip from the rest room.

The player `ArmTargetController` already demonstrates side-aware IK orientation. The Worker Collector does not need the player's input, energy, attack, or grab-detection controller, but it does need the equivalent deterministic right-side solver orientation when beginning `WorkerCollectorGrabCube`.

### 15.6 Required diagnostics

The arrival and reach logs must include enough information to distinguish navigation, coordinate conversion, and IK failures:

```text
cubeWorld=(x,y,z)
cubeLocal=(x,y,z)
pickupWorkerPlane=(x,y)
bodyWorkerPlane=(x,y)
solverTargetWorkerPlane=(x,y)
effectorWorkerPlane=(x,y)
cubeIsRightOfWorker=true/false
startDistance=value
reachDistance=value
```

A pickup run is not considered validated unless the log contains, in order:

1. safe left-side approach reached;
2. `WorkerCollectorGrabCube` started;
3. solver target moved a measurable distance;
4. hand effector entered `pickupDistance`;
5. cube carry ownership succeeded.

### 15.7 Required tests

Add focused coverage for the real body geometry, not only task-spy transitions:

1. Level 2 cube coordinates convert from the configured cube plane to worker XY exactly once.
2. A cube on the worker's right produces a body destination on the cube's left.
3. Navigation arrival leaves the solver target farther than `pickupDistance` and within configured arm reach.
4. `GrabCube` moves `RArm_Solver_Target`; it never directly moves `RHand_Effector`.
5. Reach success is measured from `RHand_Effector` in the canonical worker plane.
6. The carried cube follows the effector-aligned carry anchor, not `RArm_Solver_Target`.
7. The worker faces right and the right-arm solver uses the correct orientation before reaching.
8. A coordinate or reach-range mismatch produces an explicit diagnostic and a recoverable retry rather than a silent stall.

The existing `RuntimePrefab_UsesRightArmIkTargetAsCarryAnchor` assertion must be replaced because it currently codifies the incorrect carry-parent behavior.

### 15.8 Implementation order

1. Introduce and test the provider-owned cube-to-worker-plane conversion.
2. Expose/capture the right-arm rest offset and effector-aligned carry anchor.
3. Replace the current final-position calculation with the left-side reach-gap calculation.
4. Preserve solver-target Z while moving its worker-plane X/Y toward the pickup point.
5. Validate facing and right-arm solver orientation.
6. Measure completion at `RHand_Effector`, then secure the cube to the hand carry anchor.
7. Add the focused runtime diagnostics and tests above.
8. Run the Level 2 smoke test long enough for the worker to cross the map, reach, grab, deliver, and repeat.

No waypoint-graph, Brain, Heart, task-stack, player-control, or general Worker-role changes are required for this correction.
