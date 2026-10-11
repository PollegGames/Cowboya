# Post-Refactor Playtest Issues

Date: 2026-10-11

## Purpose

This document records the problems found during the first end-to-end playtest after the factory, robot population, and AI behavior changes, together with their implementation status.

The code fixes described below are implemented. The focused C# projects compile cleanly; the final Unity Edit Mode and full level-playthrough validation remain to be run in the already-open Unity Editor.

## Implementation status

- Level 2 and Level 3 static Worker Collector spawning now waits for room and navigation initialization, retries while providers become ready, and reports a diagnostic if it cannot spawn.
- Level 2 spawns from the nearest Rest provider and uses the normal rest-after-nine-cubes policy.
- Level 3 spawns from its Garage, uses no-rest policy, and returns to Start after the completed batch.
- Fixed generated levels now end at Level 5. Level 6 is the first dynamic level and receives the former Level 7 growth step.
- Factory alarm state is reset for each level.
- A Security Guard retains the active repair job until it reconnects to its exact assigned Security machine; queued work is checked only after that reconnection.
- An occupied Work machine keeps its current worker. The arriving worker marks that Work waypoint unavailable and replans to another Work machine, then Rest, then Start. Only Rest machines queue arrivals.
- Boss all-machines-off fainting and Boss grab ownership are restored.

## Confirmed working behavior

- Worker Transformers correctly alternate between working and resting machines.
- Worker Transformers correctly return to the Start room when their relevant machines are unavailable.
- Worker Collectors correctly execute their collection and delivery tasks when spawned and assigned.
- The current automated Edit Mode test suite passes.

## Issue 1: Level 2 was missing its Worker Collector

### Observed

Level 2 has no active Worker Collector performing the Conveyor-to-Garage collection job.

### Expected

- Level 2 must have exactly one Worker Collector.
- The Worker Collector must perform the normal white-cube Conveyor-to-Garage route.
- Level 2 has a Rest room, so after the Garage completes its nine-cube batch, the Worker Collector must go to the Rest machine.
- After resting, it must resume its assigned collection route.

### Areas to inspect

- Static-level Worker Collector spawn definitions and scene providers.
- `SceneInitiator.InitializeStaticReceptionDefenders()`.
- `WorkerCollectorMissionService.SpawnStaticSceneCollector()`.
- Level 2 Garage, Conveyor, Rest, spawn marker, and provider wiring.
- Common robot initialization and navigation setup order.

## Issue 2: Level 3 was missing its Worker Collector

### Observed

Level 3 has no active Worker Collector performing the Conveyor-to-Garage collection job.

### Expected

- Level 3 must have exactly one Worker Collector.
- It must spawn in the Garage room.
- It must perform the normal white-cube Conveyor-to-Garage route.
- Level 3 has no Rest room.
- After the Garage completes its nine-cube batch and no Rest destination can be found, the Worker Collector must receive the normal `ReturnHome` plan, travel to the Start room, and be counted as saved exactly once.

### Areas to inspect

- Level 3 Garage spawn provider and spawn marker.
- Whether static spawning incorrectly requires both a Garage and a Rest provider.
- `WorkerCollectorMissionService.SpawnStaticSceneCollector()`, which currently appears to require a Rest provider before spawning.
- Worker Collector no-Rest assignment creation and Start-room rescue flow.
- Static waypoint graph connectivity between Garage, Conveyor, and Start.

## Issue 3: Fixed generated-level progression must change

### Requested change

- Remove the current Level 4 generated configuration.
- Promote the current Level 5 configuration and content to become the new Level 4.
- Promote the current Level 6 configuration and content to become the new Level 5.
- Begin randomized generation at Level 6.
- The desired fixed generated configurations are therefore the existing Level 5 and Level 6 values, renamed to Levels 4 and 5.

### Confirmed progression contract

Level 6 is the first randomized level. It uses the growth values formerly produced for Level 7, and every later randomized level receives the values formerly produced one level later. The duplicate fixed `Setup Level 6` asset and itinerary entry are removed.

### Areas to inspect after clarification

- `RunProgressManager.runSteps` in `RunProgressManager.prefab`.
- `Setup Level 4.asset` and `Setup Level 5.asset`.
- Playable-level counting with interleaved Laboratory steps.
- `RunSetupManager` level preview selection.
- Dynamic configuration base selection and restart caching.
- Documentation and tests that define Levels 4-5 as fixed and Level 6+ as random.

## Issue 4: Alarm state persists into the next level

### Observed

After the player triggers the alarm in one level, the alarm remains active after transitioning to the next level.

### Expected

- Every playable level must start with the factory alarm in its normal/default state.
- Alarm state from the previous level must not leak through persistent objects, ScriptableObjects, static events, or run data.
- Resetting the alarm must also stop level-local alarm effects such as follower spawning, flashing, or active machine spawning loops.
- Morality and other intended run-persistent values must not be reset as a side effect.

### Areas to inspect

- `FactoryAlarmStatus` lifetime and whether its instance is persistent or asset-backed.
- `FactoryManager.SetupFactoryState()` initialization order.
- Alarm subscriptions in `EnemiesSpawner`, rooms, machines, and UI.
- Scene teardown and `OnDisable`/`OnDestroy` subscription cleanup.
- Whether the next scene reads the alarm before `FactoryManager` resets it.

## Issue 5: Security Guards chain reactivation jobs without returning to their post

### Observed

A Security Guard can reactivate multiple machines one after another without returning to its assigned Security machine.

### Required behavior

The dispatch cycle must be strictly:

```text
Guard attached to its assigned Security machine
    -> select/reserve one pending powered-off machine
    -> leave the Security machine
    -> reactivate that one machine
    -> return specifically to its assigned Security machine
    -> attach to the assigned Security machine
    -> only now inspect and accept the next pending job
```

Additional rules:

- Only one reactivation job may be active factory-wide.
- Powered-off machines discovered during the active job remain pending.
- Completing or powering on the current target must not immediately make the guard eligible for another job.
- A guard becomes eligible only after reconnecting to its assigned Security machine.
- Pending jobs must be checked and dispatched from the Security-machine connection callback.
- The guard must return to its own assigned post, not merely any available Security waypoint.
- A guard must never reactivate its own assigned Security machine.

### Areas to inspect

- `MachineSecurityManager.HandleReactivationTargetPoweredOn()` currently clears the active job before the guard has returned.
- `RobotBrainNew.CompleteReactivateTask()` currently requests the general `SecurityMachine` type rather than an exact assigned post.
- `MachineSecurityManager.HandleGuardConnectedToSecurityMachine()` should be the sole next-job dispatch point.
- The assigned-post mapping and reservation must survive the repair trip.
- Add tests for the complete repair-return-reconnect-next-job sequence, not only own-post exclusion and one active target.

## Issue 6: Boss does not faint when every machine is off

### Observed

In the tested generated level, switching off every machine did not make the Boss faint.

### Expected

- Boss fainting through `FactoryManager.OnFactoryAllMachinesOff` must remain.
- For the Boss, “all machines” includes Work, Rest, Security, and Spawning machines.
- The event must fire once when the registered machine count is greater than zero and the powered-on count reaches zero.
- `EnemiesSpawner` or the eventual population/lifecycle owner must receive the event and transition the active Boss to `RobotState.Faint`.

### Areas to inspect

- Whether every generated machine is registered in `FactoryManager.machinePowerStates`.
- Whether power-off events from all four machine categories reach the room event hub.
- Whether the all-off summary contains stale, destroyed, duplicate, or unregistered machines.
- `EnemiesSpawner.Initialize()` subscription timing and scene lifecycle.
- Whether the Boss exists and has `RobotHeartNew.Role == RobotRole.Boss` when the event fires.
- Whether another component immediately restores the Boss to `Alive` or overrides its faint pose.
- Add a runtime trace containing total registered/on/off counts and each registered machine type when the final machine powers off.

## Related architectural risk from the final review

Static rooms currently initialize machine waypoints but do not follow the same manager-registration path as generated rooms. This may affect future machine power, reservation, security dispatch, and all-machines-off behavior in static Levels 1-3. The next implementation should make static and generated room registration use one shared machine-registration method.

Run checkpoints are also currently written but not loaded by runtime code. This is separate from the playtest issues above but remains unfinished if continue-after-relaunch is required.

## Required validation after fixes

### Level 2

- One Worker Collector spawns and begins the Conveyor-to-Garage route.
- Nine accepted cubes complete a batch.
- The collector reaches and occupies the Rest machine, completes rest, and resumes the same route.

### Level 3

- One Worker Collector spawns at the Garage.
- Nine accepted cubes complete a batch.
- No Rest destination is available.
- The collector reaches Start and is counted as saved exactly once.

### Progression

- Verify the final Level 4, Level 5, and Level 6 contract after resolving the numbering question.
- Verify Laboratory steps do not increment the playable-level number.
- Verify the first randomized level uses the intended threshold and base configuration.
- Verify restart retains the active generated configuration.

### Alarm

- Trigger Wanted/Revolt in one level and complete the level.
- Confirm the next playable level begins in the normal alarm state.
- Confirm no follower-spawn coroutine or alarm visual remains active from the previous scene.

### Security reactivation

- Disable at least three eligible machines.
- Confirm exactly one job becomes active.
- Confirm the guard repairs one target and returns to its own Security machine.
- Confirm no second job starts before it reconnects.
- Confirm the next pending job begins only after reconnection.

### Boss

- In a level containing a Boss, register Work, Rest, Security, and Spawning machines.
- Power off every machine one at a time.
- Confirm the Boss remains active until the last registered machine powers off.
- Confirm the last power-off raises the all-machines-off event and leaves the Boss fainted.

## Remaining validation boundary

Run the focused Edit Mode tests and then perform the full Level 2 through Level 6 playthrough checklist above. Any remaining defect should be reported with the scene, robot role, current task, target machine, and relevant `WorkerCollectorDiagnostics` or `MachineSecurityManager` console trace.
