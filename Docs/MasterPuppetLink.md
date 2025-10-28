# MasterPuppetLink - Master-to-Puppet Active Ragdoll Link

`MasterPuppetLink` (`Assets/Scripts/Robots/MasterPuppetLink.cs`) is the bridge between an animated **master** skeleton (animation clips, IK, or pose authoring) and a physics-driven **puppet** ragdoll. The component samples the desired pose from the master hierarchy and applies physically plausible forces and torques so the ragdoll chases the animation while still reacting to the world.

> 💡 Looking for a non-physical, direct pose copier? Use `SimplePuppetBinder` (`Assets/Scripts/Robots/SimplePuppetBinder.cs`). It simply matches transform positions/rotations per frame and can auto-populate pairs by name, making it ideal when you just want `head master → head puppet` style synchronization without strength or damping tuning.

## Roles in the Setup
- **Master hierarchy** - kinematic transforms that produce the desired pose. In the cowboy prefabs this is the animated `Cowboy_Master`.
- **Puppet hierarchy** - matching rigidbodies and colliders that make up the active ragdoll (`Cowboy_Puppet`). These must share transform names so they can be paired.
- **MasterPuppetLink host** - normally lives on the master root. It references both hierarchies and contains the tunable link data.
- **Optional sensors** - `ContactPoint` entries (typically feet) detect grounding to boost or relax strength during stance and airtime.
- **Optional root body** - a main rigidbody (e.g. hips/pelvis) that can be gently anchored to the master position to avoid drift.

## Execution Flow (FixedUpdate)
1. **Ensure body cache** - collects every puppet `Rigidbody2D` that will be driven.
2. **Update contacts** - reads the configured foot/hand sensors and remembers short stick timers to avoid ground flicker.
3. **Track impacts** - compares per-body velocity deltas to detect hard hits.
4. **Evaluate balance metrics** - computes smoothed center of mass (COM), support center, pelvis height, and torso tilt.
5. **State machine** - chooses `Normal`, `Unpinned`, or `Recovery` based on stability tests (tilt, COM over support, pelvis drop, impacts). The state feeds strength multipliers.
6. **Update target velocities** - measures master/root velocities so forces work in relative space.
7. **Apply bone forces/torques** - runs a damped spring with dead zones and feed-forward so each puppet bone matches the master without buzzing.
8. **Anchor root** - optionally applies a damped spring to keep the chosen root body near the master target.

Because everything runs under physics, external impulses, collisions, and joint constraints still influence the ragdoll. The link merely provides the corrective effort needed to return toward the animated pose.

## Controller Tuning
High-level stabilisers exposed on the component:

| Field | Purpose |
| --- | --- |
| `PositionDeadZone` / `RotationDeadZone` | Ignore tiny pose errors so sub-millimetre noise does not trigger corrections. |
| `PositionVelocityDeadZone` / `RotationVelocityDeadZone` | Skip damping when the puppet already moves with the target inside these tolerances. |
| `UseMasterVelocityFeedForward` / `UseMasterAngularVelocityFeedForward` | Subtract the master target velocity so forces/torques are based on relative motion (greatly reduces lag when the animation moves quickly). |

Per-link `PositionDamping` and `RotationDamping` values now act as multipliers of the automatically derived critical damping (`1.0` ≈ critically damped). Existing presets still work, but the controller scales damping automatically when you raise `GlobalStrength`, keeping stiffness and damping balanced.

## BoneLink Mechanics
Each entry in `Links` pairs a master `Transform` with a puppet `Rigidbody2D`. Core fields:

| Field | Purpose |
| --- | --- |
| `PositionStiffness` / `PositionDamping` | Spring strength and damping ratio for positional tracking. Usually enabled on torso/hips roots. |
| `RotationStiffness` / `RotationDamping` | Spring strength and damping ratio for angular tracking. Dominant control channel for limbs. |
| `Strength` | Per-link multiplier that scales the overall effort. |
| `EnablePosition` / `EnableRotation` | Toggle which axes to drive. Limbs often rotate-only to avoid fighting joint limits. |
| `UseLocalRotation` | When true, rotation targets are derived from the master's local Z angle so the puppet respects its parent's world rotation. |
| `Region` | Categorisation (Root, Torso, Hips, Legs, Arms, Head) used for automatic defaults and regional scaling. |

During force application the script computes, after applying strength multipliers:

```
relativeVelocity = puppetVelocity - masterVelocity
positionForce = positionError * positionStiffness - relativeVelocity * positionDamping

relativeAngularVelocity = puppetAngularVelocity - targetAngularVelocity
torque = angularError * rotationStiffness - relativeAngularVelocity * rotationDamping
```

Errors inside the configured dead zones are ignored, which stops micro adjustments when the puppet already matches the master. Forces are clamped to avoid spikes and then applied directly to the rigidbody. `ResolveDynamicMultiplier` still layers regional, state, and contact multipliers on top.

## Balance Metrics and State Machine
- **Center of Mass vs. Support** - COM is mass-weighted across puppet bodies (or an override transform). Support center averages grounded contacts. When the horizontal gap exceeds `ComDistanceThreshold`, the system considers the puppet unstable.
- **Pelvis height** - compares current pelvis COM to `DesiredPelvisHeight` ± tolerance.
- **Torso tilt** - angle between torso up-vector and world up; exceeding `MaxTorsoTilt` is unstable.
- **Impacts** - large per-frame velocity changes keep the system in `Unpinned` during knockbacks.

Smoothed metrics prevent noisy transitions. The state machine drives:

| State | Trigger | Effect |
| --- | --- | --- |
| **Normal** | default when stable | Uses `NormalStrengthScale` and `NormalRootScale`. |
| **Unpinned** | any instability (tilt, COM outside, pelvis low, heavy impact) | Drops strengths (`UnpinnedStrengthScale`, `UnpinnedRootScale`) so the ragdoll can fall naturally. |
| **Recovery** | stability maintained for `StableDuration` after being unpinned | Temporarily boosts strength (`RecoveryStrengthScale`, `RecoveryRootScale`) to help stand up. Returns to Normal after `RecoveryDuration`. |

## Root Anchoring
When `RootBody` and `AnchorRoot` are set, the script applies a damped spring so the puppet's root hovers near `RootTarget` (defaults to the pelvis or component transform). The same dead zones and feed-forward logic apply, so the anchor stops tugging once the puppet is aligned and it moves with the master when the animation drives the root.

## Contact System
`Contacts` entries pair a rigidbody or sensor collider with stance/air multipliers:
- `Sensor.IsTouchingLayers(GroundLayers)` determines grounding.
- A short `ContactStickTime` keeps the grounded flag true briefly after losing contact, preventing rapid toggles.
- Grounded contacts increment an internal count so COM checks know whether the character currently has a support base.

Typical setup: two foot colliders (set as `Sensor` triggers) with higher `StanceStrength` to stiffen legs while planted but reduced `SlipStrength` so limbs relax in air.

## Auto-Populating Links
- Optional `AutoPopulateOnStart` aligns master and puppet hierarchies by name. `MasterRoot` defaults to the component's transform; `PuppetRoot` searches for a sibling or similarly named root (e.g. `Cowboy_Master` -> `Cowboy_Puppet`).
- New links receive region guesses based on name tokens (`head`, `arm`, `leg`, etc.) and `ApplyRegionDefaults` assigns sensible stiffness/damping presets for that region.
- Use **Context Menu -> Auto Populate Links** in the inspector to rebuild mappings after editing the prefab hierarchies.

## Debugging Aids
- When the component is selected, gizmos draw master and puppet targets plus COM/support markers (toggle via `DebugSettings`).
- Public read-only properties (`CurrentCenterOfMass`, `CurrentSupportCenter`, `CurrentTorsoTilt`, `CurrentPelvisHeight`, `CurrentState`) let other systems query ragdoll health or drive UI.

## Practical Usage Tips
- Start by tuning `GlobalStrength` and `RootStrength` for the overall feel, then tweak per-region multipliers instead of individual bones.
- If the puppet jitters at rest, widen the dead zones slightly or lower `PositionVelocityDeadZone` so the controller settles faster.
- Keep `EnablePosition` on root/torso/hips only; letting limbs and head drive rotation-only avoids over-constraining the hinge joints against absolute targets.
- Either disable hinge limits or widen them so the PD controller never spends time banging into hard joint stops; the controller should guide the motion, while limits are only a safety net.
- Let the root anchor handle translations for the main body. In practice we disable `EnablePosition` on the root link and keep its `Strength` modest, otherwise the root spring and anchor fight each other and produce center-of-mass buzz.
- Give the torso/hips rigidbodies some linear and angular damping (≈0.7–1.0 linear, 1.5–1.8 angular) so residual micro velocities bleed out between frames.
- If the animation update rate does not match physics and you still see buzz, disable the feed-forward toggles so the controller only reacts to current errors instead of chasing noisy target velocities.
- Keep damping values near `20-35` for positions and `18-30` for rotations; those translate to damping ratios around `1.0` with default masses.
- When authoring new animations, ensure the master hierarchy keeps joint names identical to the puppet to preserve auto-linking.
- Remember that physics happens in `FixedUpdate`, so any external scripts nudging the master pose should do so before `FixedUpdate` to avoid one-frame lag.

With this setup the master animation or IK solver defines intent, while the puppet remains a responsive active ragdoll that can brace, fall, and recover under physical influences.
