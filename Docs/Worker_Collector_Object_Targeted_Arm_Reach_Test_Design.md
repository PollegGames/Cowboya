# Worker Collector Object-Targeted Arm Reach and Pickup

> Production integration correction (2026-09-03): the isolated arm mechanics in this
> document remain valid, but the Level 2 workflow must no longer claim a cube and navigate
> to a cube-derived position before reaching the conveyor. For the corrected production
> state machine and ownership timing, see
> [`Worker_Collector_Logic_Correction_Plan.md`](Worker_Collector_Logic_Correction_Plan.md).
> That plan supersedes the Level 2 integration sequence here wherever they disagree.

Status: **Validated in isolation and integrated into the Worker Collector mission pipeline**  
Date: 2026-09-03  
Test scene: `Assets/Scenes/TestingSceneBot.unity`

## 1. Confirmed Goal

Build an isolated arm-reach test for the Worker Collector in `TestingSceneBot`.

At Play time, a scene object representing the white cube is assigned in the Inspector. The Worker Collector continuously observes that object's world position and:

1. uses the left arm while the target is on the worker's left;
2. uses the right arm while the target is on the worker's right;
3. moves the active arm toward the target through the existing 2D IK arm rig;
4. returns the inactive arm to its captured rest pose;
5. changes arms automatically when the target crosses from one side to the other;
6. keeps following when the target is moved in the Inspector during Play mode.

The arm mechanics were first validated in isolation. They are now also used by the Level 2 Worker Collector collection pipeline for approaching, reaching, and securing claimed white cubes.

## 2. Important Clarification About the Player Mechanism

The player feature is split into separate responsibilities:

- `ArcTargetFollower` converts player aim into a point on a fixed-radius circle around the body.
- `ArmTargetController` moves the arm solver targets and coordinates player-only grab, attack, input, and energy behavior.
- The left and right `LimbSolver2D` components bend the physical arm bones toward their solver targets.
- `CowboyGrabController` detects and attaches an `IGrabbable`; it is not responsible for choosing the arm path.

There is one player `ArcTargetFollower`, not one arc component per arm. The appearance of two arm arcs comes primarily from the two IK limb chains and their bend/flip configuration.

The Worker Collector already inherits both arm chains from `Worker3.prefab`:

- `LArm_Solver_Target` and `LHand_Effector`
- `RArm_Solver_Target` and `RHand_Effector`

The original `WorkerCollectorBodyController` moved only the right solver target directly. The integrated version delegates arm motion to `RobotObjectArmReachController` while retaining mission ownership, claims, pickup-distance checks, carrying, and delivery.

The Worker prefab also contained two reversed upper-arm puppet bindings. The left and right hand bindings were correct, but the upper arms were wired from Puppet to Master. Both are now wired from IK Master to physics Puppet, matching the Player and allowing the visible arm to reproduce the IK pose.

## 3. Proposed Scope

### Included in the first test

- A reusable object-targeted arm-reach component, provisionally named `RobotObjectArmReachController`.
- Serialized Inspector references for:
  - body reference;
  - target `Transform`;
  - left and right arm solver targets;
  - left and right IK solvers;
  - optional left and right hand effectors for debug/validation.
- Configurable follow speed, return speed, reach radius, and side-switch threshold.
- Continuous target tracking in `LateUpdate`.
- Left/right arm selection based on target X relative to the worker body.
- A small center dead zone (hysteresis) so the selected arm does not rapidly flicker when the cube is near the body's center line.
- Automatic fallback to the available arm if one side is not configured.
- Safe rest behavior when the target is missing or the component is disabled.
- Scene wiring only in `TestingSceneBot` for the initial experiment.
- Edit Mode tests for the reusable selection and target-position calculations.
- A manual Play Mode validation checklist.

### Still excluded

- Mouse or controller input.
- Player energy, attacks, inventory, grab buttons, throw behavior, or `CowboyGrabController` reuse.
- Changes to other enemy prefabs.

## 4. Proposed Runtime Design

```text
Inspector-assigned cube Transform
              |
              v
RobotObjectArmReachController
    | chooses side from target.x - body.x
    | projects the cube direction onto a fixed-radius orbit
    | moves active solver target
    | returns inactive solver target to rest
              v
left or right LimbSolver2D
              v
arm bones and hand follow the target
              |
              v
WorkerCollectorBodyController measures active hand-to-cube distance
    | calls WhiteCubeCargo.TryBeginCarry at pickup distance
    | keeps the existing claim, carry, delivery, and storage flow
```

The new controller should contain only generic arm targeting behavior. It must not know about white cubes, Worker Collector missions, the player, or Level 2. A later enemy or robot system can populate the same `Target` property with any relevant object.

Suggested public API:

```csharp
public Transform Target { get; set; }
public CowboyArmSide ActiveArm { get; }
public void SetTarget(Transform target);
public void ClearTarget();
```

The serialized `target` field remains visible for the `TestingSceneBot` experiment. In production, `WorkerCollectorBodyController` calls `SetTarget(assignment.Target.transform)` when the grab task begins, without coupling the reusable controller to mission types.

## 5. Target and Arc Behavior

The cube's raw position should not be copied blindly to a solver target at unlimited distance. A two-bone IK arm has a finite length, and an unreachable target can cause a fully stretched or unstable pose.

Recommended behavior:

1. Calculate direction from the body/shoulder reference to the cube.
2. Project the solver destination onto a configurable fixed-radius orbit.
3. Preserve the direction toward the cube, including its vertical component.
4. Move the active solver target toward that destination at `followSpeed`.
5. Let the existing `LimbSolver2D` chain create the curved elbow pose.

This uses the same principle as `ArcTargetFollower`: the cube supplies a direction, while the solver destination always remains on the configured orbit. Distance from the Worker to the cube does not change the orbit radius.

The validated Worker Collector orbit radius is **2.2 world units**. Separate left/right radius and offset settings can be added only if a future robot rig requires them.

## 6. Arm Selection Rule

Let:

```text
horizontalDelta = target.position.x - bodyReference.position.x
```

- If `horizontalDelta > sideSwitchThreshold`, select the right arm.
- If `horizontalDelta < -sideSwitchThreshold`, select the left arm.
- Inside the threshold, keep the previously selected arm.

Keeping the previous side inside the center band prevents left/right switching every frame when the target is nearly centered.

On a side change:

1. the previously active arm starts returning to its captured local rest position and rotation;
2. the newly active arm starts following the target;
3. only the newly active IK solver is treated as the active reaching solver;
4. solver flip values are configured per arm and not assumed to be identical.

## 7. `TestingSceneBot` Setup

The scene should contain:

- one Worker Collector instance;
- one movable white-cube scene instance;
- the new reach controller on the Worker Collector or a dedicated child;
- all arm and IK references wired in the Inspector;
- the cube instance's `Transform` assigned to the controller's `Target` field.

The Inspector must reference the **cube instance in the scene**, not only the prefab asset in the Project window. A prefab asset has no meaningful runtime world position to move. The scene cube may still be an instance of the existing white-cube prefab.

For this isolated test, the Worker Collector mission/locomotion behavior should be disabled or placed in a passive state so it cannot move the body, overwrite the right-arm solver target, or claim the cube while the arm test is running. There must be only one writer for each solver target during the test.

## 8. Manual Validation Checklist

1. Open `TestingSceneBot` and enter Play mode.
2. Confirm the worker stays in place.
3. Put the cube clearly to the worker's right.
   - The right arm follows it.
   - The left arm remains at or returns to rest.
4. Move the cube up, down, nearer, and farther while keeping it on the right.
   - The right arm updates continuously.
   - A target beyond maximum reach produces a stable fully extended direction, not solver instability.
5. Move the cube clearly to the worker's left.
   - The left arm becomes active.
   - The right arm returns smoothly to rest.
6. Move the cube repeatedly around the body's center line.
   - The dead zone prevents rapid arm flicker.
7. Disable or clear the target during Play mode.
   - Both arms return safely to rest.
   - No null-reference errors are logged.
8. Reassign the target during Play mode.
   - Tracking resumes without restarting the scene.
9. Disable and re-enable the reach controller.
   - Rest poses and solver state remain valid.
10. Exit Play mode and confirm no scene/prefab values were unintentionally changed.

## 9. Automated Validation

Edit Mode tests should cover logic that does not require visually evaluating Unity IK:

- target right of body selects the right arm;
- target left of body selects the left arm;
- target inside the center threshold preserves the previous arm;
- crossing the threshold changes arm exactly once;
- targets inside and outside the orbit both project to the configured radius;
- missing target returns a no-target/rest result;
- missing one arm falls back to the configured arm;
- clearing or disabling restores solver defaults and rest state.

The final appearance of the arm bend must still be validated in Play mode because Edit Mode tests cannot prove that the rig artwork and `LimbSolver2D` flip settings look correct.

## 10. Level 2 Integration

The accepted integration is:

1. `WorkerCollectorBodyController` calculates an approach position that places the claimed cube on the configured 2.2-unit orbit.
2. When the grab task begins, it supplies the claimed cube to `RobotObjectArmReachController`.
3. The reusable controller selects and moves the appropriate arm while reporting its active hand effector.
4. The body waits until that effector is within `pickupDistance`, then calls `WhiteCubeCargo.TryBeginCarry` using the hand-mounted carry anchor.
5. The existing Brain/Memory/Task pipeline receives `CargoSecured` and continues to garage delivery.
6. Delivery, target loss, cancellation without cargo, or disable clears the arm target and returns the arm to rest.

Other robot enemies can later use the same reach controller with a different target provider. The reusable component should therefore depend on `Transform`, arm references, and configuration—not on white-cube or player classes.

## 11. Risks and Guardrails

- **Two systems writing the same solver target:** the production body delegates solver motion to `RobotObjectArmReachController`; its legacy direct reach remains fallback-only when that component is absent.
- **Reversed puppet binding:** upper-arm pairs must remain Master-to-Puppet; an Edit Mode regression test verifies their roots and the Puppet `Rigidbody2D` destinations.
- **Incorrect IK bend on one side:** expose or correctly cache per-arm solver flip settings and validate both sides visually.
- **Rapid switching at center:** retain the previous arm inside `sideSwitchThreshold`.
- **Unreachable orbit:** configure the Worker orbit radius to fit its smaller rig.
- **Moving body changes local rest pose:** capture rest in local space and drive active destinations in world space.
- **Scene-only success but broken prefab:** the reusable reach component and its 2.2 radius are configured on the production Worker Collector prefab.
- **Confusing reach with pickup:** reaching remains generic; only `WorkerCollectorBodyController` owns the white-cube claim and attachment decision.

## 12. Validated Decisions

The proposed defaults are:

1. **Phase 1 validated visual reaching. Phase 2 performs pickup.** The mission body attaches the claimed cube only after the active hand reaches pickup distance.
2. **The cube is a pre-placed scene instance.** Its `Transform` is assigned directly in the Inspector.
3. **Targets use a fixed-radius orbit.** The cube replaces the mouse as the direction source, matching the Player mechanic.
4. **The isolated test worker remains stationary; the production worker navigates normally.**
5. **The new controller is generic.** The Worker Collector is the first consumer, but the code is designed for later robot enemies.

These decisions now cover both the isolated test and production integration.

## 13. Implemented Files

- `Assets/Scripts/Robots/ArmReach/RobotObjectArmReachController.cs`
- `Assets/Scripts/Robots/ArmReach/WorkerCollectorArmReachTestIsolation.cs`
- `Assets/Editor/UnitTests/RobotObjectArmReachControllerTests.cs`
- `Assets/Editor/UnitTests/WorkerCollectorGrabPipelineTests.cs`
- `Assets/Scenes/TestingSceneBot.unity`
- `Assets/Resources/Prefabs/Robots/Worker/Worker3.prefab`
- `Assets/Resources/Prefabs/Robots/WorkerCollector/WorkerCollector.prefab`
- `Assets/Scripts/Robots/WorkerCollector/WorkerCollectorBodyController.cs`

The test scene's existing `WorkerCollector` remains wired to its `CubeNormal` scene instance and retains its test-only isolation component. The production Worker Collector prefab does not contain that isolation component and now uses the same arm controller during Level 2 missions.

### Scene-view reach visualization

The reach controller draws editable gizmos in the Scene view:

- blue semicircle: left half of the shared target orbit;
- orange semicircle: right half of the shared target orbit;
- two translucent vertical lines: arm-switch dead zone;
- yellow line and marker: direction to the cube and the fixed-radius solver destination.

Enable the Scene view's **Gizmos** button to see them. `Maximum Reach` changes both arc radii, while `Side Switch Threshold` changes the width between the vertical switching lines. `Draw Gizmos Only When Selected` can hide the visualization unless the Worker Collector is selected.
