# RobotAI `*New` Refactor - Remaining Work (Updated April 8, 2026)

## Scope
This file tracks what is still required to complete migration from the legacy robot pipeline to the `*New` pipeline (`RobotBrainNew`, `RobotHeartNew`, `RobotMemoryNew`, `RobotTaskNew`).

Related status document:
- `Docs/RobotAI_NewPipeline_MigrationStatus_2026-04-08.md`

## Completed since the previous version
1. Spawn-time waypoint availability initialization is now wired.
2. `AllAvailableWaypoints` is no longer empty by default after robot spawn.
3. Spawn path now seeds `LastVisitedPoint` for initialized robots.
4. Worker-cycle validation mode is implemented with slot-driven transitions and worker-cycle probe logs.
5. Worker planning contract tests and slot/takeover contract tests were added in EditMode.

Evidence:
- `Assets/Scripts/AI/EnemiesSpawner.cs`

## Remaining Work

## A) Cutover control and logging
1. Decide cutover criteria from `NewShadow` to `NewOnly`.
2. Keep trace logs enabled during validation, then reduce verbosity for normal playtests.

Relevant file:
- `Assets/Scripts/Robots/NewPipeline/RobotNewPipelineRuntime.cs`

## B) Task runtime completeness
1. Keep worker `Rest`/`WorkAtMachine` completion slot-driven in validation mode; verify no hidden timer fallback remains.
2. Confirm each non-worker task type has deterministic completion/block rules in gameplay mode.
3. Validate `Body` integration paths for movement/combat/reactivation transitions.

Relevant file:
- `Assets/Scripts/Robots/Tasks/RobotTaskNew.cs`

## C) Guard dispatch correctness
1. Implement/fix station-state detection used in guard selection.
2. Re-validate guard dispatch for factory/rest/security machine OFF events.

Relevant file:
- `Assets/Scripts/Factory/Machines/MachineSecurityManager.cs`

## D) Producer cleanup after parity
1. Remove duplicate migration-era references (`brain` + `brainNew`, `memory` + `memoryNew`) once parity is confirmed.
2. Keep only the final `*New` event path in perception/damage producers.

Relevant files:
- `Assets/Scripts/Player/FollowPlayerTriggerHandler.cs`
- `Assets/Scripts/Misc/Math/Physics/HealthBot.cs`

## E) Validation and regression
1. Worker-only proof run (4 workers, 1 work, 1 rest) and verify at least one full rest->work takeover sequence.
2. Run role-based test matrix: Worker, SecurityGuard, WorkerSpawner, Follower, Boss.
3. Validate machine ON/OFF transitions under contention (multiple robots, limited stations).
4. Run Edit Mode test suite before PR.

Command:
```bash
unity -runTests -testPlatform EditMode -projectPath "$(pwd)" -quit
```

## Done criteria for the refactor
1. `NewOnly` mode is stable in gameplay scenes.
2. No legacy pipeline dependencies remain in robot AI flow.
3. Task execution behavior matches intended role design under stress scenarios.
4. Logging and docs are reduced to maintenance-level verbosity.
