# Robot AI New Architecture Migration Guide

This document is the migration organizer for switching from `RobotBrain`/`RobotHeart` to `RobotBrainNew`/`RobotHeartNew` with event-driven memory updates.

No production behavior changes are required by this document. It is a sequencing guide.

## Target Runtime Chain

`External Event -> RobotMemoryStateNew -> RobotMemoryNew (change event) -> RobotBrainNew -> RobotHeartNew -> RobotTaskStack -> TaskHandler -> RobotBodyController`

## Migration Rules

1. Keep old and new systems side by side until parity is reached.
2. Move one event source family at a time (combat, machine, follower).
3. Do not let external systems push tasks directly during migration.
4. BrainNew is the only consumer of memory change events.
5. HeartNew is the only owner of task stack mutation policy.

## Phase Order (What To Change First)

## Phase 0: Contracts First

Change these first:
- `Assets/Scripts/Robots/Interfaces/IRobotMemoryNew.cs`
- `Assets/Scripts/Robots/RobotMemoryStateNew.cs`
- `Assets/Scripts/Robots/RobotMemoryNew.cs`
- `Assets/Scripts/Robots/RobotHeartNew.cs`
- `Assets/Scripts/Robots/RobotBrainNew.cs`

Goal:
- Freeze minimal API contracts before touching producers/managers.
- Define exact memory change events (what changed + payload).
- Define BrainNew event intake methods.
- Define HeartNew methods used by BrainNew (`TrySetIntent`, `TryPushTask`, `CompleteCurrentTask`).

## Phase 1: Memory Event Backbone

Primary files:
- `Assets/Scripts/Robots/RobotMemoryStateNew.cs`
- `Assets/Scripts/Robots/RobotMemoryNew.cs`
- `Assets/Scripts/Robots/Interfaces/IRobotMemoryNew.cs`

Implement:
- Memory fact mutation methods only in `RobotMemoryStateNew`.
- `RobotMemoryNew` subscribable change event(s) fired on fact changes.
- Optional typed change enum for routing clarity (recommended).

Do not migrate producers yet. Only make MemoryNew capable of publishing changes.

## Phase 2: BrainNew as Event Router

Primary files:
- `Assets/Scripts/Robots/RobotBrainNew.cs`
- `Assets/Scripts/Robots/RobotHeartNew.cs`

Implement:
- BrainNew subscribes to MemoryNew change events in `OnEnable`/`OnDisable`.
- BrainNew converts memory changes to high-level intent requests.
- BrainNew asks HeartNew to change task/intent, not external scripts.

Keep task execution thin; call existing handlers/body where possible.

## Phase 3: HeartNew Policy Ownership

Primary files:
- `Assets/Scripts/Robots/RobotHeartNew.cs`
- `Assets/Scripts/Robots/Tasks/RobotTasks.cs` (reuse existing task model/stack during migration)

Implement:
- Role + intent to task mapping in one place (HeartNew).
- Precedence and interruption policy in HeartNew.
- HeartNew emits task-changed event for BrainNew executor path.

At this phase, BrainNew should stop hardcoding role policy.

## Phase 4: Move Event Producers to New Brain Entry Points

Migrate these producers in this order:

1. Combat/perception
- `Assets/Scripts/Player/FollowPlayerTriggerHandler.cs`
- `Assets/Scripts/Misc/Math/Physics/HealthBot.cs`

2. Worker machine events
- `Assets/Scripts/Factory/Machines/MachineWorkerManager.cs`
- `Assets/Scripts/Factory/Machines/FactoryMachine.cs`
- `Assets/Scripts/Factory/Machines/RestingMachine.cs`

3. Guard/security machine events
- `Assets/Scripts/Factory/Machines/MachineSecurityManager.cs`
- `Assets/Scripts/Factory/Machines/SecurityMachine.cs`
- `Assets/Scripts/Factory/Machines/SpawningMachine.cs`

4. Spawn-time intent setup
- `Assets/Scripts/AI/EnemiesSpawner.cs`

Rule:
- Replace direct task pushes/calls with BrainNew event methods.

## Phase 5: Task Execution Parity

Execution files to align with new task transitions:
- `Assets/Scripts/Misc/AIHandlers/ChaseTargetHandler.cs`
- `Assets/Scripts/Misc/AIHandlers/MoveToPayloadHandler.cs`
- `Assets/Scripts/Misc/AIHandlers/ReactivateMachineHandler.cs`
- `Assets/Scripts/Factory/Machines/WaitAtMachineHandler.cs`
- `Assets/Scripts/Misc/AIHandlers/SpawnFollowersHandler.cs`
- `Assets/Scripts/Robots/Body/RobotBodyController.cs`

Goal:
- Ensure every task switch has clean enter/exit behavior.
- No dangling movement/attack/reactivation routines.

## Phase 6: Scene Wiring and Assets

Check/update:
- Role handler assets in `Assets/Scripts/ScriptObjects/Task Handlers/`
- Role maps in `Assets/Scripts/ScriptObjects/Task Handlers/Roles/`
- Robot prefabs serialized to use `RobotBrainNew`, `RobotHeartNew`, `RobotMemoryNew` once ready
- Spawner initialization path in `Assets/Scripts/AI/EnemiesSpawner.cs`

## Phase 7: Legacy Decommission

After parity:
- Remove direct producer usage of old Brain APIs.
- Remove old brain/heart memory wiring gradually.
- Keep one migration shim release before deletion.

## Producer -> BrainNew Intake Map (Recommended)

- Player detect enter/exit -> `BrainNew.OnPerceptionChanged(...)`
- Player attack zone enter/exit -> `BrainNew.OnThreatRangeChanged(...)`
- Damage taken -> `BrainNew.OnDamageTaken(...)`
- Machine state on/off -> `BrainNew.OnMachineStateEvent(...)`
- Security dispatch request -> `BrainNew.OnSecurityDispatch(...)`
- Spawn follower chase seed -> `BrainNew.OnSpawnContext(...)`

## Per-Document Coverage Mapping

- `Docs/RobotAI_IntentDriven_Architecture.md`: Heart-owned intent/task policy.
- `Docs/ArchitectureOverview.md`: managers remain routers, not task owners.
- `Docs/FollowerBehavior.md`: chase refresh remains task-driven, now triggered by MemoryNew facts.
- `Docs/SecurityGuardBehavior.md`: security dispatch still external, execution policy in HeartNew.
- `Docs/WorkerMachineFlow.md`: worker ON/OFF transitions should become BrainNew event handling plus HeartNew mapping.

## Practical Touch Order (Shortest Safe Path)

1. `Assets/Scripts/Robots/RobotMemoryStateNew.cs`
2. `Assets/Scripts/Robots/RobotMemoryNew.cs`
3. `Assets/Scripts/Robots/RobotBrainNew.cs`
4. `Assets/Scripts/Robots/RobotHeartNew.cs`
5. `Assets/Scripts/Player/FollowPlayerTriggerHandler.cs`
6. `Assets/Scripts/Misc/Math/Physics/HealthBot.cs`
7. `Assets/Scripts/Factory/Machines/MachineWorkerManager.cs`
8. `Assets/Scripts/Factory/Machines/MachineSecurityManager.cs`
9. `Assets/Scripts/Factory/Machines/SecurityMachine.cs`
10. `Assets/Scripts/Factory/Machines/SpawningMachine.cs`
11. `Assets/Scripts/AI/EnemiesSpawner.cs`
12. Handlers/body files from Phase 5

## Migration Done Criteria

- All external systems send events only to BrainNew.
- MemoryNew publishes changes and BrainNew reacts to them.
- HeartNew owns role/intent/task mapping.
- Worker/guard/follower loops match existing behavior docs.
- No direct manager-to-task push calls remain.
