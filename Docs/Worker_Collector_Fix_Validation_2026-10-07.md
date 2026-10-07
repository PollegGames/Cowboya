# Worker Collector pickup and repeating garage cycle

Implemented and validated on 2026-10-07 with Unity 6000.3.22f1.

## Runtime behavior

The worker remembers a stable source, garage, and rest assignment before a cube
exists. It travels to the source Work waypoint, acquires the nearest eligible
white cube within local reach, carries it to its reserved garage slot, and repeats.
After nine accepted deliveries, it waits for the garage's batch-completed event,
travels to rest, rests for the provider's configured duration, and starts again.

All service and physical observations enter `RobotMemoryNew`. `RobotBrainNew`
chooses the next concrete action from those facts. `RobotHeartNew` owns the intent
and task transitions. Tasks dispatch movement, acquisition, grabbing, delivery,
waiting, and resting to the body. Providers do not start body tasks directly.

## Fixes

- Navigation stops locomotion on arrival without erasing the arrival fact before
  the collector can observe it. Explicit task cancellation still clears the path.
- Production pickup aims at the cube's actual distance, bounded by maximum reach.
  The Inspector Target remains an arm aiming preview; production tasks select
  their claimed target from Memory automatically.
- Either hand can carry cargo. The selected arm holds its pose after attachment,
  avoiding an IK feedback loop targeting its own carried cube.
- Claims do not stop conveyor motion. Lost targets release claims and reservations;
  a cube lost locally triggers acquisition, while a cube lost in transit triggers
  a return to the source.
- Garage rejection waits for availability. Removing a stored cube during the
  closed processing hold interrupts the batch and resumes collection.

The existing Level_2 scene and production prefab already supply the required
providers, waypoints, carry anchor, and pipeline components. No scene overrides
or manual Inspector cube target are needed.

## Verification

- 59 targeted Edit Mode tests passed: collector pipeline, arm reach, source and
  conveyor behavior, Level_2 prefab configuration, and garage storage/processing.
- A separate Play Mode probe opened the real `Assets/Scenes/Level_2.unity`, using
  normal navigation, IK, physics, conveyor spawning, and garage processing. It
  observed actual carrying, nine stored cubes, one completed batch, rest, and a
  new `WorkerCollectorMoveToSource` task. Passed in about 70 seconds at 5x time.
- Full Edit Mode suite: 293 passed, 19 failed, 312 total. All 19 failing test names
  also failed against the pre-change commit `1b80a44` in the same isolated project
  (baseline: 269 passed, 24 failed, 293 total). No new failing tests were introduced.

Local reports are in `Logs/WorkerCollectorFix/` (ignored by Git). Validation used
an isolated project with its full Windows path; abbreviated 8.3 paths prevented
Unity from mapping scripts to prefabs. The scene probe used D3D11 because the
headless graphics device crashed while rendering this scene's sprites.
