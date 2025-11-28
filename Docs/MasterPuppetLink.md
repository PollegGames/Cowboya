# SimplePuppetBinder - Master-to-Puppet Transform Mirroring

`SimplePuppetBinder` (`Assets/Scripts/Robots/SimplePuppetBinder.cs`) keeps a physics puppet aligned to an animated master by copying transforms. It is now the active solution in the `Cowboy` prefab (`Assets/Resources/Prefabs/Robots/Cowboy.prefab`), fully replacing the old `MasterPuppetLink` workflow.

> Note: `MasterPuppetLink` has been removed from the project. If you need a force/torque-based active ragdoll in the future, you will need to restore or reimplement that behaviour.

## How the Binder Works
- **LateUpdate sampling**: Each frame the binder gathers the world position and rotation from every configured master bone. It also caches any `Rigidbody2D` or `Rigidbody` components attached to the puppet transforms so physics moves remain smooth.
- **FixedUpdate application**: Cached targets are replayed during physics ticks. When a rigidbody exists the binder calls `MovePosition` and `MoveRotation` so interpolation stays valid. If no rigidbody is present the puppet transform is reassigned directly.
- **Rotation first**: With the current configuration rotation mirroring is production ready. Position matching still has a pending smoothing pass, so expect small translation offsets on live characters until that work lands.

## Setting Up Pairs
1. Add `SimplePuppetBinder` to the master root (for the cowboy this lives on `Cowboy_Master`).
2. Assign `MasterRoot` and `PuppetRoot`. If left empty they default to the component transform, which is fine when the master and puppet hierarchies are direct children.
3. Populate the `Pairs` list with matching master and puppet transforms. Keep the order consistent from root to leaf so debugging is easier.
4. Optionally drag any rigidbodies for faster caching; otherwise the binder obtains them automatically the first time each pair updates.

### Pair Guidelines
- Match transform names between master and puppet hierarchies to avoid mistakes while authoring the list.
- Start with hips, torso, and head, then add limbs as needed. Leaving fingers or accessories unmapped is acceptable if they follow correctly through hierarchy parenting.
- If the puppet uses joints, keep `MoveRotation` active so hinge limits remain respected while the binder guides the pose.

## Debugging Tips
- Toggle `Gizmos` in the Scene view to inspect transform alignment while the game runs.
- Enable physics interpolation on puppet rigidbodies if motion looks stuttery.
- When testing positional changes, capture both `LateUpdate` and `FixedUpdate` values in the profiler to confirm targets are being updated as expected.

## Known Limitations
- Position targets currently lack easing when the master teleports. Until smoothing is implemented, avoid snapping the master hierarchy in a single frame.
- No automatic pair population is available yet. Authoring tools or editor scripts will be needed for large rigs.
- The binder does not apply forces, so external physics impulses are not countered. For ragdoll recovery, additional scripts or a future `MasterPuppetLink` replacement may be required.

## Next Steps
- Finish the positional smoothing investigation so puppet translations stay perfectly aligned.
- Evaluate whether lightweight auto-populate utilities would speed up authoring for new characters.
- Decide if a new physics-driven controller is needed once gameplay demands full ragdoll behaviour again.
