# Worker Flow Migration Plan (Brain-Owned Worker State)

## Goal
Migrate worker routing to a single authority in `RobotBrain` (or a `WorkerFlowController` owned by Brain), while simplifying machine and slot scripts.

Target outcome:
- Stable 3-worker rotation with 1 factory + 1 rest.
- No task ping-pong between machine/slot/brain.
- Clear fallback: no machine available -> go `Start`.

## Why This Migration
Current issues come from distributed decisions:
- Brain/Heart decide tasks.
- Machines also push task transitions.
- Slots can re-drive attach decisions.

This creates race conditions and loops (`Rest -> WorkAtMachine(RestingDesk) -> Rest`).

## Target Architecture
1. `RobotBrain` owns worker flow state and transitions.
2. `RobotHeart` can keep intent labels, but does not run worker routing policy.
3. Machines only:
- expose power state,
- keep occupancy,
- emit events (`OnMachineStateChanged`, `OnWorkerAttached` optional),
- accept/reject attach.
4. Slots only gate who can attach (on enter), no repeated retry loop.
5. `WaypointService` remains selector/reservation service.

## Worker Flow State Model
Use a simple explicit state enum for workers:
- `GoWork`
- `Work`
- `GoRest`
- `Rest`
- `GoStart`

Recommended transition rules:
1. `GoWork`
- Select free work target via `WaypointService`.
- If none -> `GoRest`.
2. `Work`
- Stay while machine ON.
- If machine OFF -> `GoRest`.
3. `GoRest`
- Select free rest target.
- If none -> `GoStart`.
4. `Rest`
- Wait rest duration.
- Then -> `GoWork`.
5. `GoStart`
- Stay saved/idle until machine capacity returns.
- Then -> `GoWork`.

## Migration Phases

### Phase 1: Introduce Flow Controller
Files:
- `Assets/Scripts/Robots/RobotBrain.cs`
- new file: `Assets/Scripts/Robots/WorkerFlowController.cs` (recommended)

Tasks:
- Add worker flow state enum.
- Add transition method table.
- Add one public update entry from Brain (`TickWorkerFlow` or event-driven transitions).
- Keep current Heart calls as compatibility layer during transition.

Acceptance:
- Worker can run full cycle with logs from one place (`WorkerFlow`).

### Phase 2: Make Machine Scripts Dumb
Files:
- `Assets/Scripts/Factory/Machines/FactoryMachine.cs`
- `Assets/Scripts/Factory/Machines/RestingMachine.cs`

Tasks:
- Remove worker routing from machine methods.
- Keep only:
  - occupancy assign/release,
  - power event emission,
  - optional helper `CanAcceptWorker`.
- `SendWorkerToWork`/`SendWorkerToRest` should stop deciding next state.

Acceptance:
- Machine scripts never call branching worker policy.

### Phase 3: Simplify Slots
Files:
- `Assets/Scripts/Factory/Slots/WorkerSlot.cs`
- `Assets/Scripts/Factory/Slots/RestingSlot.cs`

Tasks:
- Use `OnTriggerEnter2D` only.
- Gate by worker flow state (`GoWork` for worker slot, `GoRest` for rest slot).
- Keep `OnTriggerExit2D` only for occupancy release bookkeeping if needed.
- No `OnTriggerStay2D` task-driving behavior.

Acceptance:
- Enter events cause attach once; no re-attach spam.

### Phase 4: Unify Reservation Contract
Files:
- `Assets/Scripts/World/Waypoints/WaypointService.cs`
- `Assets/Scripts/Interfaces/IWaypointService.cs`

Tasks:
- Keep `GetLeastUsedFreeWorkPoint` free-only.
- Add explicit methods for fallback if needed:
  - `GetAnyOnWorkPoint()` for waiting/queue behavior.
- Ensure every reservation has a release point in worker flow transitions.

Acceptance:
- Reservations do not accumulate indefinitely.

### Phase 5: Reduce Heart Worker Responsibility
Files:
- `Assets/Scripts/Robots/RobotHeart.cs`

Tasks:
- Worker role in Heart should mirror flow state (intent visibility only), not select policy targets.
- Remove worker-specific reseed heuristics that compete with Brain flow.

Acceptance:
- Brain flow is single source of routing truth.

## File-Level Change Checklist

`Assets/Scripts/Robots/RobotBrain.cs`
- Add/host worker flow controller.
- Handle machine power events by transition, not by direct task replacement loops.

`Assets/Scripts/Robots/WorkerFlowController.cs` (new)
- State enum.
- Transition logic.
- Timer for `Rest`.
- Target selection via `IWaypointService`.

`Assets/Scripts/Factory/Machines/FactoryMachine.cs`
- Keep occupancy and power state only.
- Remove fallback policy from attach methods.

`Assets/Scripts/Factory/Machines/RestingMachine.cs`
- Keep occupancy + rest timer event only.
- Do not branch to start/work logic.

`Assets/Scripts/Factory/Slots/WorkerSlot.cs`
- Attach only when worker flow state allows work attach.

`Assets/Scripts/Factory/Slots/RestingSlot.cs`
- Attach only when worker flow state allows rest attach.
- No trigger-stay decision loop.

`Assets/Scripts/World/Waypoints/WaypointService.cs`
- Keep strict free-machine query.
- Add/verify release on transition exits.

## Logging Plan (Required During Migration)
Add temporary structured logs in one place (`WorkerFlowController`):
- Worker id (`name + GetInstanceID()`)
- fromState -> toState
- selected target (work/rest/start, machine/waypoint id)
- rejection reason (`no_free_work`, `rest_occupied`, `no_rest`, etc.)

Remove or reduce machine-level routing logs once stable.

## Test Matrix
1. 1 worker, 1 work, 1 rest.
2. 3 workers, 1 work, 1 rest.
3. Work machine OFF while worker attached.
4. Rest machine OFF while worker attached.
5. No rest machine ON.
6. No work machine ON.
7. Power ON/OFF spam by player.

Pass criteria:
- No infinite rest/work flip loop.
- Max one worker attached per machine.
- Overflow workers go to `Start`.
- At least one worker keeps routine when both machine types are available.

## Suggested Rollout
1. Implement flow controller behind a feature flag in `RobotBrain`.
2. Run side-by-side logs (current vs new) for one test scene.
3. Flip default to new flow after stability.
4. Remove legacy worker routing code paths.

## Non-Goals
- No change to follower/security guard combat flow in this migration.
- No UI/prefab art changes.
