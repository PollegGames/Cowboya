# Robot AI Debug: Worker Flow Breaks With 1 Factory + 1 Resting

This document captures the expected worker loop, observed behavior, and likely breakpoints
when using 3 workers with 1 factory machine and 1 resting machine. It is intended to
focus debugging and isolate the critical failure.

## Expected Worker Loop (from docs)
Source: `Docs/RobotAI_HeartBrainMemoryBody.md`

1. Default task for workers: `WorkAtMachine`.
2. When a machine turns OFF, Brain pushes `Rest`.
3. When a machine turns ON again, Brain pushes `WorkAtMachine`.
4. Only one worker should occupy the resting machine at a time. Extra workers should
   go to Start (or another fallback).

## Observed Behavior (from logs)
- When 3 workers are spawned with 1 factory machine + 1 resting machine:
  - 2 workers end up in the resting room and stay there.
  - The worker that should rotate to the factory machine does not.
  - The work/rest switching mechanism appears to stall.
- Logs show repeated transitions:
  - `RestingMachine.SendWorkerToRest` -> `ResolvePayload Rest (machine=null)` -> `CenterWaypoint`
  - `RestingMachine.SendWorkerToWork` -> `ResolvePayload WorkAtMachine from RestingMachine -> WorkWaypoint`
  - Then the worker still re-enters the rest flow and gets re-attached.

## Key Code Paths (current)
- `RestingSlot.OnTriggerEnter2D()` always calls `machine.AttachRobot(...)` for workers
  with no task-state check. `Assets/Scripts/Factory/Slots/RestingSlot.cs`.
- `RestingMachine.SendWorkerToWork()` currently clears `currentWorker` immediately
  before the worker has physically left the resting collider. `Assets/Scripts/Factory/Machines/RestingMachine.cs`.
- `RobotBrain.OnMachineStateChanged()` maps machine state to tasks. `Assets/Scripts/Robots/RobotBrain.cs`.
- `RobotBrain.ResolvePayload()`:
  - `WorkAtMachine` from `RestingMachine` -> `GetLeastUsedFreeWorkPoint()`.
  - `Rest` with `machine=null` -> `GetFirstRestPoint()`.

## Likely Failure Points (hypotheses)
1. **Resting slot re-attach race**
   - `RestingMachine.SendWorkerToWork()` clears `currentWorker` immediately.
   - Worker is still inside the `RestingSlot` trigger, so a new `OnTriggerEnter2D`
     from another collider (or another worker) sees `currentWorker == null` and attaches.
   - Result: multiple workers can be attached to the resting machine even though
     the "one worker" rule exists.

2. **Resting slot has no task/intent gate**
   - `RestingSlot` does not check the worker's current task before attaching.
   - So any worker passing through the rest collider can get re-bound to rest,
     even after being sent to work.

3. **Worker colliders trigger multiple enters**
   - If workers have multiple colliders, `OnTriggerEnter2D` fires more than once.
   - This increases the chance that a worker re-attaches to rest immediately
     after being sent to work.

## Suggested Debug Checks (next)
1. Confirm if `RestingSlot` is triggered multiple times per worker:
   - Enable `logSlotDecisions` in `RestingSlot`.
   - Add the worker collider name to the log to see duplicates.
2. Confirm if `currentWorker` is cleared before the worker exits:
   - Add a temporary log when `currentWorker` becomes null in `RestingMachine.SendWorkerToWork()`.
3. Check collider setup:
   - Does the resting machine prefab have multiple `RestingSlot` colliders?
   - Do workers have multiple colliders that enter the same trigger?
4. Check if worker is still inside rest slot when sent to work:
   - If yes, `OnTriggerExit2D` might be the better release point.

## Potential Fix Directions (not applied here)
1. **Hold occupancy until worker exits the rest trigger**
   - Track a "leaving" state but keep `currentWorker` until `OnTriggerExit2D`.
2. **Gate rest attachment based on task**
   - Only attach if worker's current task is `Rest`.
3. **Add a cooldown on rest re-attach**
   - Prevent re-attach for X seconds after sending to work.
4. **Move rest attachment out of trigger**
   - Route through a manager that assigns rest spots explicitly instead of using the collider.

## What This Document Is For
This is a focused breakdown of the failing worker loop. Use it to decide
whether the root cause is a collider/attachment race, task routing, or
reservation logic.

## Fix Applied (Current Attempt)
Applied in code to keep behavior consistent with this document:
- Hold resting slot occupancy until `OnTriggerExit2D`.
- Gate rest attachment so only workers with `CurrentTask == Rest` can attach.

Files changed:
- `Assets/Scripts/Factory/Slots/RestingSlot.cs`
- `Assets/Scripts/Factory/Machines/RestingMachine.cs`

## Investigation Log (What We Tested)
This section records the concrete changes and observations so far.

### Changes Made
1. **Resting slot gating + release on exit**
   - Rest slot now only attaches workers whose current task is `Rest`.
   - Added `OnTriggerExit2D` to release the resting worker on exit.
   - Files: `Assets/Scripts/Factory/Slots/RestingSlot.cs`

2. **Resting machine attach guard**
   - Prevent duplicate attach for same worker.
   - When rest is occupied, send extra worker to an alternate work target instead of start.
   - Files: `Assets/Scripts/Factory/Machines/RestingMachine.cs`

3. **Primary task reseed after services available**
   - `RobotBrain.InitializeServices` now resets the heart stack so primary tasks are built with `WaypointService`.
   - Files: `Assets/Scripts/Robots/RobotBrain.cs`

4. **Primary worker task selection**
   - Worker primary task now chooses:
     - free work machine if reservation succeeds,
     - else rest if a rest slot is actually available,
     - else a busy work machine as a swap target.
   - Files: `Assets/Scripts/Robots/RobotHeart.cs`

5. **Reserved machine check for work points**
   - `GetWorkOrRestPoint` only returns work points with a free ON machine.
   - Files: `Assets/Scripts/World/Waypoints/WaypointService.cs`

6. **Busy machine lookup**
   - Added `GetAnyOnFactoryMachine` to target occupied ON machines.
   - Files: `Assets/Scripts/Interfaces/IWaypointService.cs`,
     `Assets/Scripts/World/Waypoints/WaypointService.cs`

7. **Prevent WorkAtMachine overriding Rest**
   - When `Rest` task is pushed by machine state change, remove any `WorkAtMachine` tasks first.
   - Files: `Assets/Scripts/Robots/RobotBrain.cs`,
     `Assets/Scripts/Robots/RobotHeart.cs`

### Key Log Observations
From the latest runs (user log excerpts):
- Workers still often start with `WorkAtMachine (null)` before services exist.
- `GetLeastUsedFreeWorkPoint` increments repeatedly for the same waypoint.
- Rest tasks appear (`current=Rest (CenterWaypoint)`) but are frequently replaced by `WorkAtMachine`.
- Resting slot frequently logs `Ignored ... task=WorkAtMachine`.
- Workers bounce between `Rest` and `WorkAtMachine`, but do not stabilize into the expected 1-rest / 1-work / 1-queue swap pattern.

### Current Hypotheses
1. **Reservation/availability mismatch**
   - Workers can still target the same work waypoint even when the machine is already occupied.
   - Reservation and occupancy are not consistently enforced across all entry points.

2. **Rest acceptance mismatch**
   - Resting slot only accepts `Rest`, but many workers remain `WorkAtMachine` due to higher precedence or reseeding.

3. **Multiple task sources racing**
   - `OnMachineStateChanged` + `ResetIntentStack` + task expiry/complete can override each other.

### Expected Target Behavior (still unmet)
- With 1 factory (ON) + 1 resting machine (ON):
  - 1 worker stays working at the factory.
  - 1 worker rests, then after the rest delay swaps into the factory.
  - 1 worker waits and targets the occupied factory (swap target), not the start room.
