# CollectorRobot_Fly Flight and Collection Pipeline

## Document Status

This document records the implemented design for the Collector flight and collection stage. The runtime scripts, Memory -> Brain -> Heart -> Task integration, prefab wiring, machine workflow, and focused Edit Mode coverage described below are now present in the project.

Automated status on 2026-08-06: the prefab builder completed its structural and physics validation, the Collector-focused Edit Mode suite passed 43/43 tests, and the editor assemblies compiled with zero warnings or errors. The controlled in-scene Play Mode acceptance and final feel tuning in Phase 6 remain manual follow-up work; they are intentionally not replaced by an Animator or walking controller.

It continues from `Docs/CollectorRobot_Fly_MasterPuppet_Physics_Setup.md`, which remains the source of truth for the current master/puppet hierarchy, scale, rigidbodies, colliders, hinge, and binder.

The central decision in this document is that the Collector must use the existing robot architecture:

```text
world or machine observation
        -> Memory fact changes
        -> Brain chooses one task
        -> Heart owns/replaces the active task
        -> Task starts or stops one physical operation
        -> Collector body/magnet executes it
        -> new discrete observation returns to Memory
```

There must not be a separate `CollectorMissionController` with another mission-state enum. That would create two competing sources of truth beside Memory, Brain, Heart, and the task stack.

## Approved Gameplay Outcome

When `SpawnRobotCollector` discovers a dead non-Collector robot:

1. The machine queues and claims that corpse.
2. It opens its panels and obtains one inactive `CollectorRobot_Fly` from the pool.
3. It assigns the complete job while the Collector is inactive.
4. The Collector activates with role `Collector` and leaves the machine under physical flight force.
5. It flies on a direct, continuously updated route toward the corpse while locally avoiding obstacles and other robots.
6. It holds near the corpse, aims its magnet, and attracts every eligible robot part.
7. When every required part is settled and secured, it returns to its owning machine.
8. If a part escapes, the Collector pauses the return and gathers the missing cargo again.
9. The machine opens for the returning Collector and validates a small intake zone.
10. The corpse is removed only after the Collector and all required parts are close enough to the intake.
11. The Collector is reset and returned to the pool, then the next queued corpse may be dispatched.

Initial policy: one active Collector per machine. Additional corpses wait in a deterministic queue.

## Preserved Physics Decisions

The next stage must preserve the approved physics baseline:

- Final root scale: `0.4`.
- Enemy tag and Enemy layer.
- Puppet body mass: `1.5`.
- Puppet magnet mass: `0.35`.
- Both gravity scales: `1`.
- Magnet connected by `HingeJoint2D` with `-90` to `+90` degree limits.
- `SimplePuppetBinder` remains the rotation authority for the physical body and magnet.
- `PropellerPivot` remains visual-only.
- No walking Animator or walking movement controller.
- With flight disabled, the robot still falls in Play Mode.

The empty articulated robot has a supported mass of `1.85 kg`. Under normal Unity gravity, the initial hover force is approximately `18.15 N`, before adding steering acceleration.

The selected flight model is force-controlled hover, not a balloon or conveyor model. A balloon/buoyancy force is useful for passive floating but gives weak stopping and target control; a conveyor/line attachment bypasses the collision response the Collector needs. Capped gravity compensation plus position/velocity feedback preserves real dynamic Rigidbody behaviour while still producing controlled flight.

## Explicit Non-Goals for This Stage

- No walking animation or walking controller.
- No general room-to-room waypoint or A-star flight navigation.
- No globally enabled Enemy-to-Enemy collision.
- No multiple simultaneous Collectors from one machine.
- No conveyor-style transform movement, kinematic transport, or teleporting.
- No final combat balance, audio pass, or elaborate particle effects.
- No re-enabling all same-corpse part collisions during magnetic capture; that can be tested later without destabilizing the first implementation.

The target area is currently the machine's broad local detection zone, so direct travel plus local avoidance is the correct first scope. A true route planner can be added later if Collectors must cross rooms or navigate around large static walls.

## Architecture Contract

### Existing pipeline

The relevant current runtime flow is:

```text
Producer
  -> RobotBrainNew public ingress method (records facts only)
  -> RobotMemoryStateNew mutation
  -> MemoryChangeEvent with full RobotMemorySnapshotNew
  -> RobotMemoryNew.OnMemoryChanged
  -> RobotBrainNew.BuildOptions / BuildTaskFromOptions
  -> RobotHeartNew task stack
  -> RobotTaskNew.Enter / Exit
  -> physical body
```

Some existing producers enter through a public `RobotBrainNew` method first. That method is only an ingress adapter: it writes Memory and lets the normal Memory event drive planning. It must not push a Heart task directly.

The current authoritative stack is `RobotTaskStackNew`, which is a simple LIFO stack. `RobotIntentType` and the precedence-based `RobotTaskStack` in `RobotTasks.cs` are legacy types and must not be extended for this feature.

### Responsibility boundaries

| Owner | Collector responsibility | Must not do |
| --- | --- | --- |
| `SpawnRobotCollectorController` | Discover and queue corpses, claim one target, control panels, spawn/pool one Collector, grant dock access, validate intake | Choose Collector tasks or drive its Rigidbody |
| `DeadRobotCollectable` | Own target claim, eligible-part list, live part centre, validity, and final disposal | Choose a Collector task |
| Memory | Store the stable assignment and discrete observed facts | Apply forces or decide the next phase |
| Brain | Convert role plus Memory snapshot into exactly one concrete task | Apply physics or operate panels |
| Heart | Own the active task and its Enter/Exit lifecycle | Recalculate world facts |
| Task | Start/stop one already-selected physical command | Search strategically, write Memory, or queue its own next phase |
| Collector body | Hover, seek a live target, avoid locally, aim magnet, retain cargo, and emit observations | Select a mission phase |
| Flight visuals | Show current thrust through propeller rotation | Affect physics or planning |

### Forbidden shortcuts

The implementation must not:

- call `RobotHeartNew.QueueTask` from the machine;
- call a Collector motor directly from the machine after assignment;
- write changing target positions to Memory every frame;
- use the corpse's current `Vector3` as a task payload;
- add a second mission-phase enum to a Collector coordinator;
- use `transform.position`, `MoveTowards`, or direct Rigidbody velocity assignment as the normal flight mechanism;
- attach the walking `RobotBodyController` merely to satisfy the existing task context.

## New Role, Task Types, and Stable Mission Contract

### Robot role

Append `Collector = 5` after `Boss` in `RobotRole`. Do not insert it between existing values because the enum is serialized in prefabs and scenes.

The prefab must serialize the Heart role as `Collector`, and the machine must reassert that role while the pooled object is inactive.

### Collector task types

Append these values after the current final `RobotTaskType` value:

```text
CollectorStandby
CollectorLaunch
CollectorFlyToTarget
CollectorGatherCargo
CollectorReturnHome
CollectorAbortAndReturn
CollectorDock
```

`Dead` remains the global terminal task and overrides every Collector task.

`CollectorStandby` is the Collector role's default task. It is inert: no lift, no magnet, and no movement command. Normally the complete mission is assigned before activation, so the first visible task is `CollectorLaunch`, not Standby.

### Stable assignment object

All Collector tasks for one job use the same stable reference payload:

```csharp
public sealed class CollectorMissionAssignment
{
    public int MissionId { get; }
    public SpawnRobotCollectorController Home { get; }
    public DeadRobotCollectable Target { get; }
    public CollectorTargetClaim Claim { get; }
}
```

The assignment deliberately stores live object references, not copied positions. The flight body obtains the latest corpse centre and machine marker positions every physics step.

This is required because Brain and `RobotTaskStackNew` compare tasks by task type plus payload equality. Creating a new payload or `Vector3` every frame would repeatedly replan and grow/churn the task stack.

`CollectorTargetClaim` is an opaque token containing the target identity, target generation, and a monotonically increasing claim version assigned on every successful `TryClaim`. A machine-local mission ID alone is not globally unique enough.

The exact `CollectorMissionAssignment` reference is the opaque mission handle. Every observation must match the current assignment reference. Target/cargo observations additionally require the full opaque claim to remain valid. After target invalidation, safe AbortReturn, dock-access, and abort-intake observations still use the matching assignment and must remain acceptable even though the target claim is no longer valid. A callback from a previous phase, claim, corpse lifecycle, or pooled Collector use is rejected.

## Collector Memory Design

### Facts, not a mission state

Add a nested fact group to `RobotMemorySnapshotNew`:

```csharp
public struct CollectorMissionFacts
{
    public CollectorMissionAssignment Assignment;
    public bool LaunchExitReached;
    public bool TargetApproachReached;
    public int RequiredPartCount;
    public int SecuredPartCount;
    public bool CargoSecure;
    public bool CargoLost;
    public bool TargetUnavailable;
    public bool MissionCancelled;
    public bool DockApproachReached;
    public bool DockAccessGranted;
    public bool IntakeConfirmed;
    public bool FlightFault;
}

// Field added to RobotMemorySnapshotNew:
public CollectorMissionFacts Collector;
```

These are observations or stable assignment data. There is intentionally no `CollectorMissionPhase` field. Brain derives the next task from these facts.

Do not store the following in Memory:

- current corpse-centre position;
- current dock position;
- Rigidbody position or velocity;
- motor thrust;
- propeller speed;
- per-frame avoidance direction;
- each cargo part's per-frame position.

Those values are live physical data, and writing them to Memory would trigger needless Brain replanning.

### Memory change types

Append typed changes after existing `MemoryChangeType` values:

```text
CollectorMissionAssigned
CollectorLaunchChanged
CollectorTargetChanged
CollectorCargoChanged
CollectorDockChanged
CollectorTargetInvalidated
CollectorFlightFaultChanged
CollectorMissionCleared
```

### Atomic Memory operations

Expose idempotent operations through `RobotMemoryNew`, backed by `RobotMemoryStateNew`:

```csharp
bool TryAssignCollectorMission(CollectorMissionAssignment assignment);
bool TryApplyCollectorObservation(CollectorBodyObservation observation);
bool TrySetCollectorDockAccess(CollectorMissionAssignment assignment, bool granted);
bool TryConfirmCollectorIntake(CollectorMissionAssignment assignment);
bool TryInvalidateCollectorTarget(CollectorMissionAssignment assignment);
bool TryCancelCollectorMission(CollectorMissionAssignment assignment);
bool TryClearCollectorMission(CollectorMissionAssignment assignment, bool notify);
void ResetAll(bool notify = true);
```

Assignment is one atomic mutation and one event. It must reset every previous Collector progress fact before raising `CollectorMissionAssigned`. Publishing home, target, counts, and flags through several events would allow Brain to plan from a partially initialized job.

Required consistency rules:

- Duplicate observations are ignored and emit no Memory event.
- Assignment resets launch, target, cargo, dock, intake, cancellation, and fault facts.
- `CargoLost = true` clears `CargoSecure`, `DockApproachReached`, and `DockAccessGranted`.
- A newly secured cargo clears `CargoLost`.
- Target invalidation never destroys the target remotely.
- Intake confirmation is accepted only for the current assignment and valid opaque claim.
- Pool reset clears every Collector reference and fact.

## Brain Integration

### Public ingress methods

Add Collector-specific public methods to `RobotBrainNew`:

```csharp
bool OnCollectorMissionAssigned(CollectorMissionAssignment assignment);
bool OnCollectorBodyObservation(CollectorBodyObservation observation);
bool OnCollectorDockAccessChanged(CollectorMissionAssignment assignment, bool granted);
bool OnCollectorTargetInvalidated(CollectorMissionAssignment assignment);
bool OnCollectorIntakeConfirmed(CollectorMissionAssignment assignment);
bool OnCollectorMissionCancelled(CollectorMissionAssignment assignment);
```

Each method validates basic input, calls one Memory operation, and returns whether that transaction was accepted. It never queues a task or commands the body. The machine must not dispose cargo or advance its queue after a rejected/stale intake transaction.

The machine may call the assignment/dock/intake methods on the specific spawned robot's Brain. Body and magnet observations should pass through a small `CollectorRobotObservationBridge` that subscribes to physical events and calls the corresponding Brain ingress method. The bridge is an event adapter, not a decision controller.

### Brain options

Append diagnostic flags beginning at `1 << 6`, without changing existing bit values:

```text
CollectorHasMission
CollectorCargoSecure
CollectorTargetUnavailable
CollectorDockAccessGranted
CollectorFlightFault
```

The Collector planner may also read detailed Collector facts directly from the snapshot. The flags are for useful coarse decisions and trace output, not a second state representation.

The generic `NeedMachine` and `MachineUnavailable` calculation must be skipped for role `Collector`. This robot returns to its assigned home machine but does not participate in factory waypoint selection.

### Planning priority

Brain returns exactly one task using this priority:

| Priority condition | Planned task | Meaning |
| --- | --- | --- |
| `IsDead` | `Dead` | Stop all Collector actuators and use normal death physics |
| No assignment or intake already confirmed | `CollectorStandby` | No active job |
| Target unavailable, mission cancelled, or unrecoverable flight fault; no dock access | `CollectorAbortAndReturn` | Abort and return empty/with whatever remains |
| Target unavailable/cancelled/faulted and dock access granted | `CollectorDock` | Enter the machine for abort finalization |
| Launch exit not reached | `CollectorLaunch` | Leave the machine safely |
| Cargo was lost | `CollectorGatherCargo` | Recover missing assigned parts around their live centre |
| Target approach not reached | `CollectorFlyToTarget` | Outbound direct flight |
| Cargo not secure | `CollectorGatherCargo` | Attract and settle all required parts |
| Dock access not granted | `CollectorReturnHome` | Fly to and hold at live dock approach |
| Otherwise | `CollectorDock` | Enter intake slowly and request validation |

`CollectorReturnHome` remains the same stable task when `DockApproachReached` changes. The machine observes that fact, opens its panels, then grants dock access. Only that grant causes Brain to plan `CollectorDock`.

Damage does not make the Collector flee in the first implementation. `Dead` still pre-empts it normally.

## Heart and Task-Stack Integration

### Replace sequential Collector phases

The current Heart always pushes a changed plan. That is correct for temporary LIFO interruptions, but wrong for sequential Collector phases: Launch, Fly, Gather, Return/AbortReturn, and Dock would remain underneath each other and could resume later.

Add a family-aware stack operation such as `RobotTaskStackNew.ReplaceCollectorFamily(RobotTask task)` and apply this Heart policy:

- Before adding a Collector plan, remove every existing Collector-family task anywhere in the stack, then add the new plan.
- If the new task is `Dead`, push it as a real interrupt.
- Existing roles retain their current LIFO behaviour.
- Pool reset clears the complete stack.

This makes Heart the sole active mission-phase owner without creating a second state machine.

Centralize that policy in one `RobotHeartNew.ApplyPlannedTask` method and use it both from the normal `OnPlannedTask` event and from the `TryGetCurrentPlan` bootstrap in `OnEnable`. The current bootstrap pushes directly; leaving it separate would retain the default Standby underneath the first Launch task.

Collector phase changes are driven by observations through Memory and Brain. Do not both change a Collector Memory milestone and call `CompleteCurrentTask` from the same callback; current synchronous Memory events can otherwise re-enter Heart while it is popping a task. Replacement through the normal plan event avoids that ordering hazard.

### Heart defaults and context

Add `CollectorStandby` to `RobotHeartNew.BuildDefaultTask`.

Keep the existing optional `RobotBodyController` for ground robots and extend `RobotTaskContextNew` with a narrow Collector-only execution seam:

```csharp
public ICollectorTaskBody CollectorBody;
```

Heart serializes/caches the concrete `CollectorRobotBodyController` from the same root and supplies it through `ICollectorTaskBody`. The walking `RobotBodyController` remains null on this prefab. This narrow interface supports deterministic fake/spy task tests without making the concrete MonoBehaviour virtual.

A broad `IRobotBody` refactor is intentionally deferred. Adding one optional Collector body capability is smaller and safer than changing every existing ground-task handler.

### Task handlers

Add these `RobotTaskNew.Enter` commands:

| Task | Physical command |
| --- | --- |
| `CollectorStandby` | `CollectorBody.StopAllActuators()` |
| `CollectorLaunch` | `CollectorBody.BeginLaunch(assignment)` |
| `CollectorFlyToTarget` | `CollectorBody.BeginOutbound(assignment)` |
| `CollectorGatherCargo` | `CollectorBody.BeginGathering(assignment)` |
| `CollectorReturnHome` | `CollectorBody.BeginReturn(assignment)` |
| `CollectorAbortAndReturn` | `CollectorBody.BeginAbortReturn(assignment)` |
| `CollectorDock` | `CollectorBody.BeginDocking(assignment)` |
| `Dead` for Collector role | `CollectorBody.StopAllActuators()` and release magnetic ownership |

Every Collector task must also have an `Exit` case. Exit cancels the current destination, callbacks, and task-specific acquisition mode. It must not write Memory or choose the next task.

Append a `Disabled` exit reason without renumbering existing values. `RobotHeartNew.OnDisable` calls `Exit(..., Disabled)` once, then sets `activeTopTask = null` so an ordinary re-enable enters the retained top task again. Pool cleanup later clears the stack but does not call Task Exit a second time.

Every Collector `Enter` handler checks `RobotNewPipelineRuntime.ShouldDriveGameplay` before starting a physical command. Exit, disable, death, and pool cleanup always run even when gameplay driving is disabled.

Captured cargo should remain in hold mode across Gather -> Return -> Dock. `StopAllActuators` is reserved for Standby, death, cancellation cleanup, disable, and pool reset.

`CollectorAbortAndReturn` is the explicit exception: its physical Enter stops new acquisition and synchronously releases/restores all cargo links, so it returns empty. It does not release the target claim itself; the owning machine releases/requeues that claim when abort intake or emergency recovery finalizes.

`RobotHeartNew.OnDisable` currently unsubscribes without calling `Task.Exit`. The explicit Disabled path above corrects that without double-exiting during pool release. Physical cleanup remains idempotent because disable, death, and pool paths can still meet after the one task-lifecycle Exit.

## Collector Body Pillar

Create the following focused components:

```text
Assets/Scripts/Robots/Collector/
|-- CollectorMissionContracts.cs
|-- CollectorRobotBodyController.cs
|-- CollectorFlightMotor2D.cs
|-- CollectorObstacleSensor2D.cs
|-- CollectorMagnetController2D.cs
|-- CollectorFlightVisuals.cs
|-- CollectorRobotObservationBridge.cs
|-- CollectorPoolLifecycle.cs
|-- CollectorCargoLink.cs
`-- DeadRobotCollectable.cs
```

### CollectorRobotBodyController

This is the Body facade exposed to tasks. It owns serialized references to:

- puppet `bone_Body` Rigidbody2D;
- puppet `bone_Magnet` Rigidbody2D;
- master `bone_Magnet` Transform;
- flight motor;
- obstacle sensor;
- magnet/cargo controller;
- propeller visuals;
- body and magnet rest poses;
- current command token.

It exposes only physical commands and observations:

```csharp
void BeginLaunch(CollectorMissionAssignment assignment);
void BeginOutbound(CollectorMissionAssignment assignment);
void BeginGathering(CollectorMissionAssignment assignment);
void BeginReturn(CollectorMissionAssignment assignment);
void BeginAbortReturn(CollectorMissionAssignment assignment);
void BeginDocking(CollectorMissionAssignment assignment);
void StopAllActuators();
void ResetPhysicalState();
event Action<CollectorBodyObservation> OnObservation;
```

Every command and observation carries the stable assignment reference plus a Body command token. The Body facade rejects a stale command token before publishing anything; Brain/Memory then validate the assignment, and additionally validate the opaque claim for target/cargo observations. A delayed callback from a replaced task cannot mutate the current phase or mission.

The Body facade never decides which `Begin...` command should run.

All references needed by `Begin...` are cached in `Awake`/`EnsureInitialized`, because Heart can enter the initial task during its own `OnEnable`. A `Begin...` call must never emit a synchronous observation: the observation bridge may not have run its `OnEnable` yet. First arrival/count/fault observations are produced no earlier than the next Update/FixedUpdate, after every component is enabled.

Unity does not guarantee that sibling `MonoBehaviour.OnEnable` callbacks run in dependency order. On the Collector prefab, Heart/Brain can enter `CollectorLaunch` earlier in the same `SetActive(true)` pass than `CollectorFlightMotor2D` receives its own enable callback. `StartFlight` therefore retains a pending target provider and profile while inactive, and the motor activates that pending command from `OnEnable`. The temporary `isActiveAndEnabled == false` value during this activation pass is not a flight fault; an authored disabled/missing motor or a motor still inactive on the first physics step is.

### Why the walking body cannot be reused

`RobotBodyController` inherits the Animator-oriented `AnimatorBaseAgentController`. That controller calls Animator methods from its movement loop and expects ground/waypoint movement. The Collector deliberately has no Animator and needs continuous force flight toward live physical targets.

Adding a dummy Animator or letting the walking controller auto-select a child Rigidbody would be unsafe. In this Collector hierarchy the magnet may be discovered before the body, and the movement model would still be wrong.

## Flight Motor

### Force model

Use capped position/velocity feedback in `FixedUpdate`:

```text
position error  = live target - body position
desired velocity = clamp(position error * positionGain, maxSpeed)
requested accel  = clamp((desired velocity - current velocity) * velocityGain, maxAcceleration)
gravity force    = -Physics2D.gravity * sum(connected mass * gravityScale)
maneuver force   = clamp(supported mass * requested accel, remaining thrust budget)
applied force    = gravity force + maneuver force
```

Apply the result with `Rigidbody2D.AddForce` to puppet `bone_Body` only.

Calculate supported mass from the actual body and magnet Rigidbody values. Do not hardcode `18.15 N`; that number is only the current expected baseline.

Configure `maxForce` above the full gravity-compensation requirement. Never clamp the combined vector in a way that trades away vertical support for a large horizontal request; reserve gravity force first, then clamp maneuver force to the remaining budget (or cap horizontal/vertical components separately).

Do not:

- set the Rigidbody velocity every frame;
- use `MovePosition` for normal travel;
- move the root Transform;
- apply an angular stabilization torque.

The binder already uses `MoveRotation` for the paired body Rigidbody. An angular flight controller would fight it.

### Live targets

Sample these on every physics step:

- Launch: machine `LaunchExitPoint`.
- Outbound: `DeadRobotCollectable.GetLiveCollectionCenter(claim)` plus a configurable hover offset.
- Gather/recovery flight: preserve the stable hover height reached at the end of
  outbound travel while following the live centre of unsecured required parts on X.
  Magnet aim tracks that live centre on both axes. This lets the Collector move over a
  final piece pinned along the floor without allowing lifted cargo to recursively raise
  its hover target.
- Return: machine `DockApproachPoint`.
- Dock: machine `IntakePoint` at reduced speed.

These marker objects are position-only `Transform`s, not additional trigger zones or
physics bodies. `SpawnPoint` is the pooled robot's initial pose, `LaunchExitPoint` is
the safe point outside the panels, `DockApproachPoint` is the outside waiting point
used before the panels grant access, and `IntakePoint` is the final target inside the
machine. `CollectorIntakeZone` is the only acceptance volume. Machines without a
panel/gate can reference the same Transform for several marker roles, but this machine
keeps outside approach and inside intake separate so the Collector cannot fly into a
closed panel or close the panels around itself.

Intake overlap must be evaluated in the 2D physics plane. Do not use
`Bounds.Intersects` directly: it also compares presentation Z, while this machine's
2D intake collider and the Collector's 2D body colliders can legitimately have
different Z values.

Never cache these as world-space vectors in the assignment. The current machine `SpawnPoint` uses `MoveWithPlayerPosition`, so its children can move during the mission.

### Arrival rule

Arrival is not a single distance check. For fixed machine markers, require:

- position inside the task-specific radius;
- speed under an arrival-speed limit;
- both conditions held for a short settle time.

The outbound corpse approach is the deliberate exception: require the position radius
for the settle time, but do not require low absolute Collector speed. Corpse pieces are
dynamic and can still be sliding or pushed by other robots; requiring the Collector to
be nearly stationary can leave it following a valid corpse forever without enabling the
magnet. Launch, return approach, and docking retain the low-speed requirement.

Emit one idempotent observation when the condition becomes true. Do not emit it every frame.

### Initial serialized tuning range

These are starting values for implementation and scene tuning, not permanent balance constants:

| Setting | Empty/outbound start | Loaded/docking start |
| --- | ---: | ---: |
| Position gain | `1.5-2.5` | `1.2-2.0` |
| Velocity gain | `3-6` | `4-7` |
| Maximum speed | `4-6` | `2.5-4` |
| Maximum acceleration | `12-20` | `8-12` |
| Approach radius | `0.5-0.75` | Dock: `0.2-0.35` |
| Arrival settle time | `0.25-0.5 s` | `0.4-0.7 s` |

The loaded profile should look heavier even though world-target cargo joints prevent the many corpse pieces from physically pulling the light `1.85 kg` Collector out of the air.

## Collision and Local Avoidance

The current Physics2D matrix disables Enemy-to-Enemy physical contacts. Globally enabling those contacts would alter every existing robot and is out of scope.

For the first implementation:

- keep normal physical collision with the environment;
- query the Enemy layer in `CollectorObstacleSensor2D` so other robots are still detected;
- filter the Collector's own body/magnet colliders, the assigned corpse, and every currently owned cargo part from avoidance results;
- use forward circle casts/overlaps and short side probes;
- combine direct seek velocity with bounded separation/avoidance velocity;
- preserve collision impulses instead of overwriting Rigidbody velocity;
- attempt a short absolute move to the right, retry the original live route, then
  attempt the same move to the left if progress still stalls;
- write a `FlightFault` fact only after the bounded retries fail.

Per-frame avoidance is a Body detail and does not belong in Memory. Only a genuine, timed stall is a discrete fact worth replanning.

### Bounded stall recovery sequence

Keep stall recovery inside `CollectorRobotBodyController`, which already owns the single
live target provider consumed by `CollectorFlightMotor2D`. Do not add a second movement
MonoBehaviour that also commands the Rigidbody; two target owners would compete with the
Brain -> Heart -> Task -> Body command lifecycle.

For each active flight command:

1. Measure progress against the unmodified live mission target. A reduction smaller than
   `minimumProgress` during `stallTimeout` counts as a stall.
2. Record the Collector's current physical position and temporarily replace the motor
   target with that position plus `recoveryOffsetDistance` on world right.
3. End that bounded escape when its duration expires or its target is reached, remove the
   temporary target, and retry the original live mission route.
4. If the original route stalls again, repeat from the then-current position on world
   left, then retry the original route again.
5. Any meaningful progress beyond the original stall distance resets the right/left
   attempt budget. A later obstacle therefore receives a fresh recovery sequence.
6. Publish `FlightFault` only when both escape attempts and their direct-route retries
   fail. Memory, Brain, and Heart continue handling that discrete failure normally.

The recovery target must be absolute and local to the position where the stall was
detected. Adding an offset to a distant mission target is insufficient: for example, a
target far to the left plus a small right offset can still command the robot left and
never produce the intended unjamming motion. Log only recovery transitions and attempts,
not per-frame distance samples.

If later design requires real Collector-versus-robot contact, add a dedicated `CollectorPhysics` layer and configure only the needed matrix pairs. Do not enable Enemy-to-Enemy globally.

## Flight Visualization and Magnet Aiming

### Propeller

`CollectorFlightVisuals` rotates the existing `PropellerPivot.localRotation` around Z.

- Rotation speed follows normalized applied thrust.
- Hover retains a nonzero minimum spin speed.
- Spin ramps up and down instead of snapping.
- It stops on flight disable, death, and pool reset.
- It has no Rigidbody, collider, bone, or Animator.

This is the required first visual proof that flight is active. Audio, particles, and a subtle light can be added later without changing the pipeline.

### Magnet aim

Aim the magnet by rotating the master `bone_Magnet` in `Update`, before `SimplePuppetBinder.LateUpdate` reads it. At initialization, capture the authored master-to-puppet rest mapping and the `HingeJoint2D` reference angle. Convert the desired world direction through that mapping, smooth it, and clamp by the hinge's actual joint-angle limits. Do not assume a raw master local Euler delta of `+90` produces a puppet `jointAngle` of `+90` in this nested authored rig.

Do not apply torque or direct rotation to the puppet magnet Rigidbody. The binder and hinge remain the physical authorities.

Restore the master magnet's authored local rest rotation on stop, death, and pool reset.

## Dead Robot Target and Claiming

### DeadRobotCollectable

`DeadRobotCollectable` owns the corpse-side contract:

```csharp
bool TryClaim(int missionId, Object owner, out CollectorTargetClaim claim);
bool IsClaimValid(CollectorTargetClaim claim);
void ReleaseClaim(CollectorTargetClaim claim);
IReadOnlyList<Rigidbody2D> GetRequiredParts(CollectorTargetClaim claim);
Vector2 GetLiveCollectionCenter(CollectorTargetClaim claim);
bool AreAllRequiredPartsInside(Collider2D intake, CollectorTargetClaim claim);
void CompleteCollection(CollectorTargetClaim claim);
```

`TryClaim` is atomic on Unity's main thread. This prevents two machines from dispatching Collectors to the same corpse.

Every successful `TryClaim` increments a claim version and returns a new opaque token. `TargetGeneration` advances when the same robot identity is prepared for a new collectible dead lifecycle: on pool acquire for pooled robots, or when a non-pooled revived robot dies again. Beginning a generation clears prior claim/completed state; activating its dead state recaches parts. Revival, release, or destruction invalidates the old generation immediately.

Expose scoped target events such as `OnInvalidated`, `OnRequiredPartsChanged`, and `OnClaimLost`. The active observation bridge subscribes only for its opaque claim and unsubscribes on task replacement, cancellation, death, disable, and pool release. `IsClaimValid` also treats a destroyed Unity-object owner as invalid so a vanished machine cannot lock the target forever.

The live centre must be calculated from eligible Rigidbody positions. The dead robot's root Transform is not reliable after death because its physical children scatter independently.

### Creating collectables

The machine's periodic scan should call a safe `DeadRobotCollectable.EnsureFor(RobotStateController)` until all supported robot prefabs contain the component explicitly.

Keep periodic reconciliation even if the machine also subscribes to `RobotStateController.OnAnyRobotKilled`. `DeadRobotSpawner` uses `SetInitialDeadState`, which does not publish a new global kill event.

Because `EnsureFor` can attach `DeadRobotCollectable` to a pooled enemy root, the component participates in root pool lifecycle. On release it invalidates the active claim and clears cached parts. On acquire it advances `TargetGeneration`, clears completed/claim state, and marks itself non-collectible while the robot is alive. The first subsequent death activates that prepared generation and recaches parts without advancing it again. A non-pooled Alive -> Dead lifecycle advances generation when it begins. These operations are order-independent with `RobotStateController` pool callbacks.

### Eligible required parts

At claim time, cache the required-part set from dynamic, simulated puppet Rigidbodies belonging to the dead root.

Reject a claim with zero eligible physical parts. An empty set must not become vacuously secure or cause a successful corpse disposal.

Exclude:

- a robot whose Heart role is `Collector`;
- `SecurityBadgePickup` and other explicitly marked loot;
- static, kinematic, or non-simulated Rigidbodies;
- trigger/sensor/helper bodies;
- the active Collector's own body and magnet.

Prefer an explicit exclusion marker for future loot instead of relying only on names.

If a required part is destroyed externally, remove it from the required set and publish updated counts. Do not wait forever on a null Rigidbody.

## Magnetic Cargo

### Attraction mechanism

Use damped `TargetJoint2D` attraction, following the project's existing `JunkPickup` pattern, but create a separate joint owned only by the Collector feature.

For each required part:

1. Add a dedicated `CollectorCargoLink` and its owned `TargetJoint2D` if the part is eligible.
2. Never reuse or overwrite an existing pickup/player TargetJoint.
3. Assign a distributed cargo slot below/behind the magnet rather than one shared point. Slot spacing is measured in world units so the Collector prefab's `0.4` root scale cannot collapse every slot into the same point.
4. Update the joint's world target each `FixedUpdate`.
5. Use moderate frequency, high damping, and a capped force.
6. During cleanup, first disable the owned joint synchronously, invalidate its assignment/claim owner, restore every temporarily changed property and collision override, and only then schedule destruction of the owned joint/link. Unity `Destroy` is deferred, so disabling/invalidation must make the same frame safe.

Do not parent corpse parts to the Collector. Do not connect the complete 13-plus-kilogram corpse to the light body with hard Distance/Fixed joints. World-target attraction provides the desired magnetic look without making the Collector's flight controller support the full corpse mass and gravity scale.

### Capture modes

- Gather mode: attract all unsecured required parts.
- Carry mode: retain already assigned parts; do not acquire unrelated nearby bodies.
- Recovery mode: gather escaped required parts around their current centre.
- Off mode: remove all owned magnetic control and restore original state.

### Secure rule

Cargo becomes secure when every current required part:

- has an active cargo link owned by the current mission;
- is inside the secure envelope around either its assigned slot or the magnet centre;
- is settled relative to the moving cargo rack and remains inside the qualifying envelope for the normal dwell period.

The secure radius represents a compact cargo envelope around both the assigned slot and
the magnet centre. A slot remains the TargetJoint force destination, but it is not the
sole authority for completion: hierarchy-order slot assignment is arbitrary, and a
visibly captured part can be close to the magnet while far from its particular grid slot.
This matters for the 13-part Worker Spawner and Follower corpses. Require an active owned
link plus dwell and relative settling inside either envelope.

As a bounded anti-deadlock fallback, a part that remains continuously inside either
secure envelope for three dwell periods is also secured even if corpse-to-corpse contact
keeps its reported velocity noisy. A secured part is not declared lost for close-range
jitter; it must exceed the outer escape envelope around both references.

If an owned part remains unsecured for the configured recovery delay, increase only that
part's capped joint force. While recovery is active, temporarily ignore only collision
contacts currently pinning that specific part, then restore their exact previous states as
soon as the part is secured or released. Accept the part after the normal dwell anywhere
inside the existing escape envelope. This recovery does not accept a distant body, does
not change ownership, and does not bypass the requirement that every corpse part has an
active Collector link. Log the recovered part plus its slot and magnet-centre distances
once so repeated physical edge cases remain diagnosable without per-frame logging.

Publish `RequiredPartCount`, `SecuredPartCount`, `CargoSecure`, and `CargoLost` only when those discrete values change.

If one part escapes during return, clear cargo/dock facts through the observation path. Brain replaces Return with Gather. Once all pieces settle again, Brain chooses Return again.

### Collision policy

Keep existing same-corpse collision ignores during the first implementation. Those ignores prevent a dense magnet pile from exploding. Parts may still react to world collisions according to their existing layers.

If visible part-to-part collision is later required, test a dedicated cargo layer and limited collision pairs. Do not remove all `IgnoreCollision` relationships at once.

### Security badge and other loot

A security badge is not corpse cargo. Exclude both `SecurityBadgePickup` components and
objects tagged `BadgeSecurity`; the authored Follower badge is tag-only and otherwise
creates a false fourteenth required part. Before final corpse disposal, call
`SecurityBadgePickup.OnRelease(Vector2.zero)` where that component exists. For a tag-only
badge, disable its attachment joint and reparent it to the world. Also remove
`PickupType.SecurityBadge` from the corpse root's `Inventory` if that slot still
references the badge; reparenting alone can leave pooled Inventory bookkeeping pointing
at world loot.

Final disposal must remove the claimed robot body and required parts only. After synchronously marking the opaque claim completed and detaching excluded loot, release the corpse root through `ObjectPool.Release`. Tracked enemy instances are recycled; unmanaged `DeadRobotSpawner` instances use ObjectPool's destroy fallback. Disable Collector-owned joints synchronously before any deferred destruction.

Final disposal must not call `RobotStateController.MarkAsSaved`, because that changes saved-robot progression and morality counters.

## SpawnRobotCollector Machine Changes

### Required machine state

Extend `SpawnRobotCollectorController` with:

```text
Queue<DeadRobotCollectable> pendingTargets
CollectorMissionAssignment pendingLaunchMission
CollectorMissionAssignment activeMission
GameObject activeCollector
Collector prefab reference
SpawnPoint
LaunchExitPoint
DockApproachPoint
IntakePoint
CollectorIntakeZone
```

Keep the existing broad `RobotDetectionZone` only for discovering corpses. It is much too large to confirm a successful return.

Create `LaunchExitPoint`, `DockApproachPoint`, and `IntakePoint` as children of the existing moving `SpawnPoint`, and always sample their live world positions.

### Queue rules

- Deduplicate child colliders into one `DeadRobotCollectable`.
- Ignore living targets and Collector-role robots.
- Remove invalid/reanimated/destroyed targets.
- Keep separate `queued`, `claimed`, `active`, and `completed` ownership; do not use the current `detectedDeadRobots` HashSet as the whole scheduler.
- Select deterministically by distance from the machine, then instance ID as a tie-breaker. Do not rely on Physics2D overlap result order.
- Dispatch only when no active Collector exists.
- If a claim fails because another machine owns it, skip it and continue scanning.

The current parameterless `OnPanelsOpenReady` can remain for compatibility, but launch must use the stored `pendingLaunchMission`. Never rescan and guess which corpse belongs to a panel callback.

While a Collector is active, the machine subscribes to that robot's `RobotMemoryNew.OnMemoryChanged` and watches only events whose assignment/opaque claim matches its active mission, such as `LaunchExitReached` and `DockApproachReached`. It unsubscribes on completion, abort, death, disable, and pool release. `RobotStateController.OnStateChanged` is cleared by existing pool cleanup, so state subscriptions must also be installed for every dispatch rather than assumed to survive reuse.

### Panel control

Refactor the fixed open-hold-close cycle into explicit operations:

```text
OpenPanels
HoldPanelsOpen
ClosePanels
```

Outbound panels remain open until the assigned Collector reports `LaunchExitReached`. Return panels open after `DockApproachReached` and remain open until intake completes or the active mission aborts.

Dock access is revocable. If panels can no longer remain open, the machine disables, or cargo becomes lost during Return/Dock, the machine calls `OnCollectorDockAccessChanged(assignment, false)` and the intake rejects that assignment. For cargo loss, keep the panels open only until the Collector clears a small dock safety zone, then close them. `CargoLost` has already cleared `DockApproachReached`, so access is granted again only after a fresh matching approach observation.

Panel animation is machine actuator state, not Collector mission state, so it can have its own small open/closed transition logic.

### Dispatch order

```text
1. Select a valid queued corpse.
2. Create a mission ID and atomically claim it.
3. Get an inactive Collector from ObjectPool; its root acquire lifecycle performs the complete reset.
4. Configure/verify role Collector while inactive and verify that no old mission, claim, cargo, or task remains.
5. Restore the authored master/puppet child rest pose, then translate/rotate the final container so the puppet body's reference point aligns with the current SpawnPoint. Do not position body and magnet independently; the authored offset and hinge place the magnet.
6. Atomically assign the stable mission through RobotBrainNew.
7. Store it as pendingLaunchMission.
8. Open panels.
9. Activate the Collector only when panels are fully open.
10. Keep panels open until LaunchExitReached, then close them.
```

If prefab acquisition or assignment fails, release the claim and clear the pending job immediately.

### Return and intake order

```text
1. CollectorReturnHome targets the live DockApproachPoint.
2. DockApproachReached enters Memory.
3. Machine opens and holds the panels.
4. When fully open, machine calls Brain.OnCollectorDockAccessChanged(assignment, true).
5. Brain replaces Return/AbortReturn with CollectorDock.
6. Dock task targets the live IntakePoint slowly.
7. Intake validates the active assignment, opaque claim, and Collector body.
8. Machine applies the normal or abort intake rule described below.
9. Machine calls Brain.OnCollectorIntakeConfirmed(assignment) and continues only when it returns true.
10. Machine marks its active mission finalized synchronously and ignores duplicate intake callbacks.
11. Physical cleanup/disposal is performed exactly once.
12. Collector is prepared, deactivated, reset, and released to ObjectPool exactly once.
13. Panels close and the next queued job may start.
```

The Collector root Transform is not the physical location authority. Validate the puppet body Rigidbody position/collider at intake.

Normal and abort intake have different acceptance rules:

| Outcome | Intake requirement | Target result |
| --- | --- | --- |
| Normal success | Matching Collector inside the intake, valid claim, cargo secure, and every required part inside or within the controlled cargo-rack margin of the intake | Mark claim completed synchronously, preserve excluded loot, then dispose/repool the corpse |
| Abort/cancel/fault | Matching Collector inside the intake; no requirement that corpse parts are present | Disable magnetic ownership, release the claim, requeue the corpse if still valid, and never dispose it |

Finalization must be idempotent. Set `isFinalizingMission`, mark the claim completed/released, remove the target from scheduling, and revoke further intake callbacks before calling any deferred `Destroy` or pool operation. A repeated overlap in the same frame must not dispose twice, release the Collector twice, or advance the queue twice.

Memory multicast events are synchronous. A machine `OnMemoryChanged` handler may start panels or set a pending flag, but it must not pool/destroy/reset the Collector or corpse from inside that callback. Intake should call the transactional `Brain.OnCollectorIntakeConfirmed(assignment)` from the intake operation; after its boolean call returns, the Memory -> Brain -> Heart dispatch has finished, and accepted cleanup can begin safely from the caller.

## Pooling and Lifecycle

### Current risks

Current generic pooling does not reset all Memory facts or Brain's cached plan. Heart's current `OnDisable` also does not execute task cleanup. The Collector adds runtime joints and has two child Rigidbodies whose moved local poses are not reset by reparenting the root.

Without an explicit lifecycle transaction, a reused Collector can retain:

- old target/home references;
- an old claim;
- old cargo joints and ignored collisions;
- secure/dock facts;
- Brain's last plan;
- old Heart tasks;
- nonzero child-body velocity and moved local pose;
- a stale binder rotation target;
- event subscriptions from the previous mission.

### Root lifecycle coordinator

Place `CollectorPoolLifecycle` on the prefab root because `ObjectPool` invokes `IPooledObject` only on root components. It explicitly resets all child Collector systems.

Expose an idempotent `PrepareForPoolRelease(reason)` method. It is called while the Collector is still active and only gates new observations/callbacks, marks release in progress, and temporarily suspends Brain plan publication. It does not call Task Exit.

Other root components such as `RobotStateController` also implement `IPooledObject`, so every reset operation must be idempotent and must not depend on component callback ordering.

The required release order is:

1. Machine unsubscribes from active Memory/state events and marks its mission finalized/cancelled.
2. Machine calls `PrepareForPoolRelease(reason)` to reject every later observation.
3. Machine calls `SetActive(false)`.
4. Heart `OnDisable` performs the one task-lifecycle `Exit(..., Disabled)`, stops the command through that Exit, and sets `activeTopTask = null`.
5. Machine calls `ObjectPool.Release` while the GameObject is already inactive.
6. `CollectorPoolLifecycle.OnReleaseToPool` performs idempotent final cleanup but never calls Task Exit again: stop-all safety, synchronously disable owned cargo joints, restore recorded settings/collisions, release an uncompleted claim, clear subscriptions, clear Memory silently, reset Brain cache and Heart stack, zero velocities, restore authored poses, and clear binder targets.

`OnReleaseToPool` is also a safety finalizer for an incorrect caller, but the supported path must arrive inactive. If it receives an active object, it first gates Heart/observations so reset cannot start a new task; ObjectPool's later deactivation still owns the single Task Exit.

On acquire, still inactive:

1. Increment/reset the Body command-token epoch so callbacks from the previous use are stale.
2. Restore all rest poses and Rigidbody settings.
3. Clear any remaining mission/cargo references idempotently.
4. Reset Brain cache, remove release suspension, and reset Heart to Collector role without starting physical work.
5. Leave the mission empty until the machine assigns it.

Add a public binder reset method such as `SimplePuppetBinder.ClearRotationTargets()` so a pooled body cannot receive one stale `MoveRotation` before the next `LateUpdate`.

Fresh `Instantiate` can briefly run `Awake`/`OnEnable` before `ObjectPool.Get` makes the object inactive. This is why the prefab itself must already serialize role Collector and have an inert Standby default.

## Death and Failure Policy

### Collector death

Add/retain the normal enemy `RobotStateController`, Memory, Brain, Heart, `HealthBot`, `JointBreaker`, stats integration, and death task. The hinge is registered with `JointBreaker`, and acquire/reset calls `RestoreAll` before the Collector can be reused.

Assign a fresh/reset `RobotStats` instance while inactive on every acquisition. The initial implementation may use `EnemyRobotFactory.CreateRobot()` as the explicit baseline; a `CollectorRobotFactory` can later provide different balance. Never leave `RobotStateController.Stats` null, because current damage fallback can kill a stats-less enemy on its first negative health event.

On death:

- Memory `IsDead` makes Brain plan `Dead` before any Collector condition.
- Lift, propeller, magnet attraction, and owned cargo joints stop.
- Captured parts return to normal physics.
- Task/death code emits the failure and releases only physical magnetic control. The owning machine handles that event, atomically clears `activeCollector`/`activeMission`, releases the target claim, requeues the original target if still valid, closes/revokes panels, and permits the next dispatch.
- `SimplePuppetBinder` is disabled through the normal death path and the wired `JointBreaker` may break the magnet hinge.
- The dead Collector is excluded from Collector target eligibility.

The dead Collector remains in the world under normal robot death physics; it is not silently pooled by the machine. Machine ownership must still be cleared, so one dead Collector cannot permanently block the one-active-Collector slot.

### Other failures

| Failure | Required response |
| --- | --- |
| Target destroyed/reanimated before capture | Invalidate target, release magnetic ownership, return empty, then clear/requeue policy |
| Required part destroyed | Recompute required count; never wait for a null body |
| Cargo part escapes | Clear secure/dock facts and replan Gather |
| Short obstacle/collision | Local Body avoidance only; keep the same task |
| Repeated stall | Emit `FlightFault`; Brain plans bounded `CollectorAbortAndReturn` |
| Machine temporarily cannot grant access | Hold physically at DockApproachPoint |
| Controller/scene disabled | Idempotently cancel active work, release claims/joints, and avoid leaked subscriptions |
| Pool release during any phase | Stop all actuators and reset all facts/physics before reuse |

Do not silently destroy a valid corpse away from the intake. An aborted target remains/re-enters the queue unless it became invalid.

AbortReturn also has a bounded retry/time budget. If the same motor cannot reach home after that budget, the machine performs an explicit emergency recall of the Collector only: gate observations, release physical cargo control, release/requeue the valid target claim, clear active machine ownership, play/log the emergency recall, then deactivate and pool the Collector. The valid corpse is never disposed by this fallback, and the machine cannot remain permanently wedged.

## Prefab and Builder Wiring

### Collector root

The final `CollectorRobot_Fly` root should contain or receive references for:

- `SimplePuppetBinder`;
- `RobotMemoryNew`;
- `RobotBrainNew`;
- `RobotHeartNew` with role Collector;
- `RobotStateController`, `HealthBot`, explicitly initialized `RobotStats`, and `JointBreaker` wired to the magnet hinge;
- `CollectorRobotBodyController`;
- `CollectorFlightMotor2D`;
- `CollectorObstacleSensor2D`;
- `CollectorMagnetController2D`;
- `CollectorFlightVisuals`;
- `CollectorRobotObservationBridge`;
- `CollectorPoolLifecycle`.

Keep both Rigidbodies on puppet `bone_Body` and `bone_Magnet`. Do not add a Rigidbody to the root.

Prefer query-based obstacle/magnet sensing so the approved robot prefab can retain exactly its two non-trigger physical colliders. The small intake trigger belongs to the machine.

### Builder warning

`CollectorRobotFlyPrefabBuilder.BuildFinalPrefab` recreates and overwrites the final prefab. Runtime components wired only in the Inspector would be erased the next time the builder runs.

Before completing this feature, extend the builder to add and wire all runtime components and references deterministically. Extend its validation to require the Collector pipeline while retaining these checks:

- exactly two physical Rigidbodies;
- exactly two non-trigger physical colliders on the Collector;
- no Animator;
- flight disabled still falls;
- hinge remains `-90` to `+90`;
- binder remains the rotation authority;
- propeller remains visual-only.

Add a binder-enabled magnet aiming test. The existing hinge-limit simulation disables the binder before applying torque and therefore does not validate the future runtime aiming path.

## Planned File Changes

### Existing runtime files

| File | Planned change |
| --- | --- |
| `Assets/Scripts/Robots/State/RobotRole.cs` | Append Collector role |
| `Assets/Scripts/Robots/Tasks/RobotTasks.cs` | Append Collector tasks |
| `Assets/Scripts/Robots/RobotMemorySnapshotNew.cs` | Add nested Collector facts |
| `Assets/Scripts/Robots/RobotMemoryStateNew.cs` | Add atomic facts, typed events, validation, reset |
| `Assets/Scripts/Robots/RobotMemoryNew.cs` | Expose Collector and full reset APIs |
| `Assets/Scripts/Robots/RobotBrainNew.cs` | Add ingress, options, Collector planning, cache reset/release suspension, skip generic machine logic |
| `Assets/Scripts/Robots/RobotHeartNew.cs` | Add default, Collector body context, phase replacement, disable cleanup |
| `Assets/Scripts/Robots/Tasks/RobotTaskStackNew.cs` | Add Collector-family replacement |
| `Assets/Scripts/Robots/Tasks/RobotTaskNew.cs` | Add Collector Enter/Exit physical handlers |
| `Assets/Scripts/Robots/Body/RobotStateController.cs` | Stop Collector actuators/physical cargo on death; machine retains claim ownership |
| `Assets/Scripts/Misc/Math/Misc/SimplePuppetBinder.cs` | Add safe cached-target reset for pooling |
| `Assets/Scripts/Factory/Machines/SpawnRobotCollectorController.cs` | Queue, claim, spawn, explicit panels, dock, intake, pool |

### New runtime files

Add the files listed under `Assets/Scripts/Robots/Collector/` in the Body section.

### Assets/editor/tests

| File/asset | Planned change |
| --- | --- |
| `Assets/Resources/Prefabs/Robots/Collector/CollectorRobot_Fly.prefab` | Runtime component wiring |
| `Assets/Resources/Prefabs/Map/Basic/Machines/SpawnRobotCollector.prefab` | Prefab ref, live markers, small intake zone |
| `Assets/Editor/CollectorRobotFlyPrefabBuilder.cs` | Build and validate runtime wiring |
| `Assets/Editor/UnitTests/CollectorRobotFlyPrefabTests.cs` | Preserve baseline and add runtime/binder checks |
| `Assets/Editor/UnitTests/SpawnRobotCollectorControllerTests.cs` | Queue, claim, launch, dock, failure coverage |

## Implementation Order

### Phase 1: pipeline contract first

1. Append role, tasks, Memory change values, and Brain option bits without changing existing numeric values.
2. Add `CollectorMissionAssignment`, claim, fact, and observation contracts.
3. Implement atomic Memory assignment/observation/reset with stale-ID rejection.
4. Implement Collector Brain mapping and skip generic waypoint/machine flags.
5. Add Heart default plus shared Collector-family replacement policy for bootstrap and event plans.
6. Add empty Collector task Enter/Exit seams using an `ICollectorTaskBody` fake in tests.

Exit criterion: every Memory scenario selects exactly one expected task, sequential phases replace rather than stack, and `Dead` overrides everything.

### Phase 2: target claim and machine queue

1. Add `DeadRobotCollectable` and eligible-part enumeration.
2. Replace the machine's detection-only HashSet role with queued/claimed/active ownership.
3. Keep periodic scans and add optional kill-event fast-path.
4. Add mission IDs and one-active-Collector dispatch.
5. Refactor panels to explicit open/hold/close operations.

Exit criterion: two machines cannot claim the same corpse, child colliders do not duplicate jobs, and pre-dead spawned robots are found.

### Phase 3: flight and visual body

1. Implement Body facade, PD force motor, live target providers, arrival settling, and local sensing.
2. Implement propeller visualization.
3. Wire Launch, Outbound, Return, and Dock commands.
4. Verify no angular controller fights `SimplePuppetBinder`.

Exit criterion: motor off falls; motor on hovers, follows moved targets, reacts to collision, and reaches live machine/corpse points without teleporting.

### Phase 4: magnet and cargo

1. Implement master-bone magnet aim.
2. Add Collector-owned damped TargetJoints and distributed cargo slots.
3. Add secure counts, dwell rule, cargo loss, and recovery.
4. Preserve excluded badge/loot and restore all temporary settings.

Exit criterion: all required pieces secure before return; one escaping piece replans Gather; reacquisition resumes Return.

### Phase 5: intake, disposal, and pooling

1. Add dock approach handshake and small intake validation.
2. Dispose only the accepted claimed corpse.
3. Implement root lifecycle reset, Brain cache reset, Heart cleanup, binder reset, and child Rigidbody pose reset.
4. Extend the prefab builder and wire both prefabs.

Exit criterion: two reuse cycles contain no old task, target, joint, fact, velocity, pose, or subscription.

### Phase 6: focused end-to-end tuning

Run the real scene sequence with at least two queued corpses, collision interference, moving machine points, cargo loss, target destruction, Collector death, and machine disable.

## Test Plan

### Edit Mode pipeline tests

Create `CollectorRobotPipelineTests.cs`:

- Existing role enum numeric values are unchanged and Collector default does not throw.
- Assignment is atomic, complete, idempotent, and emits one typed event.
- Stale assignment, claim-version, target-generation, and command-token observations are rejected.
- Brain maps the normal Standby -> Launch -> Fly -> Gather -> Return -> Dock sequence exactly and maps invalid/cancelled/faulted missions to AbortReturn.
- Dead overrides every Collector snapshot.
- Invalid target selects safe abort return.
- Cargo loss during Return selects Gather; secure cargo selects Return again.
- A moving target keeps the same stable payload and does not grow the Heart stack.
- Heart calls Exit once before Enter once when replacing phases.
- Immediately after first activation, the entire stack contains exactly one Collector-family task; no Standby remains underneath Launch.
- `Begin...` emits no synchronous observation before the observation bridge has enabled.

### Flight physics tests

Create `CollectorFlightMotorTests.cs`. `PhysicsScene2D.Simulate` advances physics but does not invoke MonoBehaviour `FixedUpdate`, `Update`, or `LateUpdate` in Edit Mode. Put runtime logic behind deterministic methods such as `StepPhysics(dt)`, `StepAim(dt)`, and `StepVisual(dt)` that the normal Unity callbacks call. Edit Mode tests explicitly call the appropriate step before `Simulate`; alternatively, keep lifecycle-order/binder tests in Play Mode.

- Motor disabled preserves the approved falling baseline.
- Motor enabled supports body plus hinged magnet near the hover target.
- Force, acceleration, and speed remain capped.
- A moved live target is followed without replanning.
- A collision impulse deflects the robot and it resumes progress without velocity overwrite.
- Binder and flight motor do not compete for angular control.
- Propeller rotates only while flight is active and resets on disable/pool.
- Binder-enabled aim tests explicitly execute aim -> binder target capture -> physics application order rather than assuming `Simulate` invokes those callbacks.

### Claim and machine tests

Extend `SpawnRobotCollectorControllerTests.cs` and add `DeadRobotCollectableTests.cs`:

- Preserve current child-collider dedupe, death-inside-zone, wrong-layer, living-target, and panel tests.
- Two machines racing one target produce one successful claim.
- Two corpses queue while one Collector is active.
- Panel-open callback launches the stored pending job exactly once.
- Assignment is installed before activation.
- A moving SpawnPoint changes live return targets.
- Missing prefab/pool/target failure releases the claim and does not wedge panels.
- Collector-role dead robots are never queued.
- Disable/enable does not duplicate subscriptions or lose valid uncompleted targets.
- The same pooled corpse identity can die, be claimed, revive/release, and die again; its old generation/claim is rejected and the new death queues.
- A destroyed/disabled claim owner cannot lock another machine out.
- A target with zero eligible parts is rejected rather than considered secure.
- Candidate lists returned in different overlap orders produce the same distance/instance-ID dispatch order.
- Normal intake requires every part and disposes once; abort intake requires only the matching Collector, requeues a valid target, and never disposes it.
- Duplicate intake callbacks in the same frame produce one claim finalization, one pool release, and one queue advance.
- Collector death clears machine active ownership, requeues the target, closes panels, and permits the next dispatch while the dead Collector remains ineligible.
- Cargo loss after dock access revokes intake, clears access, and closes panels after the Collector leaves the safety zone.
- Target destruction, reanimation, cancellation, and FlightFault all reach a terminal non-wedged machine outcome.

### Magnet tests

Create `CollectorMagnetControllerTests.cs`:

- Only eligible assigned rigidbodies are controlled.
- Badge/loot, sensors, static bodies, and Collector bodies are excluded.
- Existing TargetJoints are untouched; only owned links are removed.
- Attraction is finite, capped, damped, and follows distributed moving slots.
- Secure requires every part and verifies normal low-relative-speed dwell plus the bounded in-radius jitter fallback.
- One escaping part clears secure state.
- Cleanup restores all recorded properties and collision overrides.
- Same-frame abort first disables/invalidate links synchronously; deferred Destroy leaves no old joint active or callback accepted.
- Binder-enabled master magnet aiming reaches both approved hinge limits without torque conflict.

### Pool lifecycle tests

Create `CollectorRobotPoolLifecycleTests.cs`:

- Release/reacquire clears mission, claim, cargo, joints, callbacks, velocities, poses, binder target, Brain cache, and Heart stack.
- Assignment while inactive makes Launch the first actionable task on enable.
- Release during every Collector phase runs Exit/cleanup exactly once.
- Two reuse cycles produce one callback per observation, not duplicates.
- At least one lifecycle test uses the real `ObjectPool.Get -> PrepareForPoolRelease -> SetActive(false) -> ObjectPool.Release` path rather than invoking lifecycle methods in isolation.
- Reuse restores `JointBreaker`, assigns non-null fresh stats, and leaves both authored child Rigidbody poses valid for the hinge.

Every test fixture that changes `RobotNewPipelineRuntime.Mode`, `DriveGameplayInShadow`, trace/probe flags, or other global pipeline state must snapshot the prior values and restore them in `TearDown`.

### End-to-end acceptance

One focused Play Mode test or controlled manual acceptance must observe:

```text
dead robot detected
-> target claimed
-> panels open
-> Collector activates with Launch task
-> Fly task
-> Gather task
-> Return task
-> panels open and dock access granted
-> Dock task
-> all parts inside small intake
-> corpse removed
-> Collector pooled
-> next queued corpse starts
```

Also verify that `RobotStateController.OnAnyRobotSaved` is not raised.

Failure variants must include target loss, cargo loss during Dock, Collector death, machine disable, duplicate intake, and an unrecoverable FlightFault whose emergency recall pools only the Collector and leaves/requeues the valid corpse.

## Definition of Done

The feature is complete only when all of the following are true:

- Runtime trace proves `Memory -> Brain -> Heart -> Task -> Collector body` for every phase.
- The machine never queues Heart tasks or directly drives the Collector Rigidbody.
- There is no standalone Collector mission state machine.
- Exactly one Collector mission-phase task exists in Heart at a time.
- Flight disabled makes the approved prefab fall.
- Flight enabled hovers through `AddForce`, follows live targets, and preserves collision response.
- Other robots are avoided through sensing without changing the global Enemy collision matrix.
- Propeller rotation visibly communicates thrust without an Animator.
- Magnet aim uses the master bone and stays inside the existing 180-degree arc.
- Every eligible required corpse part is secured before normal return.
- Lost cargo causes Gather and can recover without a stale task.
- Intake is small, mission-specific, and validates all required parts.
- Security badge/loot survives corpse disposal.
- Corpse disposal does not call `MarkAsSaved`.
- Death, target loss, machine failure, disable, and pooling release claims and temporary joints safely.
- Two pool reuse cycles have no stale Memory, Brain plan, Heart task, physics pose, binder target, or event subscription.
- Re-running `CollectorRobotFlyPrefabBuilder` preserves all runtime wiring.
