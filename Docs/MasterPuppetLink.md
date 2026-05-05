# SimplePuppetBinder - Master-to-Puppet Rotation Mirroring

`SimplePuppetBinder` (`Assets/Scripts/Misc/Math/Misc/SimplePuppetBinder.cs`) keeps selected puppet bones rotated like matching bones from an animated master rig. It is intentionally simple: it mirrors rotations only, and every pair is authored manually in the Inspector.

> Note: `MasterPuppetLink` has been removed from the project. If a force/torque-based active ragdoll is needed later, that behavior needs a separate implementation.

## How the Binder Works
- **Manual pairs**: `Pairs` contains only the master and puppet bones that should be controlled. Unlisted puppet bodies are left alone.
- **Root-relative rotation**: Each `LateUpdate`, the binder reads the master bone's world rotation relative to `MasterRoot`, then applies the equivalent rotation under `PuppetRoot`.
- **Transform puppet bones**: If the puppet transform has no `Rigidbody2D` or `Rigidbody`, the binder writes `Transform.rotation` directly in `LateUpdate`.
- **Physics puppet bones**: If the puppet transform has a rigidbody, the binder caches the target rotation in `LateUpdate` and applies it in `FixedUpdate` using `MoveRotation`.
- **No position binding**: The binder does not copy positions, call `MovePosition`, or try to keep puppet bodies glued to master positions. Position control belongs to the puppet hierarchy, locomotion scripts, joints, or other gameplay systems.

## Setting Up Pairs
1. Add `SimplePuppetBinder` to the robot object that owns the master and puppet references.
2. Assign `MasterRoot` and `PuppetRoot`. If `MasterRoot` is left empty, it defaults to the component transform. If `PuppetRoot` is left empty, rotation mapping falls back to the component transform.
3. Add only the bones you want controlled to `Pairs`.
4. Optionally drag `PuppetBody2D` or `PuppetBody3D` references for faster startup. If left empty, the binder caches the rigidbody from the puppet transform the first time that pair updates.

## Pair Guidelines
- Keep the list selective. It is valid to bind only important bodies and leave accessories, loose physics pieces, or untouched limbs out.
- Match each `Master` transform with the equivalent `Puppet` transform.
- Keep root-to-leaf ordering when practical so debugging the Inspector list is easier.
- If a puppet body uses joints, the binder still uses `MoveRotation`; it guides orientation without replacing the rest of the physics setup.

## Debugging Tips
- Toggle `Gizmos` in the Scene view to inspect master and puppet orientation while the game runs.
- Enable interpolation on puppet rigidbodies if rotation looks stuttery.
- If bodies drift in position, check the locomotion, hierarchy, joints, or other physics scripts. `SimplePuppetBinder` is not responsible for positional correction.

## Known Limitations
- Pair authoring is manual by design. The list is manually authored so only chosen bodies are controlled.
- The binder does not apply forces, copy positions, or counter external impulses.
- For full ragdoll recovery or physical pose matching, use another system alongside this binder or implement a dedicated active-ragdoll controller.
