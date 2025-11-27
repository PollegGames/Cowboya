# SimplePuppetBinder - Master-to-Puppet Rotation Mirroring

`SimplePuppetBinder` (`Assets/Scripts/Robots/SimplePuppetBinder.cs`) keeps a puppet's pose aligned to an animated master by copying **rotations** for configured transform pairs. Translation is intentionally left alone so you can drive positioning from other movement scripts or hierarchy parenting.

## How the Binder Works
- **LateUpdate sampling:** Each frame calculates the master's rotation for every configured `BonePair` and converts it into the puppet's space using the configured `MasterRoot` and `PuppetRoot` (both default to the component's transform when empty). Missing `Rigidbody2D` or `Rigidbody` references on the puppet are cached for smooth physics updates.
- **FixedUpdate application:** Rotation targets on rigidbody bones are applied using `MoveRotation` (`Rigidbody2D` uses Z Euler angles, `Rigidbody` uses the quaternion). Bones without rigidbodies are rotated immediately during `LateUpdate`.

## Setting Up Pairs
1. Add `SimplePuppetBinder` to the master root (for the cowboy this lives on `Cowboy_Master`).
2. Assign `MasterRoot` and `PuppetRoot` if the defaults are not appropriate for your hierarchy.
3. Fill the `Pairs` list with matching master and puppet transforms in a consistent top-to-bottom order.
4. Optionally drag puppet rigidbodies into the pair slots for caching; otherwise, the binder auto-fills them on first update.

### Pair Guidelines
- Match transform names between master and puppet hierarchies to avoid mistakes while authoring the list.
- Start with hips, torso, and head, then add limbs as needed. Leaving fingers or accessories unmapped is acceptable if they follow correctly through hierarchy parenting.
- If the puppet uses joints, keep `MoveRotation` active so hinge limits remain respected while the binder guides the pose.

## Debugging Tips
- Toggle `Gizmos` in the Scene view to inspect transform alignment while the game runs.
- Enable physics interpolation on puppet rigidbodies if motion looks stuttery.
- If a bone fails to rotate, double-check that the pair references the correct master/puppet transforms and that any rigidbody is present when needed.

## Known Behaviours
- Translation is untouched: keep master and puppet roots co-located or parented appropriately so positions stay aligned.
- Physics interpolation remains intact because rigidbody rotations are applied through `MoveRotation` during `FixedUpdate`.
- There is no auto-population helper for `Pairs`; author the list manually to ensure correct mapping.
