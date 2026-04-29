# Factory / Room / Machine Logic Investigation and Fix Plan (2026-04-15)

## Goal

Provide a practical, low-risk plan to refactor machine-system logic around your model:
- Factory contains Rooms
- Rooms contain Machines (+ cameras, doors, lifts)
- Machines act only when powered + robot attached
- Communication is event-driven and explicit

This document started as a plan and now also tracks implementation status.

## Explicit Project Policy (Owner Decision)

These rules are mandatory for this refactor:
1. New code only for machine-event architecture.
2. No legacy machine-event fallback in migrated paths.
3. Remove old concrete-machine events when the corresponding consumers are migrated.
4. Testing execution is user-owned (assistant does not run tests unless explicitly requested later).

## Executive Summary

You already have the right structural hierarchy, but 4 issues create complexity:
1. Machine events are inconsistent across machine types.
2. Domain orchestration and robot-task dispatch are mixed together.
3. Machine reservation is split across multiple services.
4. Alarm propagation has duplicate paths.

The fix is not a full rewrite. It is a staged consolidation:
- Standardize machine event contracts.
- Introduce Room as local event hub.
- Keep Factory as cross-room coordinator.
- Move robot-brain calls to one adapter.
- Consolidate reservation ownership.

## Current Overall Status (2026-04-15)

1. Phase 1: Complete in code.
2. Phase 2: Complete in code; tests added; runtime validation remains user-run.
3. Phase 3: Complete in code; tests added; runtime validation remains user-run.
4. Phase 4: Complete in code for Factory/Room/Machine domain paths.
5. Phase 5: Complete in code (single machine-reservation owner kept in `StationReservationService`).
6. Phase 6: Complete for machine-event compatibility cleanup in migrated scope.
7. Intentional compatibility bridge still present: `RoomManager.OnRoomAlarmChanged`.

## Current Architecture Snapshot (Observed)

### Factory

Main files:
- `Assets/Scripts/Factory/Managers/FactoryManager.cs`
- `Assets/Scripts/Factory/Core/FactoryAlarmStatus.cs`

Current role:
- Global alarm ownership and map/room initialization.
- Room registration via `MapManager.RegisterFactoryInEachRoom(...)`.
- Emits `OnFactoryAlarmChanged` from `FactoryAlarmStatus.OnAlarmStateChanged` subscription (no polling loop).

### Room

Main file:
- `Assets/Scripts/World/Rooms/RoomManager.cs`

Current role:
- Registers waypoints.
- Holds room-local machine lists.
- Registers machine instances into global managers.
- Relays factory alarm to room listeners.

Related room systems:
- `DoorController` (alarm reactions)
- `LiftShaftController` (alarm reactions)
- `SecurityCamera` (alarm triggering + player position updates)

### Machines

Base:
- `Assets/Scripts/Factory/Machines/BaseMachine.cs`

Specializations:
- `FactoryMachine`
- `RestingMachine`
- `SecurityMachine`
- `SpawningMachine`

Managers:
- `MachineWorkerManager`
- `MachineSecurityManager`
- `SpawningWorkerManager`

## Root-Cause Analysis

### Problem A: Event fragmentation

Symptoms:
- Each machine class emits custom event signatures.
- Shared concepts (power on/off, occupancy, turn-off intent) are duplicated with different names.

Impact:
- Higher subscription complexity.
- Harder to add new machine type without touching multiple managers.

### Problem B: Domain and AI coupling

Symptoms:
- Machine managers directly call `RobotBrainNew` APIs (`OnMachineStateEvent`, `OnSecurityDispatch`).

Impact:
- Domain flow (Factory/Room/Machine) is tied to one AI implementation.
- Future brain changes require machine-system edits.

### Problem C: Reservation split-brain

Symptoms:
- `StationReservationService` tracks availability by role.
- `WaypointService` tracks reserved `FactoryMachine` instances separately.

Impact:
- Potential divergence and hidden race conditions.

### Problem D: Alarm duplication

Symptoms:
- `FactoryAlarmStatus.OnAlarmStateChanged` exists.
- `FactoryManager` also relays via polling `Update`.

Impact:
- Multiple propagation paths and ambiguous source of truth.

## Target Architecture (Concrete)

## 1) Domain boundaries

1. Machine domain:
- Machine state and machine-local behavior only.
- No direct robot-intent decisions.

2. Room domain:
- Aggregates child machine/device events.
- Emits normalized room events.

3. Factory domain:
- Cross-room policy only (global alarm, all-machines-off, global dispatch rules).

4. AI adapter domain:
- Only place that translates domain events into `RobotBrainNew` calls.

## 2) Canonical event contracts

Introduce uniform envelopes (names may vary, but semantics should not):

Machine-level:
- `MachinePowerChanged(machineId, machineType, roomId, isOn)`
- `MachineOccupancyChanged(machineId, occupantId, isOccupied)`
- `MachineTurnedOff(machineId, previousOccupantId, reason)`
- `MachineWorkProduced(machineId, workKind, payload)`
- `MachineReactivated(machineId, byRobotId)`

Room-level:
- `RoomMachineChanged(roomId, machineEvent)`
- `RoomAccessChanged(roomId, accessType, state)`
- `RoomThreatChanged(roomId, state, source)`

Factory-level:
- `FactoryAlarmChanged(state, sourceRoomId, sourceType)`
- `FactoryMachinesSummaryChanged(totalOn, totalOff, byType)`
- `FactoryAllMachinesOff()`

## 3) Subscription ownership matrix

- Machines publish machine events.
- Room hub subscribes to all machine events in that room.
- Factory coordinator subscribes to room hubs.
- AI adapter subscribes to factory + room events and calls brain APIs.
- Doors/lifts/cameras subscribe only to room events (not directly to factory unless truly global).

## Migration Plan (File-by-File)

## Phase 1 Audit (Current Status)

Audit date: `2026-04-15`
Overall Phase 1 status: Complete.

Status summary:
1. Unified machine contract in `BaseMachine`: Done.
2. Manager migrations to unified events:
- `MachineWorkerManager`: Done (new events only).
- `MachineSecurityManager`: Done (new events only).
- `SpawningWorkerManager`: Done (new events only).
3. Legacy machine-specific events in concrete machine classes: Removed.

Important note:
- We intentionally migrated managers without legacy fallback (as requested).  
- Legacy events (`OnMachineStateChanged`, `OnMachineTurningOff`) are now removed from:
  - `FactoryMachine`
  - `RestingMachine`
  - `SecurityMachine`
  - `SpawningMachine`

Phase 1 exit-criteria check (under new-only policy):
1. "Every machine state transition can be observed through unified events": Satisfied at base-contract level.
2. "Consumers can migrate to unified events": Satisfied for the 3 manager targets above.
3. "No legacy fallback / legacy machine events in migrated domain": Satisfied.

Phase 1 implementation details completed:
1. Added unified event envelopes in `BaseMachine`:
- `MachinePowerChangedEvent`
- `MachineOccupancyChangedEvent`
- `MachineTurnedOffEvent`
2. Added unified events in `BaseMachine`:
- `OnMachinePowerChanged`
- `OnMachineOccupancyChanged`
- `OnMachineTurnedOff`
3. Added `AttachedRobot` tracking in `BaseMachine`.
4. Updated `MachineWorkerManager` to subscribe only to unified events and unsubscribe on destroy.
5. Updated `MachineSecurityManager` to subscribe only to unified events and unsubscribe on destroy.
6. Updated `SpawningWorkerManager` to subscribe only to unified power event and unsubscribe on destroy.
7. Added a worker-resolution guard in `MachineWorkerManager` so machine turn-off handling still works when `PreviousRobot` is unavailable.
8. Removed legacy concrete-machine events and invocations from `FactoryMachine`, `RestingMachine`, `SecurityMachine`, and `SpawningMachine`.

## Phase 2 Investigation (2026-04-15)

Investigation scope:
- `RoomManager`
- room-local consumers (`DoorController`, `LiftShaftController`, `AlarmFlasher`, `SecurityCamera`)
- machine base/unified events from `BaseMachine`

Phase 2 status:
- Core implementation now in place (room hub + machine aggregation + camera threat routing).
- Final validation pending user-run Edit Mode tests and scene smoke check.

What is already in place for Phase 2:
1. Machine unified events exist and are stable in `BaseMachine`:
- `OnMachinePowerChanged`
- `OnMachineOccupancyChanged`
- `OnMachineTurnedOff`
2. `RoomManager` already owns room machine lists by type.
3. Room-local alarm consumers already subscribe through `RoomManager.OnRoomAlarmChanged`:
- `DoorController`
- `LiftShaftController`
- `AlarmFlasher`

Phase 2 implemented items:
1. Added room-domain event contracts and room hub component.
- New file: `Assets/Scripts/World/Rooms/RoomEventHub.cs`
- Added:
  - `RoomMachineChangedEvent`
  - `RoomThreatChangedEvent`
  - `RoomEventHub`
2. Updated `RoomManager` to aggregate machine unified events.
- Subscribes/unsubscribes to all room machine types (`Factory`, `Resting`, `Security`, `Spawning`).
- Republishes machine events through room-domain event stream.
3. Added room threat API in `RoomManager`.
- `RaiseRoomThreat(...)`
- `UpdateTrackedPlayerPositionIfAlarmActive(...)`
- `UpdateLastKnownPlayerPosition(...)`
4. Migrated `SecurityCamera` to room-domain threat flow.
- Camera now raises threat through `RoomManager.RaiseRoomThreat(...)`.
- Removed direct camera writes to `factoryAlarmStatus.CurrentAlarmState`.
5. Added Phase 2 tests in `RoomManagerTests`.
- Verifies room machine-event republishing on machine power-off.
- Verifies room threat-event publishing.

Remaining Phase 2 validation/polish:
1. Run Edit Mode tests in Unity and verify no regressions.
2. Scene smoke-test camera threat escalation and room alarm listeners.
3. Optional follow-up: move machine-manager registration in `RoomManager.Initialize` outside `waypointService` gate (identified risk, not changed in this phase).

Phase 2 implementation checklist (execution-ready):
1. Add room-domain event contracts and a room hub component.
- Suggested file: `Assets/Scripts/World/Rooms/RoomEventHub.cs`
- Add envelopes for:
  - `RoomMachineChanged(room, machine, eventKind, isOn/isOccupied, previousRobot)`
  - `RoomThreatChanged(room, threatState, source)`
2. Update `RoomManager` to own machine event wiring.
- Subscribe/unsubscribe to all room machine instances on init/destroy.
- Re-publish machine unified events through room-domain events.
3. Keep `OnRoomAlarmChanged` as compatibility bridge for current consumers.
- Continue emitting room alarm changes from factory alarm input for now.
4. Migrate `SecurityCamera` to room-domain threat API.
- Replace direct alarm mutation with `RoomManager`/`RoomEventHub` threat raise method.
- Keep player-position update behavior unchanged.
5. Add Edit Mode tests for Phase 2.
- Room republishes power/occupancy/turned-off events from child machines.
- Threat event from camera path reaches room listeners.
- Existing door/lift/alarm listeners continue receiving alarm changes.

Phase 2 exit criteria re-check (updated):
1. Room-local systems react through room events, not ad-hoc machine wiring: Partially satisfied (alarm consumers already via room; machine consumers still to migrate as needed).
2. Room machine state changes are observable from a room-domain stream: Satisfied.
3. Camera-triggered threat escalation goes through room-domain API: Satisfied.
4. Edit Mode tests lock the above behavior: Partially satisfied (new tests added; user execution pending).

## Phase 3 Implementation Update (2026-04-15)

Phase 3 status:
- Core implementation complete in code.
- Validation remains user-run (Edit Mode tests + scene smoke checks).

Implemented now:
1. Canonical factory alarm source switched to `FactoryAlarmStatus.OnAlarmStateChanged`.
- Removed `FactoryManager.Update` polling relay path.
- `FactoryManager` now subscribes/unsubscribes to `factoryAlarmStatus.OnAlarmStateChanged`.
2. Factory now subscribes to room machine-domain events.
- `MapManager.RegisterFactoryInEachRoom(...)` now returns the initialized room list.
- `FactoryManager` subscribes to `RoomManager.OnRoomMachineChanged` for cross-room coordination.
3. Factory-level machine summary + all-off events added.
- `FactoryMachinesSummaryChangedEvent` added in `FactoryManager.cs`.
- New events:
  - `OnFactoryMachinesSummaryChanged`
  - `OnFactoryAllMachinesOff`
- Summary tracks:
  - `TotalOn`
  - `TotalOff`
  - per-type ON counts
- `OnFactoryAllMachinesOff` is latched and resets when any machine turns on.

Remaining to close Phase 3:
1. Global all-off consumer migration: Completed for `EnemiesSpawner`.
- `EnemiesSpawner` now subscribes to `IFactoryManager.OnFactoryAllMachinesOff`.
- Removed dependency on `MachineSecurityManager.OnAllMachinesOff` for global boss-faint behavior.
- Removed legacy manager-local `OnAllMachinesOff` signal path from `MachineSecurityManager` runtime code to avoid parallel global all-off channels.
2. Added/extended Edit Mode tests for Factory coordinator behavior.
- `FactoryManagerTests.RoomMachineEvents_EmitFactorySummary_AndAllOffIsLatched`
- `FactoryManagerTests.AlarmStatusChange_RaisesOnFactoryAlarmChanged_WithoutPolling`
3. Still required for phase sign-off:
- User-run full Edit Mode suite.
- Scene smoke checks for alarm propagation + all-machines-off gameplay flow.

## Phase 4 Implementation Update (2026-04-15)

Phase 4 status:
- Complete in code for Factory/Room/Machine flow.

Implemented now:
1. Added domain-event adapter runtime:
- New file: `Assets/Scripts/AI/RobotDomainEventAdapter.cs`
- Added domain dispatch events:
  - `RobotMachineStateDispatchEvent`
  - `RobotSecurityDispatchEvent`
  - `RobotPerceptionDispatchEvent`
- Added bus + adapter:
  - `RobotDomainEventBus`
  - `RobotDomainEventAdapter`
2. Migrated `MachineWorkerManager` off direct brain calls.
- Replaced direct `OnMachineStateEvent(...)` usage with `RobotDomainEventBus.PublishMachineStateDispatch(...)`.
3. Migrated `MachineSecurityManager` off direct brain calls.
- Replaced direct `OnSecurityDispatch(...)` + `OnMachineStateEvent(...)` usage with domain-event publishes.
4. Migrated `EnemiesSpawner` follower perception dispatch.
- Replaced direct `OnPerceptionChanged(...)` usage with `RobotDomainEventBus.PublishPerceptionDispatch(...)`.
5. Bootstrapped adapter creation in runtime entry points.
- `SceneInitiator.InitializeFactory()` now calls `RobotDomainEventAdapter.EnsureInScene()`.
- `RunSetupManager.ShowRealPreview()` now calls `RobotDomainEventAdapter.EnsureInScene()`.
6. Extended migration to remove remaining direct brain calls in machine-side scripts.
- `SecurityMachine` now publishes machine-state dispatch through the domain bus.
- `SpawningMachine` now publishes machine-state dispatch through the domain bus.
- `MachineReactivationTrigger` now publishes reactivate-completion through the domain bus.

Phase 4 closure notes:
1. User-run Edit Mode tests and scene smoke tests are still required for runtime sign-off.
2. Direct brain calls may still exist in non-factory paths (for example player/perception entry points), which are intentionally outside this refactor scope.
3. `RoomManager.OnRoomAlarmChanged` is intentionally still present as a compatibility bridge.
4. Historical root-cause analysis sections are intentionally retained.

## Phase 5 Implementation Update (2026-04-15)

Phase 5 status:
- Complete in code.

Implemented now:
1. Removed machine-reservation ownership from `WaypointService`.
- Removed machine reservation map and machine reservation APIs from `WaypointService`.
- Removed machine reservation API surface from `IWaypointService`.
2. Removed machine-side coupling to waypoint reservation checks in `FactoryMachine`.
- `FactoryMachine` no longer reads/releases machine reservation via `waypointService`.
3. Effective reservation owner is now `StationReservationService` for machine reservation flows used by managers/dispatch.

Phase 5 closure notes:
1. Reservation split-brain between `WaypointService` and `StationReservationService` is removed in migrated scope.
2. User-run Edit Mode tests remain required for runtime sign-off.

## Phase 6 Implementation Update (2026-04-15)

Phase 6 status:
- Complete in code for migrated machine-event scope.

Implemented now:
1. Removed remaining unused compatibility/legacy event surfaces tied to old manager-local paths:
- Removed unused `MachineSecurityManager` machine-specific turn-off events that no longer had consumers.
- Removed unused `SpawningWorkerManager.OnSpawningMachinePowerChanged` event surface.
2. Removed dead reservation compatibility interface:
- Deleted `IMachineReservationService` (no remaining consumers).
3. Kept explicit compatibility bridge by design:
- `RoomManager.OnRoomAlarmChanged` remains intentionally for listener compatibility until final listener migration.

## Historical Phase Plan (Reference Only)

The sections below are the original phase definitions kept for traceability.
Use the implementation update sections above as the source of truth for current status.

## Phase 0: Safety baseline

Do first:
1. Freeze behavior with tests before refactor.
2. Add event-flow debug logging toggles (off by default).

Files to cover with tests:
- `MachineSecurityManager`
- `MachineWorkerManager`
- `RoomManager`
- `StationReservationService`

## Phase 1: Machine contract normalization

Primary files:
- `BaseMachine.cs`
- `FactoryMachine.cs`
- `RestingMachine.cs`
- `SecurityMachine.cs`
- `SpawningMachine.cs`

Actions:
1. Add common machine event API in `BaseMachine`.
2. Keep existing per-machine events temporarily (compat layer).
3. Emit both old + new events until consumers migrate.

Exit criteria:
- Every machine state transition can be observed through unified events.

## Phase 2: Room event hub

Primary file:
- `RoomManager.cs`

New support file (suggested):
- `RoomEventHub.cs`

Actions:
1. Room subscribes to all child machine unified events.
2. Room repackages and emits room-domain events.
3. Move door/lift/camera subscriptions to room-domain events where possible.

Exit criteria:
- Room-local systems react through room events, not ad-hoc machine wiring.

## Phase 3: Factory coordinator cleanup

Primary files:
- `FactoryManager.cs`
- `FactoryAlarmStatus.cs`

Actions:
1. Remove duplicate alarm relay path; pick one canonical source.
2. Factory subscribes to room hubs for cross-room decisions only.
3. Emit global summary events (`AllMachinesOff`, counts, alarm transitions).

Exit criteria:
- Global rules are centralized in Factory; room-local rules are not.

## Phase 4: AI adapter extraction

Primary files affected:
- `MachineWorkerManager.cs`
- `MachineSecurityManager.cs`
- `EnemiesSpawner.cs`

New support file (suggested):
- `RobotDomainEventAdapter.cs`

Actions:
1. Stop direct robot API calls in machine managers.
2. Publish domain events only.
3. Adapter listens and calls `RobotBrainNew` (`OnMachineStateEvent`, `OnSecurityDispatch`, etc.).

Exit criteria:
- Machine/room/factory code compiles and works without any direct brain calls.

## Phase 5: Reservation consolidation

Primary files:
- `StationReservationService.cs`
- `WaypointService.cs`

Actions:
1. Select single owner for machine reservation (recommended: `StationReservationService`).
2. `WaypointService` queries reservation owner instead of storing machine reservations.
3. Delete duplicate reservation maps after parity.

Exit criteria:
- Exactly one authoritative reservation state for machines.

## Phase 6: Remove compatibility layer

Actions:
1. Remove legacy per-machine event subscriptions.
2. Remove duplicate handlers and dead code.
3. Keep only normalized contracts.

Exit criteria:
- No consumer depends on old machine-specific event signatures.

## Risk Register and Mitigations

1. Risk: event order regressions.
- Mitigation: define event ordering contract (`PowerOff` before `OccupancyFreed`, etc.) and lock with tests.

2. Risk: guard dispatch deadlocks when security machines toggle fast.
- Mitigation: add debounced dispatch or idempotent dispatch keys.

3. Risk: reservation leaks on robot death/pool release.
- Mitigation: add cleanup hooks from `RobotStateController`/pool release path.

4. Risk: alarm desync during scene init.
- Mitigation: single initialization source and explicit startup event replay.

## Minimum Test Plan (Edit Mode)

Create or extend tests for:
1. Machine contract tests:
- Power on/off emits expected sequence.
- Attach/release occupancy events and ids are correct.

2. Room hub tests:
- Room receives child machine events and republishes normalized room events.

3. Factory coordinator tests:
- All-machines-off raised once (latched) and reset when any machine turns on.

4. Reservation tests:
- Reserve/release lifecycle consistent across robot death, power off, and reactivation.

5. Adapter tests:
- Given domain event X, adapter sends expected `RobotBrainNew` call Y.

Command reminder:
- `unity -runTests -testPlatform EditMode -projectPath "$(pwd)" -quit`

## Implementation Rules to Keep Refactor Stable

1. No big-bang rewrite.
2. Do not add compatibility fallback paths for machine events (new-only policy).
3. Change one layer at a time (Machine -> Room -> Factory -> Adapter -> Reservation).
4. After each phase: user runs Edit Mode tests and verifies scene smoke test.
5. Do not refactor behavior and architecture in same commit.

## Recommended Commit Plan

1. `docs(factory): detailed machine-room-factory refactor plan`
2. `refactor(machine): add unified machine event contract (new-only policy)`
3. `refactor(room): add room event hub and migrate local listeners`
4. `refactor(factory): centralize global coordination and alarm propagation`
5. `refactor(ai): introduce robot domain event adapter`
6. `refactor(reservation): consolidate machine reservation authority`
7. `cleanup(machine): remove legacy machine-specific event wiring`

## Files Referenced

- `Assets/Scripts/Factory/Managers/FactoryManager.cs`
- `Assets/Scripts/Factory/Core/FactoryAlarmStatus.cs`
- `Assets/Scripts/World/Map/MapManager.cs`
- `Assets/Scripts/World/Rooms/RoomManager.cs`
- `Assets/Scripts/Factory/Machines/BaseMachine.cs`
- `Assets/Scripts/Factory/Machines/FactoryMachine.cs`
- `Assets/Scripts/Factory/Machines/RestingMachine.cs`
- `Assets/Scripts/Factory/Machines/SecurityMachine.cs`
- `Assets/Scripts/Factory/Machines/SpawningMachine.cs`
- `Assets/Scripts/Factory/Machines/MachineWorkerManager.cs`
- `Assets/Scripts/Factory/Machines/MachineSecurityManager.cs`
- `Assets/Scripts/Factory/Machines/MachineReactivationTrigger.cs`
- `Assets/Scripts/Factory/Managers/SpawningWorkerManager.cs`
- `Assets/Scripts/Factory/Reservations/StationReservationService.cs`
- `Assets/Scripts/World/Waypoints/WaypointService.cs`
- `Assets/Scripts/World/Doors/DoorController.cs`
- `Assets/Scripts/World/Lifts/LiftShaftController.cs`
- `Assets/Scripts/World/Camera/SecurityCamera.cs`
- `Assets/Scripts/AI/EnemiesSpawner.cs`

## Decision Checkpoint

Phases 1 through 6 are complete in code for the scoped Factory/Room/Machine refactor.
The remaining sign-off work is validation only:
1. User-run Edit Mode test suite.
2. Scene smoke checks for alarm flow, machine dispatch, and reservation behavior.
Compatibility notes kept intentionally:
1. `RoomManager.OnRoomAlarmChanged` remains until all listeners are migrated.
2. Historical root-cause text is retained for migration traceability.
