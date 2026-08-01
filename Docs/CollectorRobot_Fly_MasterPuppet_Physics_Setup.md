# CollectorRobot_Fly Master/Puppet Physics Setup

## Purpose

This document is the implementation guide for converting the existing flying Collector artwork into the same master/puppet structure used by the other robots in this project.

The work covered here ends at a stable, falling, physics-enabled prefab. It prepares the robot for later flight, magnet targeting, and propeller animation without implementing any of those behaviours now.

## Starting Source State

- Final prefab path: `Assets/Resources/Prefabs/Robots/Collector/CollectorRobot_Fly.prefab`
- PSD source path: `Assets/Resources/Prefabs/Robots/Collector/Others/CollectorRobot_Fly.psd`
- Before this conversion, the final prefab was only an instance of the PSD source.
- Its starting scale was `0.4`, which is preserved as the final gameplay size.
- Before conversion it contained no added `Rigidbody2D`, collider, joint, master copy, puppet copy, or `SimplePuppetBinder`.
- The imported rig has two relevant bones:
  - `bone_Body`
  - `bone_Magnet`
- The propeller object is named `Helice` and has no bone.

Do not edit the PSD bone weights or regenerate its rig as part of this task. The existing body and magnet weighting is the source of truth.

## Approved Decisions

- The robot is an enemy.
- Use the `Enemy` tag and `Enemy` layer (`7`) on the final root and physics-bearing objects.
- The final gameplay scale remains `0.4`.
- The robot must use dynamic 2D physics and fall under gravity in Play Mode.
- The main body mass is `1.5`.
- The magnet mass is `0.35`.
- The magnet is a separate dynamic body connected to the main body with a `HingeJoint2D`.
- The magnet has a total rotation arc of 180 degrees: `-90` to `+90` degrees around its downward rest pose.
- The propeller remains unboned and non-physical. It will rotate independently later.
- No Animator, flight controller, locomotion, target tracking, magnet control, collection logic, or propeller rotation is added in this phase.

## System Boundary

The project does not use the removed `MasterPuppetLink` component. The relevant existing system is:

`Assets/Scripts/Misc/Math/Misc/SimplePuppetBinder.cs`

`SimplePuppetBinder` mirrors selected master-bone rotations to matching puppet rigidbodies. It does not copy positions. Consequently:

- the physical main body is responsible for the robot's position;
- the hinge is responsible for keeping the physical magnet attached;
- the master rig must travel with the physical body through hierarchy placement;
- the unboned propeller must be parented to a body-following Transform;
- no movement script should be introduced to compensate for an incorrect hierarchy.

## Target Asset Layout

Create these working prefabs from the PSD instance:

- `Assets/Resources/Prefabs/Robots/Collector/Others/CollectorRobot_Fly_Master.prefab`
- `Assets/Resources/Prefabs/Robots/Collector/Others/CollectorRobot_Fly_Puppet.prefab`

Keep the final assembled prefab at its existing path so its `.meta` GUID is preserved:

- `Assets/Resources/Prefabs/Robots/Collector/CollectorRobot_Fly.prefab`

Do not delete or replace `CollectorRobot_Fly.prefab.meta`.

## Target Hierarchy

The final hierarchy should follow this logical structure. Imported `Sprites` containers may add visual children not shown here.

```text
CollectorRobot_Fly                       [final container, SimplePuppetBinder]
└── CollectorRobot_Fly_Puppet            [puppet root]
    ├── bone_Body                         [physical root: Rigidbody2D + body collider]
    │   ├── CollectorRobot_Fly_Master     [master root; no physics]
    │   │   ├── bone_Body                 [master body bone]
    │   │   └── bone_Magnet               [master magnet bone]
    │   └── PropellerPivot                [future visual rotation pivot]
    │       └── Helice                    [visible propeller; no physics]
    ├── bone_Magnet                       [Rigidbody2D + collider + HingeJoint2D]
    └── Sprites                           [remaining puppet render objects]
```

Important hierarchy rules:

- `CollectorRobot_Fly` is an identity/container root, not a third physics body.
- `bone_Body` is the authoritative physical root for future movement systems.
- Do not add a `Rigidbody2D` to `CollectorRobot_Fly` or `CollectorRobot_Fly_Puppet`.
- Place the master root under the puppet `bone_Body`. This makes the master rig follow body position because `SimplePuppetBinder` does not bind positions.
- Preserve the imported relationship of the two puppet bones. Their physical attachment comes from the hinge; do not invent an extra physics root between them.
- Only one visual copy should render in gameplay. Disable the master copy's renderers or its visual `Sprites` container while keeping the master bones active.

## Transform Rules

### Final container

- Name: `CollectorRobot_Fly`
- Position: `(0, 0, 0)`
- Rotation: `(0, 0, 0)`
- Scale: `(0.4, 0.4, 0.4)`
- Tag: `Enemy`
- Layer: `Enemy` (`7`)

The current prefab instance contains a scene-authored non-zero position. Do not preserve that position in the finished asset.

### Master and puppet roots

- Keep both working-prefab roots at local position zero, identity rotation, and scale one before final assembly.
- In the final prefab, keep the puppet root at local position zero, identity rotation, and scale one.
- First overlay the master and puppet rigs in world space. Then parent the master root under the puppet `bone_Body` while preserving its world transform.
- The nested master root will therefore have a non-zero body-relative local position and rotation. This is correct; do not zero it after parenting.
- Do not apply the `0.4` scale again below the final container.
- Preserve imported bone rotations. Do not zero individual bones simply to make their Inspector values look cleaner.
- Before adding physics, verify the master and puppet body/magnet bones overlay in their authored rest pose.

## Safe Construction Sequence

### 1. Preserve and duplicate the source setup

1. Leave `CollectorRobot_Fly.psd` and its `.meta` file untouched.
2. Duplicate the current PSD-based prefab into the Master and Puppet working prefabs listed above.
3. Reset both working prefab roots to local position zero, local rotation zero, and scale one.
4. Work on duplicates of the PSD instance. If hierarchy editing is blocked by the nested PSD prefab, use **Unpack Completely** on the working copy only.
5. Do not unpack or modify the PSD asset itself.

### 2. Prepare the master copy

1. Rename its root `CollectorRobot_Fly_Master`.
2. Keep `bone_Body` and `bone_Magnet` and their authored rest transforms.
3. Remove any `Rigidbody2D`, collider, or joint if one was copied accidentally.
4. Keep master bones active.
5. Disable the master visual renderers or the master's visual `Sprites` container to prevent double rendering.
6. Do not add an `Animator`, animation clip, IK solver, movement component, or control script.

The master is only a rotation-source rig at this stage.

### 3. Prepare the puppet copy

1. Rename its root `CollectorRobot_Fly_Puppet`.
2. Keep the visible, weighted body and magnet artwork connected to their existing puppet bones.
3. Keep the propeller visual, but remove it from any static visual container that does not follow `bone_Body`.
4. Create `PropellerPivot` under the puppet `bone_Body`.
5. Position `PropellerPivot` exactly at the propeller axle/centre.
6. Reparent `Helice` beneath `PropellerPivot` while preserving its world position.
7. Confirm that rotating `PropellerPivot` in the Scene view rotates only the propeller around its centre.
8. Restore `PropellerPivot` rotation to zero before saving.

Do not add a propeller bone. Do not put a rigidbody, collider, hinge, or animation component on `PropellerPivot` or `Helice`.

### 4. Assemble the final prefab

1. Create a clean `CollectorRobot_Fly` container at the origin.
2. Apply the final scale `0.4` only to this container.
3. Place the puppet root directly under the container.
4. Overlay the master and puppet rigs, then place the master root under the puppet `bone_Body` while preserving the master's world transform. Do not zero the resulting master local transform.
5. Save/replace the content of the existing final prefab without deleting its `.meta` file.
6. Apply the `Enemy` layer throughout the physical puppet hierarchy.
7. Apply the `Enemy` tag to the final root, puppet root, `bone_Body`, and `bone_Magnet`. Visual-only children may remain `Untagged`.

## Rigidbody2D Configuration

These are deliberately light starting values for the flying Collector. The existing walking robots commonly use a gravity scale of `3`; this prefab starts at `1` so it still falls but is easier to support with a later flight controller.

| Setting | `bone_Body` | `bone_Magnet` |
| --- | ---: | ---: |
| Body Type | Dynamic | Dynamic |
| Simulated | On | On |
| Use Auto Mass | Off | Off |
| Mass | `1.5` | `0.35` |
| Linear Damping | `1` | `1` |
| Angular Damping | `2` | `2` |
| Gravity Scale | `1` | `1` |
| Interpolate | Interpolate | Interpolate |
| Collision Detection | Continuous | Discrete |
| Sleeping Mode | Start Awake | Start Awake |
| Constraints | None | None |
| Material | None initially | None initially |

Do not freeze Z rotation on the magnet. Both the hinge and binder require it to rotate.

Do not use different gravity scales on the two connected bodies. A mismatch makes the light magnet pull against the joint continuously.

## Collider Configuration

### Body collider

- Add a non-trigger `BoxCollider2D` to the puppet `bone_Body`.
- Fit it to the solid chassis/body silhouette.
- Exclude the propeller and the hanging magnet.
- Avoid extending the collider into large transparent corners of the sprite.
- Start with one simple collider. Add another simple collider only if the cockpit or chassis shape cannot be represented safely by one box.

### Magnet collider

- Add a non-trigger `BoxCollider2D` to the puppet `bone_Magnet`.
- Fit it to the magnet's solid visible envelope.
- Keep it clear of the body collider in the authored rest pose.
- Do not add a collection trigger in this phase. A later retrieval sensor should be a separate trigger with a clear gameplay owner.

### Propeller

- No collider.
- No rigidbody.
- It must never affect the body's mass or collision outline.

Collider dimensions must be fitted visually in Prefab Mode after the final `0.4` root scale is in place. Do not copy numeric collider sizes from Worker or SecurityGuard because their art and bone axes differ.

## HingeJoint2D Configuration

Add the `HingeJoint2D` to the puppet `bone_Magnet`, not to the body or container.

| Setting | Value |
| --- | --- |
| Connected Body | puppet `bone_Body` Rigidbody2D |
| Enable Collision | Off |
| Auto Configure Connected Anchor | On during initial placement |
| Anchor | Magnet's top connection/pivot point |
| Connected Anchor | Same world point on the body |
| Use Motor | Off |
| Use Limits | On |
| Lower Angle | `-90` |
| Upper Angle | `90` |
| Break Force | Infinity |
| Break Torque | Infinity |

Set and verify the authored downward magnet pose before configuring limits. That pose is joint angle zero. The result is a total 180-degree allowed arc, not `-180` to `+180`.

After auto-configuration, inspect both anchor gizmos at high zoom. They must occupy the same world point. If later hierarchy edits move them apart, repair the anchors before tuning damping or mass.

## SimplePuppetBinder Configuration

Add one `SimplePuppetBinder` to the final `CollectorRobot_Fly` container.

| Field | Assignment |
| --- | --- |
| Master Root | `CollectorRobot_Fly_Master` |
| Puppet Root | `CollectorRobot_Fly_Puppet` |
| Rotation Sharpness | `0` |

Create exactly two pairs in root-to-leaf order:

1. Master `bone_Body` -> Puppet `bone_Body` -> Puppet Body2D `bone_Body`
2. Master `bone_Magnet` -> Puppet `bone_Magnet` -> Puppet Body2D `bone_Magnet`

Assign the optional `PuppetBody2D` fields explicitly instead of relying on runtime discovery. This makes the prefab wiring visible and auditable in the Inspector.

The binder is allowed because it is the existing master/puppet infrastructure, not movement behaviour. With no Animator, both master bones remain in their authored rest rotations and the physical puppet follows that neutral pose while falling.

## Propeller Preparation for the Later Phase

The correct preparation is a clean pivot hierarchy, not another bone or physics body:

```text
bone_Body
└── PropellerPivot
    └── Helice
```

Later, an animation or visual component can rotate only `PropellerPivot.localRotation` around Z. That later system must not rotate `bone_Body`, must not drive a Rigidbody2D, and must not be added during this task.

## Play Mode Verification

Use a temporary test scene or an unsaved scene instance. Do not add test-only objects or components to the prefab.

### Structure check before Play Mode

- Final root position and rotation are zero.
- Final root scale is exactly `0.4`.
- Final root and both physics objects use `Enemy` tag/layer as specified.
- There are exactly two `Rigidbody2D` components in the prefab.
- There is exactly one `HingeJoint2D`, on puppet `bone_Magnet`.
- The hinge is connected to puppet `bone_Body`.
- There is exactly one `SimplePuppetBinder` with two valid pairs.
- There is no `Animator` and no new movement, flight, collection, targeting, or propeller script.
- Only the puppet artwork renders.
- `Helice` is under `PropellerPivot`, and `PropellerPivot` is under puppet `bone_Body`.

### Falling test

1. Place the prefab above a Ground-layer floor.
2. Enter Play Mode without adding forces or movement components.
3. Confirm the complete robot falls under gravity.
4. Confirm body, master rig, visible artwork, and propeller remain together.
5. Confirm the magnet remains connected at the hinge anchor.
6. Confirm the body and magnet do not collide with each other or explode apart.
7. Confirm the body collides with the floor and does not pass through it.

### Hinge-limit test

1. Temporarily disable `SimplePuppetBinder` on the scene instance only.
2. Apply a small test torque or use a controlled collision to swing the magnet.
3. Confirm it stops near `-90` and `+90` degrees relative to the downward rest pose.
4. Confirm there is no full rotation and no anchor separation.
5. Re-enable the binder and confirm the magnet returns to the master's neutral direction without jitter.
6. Exit Play Mode without applying temporary scene changes to the prefab.

### Propeller-pivot editor test

1. Outside Play Mode, temporarily rotate `PropellerPivot` by 90 degrees.
2. Confirm only `Helice` rotates.
3. Confirm it rotates around the visible axle rather than orbiting the robot.
4. Restore the pivot rotation to zero before saving.

## Failure Checks

If the body falls but the artwork, master, or propeller remains behind, repair the hierarchy. Do not add a position-following script.

If the magnet detaches, inspect the connected body and both hinge anchors before changing mass or gravity.

If the magnet cannot rotate, check Rigidbody2D constraints, hinge limits, and the binder assignment. Do not enable a hinge motor.

If two copies of the robot are visible, disable the master renderers. Do not delete the master bones.

If the propeller orbits around the chassis when rotated, move `PropellerPivot` to the axle while preserving the propeller's world position.

If the final robot is the wrong size, ensure the `0.4` scale exists only on the final container and all nested roots remain at scale one.

## Definition of Done for This Phase

This phase is complete only when:

- the final prefab keeps its existing asset path and GUID;
- the hierarchy contains a non-physical container, physical puppet, and non-physical master;
- `bone_Body` and `bone_Magnet` are correctly paired through `SimplePuppetBinder`;
- the two approved masses are present;
- the magnet is attached by a stable, limited hinge with a total 180-degree arc;
- the propeller follows the falling body and has a centred independent pivot;
- the complete robot falls and collides in Play Mode;
- the prefab is tagged/layered as an enemy;
- no animation or movement behaviour has been added.

## Explicitly Deferred Work

- Animator and animation clips
- Flight or hover forces
- Navigation and locomotion
- Body tilt during flight
- Magnet target selection
- Master magnet aiming
- Collection sensors and retrieval behaviour
- Propeller rotation animation or code
- Audio, VFX, damage, health, energy, and AI integration

These systems should be designed only after this physics baseline has been built and verified.

## Decision Record

- Date: 2026-08-01
- Physics state without movement: dynamic and falling
- Enemy classification: approved
- Final scale: `0.4`
- Body mass: `1.5`
- Magnet mass: `0.35`
- Magnet joint: `HingeJoint2D`
- Magnet range: total 180 degrees (`-90` to `+90`)
- Propeller: independent non-physical Transform, implementation deferred
- Animation and movement: explicitly out of scope
