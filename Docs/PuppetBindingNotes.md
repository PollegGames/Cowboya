# Puppet binding drift analysis

The map scene loads two workers through the enemy spawner. One puppet stayed glued to its worker, while the other (with a Rigidbody) drifted—the puppet sprites walked away even though the master's sprites remained aligned. This document explains why, what changed in `SimplePuppetBinder`, and how the fix was derived.

## What went wrong
- **Transform-only puppets were fine.** For bones without physics components, the binder wrote `Transform.position` and `Transform.rotation` directly in `LateUpdate`, so their sprites matched their masters each frame.
- **Physics-driven puppets were missing a position copy.** Rigidbodies ignore `Transform` writes outside the physics loop. The binder deferred rotations to `FixedUpdate` for bodies, but it never stored a matching position target, leaving Unity's physics to integrate the rigidbodies independently. That independent motion created the visible drift between the puppet sprites and the worker master.

## What the solution changes
- **Capture both rotation and position for physics bones.** Each `BonePair` now caches the master's pose every frame and marks targets for `FixedUpdate` when a Rigidbody is present.
- **Apply targets with physics-safe APIs.** `FixedUpdate` now sends cached poses to rigidbodies using `MovePosition`/`MoveRotation`, keeping physics-driven puppets exactly in sync with their masters just like the transform-only path.
- **Consistent root mirroring.** Poses are mirrored relative to the configured master and puppet roots before caching, so offset rigs still match regardless of where the roots live in the hierarchy.

## How the fix was derived
1. **Reproduced the drift:** Observed that only the worker whose puppet had a Rigidbody wandered while the transform-only worker stayed aligned.
2. **Traced update paths:** Reviewed `SimplePuppetBinder` and saw that rotations for rigidbodies were deferred to `FixedUpdate` but positions were only applied for transform-only rigs.
3. **Checked Unity physics rules:** Confirmed that Rigidbody transforms must be driven through physics methods inside `FixedUpdate`; otherwise, the physics simulation overwrites manual transform changes.
4. **Symmetric data flow:** Added cached position targets alongside rotation targets so the physics path receives the same data as the transform path, ensuring both puppet types consume identical pose information.
5. **Validated the outcome:** With both position and rotation pushed through `FixedUpdate`, the physics-driven puppet tracks its worker like the transform-only rig, eliminating the one-sided drift.

## Why the Cowboy player did not visibly drift
The Cowboy prefab also uses `SimplePuppetBinder`, but its hierarchy meant the missing Rigidbody position copy was effectively masked:

- The master (`Cowboy_Master`) lives under the puppet root (`Cowboy_Puppet`), so any locomotion applied to the puppet root already drags the master along. 【F:Assets/Resources/Prefabs/Robots/Player/Cowboy_Player.prefab†L1200-L1225】【F:Assets/Resources/Prefabs/Robots/Player/Cowboy_Player.prefab†L3065-L3084】
- Gameplay scripts steer the Cowboy by driving those puppet rigidbodies directly, so even though the binder was only copying rotations for physics bones, the roots stayed superimposed and no gap appeared. 【F:Assets/Resources/Prefabs/Robots/Player/Cowboy_Player.prefab†L180-L259】【F:Docs/MasterPuppetLink.md†L1-L24】
- The worker prefab lacked that shared root motion; its master moved independently, exposing the missing Rigidbody position sync that the binder change now fixes for all rigs.
