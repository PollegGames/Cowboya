# Robot AI New Pipeline - Migration Status (April 8, 2026)

## Context
This status note summarizes where the `*New` robot pipeline stands after the latest integration pass and the runtime logs captured during `MapGeneration` scene load.

This document now includes the worker-cycle validation implementation added on April 8, 2026.

## What the latest logs show
- Scene bootstrap and map initialization complete successfully.
- Robot initialization runs for workers and boss without runtime exceptions.
- `RobotMemoryNew` emits `MemoryNew.OnChanged` events at spawn time for:
  - `LastVisitedPointChanged`
  - `WaypointAvailabilityChanged`
- The trace lines are expected with current runtime switches:
  - `RobotNewPipelineRuntime.Mode = NewShadow`
  - `RobotNewPipelineRuntime.EnableTrace = true`

Conclusion: these logs mostly show normal initialization + verbose tracing, not a crash path.

## Migration status by area

## 1) Runtime switches
- Status: `IN PLACE`
- Evidence: `Assets/Scripts/Robots/NewPipeline/RobotNewPipelineRuntime.cs`
- Notes:
  - New pipeline is active for planning (`NewShadow`).
  - New pipeline is not yet authoritative for gameplay movement/execution (`ShouldDriveGameplay` is true only in `NewOnly`).

## 2) Memory backbone (`RobotMemoryStateNew` -> `RobotMemoryNew`)
- Status: `MIGRATED`
- Evidence:
  - `Assets/Scripts/Robots/RobotMemoryStateNew.cs`
  - `Assets/Scripts/Robots/RobotMemoryNew.cs`
- Notes:
  - Fact mutations and event emission are wired.
  - Waypoint availability dictionary is initialized and updated.

## 3) Spawn-time memory seeding
- Status: `MIGRATED`
- Evidence: `Assets/Scripts/AI/EnemiesSpawner.cs`
- Notes:
  - New robots call `InitializeWaypointAvailability(waypointService.GetAllWaypoints())`.
  - Spawned robots set `LastVisitedPoint`.
  - This closes the old gap where `AllAvailableWaypoints` could be empty.

## 4) Producer -> BrainNew event flow
- Status: `PARTIALLY MIGRATED (dual path still present)`
- Evidence:
  - Perception: `Assets/Scripts/Player/FollowPlayerTriggerHandler.cs`
  - Damage: `Assets/Scripts/Misc/Math/Physics/HealthBot.cs`
  - Machine/security dispatch: `Assets/Scripts/Factory/Machines/*.cs`
- Notes:
  - Main producers call `OnPerceptionChanged`, `OnDamageTaken`, `OnMachineStateEvent`, `OnSecurityDispatch`.
  - Some scripts still keep duplicated/legacy-looking serialized references (`brain` + `brainNew`, `memory` + `memoryNew`), so cleanup is still needed after parity confirmation.

## 5) Brain/Heart/task runtime
- Status: `FUNCTIONAL FOR SHADOW PLANNING, NOT FINAL FOR CUTOVER`
- Evidence:
  - `Assets/Scripts/Robots/RobotBrainNew.cs`
  - `Assets/Scripts/Robots/RobotHeartNew.cs`
  - `Assets/Scripts/Robots/Tasks/RobotTaskNew.cs`
- Notes:
  - Brain computes options and planned tasks from memory snapshots.
  - Heart stack behavior is active and traceable.
  - Task runtime still contains provisional/fallback behavior (many timed `ScheduleCompleteCurrentTask` paths and TODO-style comments), so final behavior parity still needs validation before `NewOnly`.

## 6) Known logic risk observed in current code
- `MachineSecurityManager.IsGuardStationedAtSecurityMachine(...)` currently returns `false` unconditionally.
- Impact:
  - Guard candidate filtering/dispatch may not reflect intended stationing logic.
  - This should be fixed before final cutover.

## 7) Observability baseline implemented
- `RobotEcosystemProbe` now records:
  - spawn role + initial waypoint
  - Brain entrypoint calls (`OnPerceptionChanged`, `OnDamageTaken`, `OnMachineStateEvent`, `OnSecurityDispatch`)
  - Heart planned/current task transitions
  - slot attach/reject outcomes (`WorkerSlot`, `RestingSlot`, `SecuritySlot`)
- Runtime toggles available in `RobotNewPipelineRuntime`:
  - `EnableTrace`
  - `EnableEcosystemProbe`
  - `EnableProbeSummaryOnSceneInit`
- Scene startup now emits a structured probe summary after enemies are spread.

## Worker-only movement proof update (implemented)

## Runtime policy and toggles
- `RobotNewPipelineRuntime.WorkerCycleValidationMode` added.
- In validation mode:
  - Worker `Rest` / `WorkAtMachine` no longer auto-complete from generic task timers.
  - Worker cycle completion is driven by slot/machine lifecycle events.
  - Worker summaries are dumped at scene start + `t+10s` + `t+30s`.

## Worker planning contract (Brain)
- Worker now follows deterministic cycle mapping:
  1. `connected + last=Work` -> `WorkAtMachine`
  2. `connected + last=Rest` -> `Rest`
  3. `disconnected + last=Rest` -> `GoToMachine(Work)`
  4. `disconnected + last=Work` -> `GoToMachine(Rest)`
- Worker `MachineUnavailable` now means no Work/Rest waypoint exists at all (not merely unavailable flags).
- Worker no longer falls to `Idle` while Work/Rest waypoints exist.

## Slot lifecycle and de-churn
- `WorkerSlot`:
  - per-robot duplicate trigger guard
  - owner-only release checks
  - validation work-hold release path to keep cycle moving
- `RestingSlot`:
  - per-robot duplicate trigger guard
  - owner-only release checks
  - waiting queue + takeover on rest timer completion
  - on `rest_done`, current rest owner is released and next waiting worker is promoted

## Probe and observability additions
- `RobotEcosystemProbe` now tracks:
  - worker chain transitions (`WorkerCycle ... reason=attach|release|rest_done|replan`)
  - current target waypoint per robot
  - current machine ownership per robot
  - duplicate counters:
    - `slot_attach_ignored_duplicate`
    - `slot_release_ignored_non_owner`
- New worker summary line includes:
  - last transitions (tail)
  - current target
  - current owner machine

## EditMode contract coverage added
- Worker planning mapping tests for all 4 cycle states.
- Worker no-idle test when Work/Rest waypoints exist.
- Slot dedupe test for duplicate trigger entries.
- Rest takeover test (A released on rest done, B promoted).
- Memory transition test on rest attach/release.

## What still needs to be done

## Priority 0 - Before enabling `NewOnly`
1. Validate behavior parity role by role (Worker, SecurityGuard, WorkerSpawner, Follower, Boss) with trace on.
2. Replace provisional task handlers in `RobotTaskNew` with fully validated execution rules where needed.
3. Fix guard station-state check in `MachineSecurityManager` and retest dispatch scenarios.
4. Run focused scene tests with machine ON/OFF transitions and multi-robot contention.

## Priority 1 - Cutover readiness
1. Switch runtime mode from `NewShadow` to `NewOnly` in a controlled test branch.
2. Keep trace on during first cutover run, then reduce verbosity once stable.
3. Remove remaining dual/legacy references in producer scripts.

## Priority 2 - Cleanup
1. Remove migration shims no longer needed after stable cutover.
2. Consolidate docs naming (`RobotAI_SomethingNew_RemainingWork.md` is a temporary name).
3. Add a short regression checklist to run before each PR touching robot AI pipeline.

## Suggested validation checklist for next pass
1. Start run from main menu and verify no exceptions during spawn/bootstrap.
2. Worker-only focus: verify visible movement cycle (`Work <-> Rest`) for at least two workers.
3. Confirm `WorkerCycle` logs include `attach`, `release`, `rest_done`, and `replan`.
4. Confirm `slot_attach_ignored_duplicate` and `slot_release_ignored_non_owner` do not grow uncontrollably.
5. Trigger machine OFF/ON and verify worker/guard replanning behavior.
6. Trigger combat perception + damage and verify follower/security transitions.
5. Run Edit Mode tests:
   - `unity -runTests -testPlatform EditMode -projectPath \"$(pwd)\" -quit`
