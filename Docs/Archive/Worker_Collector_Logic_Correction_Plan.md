# Worker Collector Logic Correction Plan

Status: **Implementation design - ready for review**  
Date: 2026-09-03  
Primary scene: `Assets/Scenes/Level_2.unity`  
Related design: `Docs/Level_2_Cube_Worker_and_Garage_Design_Review.md`

## 1. Purpose

Correct the Worker Collector runtime loop so it services the cube garage by traveling
to stable room waypoints and reacting to local observations. The worker must wait at the
conveyor Work waypoint, reach for a cube as the conveyor brings it close, carry it to the
garage, deposit it, repeat until a nine-cube batch is processed, rest, and begin again.

The correction must preserve the established pipeline:

```text
World/provider/body observation
    -> RobotMemoryStateNew / RobotMemoryNew stores facts
    -> RobotBrainNew selects one concrete action
    -> RobotHeartNew owns and changes the active task
    -> RobotTaskNew dispatches that task
    -> RobotBodyController / WorkerCollectorBodyController performs physical work
```

The Worker Collector body may detect local physical facts and report them. It must not
choose its next high-level task. Providers expose capabilities and observations; they do
not push tasks directly.

## 2. Player-Visible Behavior

The required loop is:

```text
Spawn
    -> travel to conveyor Work waypoint
    -> wait at that waypoint while the conveyor runs normally
    -> select the nearest eligible white cube in the arm acquisition area
    -> move the appropriate arm toward the moving cube
    -> automatically grab when the active hand is close enough
    -> travel to the garage delivery waypoint
    -> release/store the cube in the reserved garage slot
    -> if garage occupancy is below 9: return to conveyor
    -> if the ninth cube was accepted: wait for garage processing
    -> after the batch is removed: travel to rest
    -> rest for the configured duration
    -> return to conveyor and repeat
```

The player remains allowed to take a loose, targeted, carried, or stored cube. The worker
must recover through facts and replanning rather than fighting the player for ownership.

## 3. Findings from the Current Implementation

The current implementation uses Memory, Brain, Heart, and Task, but models the wrong
pickup workflow.

### 3.1 Current startup and pickup sequence

Current behavior is:

```text
WorkerCollectorFindCube polls at the spawn position
    -> MissionService reserves a garage slot
    -> SourceProvider waits for a cube near its approach waypoint
    -> MissionService claims that specific cube
    -> one immutable mission contains source + cube + claim + garage + reservation + rest
    -> Brain plans WorkerCollectorMoveToCube
    -> body calculates a final world position from that cube
    -> conveyor stops the cube because it is Claimed
    -> worker attempts to reach the calculated cube position
```

This differs from the required behavior in four important ways:

1. `WorkerCollectorFindCube` performs no navigation. The worker waits wherever it spawned.
2. A specific cube is required before the worker can be told where the conveyor is.
3. The cube is claimed too early and the conveyor deliberately stops a claimed cube.
4. Navigation follows a cube-derived final position instead of the stable conveyor Work
   waypoint.

### 3.2 Why the arrival-tolerance adjustment was insufficient

The pickup route currently replaces the final Work waypoint position with a calculated
position around the selected cube. Relaxing its vertical arrival threshold can avoid one
physics deadlock, but it cannot correct the task model. The robot is still chasing a
specific cube before entering a local waiting/acquisition state.

The configured pickup arrival threshold may remain as a safety/tuning value if a short
local body adjustment is retained later. It must no longer be the condition for traveling
across the level to a moving cargo target.

### 3.3 Current delivery and batch behavior

The following existing behavior is conceptually correct and should be retained:

- a secured cube causes Brain to plan garage travel;
- the garage owns nine explicit storage slots;
- a cube counts only after storage accepts it;
- deliveries one through eight restart acquisition;
- delivery nine waits for the authoritative batch-completed event;
- the garage closes, validates the full batch, removes its nine registered cubes, and
  opens;
- batch completion leads to rest;
- completion of rest begins another service cycle.

The correction is therefore primarily a redesign of assignment, source travel, local
acquisition, and pickup recovery.

## 4. Correct Domain Model

The implementation must separate stable service routing from temporary cube ownership.

### 4.1 Stable service assignment

Create or reshape the mission payload so the durable assignment contains only stable
capabilities:

```text
WorkerCollectorServiceAssignment
    SourceProvider
    SourceApproachWaypoint
    DropOffProvider
    RestProvider
    ServiceId
```

It must not require a cube, cube claim, or garage slot reservation to exist.

The service assignment is the runtime representation of the worker's durable goal:
**service this garage using this conveyor and this rest location**.

The durable goal does not need to be an endlessly executing `ServiceGarage` body task.
In the current `*New` architecture, Brain should derive one concrete physical task from
the service facts, and Heart should execute that task. This keeps task execution simple
and avoids placing business policy in Heart or Body.

### 4.2 Temporary cube target

Cube selection is local and short-lived:

```text
WorkerCollectorCubeTarget
    WhiteCubeCargo Target
    WhiteCubeClaim Claim
```

It is created only while the worker is at the source and a suitable cube is inside the
configured acquisition area. It is cleared when:

- the cube leaves the valid collection area;
- the conveyor destroys it at the exit;
- the player grabs it;
- the cube becomes disabled;
- pickup fails or is cancelled;
- the worker successfully transfers it into garage storage.

### 4.3 Garage reservation

A garage reservation is also temporary. It should be acquired only when a cube is about
to be collected or after cargo becomes secured, not while the worker is still at spawn.

Recommended first implementation:

1. Confirm the garage is accepting and reserve a slot before committing the grab.
2. If no slot is available, do not take another cube; remain at the source or enter an
   explicit garage-availability wait state.
3. Release the reservation on target loss, cargo theft, cancellation, provider disable,
   or failed delivery.

This preserves capacity correctness without coupling source navigation to a cube.

## 5. Memory Facts

Replace the monolithic progress interpretation with explicit observed facts. Suggested
`WorkerCollectorMissionFacts` fields are:

```text
ServiceAssignment
SourceApproachReached
Target
TargetClaim
TargetAvailable
CargoSecured
GarageReservation
DropOffApproachReached
DeliveryAccepted
WaitingForBatch
BatchCompleted
RestApproachReached
RestUntil
RestCompleted
NavigationFault
```

Rules:

- Memory stores facts only.
- `SourceApproachReached` is reported by Body after waypoint arrival.
- Target selection/claim is reported through a narrow Brain ingress method and committed
  to Memory before Brain plans arm reach.
- `CargoSecured` is reported only after `CubePickup` has attached the cube to the worker's
  carry anchor.
- Garage occupancy is not copied into a worker-side counter. `WaitingForBatch` and
  `BatchCompleted` come from the garage's authoritative result.
- Starting a new ordinary delivery clears target, claim, reservation, approach, and
  delivery facts without discarding the stable service assignment.
- Completing rest clears cycle progress and sends the worker back to the source.

Suggested new or renamed memory changes:

```text
WorkerCollectorServiceAssigned
WorkerCollectorSourceApproachChanged
WorkerCollectorTargetAcquired
WorkerCollectorTargetInvalidated
WorkerCollectorCargoChanged
WorkerCollectorGarageReservationChanged
WorkerCollectorDropOffChanged
WorkerCollectorDeliveryChanged
WorkerCollectorBatchCompleted
WorkerCollectorRestChanged
WorkerCollectorNavigationFaultChanged
```

## 6. Brain Planning Rules

`RobotBrainNew.BuildWorkerCollectorTask` remains the only mapping from Worker Collector
facts to concrete tasks.

Recommended task family:

```text
WorkerCollectorStandby
WorkerCollectorMoveToSource
WorkerCollectorFindCube
WorkerCollectorGrabCube
WorkerCollectorMoveToGarage
WorkerCollectorDepositCube
WorkerCollectorWaitForGarage
WorkerCollectorWaitForBatch
WorkerCollectorMoveToRest
WorkerCollectorRest
```

`WorkerCollectorMoveToCube` should be removed from active planning or retained only as a
temporary compatibility enum value. It must not be used for cross-level travel to a
moving cube.

Brain decision order:

```text
if no valid service assignment
    -> WorkerCollectorStandby (service discovery requests assignment separately)

else if navigation fault requires recovery
    -> configured recovery task/standby policy

else if batch completed and rest not reached
    -> WorkerCollectorMoveToRest

else if batch completed and rest not completed
    -> WorkerCollectorRest

else if delivery filled slot 9 and batch not completed
    -> WorkerCollectorWaitForBatch

else if cargo is secured and drop-off not reached
    -> WorkerCollectorMoveToGarage

else if cargo is secured and drop-off reached
    -> WorkerCollectorDepositCube

else if source approach not reached
    -> WorkerCollectorMoveToSource

else if no valid local cube target
    -> WorkerCollectorFindCube

else
    -> WorkerCollectorGrabCube
```

After deliveries one through eight, reset per-delivery facts but retain the service
assignment. Brain then selects `WorkerCollectorMoveToSource` unless the worker is already
at the source and that fact is still valid.

After rest, reset cycle/rest facts and select `WorkerCollectorMoveToSource`.

## 7. Task and Body Contracts

### 7.1 `WorkerCollectorMoveToSource`

Body action:

- clear arm target;
- navigate only to `SourceProvider.ApproachWaypoint`;
- do not require or select a cube;
- do not replace the waypoint with a cube-derived world position;
- report `SourceApproachReached` through Brain when `HasArrivedAtDestination()` becomes
  true.

This task is the only cross-room movement used to reach the conveyor.

### 7.2 `WorkerCollectorFindCube`

This becomes a stationary local acquisition task.

Body/source action:

- stop locomotion at the source Work waypoint;
- observe loose `WhiteCubeCargo` instances owned by the source;
- select the nearest eligible cube to the active hand/body, not the first hierarchy item;
- restrict selection to a configurable arm acquisition radius;
- acquire a claim and garage reservation through the service/provider contracts;
- report the target and claim to Memory through Brain ingress.

Polling at a small local interval is acceptable for physical target discovery. It is not
global strategic planning. If the conveyor/provider can publish cube-entered and
cube-exited events reliably, prefer those events and keep a low-frequency validation
fallback.

### 7.3 `WorkerCollectorGrabCube`

Body action:

- remain stationary;
- set `RobotObjectArmReachController.Target` to the current Memory target;
- choose the correct arm using the reusable arm controller;
- continuously follow the cube while it moves on the conveyor;
- when the active hand is within `pickupDistance`, call
  `WhiteCubeCargo.TryBeginCarry(claim, carryAnchor)`;
- report `CargoSecured` only after attachment succeeds.

The Inspector `Target` field remains empty in the production prefab. Runtime task entry
sets it, and task exit/loss/delivery clears it.

### 7.4 Conveyor behavior

The conveyor must continue moving an available or worker-claimed cube. Remove the current
rule that returns early when `WhiteCubeCargoState.Claimed`.

Expected outcomes:

- the cube approaches and passes the waiting worker naturally;
- the arm follows it during the grab task;
- successful `CubePickup.OnGrab` removes it from conveyor ownership through the existing
  grab notification;
- if it reaches the conveyor exit first, the conveyor destroys it, `WhiteCubeCargo`
  invalidates the claim, Memory clears the target, and Brain returns to local
  `WorkerCollectorFindCube` without sending the worker across the map.

### 7.5 Garage tasks

Keep the current responsibilities:

- `MoveToGarage` navigates using the garage approach waypoint and stable delivery point.
- `DepositCube` requests authoritative storage acceptance and releases the cube into its
  reserved slot.
- if the garage temporarily cannot accept a carried cube, keep ownership and use
  `WorkerCollectorWaitForGarage`; do not silently leave `DepositCube` in a state that has
  no observation capable of causing replanning.
- successful deliveries below capacity reset the delivery and source-approach facts for
  the next trip.
- the ninth successful delivery enters `WaitForBatch`.

### 7.6 Rest tasks

Keep the current batch-driven rest rule:

- only `GarageCubeProcessor.OnBatchCompleted` authorizes rest;
- move to the registered rest waypoint;
- start the configurable rest timer only after arrival;
- report rest completion through Brain ingress;
- retain the stable service assignment and return to the conveyor afterward.

## 8. Provider and Service Responsibilities

### `WorkerCollectorMissionService`

Change from specific-cube mission matching to stable service matching.

It should:

- register sources, garages, and rest providers;
- assign compatible stable service providers even when no cube exists;
- never require a cube to tell a worker where its conveyor is;
- avoid reserving a garage slot during initial service assignment;
- coordinate or validate short-lived target claims/reservations when local acquisition
  begins;
- release temporary ownership on failure and provider teardown.

### `WorkerCollectorWhiteCubeSourceProvider`

It should:

- expose its stable Work approach waypoint;
- expose/query active white cargo under its conveyor hierarchy;
- select the nearest eligible target relative to a supplied worker/hand position;
- enforce a configurable acquisition radius;
- never decide worker tasks;
- never require the worker to chase the cube's instantaneous position with locomotion.

### `WorkerCollectorDropOffProvider`

It remains responsible for:

- availability and slot reservation;
- stable approach/delivery/wait positions;
- accepting a valid carried cube;
- reporting batch completion/interruption to the waiting worker;
- releasing reservations and reporting destination loss on disable.

## 9. Failure and Recovery Rules

| Situation | Memory observation | Brain result |
|---|---|---|
| No cube on conveyor | No local target | Stay in `FindCube` at source |
| Candidate passes out of range | Target invalidated | Clear arm, remain in `FindCube` |
| Conveyor destroys candidate | Target invalidated | Clear arm, remain in `FindCube` |
| Player grabs candidate | Target invalidated | Clear arm, remain in `FindCube` |
| Player steals carried cube | Cargo lost | Return to source/acquisition |
| Garage has no slot before grab | Reservation unavailable | Do not grab; wait/retry locally |
| Garage rejects carried cube temporarily | Destination unavailable/closed | Keep cargo and wait for garage |
| Source provider disappears | Service source unavailable | Clear service assignment and standby/reassign |
| Garage provider disappears | Service drop-off unavailable | Preserve cargo safely and standby/reassign by explicit policy |
| Worker navigation becomes stuck | Navigation fault | Worker-specific recovery; no ordinary-worker respawn call |

The existing generic stuck path currently logs that the respawn service is null and then
returns the Worker Collector to the pool. Correct this as part of the redesign:

- do not call ordinary `RespawnWorker()` for `RobotRole.WorkerCollector`;
- either provide a dedicated Worker Collector respawn service or report a navigation
  fault to Memory and let Brain select a safe recovery;
- release cube claims and garage reservations exactly once when the worker is actually
  despawned.

## 10. Implementation Order

### Phase 1 - Contracts and Memory

1. Split stable service assignment from temporary target/claim/reservation state.
2. Add source-arrival and target-acquisition observations.
3. Add narrow mutation/ingress APIs in `RobotMemoryStateNew`, `RobotMemoryNew`, and
   `RobotBrainNew`.
4. Ensure per-delivery reset retains stable service providers.

### Phase 2 - Brain, Heart, and tasks

1. Add `WorkerCollectorMoveToSource` and optional `WorkerCollectorWaitForGarage`.
2. Redefine `WorkerCollectorFindCube` as stationary local acquisition.
3. Stop planning `WorkerCollectorMoveToCube` for production behavior.
4. Update the Worker Collector task family boundaries in `RobotTaskStackNew` if enum
   ordering changes.
5. Update `IWorkerCollectorTaskBody` and `RobotTaskNew` dispatch.

### Phase 3 - Source and arm acquisition

1. Navigate to the source waypoint without a cube.
2. Implement nearest-cube local selection.
3. Let claimed cubes continue along the conveyor.
4. Enter arm reach only after Memory contains a valid local target.
5. Automatically attach only within hand pickup distance.

### Phase 4 - Delivery and cycle integration

1. Move garage reservation to the local acquisition/delivery boundary.
2. Retain the existing garage storage and batch processor.
3. Reset per-delivery facts after slots one through eight.
4. Verify ninth-delivery wait, batch processing, rest, and return-to-source.

### Phase 5 - Recovery and diagnostics

1. Correct Worker Collector stuck handling.
2. Add concise state-transition diagnostics.
3. Remove obsolete moving-cube approach calculations after compatibility is no longer
   needed.

## 11. Expected File Changes

Primary runtime files:

- `Assets/Scripts/Robots/WorkerCollector/WorkerCollectorMissionContracts.cs`
- `Assets/Scripts/Robots/WorkerCollector/WorkerCollectorMissionService.cs`
- `Assets/Scripts/Robots/WorkerCollector/WorkerCollectorWhiteCubeSourceProvider.cs`
- `Assets/Scripts/Robots/WorkerCollector/WorkerCollectorBodyController.cs`
- `Assets/Scripts/Robots/WorkerCollector/WorkerCollectorDropOffProvider.cs`
- `Assets/Scripts/Robots/RobotMemorySnapshotNew.cs`
- `Assets/Scripts/Robots/RobotMemoryStateNew.cs`
- `Assets/Scripts/Robots/RobotMemoryNew.cs`
- `Assets/Scripts/Robots/RobotBrainNew.cs`
- `Assets/Scripts/Robots/RobotHeartNew.cs`
- `Assets/Scripts/Robots/Tasks/RobotTasks.cs`
- `Assets/Scripts/Robots/Tasks/RobotTaskStackNew.cs`
- `Assets/Scripts/Robots/Tasks/RobotTaskNew.cs`
- `Assets/Scripts/Factory/Upgrades/GarageCubeConveyorController.cs`
- `Assets/Scripts/Robots/Body/RobotBodyMaintenance.cs`

Prefab/scene files should change only when a new serialized acquisition radius, task
component reference, or service-provider field requires it:

- `Assets/Resources/Prefabs/Robots/WorkerCollector/WorkerCollector.prefab`
- `Assets/Resources/Prefabs/Map/ROOM_Conveyor.prefab`
- `Assets/Resources/Prefabs/Map/ROOM_Garage.prefab`
- `Assets/Resources/Prefabs/Map/ROOM_resting.prefab`
- `Assets/Scenes/Level_2.unity`

Do not add direct Level 2 object references to the Worker Collector prefab. Provider
registration remains the reusable connection mechanism for authored and generated maps.

## 12. Automated Test Plan

### Memory and Brain tests

- Service assignment succeeds when no cube exists.
- Service assignment with source not reached plans `MoveToSource`.
- Source arrival with no target plans stationary `FindCube`.
- Valid local target acquisition plans `GrabCube`.
- Cargo secured plans `MoveToGarage`.
- Garage arrival plans `DepositCube`.
- Delivery below capacity resets per-delivery facts and plans `MoveToSource`.
- Ninth delivery plans `WaitForBatch`.
- Batch completion plans `MoveToRest`, then `Rest` after arrival.
- Rest completion plans `MoveToSource` while retaining the service assignment.
- Target loss before pickup stays at the source and plans `FindCube`.
- Cargo theft after pickup plans return to source.

### Source and conveyor tests

- Source returns the nearest eligible white cube inside acquisition range.
- Source rejects colored cubes, stored cubes, externally held cubes, and out-of-range
  cubes.
- A claimed conveyor cube continues moving.
- Grabbing a conveyor cube removes it from conveyor ownership and schedules the next cube.
- A cube reaching the exit invalidates its worker claim and schedules the next cube.

### Body and arm tests

- `MoveToSource` sends exactly the source Work waypoint and no cube-derived final position.
- `FindCube` stops locomotion.
- `GrabCube` assigns the target at runtime and selects the correct arm.
- A moving cube is attached only when the active hand enters `pickupDistance`.
- Target loss clears the arm target without moving away from the source.
- Successful attachment reports cargo secured once.

### Garage and cycle regression tests

- Existing nine-slot occupancy and player-removal tests continue passing.
- Deliveries one through eight do not trigger rest.
- Only a successfully processed nine-cube batch triggers rest.
- A batch interrupted by player removal does not trigger rest.
- After rest, the same worker returns to its assigned conveyor.

### Unchanged-role regression tests

- `RobotRole.Worker` planning and prefab behavior remain unchanged.
- `RobotRole.Collector` flying collection behavior remains unchanged.
- Security, follower, spawner, and boss task planning remain unchanged.

## 13. Play Mode Acceptance Scenario

Run Level 2 without manually assigning the arm target.

1. Worker Collector spawns at the rest provider.
2. Before any cube is available, it begins traveling to the conveyor Work waypoint.
3. It stops at the authored source position and does not chase a cube with locomotion.
4. Conveyor cubes continue moving at normal speed.
5. When a white cube enters acquisition range, the nearer arm follows it.
6. The hand automatically grabs it within `pickupDistance`.
7. The worker carries it through the waypoint graph to the garage.
8. The cube is stored in the first available explicit slot.
9. Steps 2-8 repeat for slots one through nine.
10. The garage closes only at nine occupied slots, removes that exact batch, and reopens.
11. The worker goes to rest only after the batch-completed observation.
12. After the rest timer, the worker returns to the conveyor and begins another batch.

During this scenario:

- taking a targeted cube makes the worker select another local cube;
- taking a carried cube sends the worker back to the source;
- taking a stored cube immediately decreases authoritative occupancy;
- no `Cannot respawn: service is null` error appears;
- no Worker Collector is repeatedly pooled and recreated because of an unreachable
  moving-cube destination;
- the production arm controller's Inspector target is empty outside `GrabCube` and is
  populated only at runtime during local reach.

## 14. Required Diagnostics

Use transition logs, not per-frame spam. At minimum record:

```text
ServiceAssigned source=<name> garage=<name> rest=<name>
MoveToSource waypoint=<name>
SourceReached body=<position>
WaitingForCube acquisitionRadius=<value>
TargetAcquired cube=<name> distance=<value> claim=<version>
GrabStarted arm=<left/right> handDistance=<value>
CargoSecured cube=<name>
MoveToGarage waypoint=<name> slot=<index>
DeliveryAccepted occupied=<count>/9
BatchWaiting
BatchCompleted
MoveToRest waypoint=<name>
RestStarted duration=<seconds>
RestCompleted
TargetLost reason=<player/conveyor-exit/disabled/out-of-range>
NavigationFault task=<task> body=<position> waypoint=<name>
```

These logs must make it possible to distinguish service discovery, room navigation,
local cube waiting, arm reach, carrying, garage processing, and rest without inspecting
serialized runtime fields manually.

## 15. Definition of Done

The correction is complete only when:

1. A worker travels to the conveyor even if no cube currently exists.
2. Cross-room navigation uses stable provider waypoints, never a moving cube position.
3. The worker waits at the source and the conveyor continues moving claimed candidates.
4. The arm tracks and automatically grabs an eligible moving cube locally.
5. Every high-level transition is caused by a Memory fact and planned by Brain.
6. Heart remains the sole task-stack owner and Task remains a thin dispatcher.
7. The existing garage, nine-cube batch, and rest loop works end to end.
8. Player cube theft recovers correctly at every ownership stage.
9. Worker Collector stuck recovery does not use a missing ordinary-worker respawn service.
10. Edit Mode tests pass and the Level 2 Play Mode acceptance scenario completes two
    consecutive batches without manual Inspector intervention.
