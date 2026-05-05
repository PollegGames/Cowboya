# Cowboy Puppet Rotation Smoothing

This document tracks possible ways to make the Cowboy player's puppet rotation feel smoother while keeping `SimplePuppetBinder` rotation-only and manually paired.

## Current Cowboy Setup

`SimplePuppetBinder` is attached to `Cowboy_Player.prefab` and binds 13 manually selected puppet bones:

- `Head_Bone`
- `Body_Bone`
- `Hips_Bone`
- `RHand_Bone`
- `LHand_Bone`
- `LFoot_Bone`
- `RFoot_Bone`
- `LUpLeg_Bone`
- `RUpLeg_Bone`
- `BodyLow_Bone`
- `Torso_Bone`
- `RArm_Bone`
- `LArm_Bone`

Every bound Cowboy puppet bone has a `Rigidbody2D`, so the Cowboy always uses the physics path:

```csharp
pair.targetRotation = rotation;
rb2D.MoveRotation(pair.targetRotation.eulerAngles.z);
```

The Cowboy rigidbodies already have interpolation enabled, so basic render smoothing is already configured in the prefab.

## Current Rotation Logic

The binder computes a root-relative master rotation and reapplies it under the puppet root:

```csharp
Quaternion localMasterRotation = masterRootInverseRotation * pair.Master.rotation;
Quaternion rotation = puppetRootRotation * localMasterRotation;
```

For Cowboy, the target is then applied in `FixedUpdate`:

```csharp
rb2D.MoveRotation(pair.targetRotation.eulerAngles.z);
```

This is exact target following. The puppet does not blend toward the master rotation; it moves directly to the latest target angle on each physics tick.

## Problem Types

Before choosing a solution, identify which problem is visible in play mode.

Current Cowboy observation:

- The original exact binder has good walking stability.
- Walking and general movement are already functional.
- Arms are acceptable, but attack/grab input should feel more reactive.
- The main issue is not broken tracking; it is the rigid feel of the body parts.
- The central chain from head to hips feels too stiff.
- The upper body and lower body do not feel physically connected enough; they can feel like different systems.
- The desired direction is more physical liberty in selected body parts while keeping the stable walking base.

### Visual Stepping

The puppet reaches the correct angle, but the movement appears to update in small physics-tick jumps.

Likely causes:

- master animation is sampled in `LateUpdate`
- puppet rigidbodies are applied in `FixedUpdate`
- render frame rate and physics tick rate do not line up perfectly

### Harsh Pose Snapping

The puppet follows correctly, but the movement feels too immediate or robotic.

Likely cause:

- `MoveRotation` receives the final target angle directly every physics tick

## Candidate Solution A: Keep Exact Rotation

Do not change `SimplePuppetBinder`.

Use this if the current Cowboy feel is correct and the issue is only theoretical.

Pros:

- current behavior stays exact
- no animation lag
- no new tuning values
- lowest risk

Cons:

- no softness
- fast animation changes remain abrupt

Test result:

- Status: baseline
- Notes: original behavior is stable and still the comparison point for every experiment.

## Candidate Solution B: Rigidbody2D Interpolation Check

Confirm all Cowboy puppet rigidbodies have interpolation enabled.

Current investigation result: the 13 bound Cowboy puppet rigidbodies already use interpolation.

Pros:

- no binder logic change
- improves visual stepping when interpolation is missing

Cons:

- already configured for Cowboy
- does not soften target rotation itself

Test result:

- Status: already checked
- Notes: Cowboy bound bodies have interpolation enabled.

## Candidate Solution C: Max-Speed Rotation

Rotate toward the target by a maximum number of degrees per second.

Concept:

```csharp
float targetAngle = pair.targetRotation.eulerAngles.z;
float smoothedAngle = Mathf.MoveTowardsAngle(
    rb2D.rotation,
    targetAngle,
    rotationSpeed * Time.fixedDeltaTime
);

rb2D.MoveRotation(smoothedAngle);
```

Suggested initial values:

- `720` degrees/second for softer motion
- `1080` degrees/second for responsive motion
- `1440` degrees/second for near-exact motion with some smoothing

Pros:

- predictable
- easy to tune
- prevents instant large angle jumps
- good for responsive player characters

Cons:

- puppet can lag behind fast master animation
- one global speed may not fit every body part

Test result:

- Status: tested and removed
- Chosen value: `1080` degrees/second on `Cowboy_Player.prefab`
- Notes: felt only slightly better. Start walking and ending movement were a little smoother, but the output stayed close to the original. Attack felt less fast. The rigid head-to-hips feel remained, so this does not solve the main problem. Candidate C was removed before testing another solution.

## Candidate Solution D: Exponential Sharpness Smoothing

Blend from current angle to target angle using a frame-rate independent sharpness value.

Concept:

```csharp
float targetAngle = pair.targetRotation.eulerAngles.z;
float t = 1f - Mathf.Exp(-rotationSharpness * Time.fixedDeltaTime);
float smoothedAngle = Mathf.LerpAngle(rb2D.rotation, targetAngle, t);

rb2D.MoveRotation(smoothedAngle);
```

Suggested initial values:

- `6` to `10` for soft motion
- `12` to `18` for responsive motion
- `20+` for near-exact motion

Pros:

- smooth and natural feeling
- responsive when target is far away
- settles gently near the target
- good first code experiment for Cowboy

Cons:

- less predictable than max-speed smoothing
- can feel floaty if the value is too low
- always introduces some delay

Test result:

- Status: re-enabled for second play-mode test
- Chosen value: `10` on `Cowboy_Player.prefab`
- Notes: first pass tested around `14`, then adjusted around `8` to `10`. It felt okay and slightly smoother, but did not significantly change the main feeling. Values around `8` to `10` felt better for bodies with Z rotation frozen, so the second pass uses `10`. The biggest factor is still which Cowboy bodies have Z rotation frozen; changing those constraints affects the feel more than this smoothing mode.

## Candidate Solution E: Per-Bone Smoothing

Give each `BonePair` its own smoothing value, or use a global value with optional per-pair overrides.

Possible setup:

```csharp
public float RotationSharpnessOverride;
```

Expected tuning direction:

- torso, hips, head: faster
- arms and legs: medium
- hands and feet: tuned based on gameplay feel

Pros:

- highest control
- different body parts can feel different
- useful if one global value cannot fit the whole Cowboy body

Cons:

- heavier Inspector setup
- more tuning work
- easier to create inconsistent motion

Test result:

- Status: not tested
- Notes:

## Candidate Solution F: Angular Velocity Drive

Drive `Rigidbody2D.angularVelocity` from the angle error instead of using direct `MoveRotation`.

Concept:

```csharp
float error = Mathf.DeltaAngle(rb2D.rotation, targetAngle);
rb2D.angularVelocity = error * strength;
```

A real implementation would also need damping and limits.

Pros:

- more physical behavior
- can interact more naturally with joints and collisions

Cons:

- more like an active ragdoll controller
- needs careful tuning
- can overshoot or wobble
- higher risk for the current simple binder design

Test result:

- Status: tested and removed
- Notes: not viable for the current Cowboy setup. No values gave a useful result because angular velocity depends on physics rotation being allowed, while many important Cowboy bodies have Z rotation frozen. The central chain from head to hips is mostly constrained, so this path either does nothing on those bodies or requires constraint changes that would alter the stable walking setup too much. Candidate F was the worst fit and was removed.

## Recommended Test Order

1. Keep current exact behavior as the baseline.
2. Test exponential sharpness smoothing with `RotationSharpness = 12`.
3. If it feels floaty, test `RotationSharpness = 18`.
4. If the motion needs stricter control, test max-speed rotation with `RotationSpeed = 1080`.
5. If one global value cannot fit the whole body, consider per-bone smoothing.
6. Avoid angular velocity drive unless the goal changes toward active ragdoll behavior.

## Evaluation Checklist

Use the same Cowboy scene and same movement inputs for each test.

- Walking left/right keeps the upper body readable.
- Jumping does not make limbs snap harshly.
- Crouching does not create delayed body alignment.
- Aiming/arms still feel responsive.
- Faint and recovery still work because `PlayerMovementController` disables/restores the binder.
- No obvious extra lag compared with the master animation.
- No new jitter from physics constraints or joints.

## Decision Log

### Baseline

- Date:
- Build/scene:
- Observation:

### Attempt 1

- Candidate:
- Values:
- Result:
- Keep, adjust, or reject:

### Attempt 2

- Candidate:
- Values:
- Result:
- Keep, adjust, or reject:
