# Puppet Binding Notes

`SimplePuppetBinder` is a rotation-only binder. It exists to make selected puppet bones follow the orientation of matching animated master bones while leaving position control to the rest of the robot setup.

## Current Design
- **Only selected bones are bound.** The `Pairs` list is manual. This is intentional so a prefab can bind only the puppet bodies that should be driven by the animated master.
- **Rotations are mirrored through roots.** The binder converts each master bone's rotation from `MasterRoot` space into `PuppetRoot` space, then applies that rotation to the puppet bone.
- **Rigidbody bones are handled in physics time.** When a puppet bone has `Rigidbody2D` or `Rigidbody`, the binder stores the target rotation during `LateUpdate` and applies it with `MoveRotation` during `FixedUpdate`.
- **Non-rigidbody bones are written directly.** Puppet bones without a rigidbody receive `Transform.rotation` in `LateUpdate`.
- **Positions are not mirrored.** The binder does not cache target positions and does not call `MovePosition`.

## Why Position Is Not Bound
Position syncing is deliberately outside this class. The puppet's position can be driven by hierarchy movement, locomotion, joints, collisions, or other gameplay scripts. Copying positions here would make the binder take control of more of the puppet than intended and could fight the existing physics setup.

If a puppet body needs positional correction, add that behavior in a separate controller or adjust the prefab's hierarchy/physics configuration. `SimplePuppetBinder` should remain focused on rotation mirroring for manually selected bodies.

## Practical Expectations
- A bound puppet bone should rotate like its matching master bone.
- An unbound puppet bone should not be touched by this script.
- A bound rigidbody can still move according to physics or other scripts; this binder only guides its rotation.
- If visible drift is a problem, investigate the systems responsible for body positions rather than this binder's rotation path.
