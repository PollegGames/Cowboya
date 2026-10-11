# Factory Robot Population and AI Refactor Plan

## Purpose

This document reconciles the current `tourB` source with the required factory, robot population, and robot AI ownership model. It is the implementation plan for the refactor. Existing uncommitted code, prefab, scene, asset, and test changes must be preserved and reviewed as part of each stage.

The target is one factory domain, one robot population lifecycle, and one robot decision pipeline:

```text
Factory capabilities and assignments -> Memory -> Brain -> Heart -> Task -> Body
Body observations -> domain event or Brain ingress -> Memory
```

Factory services may publish facts and stable assignments. They must not choose or push an individual robot task. `RobotHeartNew` remains the sole owner of task-stack mutation.

## Current architecture map

| Responsibility | Current owner | Current issue |
| --- | --- | --- |
| Room registration and factory-wide power summary | `FactoryManager` plus `RoomManager` event hubs | `FactoryManager` tracks power state but is not yet a complete capability registry. Its all-off event includes every machine type, while Worker rescue requires only Work and Rest machines. |
| Physical machine occupancy | `BaseMachine` and concrete machine subclasses | This is the correct authority, but manager-side waiting and dispatch state can duplicate or outlive occupancy. |
| Machine reservation | `StationReservationService` | Correct consolidation direction, but reservations do not record the reserving robot or reservation purpose, so deterministic ownership and release are incomplete. |
| Worker Transformer dispatch | `MachineWorkerManager` | Publishes Memory facts, but also tracks `waitingWorkers` and directly calls `Heart.BlockCurrentTask()`. |
| Security dispatch | `MachineSecurityManager` | Owns pending jobs and guard selection, but can fall back to the disabled security machine's own displaced guard and mixes job allocation with robot-state inspection. |
| Worker Spawner attachment and follower cadence | `SpawningMachine` and `EnemiesSpawner` | Power-off sends the attached robot toward Start instead of fainting it. Follower creation still depends on the broad `EnemiesSpawner`. |
| Robot creation and initialization | `EnemiesSpawner`, static providers, `SceneInitiator`, factories, and initialization helpers | No single population contract. Static Worker Collectors and generated Worker Collectors use different entry points. `EnemiesSpawner` also contains setup, pooling, role behavior, animation, follower perception, and boss behavior. |
| Worker Collector assignment and claims | `WorkerCollectorMissionService` plus providers | The service combines provider registration, static spawning, garage binding, assignment, cube claiming, and slot reservation. It uses static lists and instance-ID dictionaries. |
| Robot decision flow | `RobotMemoryNew`, `RobotBrainNew`, `RobotHeartNew`, task classes, and body controllers | The intended pipeline exists, but machine code can still mutate Heart state and coordinator code constructs task objects for diagnostics. |
| Save and kill counting | `RobotStateController` global events and `FactoryManager` | Save handling counts only `Worker` and `WorkerSpawner`; valid saves for Worker Collectors and guards are excluded. There is no explicit idempotency guard at the factory boundary. |
| Run and level progression | `RunProgressManager`, `RunStepDefinition`, and `RunMapConfigSO` | Playable-level counting has been improved, Levels 4-5 are explicit generated steps, and dynamic generation begins at Level 6. Fallback scene-index constants still need cleanup. |

## Target architecture map

| Authority | Owns | Does not own |
| --- | --- | --- |
| `FactoryManager` | Scene-scoped registry of rooms and machine capabilities, aggregate power conditions, alarm state, factory domain events | Robot task selection, machine occupancy, robot creation |
| Each `BaseMachine` subclass | Its attached robot, physical occupancy, power, processing, attach/release rules, machine-local events | Global assignment policy or another machine's state |
| `StationReservationService` | Exclusive station reservations with explicit robot, purpose, and lifecycle | Physical occupancy |
| `FactoryWorkCoordinator` | Stable robot jobs, pending reactivation jobs, source/destination pairing, allocation policies, job availability events | Brain decisions, Heart stack changes, Body commands, robot instantiation |
| `RobotPopulationManager` | Prefab selection, spawn location, pooled lifecycle, common initialization, role dependencies, registration, cleanup | Ongoing role decisions or factory job policy |
| `Memory` | Observations, assignments, claims, conditions, failure reasons, relevant history | Task-stack mutation |
| `Brain` | Role-aware action choice from Memory | Physical execution |
| `Heart` | Task stack, interruption, resumption, completion | Factory allocation policy |
| `Task` and `Body` | Delegation and physical execution | Persistent factory authority |
| `RunProgressManager` | Run itinerary, playable-level identity, fixed Level 4-5 configs, Level 6+ random config lifecycle | Scene-specific spawning policy |

## Component disposition

### Retain

- `FactoryManager`, `RoomManager`, `RoomEventHub`, and normalized `BaseMachine` events.
- Concrete machine classes as occupancy and processing authorities.
- `StationReservationService` as the single machine-reservation service.
- `RobotMemoryNew`, `RobotBrainNew`, `RobotHeartNew`, existing tasks, and body controllers.
- `RobotDomainEventAdapter` as an ingress adapter where it prevents direct domain-to-Brain coupling.
- Existing robot factories, pools, navigation helpers, prefabs, and validated hand/cube physics.
- `RunProgressManager`, `RunStepDefinition`, and `RunMapConfigSO` as the run-progression model.

### Consolidate

- Move Worker Collector route assignment, pending security reactivation work, and other shared factory jobs behind one scene-scoped `FactoryWorkCoordinator` API.
- Extend reservation records so every reservation has an owner and deterministic release path.
- Route both static and generated robot creation through one `RobotPopulationManager` initialization contract.
- Reduce `MachineWorkerManager`, `MachineSecurityManager`, and `SpawningWorkerManager` to registration and domain-event adapters during migration, then remove any responsibility that the coordinator or machine now owns.

### Replace or retire after migration

- Replace `WorkerCollectorMissionService` static registries and spawning entry point with scene-scoped coordinator registration and population requests. Keep a temporary facade only while callers migrate.
- Replace `EnemiesSpawner.InitializeRobot` and role-specific creation branches with population-manager requests. Retain `EnemiesSpawner` temporarily as an `IEnemiesSpawner` compatibility facade for unmigrated callers.
- Remove `MachineWorkerManager.waitingWorkers`, direct `Heart.BlockCurrentTask()` calls, and other duplicate waiting state once equivalent Memory facts and tests are in place.
- Remove guard-selection fallback to the displaced guard and any duplicate pending-job state after coordinator migration.
- Remove `WorkerSpawnerWorkAnimationController` and `BossAllMachinesOffFaintController` from `EnemiesSpawner.cs` only after their ownership and prefab wiring are made explicit elsewhere.

## Required behavior decisions

1. Worker freedom is based only on all registered Work and Rest machines being disabled. Security and Spawning state must not participate.
2. A machine has no more than one attached active robot. Assignment and reservation do not imply occupancy.
3. A Security Guard never receives a reactivation job for its own disabled security machine.
4. A disabled Spawning machine stops immediately and makes its attached Worker Spawner faint. It does not route that robot to Start.
5. Worker Collector route assignments persist through temporary task interruption. Cube and garage-slot claims are short-lived resources and are released independently.
6. Collector search and approach do not require a garage slot reservation. The slot is reserved only when the delivery contract requires it.
7. A confirmed nine-cube batch completion triggers rest. If no valid rest destination exists, the collector reaches Start and uses the normal save transition.
8. Save and kill transitions are counted once per robot, regardless of role, while player transitions remain excluded.
9. Levels 1-3 use static scenes, Levels 4-5 use explicit fixed generated configs, and Level 6 onward uses random generated configs. The playable level number is not the run-step index.
10. Serialized `RobotRole` and `RobotTaskType` values remain in their current order.

## Confirmed gameplay clarifications

- Boss fainting through the existing global all-machines-off event remains. For the Boss, all machines means Work, Rest, Security, and Spawning.
- Worker Transformers do not receive a factory-wide rescue broadcast. Each Worker records Work and Rest machine availability through its own observations and goes to Start only when its Memory has exhausted both categories.
- Worker Collectors treat Rest as their relevant machine category after a completed batch. A powered but occupied or reserved Rest machine means wait in the queue; it does not mean Start. If a Rest machine powers off, collectors record it and search for another remembered Rest destination. They use Start only when no powered Rest destination remains.
- Security Guards treat their assigned Security machine and Rest machines as their normal lifecycle set. A disabled own post sends its guard toward Rest; no remembered Rest destination sends it to Start.
- Exactly one security reactivation job may be active factory-wide. Additional powered-off machines remain pending. The active guard handles one machine, returns to its assigned Security post, and only then becomes eligible for the next pending job. A guard never reactivates its own disabled post.
- Worker Spawner power-off immediately stops spawning, releases machine occupancy, and faints the worker at its current position. Power-on automatically reconnects and wakes that same worker only while it remains in the machine's connection range. No replacement worker is dispatched and an out-of-range worker does not resume spawning.
- Only Worker Transformer, Worker Collector, and Security Guard roles are save-eligible.
- Level 6 and later retain the room-derived population formulas documented in `Generated_Map_Worker_Population.md`.
- C# EditMode tests may be added or extended in the existing suite. Unity Test Runner execution remains user-run.

## Implementation stages

### Stage 0 Preserve and baseline

- Inventory all current uncommitted changes and classify each as part of progression, spawning, Worker Collector, prefab wiring, or unrelated work.
- Capture prefab and scene references for every robot role and machine manager before moving code.
- Add characterization EditMode tests for the current Memory-Brain-Heart boundaries, machine occupancy, reservation release, save idempotency, and level numbering.
- Run the existing EditMode suite before structural edits. Record failures without rewriting tests merely to obtain a green result.

Exit gate: the branch can be restored from commits plus the preserved working tree, and every pre-existing failure is classified.

### Stage 1 Factory registry and ownership

- Extend `FactoryManager` registration to expose typed machine capabilities and a Work-and-Rest-disabled condition separate from the global all-machines-off summary.
- Make registration and unregistration symmetric for static and generated rooms.
- Harden `BaseMachine.AttachRobot` and concrete overrides so a second active robot cannot replace occupancy silently.
- Change `StationReservationService` from role-only availability lists to explicit reservation records containing machine, robot, purpose, and release reason.
- Add lifecycle cleanup for power-off, occupancy, disable, destroy, robot death, and scene teardown.

Exit gate: one owner exists for physical occupancy and one owner exists for each reservation; tests cover duplicate attach and stale release.

### Stage 2 Factory work coordination

- Introduce scene-scoped `FactoryWorkCoordinator` interfaces for stable jobs and short-lived resource claims.
- Move pending security reactivation targets and guard allocation policy into the coordinator.
- Exclude a guard's own disabled security machine before candidate selection; reserve each reactivation job once.
- Move Worker Collector source/garage/rest assignment out of static service state. Keep cube claims and garage-slot reservations explicit and independently releasable.
- Publish assignment and availability changes as facts. Do not create or push `RobotTask` instances from the coordinator.
- Remove `MachineWorkerManager` Heart calls and waiting dictionaries after Brain replanning is driven by Memory updates.

Exit gate: factory code never calls Heart or Body, stable jobs survive task interruption, and failed allocations remain pending or produce an explicit recoverable failure.

### Stage 3 Unified robot population

- Add `RobotPopulationManager` with a role-to-prefab catalog and a single initialization request/result contract.
- Centralize pool acquisition, naming and stats, position, activation order, navigation, Brain/Heart/Memory/Body readiness, role dependencies, factory registration, and cleanup.
- Route generated Workers, Worker Spawners, Security Guards, Reception Security, Followers, Bosses, and Worker Collectors through that contract in small migrations.
- Route static-scene Worker Collector creation through the same contract; static providers supply spawn definitions but do not instantiate independently.
- Keep `EnemiesSpawner` as a narrow facade until all callers and prefabs are migrated, then rename or retire it.

Exit gate: static and generated modes produce equivalent initialized robots, duplicate spawn protection is tested, and pooled disable/re-enable does not retain stale jobs or claims.

### Stage 4 Role contract integration

- Worker Transformer: alternate Work and Rest using reservations and Memory; rescue eligibility uses only the Work/Rest disabled condition.
- Worker Collector: implement the persistent route, delayed cube/slot claims, nine-cube batch completion, rest-or-Start outcome, and loss/stolen-cube recovery through the existing decision pipeline.
- Security Guard: implement exclusive reactivation work, own-machine exclusion, and rest-or-Start behavior when its post is disabled.
- Worker Spawner: on machine power-off, stop cadence, release occupancy safely, and push a machine-power observation that leads Brain/Heart to the existing Faint task.
- Follower: retain follow-only behavior and verify no attack path is configured.
- Boss: retain current defend/attack behavior and separate any all-machines-off compatibility component from population code.
- Preserve flying Collector and SecurityReception behavior as separate roles.

Exit gate: role scenarios pass without any new role controller competing with Brain.

### Stage 5 Progression and bootstrap convergence

- Make the configured run itinerary and direct-scene lookup derive from the same step definitions; remove stale hard-coded step indices.
- Keep Levels 4-5 bound to `Setup Level 4` and `Setup Level 5`.
- Make Level 6 the first random configuration and cache it for restart consistency while preserving the former Level 7 growth values.
- After either static registration or generated-map construction, invoke the same factory registration, work coordination, and population startup sequence.
- Replace slot-number and scene-name job selection with registered room capabilities.

Exit gate: Levels 1-3, 4-6, and 7+ differ only in map/spawn definitions, not robot initialization or factory lifecycle.

### Stage 6 Collector grabbing diagnosis

- Reproduce on current `tourB` and compare with commit `5a81caeea88d0f9a30bcf4bd69bb384cd1c0cea3` without overwriting current work.
- Instrument these exact transitions: Assigned, Source reached, Cube discovered, Cube claimed, Pickup approach, Grab task, Physical attachment, Cargo secured.
- Record a reason at the first failed transition and fix that subsystem.
- Do not change grabbing distance, hand physics, joints, or attachment strategy unless the trace proves that physical attachment is the first failing subsystem.

Exit gate: the first failure is observable; successful pickup retains the validated hand/cube mechanics.

### Stage 7 Cleanup and validation

- Remove compatibility facades, static registries, unused serialized fields, and duplicate event paths only after reference searches and prefab/scene validation.
- Update architecture and role documentation alongside the final code.
- Run EditMode tests, then targeted PlayMode or manual scene checks where Unity automation cannot cover runtime physics and navigation.
- Report exactly which validations ran and which remain manual.

## Test matrix

| Area | Required checks |
| --- | --- |
| Ownership | One attached robot per machine; reservation is not occupancy; disable/destroy clears subscriptions and claims. |
| Worker Transformer | Work, transform, rest, and rescue eligibility when all Work/Rest machines are off. |
| Worker Collector | Stable route, exclusive cube and slot claims, nine-cube completion, rest, Start save fallback, stolen/lost cube recovery, retry when no cube exists. |
| Security Guard | Other-machine reactivation, own-machine exclusion, exclusive job reservation, rest or Start when its post is disabled. |
| Worker Spawner | Alarm cadence while active; immediate stop and faint on power-off; no silent resume. |
| Other roles | Follower cannot attack; Boss remains defend/attack only; flying Collector and SecurityReception do not regress. |
| Lifecycle | Pool disable/re-enable, scene transition cleanup, no duplicate spawn, one save/kill count per robot. |
| Progression | Static Levels 1-3, fixed generated Levels 4-5, random Level 6+, laboratory interleaving, restart keeps the active random config. |

## First implementation slice

The first code slice should be limited to contracts and tests:

1. Add role-independent save idempotency tests and correct `FactoryManager.HandleRobotSaved` without changing enum values.
2. Add a factory Work/Rest-disabled summary and tests, leaving the existing global summary intact for compatibility.
3. Add guard own-machine exclusion and exclusive pending-job tests, then remove the fallback to the displaced guard.
4. Add spawning-machine power-off tests that specify stop plus Faint, then implement the behavior through Memory/Brain rather than direct Heart mutation.
5. Add reservation-owner data structures behind the existing `StationReservationService` API before migrating callers.

This slice addresses confirmed contract violations while creating the seams needed for the larger coordinator and population migrations.

## Implementation update 2026 10 11

The first controlled behavior and ownership slice is implemented in the working tree:

- save eligibility now includes only Worker Transformer, Worker Collector, and Security Guard;
- Worker machine shutdown knowledge is local to the attached robot or the robot that reaches an off machine; `MachineWorkerManager` no longer keeps a waiting-worker dictionary or calls Heart directly;
- Rest slots no longer replace the current occupant and retry queued robots when the short rest completes;
- Worker Collectors reserve the physical Rest machine, wait while it is occupied, select another powered Rest provider after shutdown, and return to Start when none remains;
- security reactivation permits one active factory-wide job, retains additional pending jobs, remembers each guard's assigned post, and excludes a guard from reactivating that post;
- Spawning machine power-off stops spawning, releases occupancy, and faints the attached Worker Spawner; power-on reconnects it only while it remains in range;
- station reservations now record owner and purpose, support a machine shared by multiple eligible roles, and reject release by another robot;
- Level 6 and later dynamic configurations are cached by playable level and derive from the most recent explicit generated configuration rather than legacy fallback list order;
- Worker Collector and robot-domain static registries reset on Unity subsystem registration.

Local C# compilation passes for both runtime and editor assemblies. Unity EditMode and scene/runtime validation remain project-owner actions.
