# Cowboy Two-Arm Grab And Speed Attack Design

## Goal

Player arm input should be symmetrical and physical:

- With no mouse button held, the active arm follows the mouse target in movement mode.
- Movement mode chooses the right arm when the mouse target is on the right side of the body and the left arm when the target is on the left side.
- Automatic movement does not switch arms immediately. Once an arm is active, the mouse target must stay on the opposite side for `2` seconds before movement control switches to the other arm. This keeps fast side-to-side motion on the same arm.
- Holding left mouse button immediately selects the left arm, drives it toward the mouse target, and puts it in grab mode.
- Holding right mouse button immediately selects the right arm, drives it toward the mouse target, and puts it in grab mode.
- While one mouse button is held, the selected arm moves and the other arm returns to its origin/rest pose.
- Any arm that is not currently driven returns to its origin/rest pose, including the previously active automatic movement arm after control switches sides.
- Grab mode disables speed-based attack for that arm.
- Each grab-mode arm keeps trying to grab while its button is held, not only on the first button-down frame.
- Each empty arm can temporarily become an attack when its hand moves fast enough in movement mode.
- When speed drops below the attack threshold, the arm returns to movement mode after a short grace period, roughly one or two frames.
- The player should be able to hold one object per arm at the same time.
- Once an object is grabbed, it stays held until the matching ungrab key is pressed or a forced release happens.
- Both arms can grab the same physical object at the same time.

## Current Runtime Shape

`ArcTargetFollower` moves the shared `Target` transform around the player from the mouse position.

`ArmTargetController` reads mouse buttons directly, chooses one active movement arm, moves that solver target toward `Target`, and calls grab or attack systems.

`CowboyGrabController` stores per-arm held object state:

```csharp
private ArmGrabState leftGrab;
private ArmGrabState rightGrab;
```

`SimpleAttackController` currently activates one hand hitbox at a time through `SetArmAttackActive`.

`IGrabbable` already has the right basic lifecycle for two hands:

```csharp
bool CanBeGrabbed(Inventory inventory);
void OnGrab(Transform grabParent);
void OnRelease(Vector2 throwForce);
void OnAttract(Vector2 attractPoint);
```

The missing part is per-arm ownership in the controller, not a new grabbable contract.

## Grabbing Logic Effects

Supporting both arms means `CowboyGrabController` should store per-arm grab state:

```csharp
private IGrabbable leftHeldObject;
private IGrabbable rightHeldObject;
```

or a small per-arm state object:

```csharp
private ArmGrabState leftGrab;
private ArmGrabState rightGrab;
```

The controller needs to answer these questions per arm:

- Does this arm currently hold an object?
- Which object does this arm hold?
- Which anchor should this arm detect from?
- Which parent should this arm attach to?
- Which object should this arm release?
- Is this arm inside its regrab lockout window after a manual release?

Important rules:

- Automatic movement and click-held grab mode are separate.
- In movement mode, only the active arm follows the mouse target and speed-based attack is allowed.
- In grab mode, the selected clicked arm follows the mouse target, continuously tries to grab, and cannot attack.
- Clicking left or right overrides automatic side selection immediately.
- Automatic side switching has a `2` second delay, but click selection does not.
- Both hands may hold the same object at the same time.
- Releasing left should not release the right-hand object.
- Releasing right should not release the left-hand object.
- `ReleaseAllImmediate` should release both arms.
- Inventory-only pickups such as batteries and security badges can stay special-cased because they are consumed or attached to the player/robot instead of becoming held physical objects.
- Grab/hold energy should be charged by active hold time, not by successful grab.
- Holding an arm active should consume a small amount of energy every configured interval, whether or not that arm successfully grabs something.
- Hold energy cost should be `0.1` energy per active arm every `1` second. Holding both arms costs `0.2` energy per second total.
- Attack energy should be charged when speed-based attack activates.
- Attack energy should be spent once when an arm enters attack mode. It should not spend again until the arm leaves attack mode and becomes eligible to attack again.
- Releasing the mouse button should stop driving that arm but should not drop the held object.
- Pressing the matching ungrab key should release/throw the held object using the current throw-force behavior.
- After a manual release, the released arm should not immediately reacquire an object during a `2` second regrab lockout window.
- If the player faints, all physically held objects are released. Consumed/attached inventory pickups such as badges and batteries are not affected by this forced release.

## Speed-Based Attack

Attack should become a derived state, not a separate left-click action.

Attack only happens in movement mode. Holding a mouse button puts the selected arm in grab mode, so speed-based attack is disabled until the click is released.

For phase 1, attack only happens when the arm is not holding an object. If the arm holds a cube or another object, speed-based attack is ignored until the later held-object-hitbox phase.

For each empty arm, `ArmTargetController` should track hand movement speed from solver target position delta.

Later, when held-object hitboxes are implemented, the preferred signal will depend on whether the arm is holding an object:

- If the arm holds an object with a `Rigidbody2D`, use object velocity.
- Otherwise use hand/solver target movement speed from position delta.

That gives this rule:

```text
held object speed, if available
else hand target speed
```

When speed is above `attackSpeedThreshold`, an empty arm enters attack mode. When speed falls below threshold, keep attack active for `1` second so it does not flicker off immediately. After that attack window ends, the arm can enter attack mode again if it becomes fast enough, spending attack energy again at that point.

## Two Hands On One Object

The existing `IGrabbable` interface was designed around one hand controlling one object:

```csharp
void OnGrab(Transform grabParent);
void OnAttract(Vector2 attractPoint);
void OnRelease(Vector2 throwForce);
```

For `CubePickup`, `OnGrab` stores one `followTarget` and parents the cube to one grab parent. If the left hand grabs a cube and then the right hand grabs the same cube, the current cube logic can only remember one target. The second grab can replace the first target instead of making the cube follow both hands.

To support true two-hand holding on one cube, the cube or grab controller needs extra logic. Clean options:

- Give grabbables optional two-hand support, for example two follow targets and an averaged attraction point.
- Keep `IGrabbable` simple and let `CowboyGrabController` detect when both arms hold the same object, then call `OnAttract` with an averaged point between left and right hands.

Chosen behavior: use the second option. When both arms hold the same object, `CowboyGrabController` should attract the object toward the midpoint between the two hand anchors. This keeps the public grabbable contract smaller and makes the shared object feel pulled by both hands.

## Object As Hitbox

Making the held object become the hitbox is possible, but it is explicitly deferred until the two-arm grab and hand-speed attack behavior works well in playtests.

Reasons:

- `AttackHitbox` currently assumes a fixed child hitbox on the player hand.
- A held object may have different colliders, rigidbodies, layers, and damage behavior.
- The same object should not damage the player holding it.
- The same swing should not apply damage repeatedly every physics frame.

A clean design is to add an optional component for held-object attacks, for example `HeldObjectAttackHitbox`.

That component can:

- Live on grabbable objects that can be used as weapons.
- Be activated by the arm controller while the owning arm is above speed threshold.
- Use the held object's collider and velocity for damage/push direction.
- Know the owning `RobotStateController` and `CowboyArmSide`.
- Ignore the owner and optionally ignore the other held object.
- Deactivate itself when the speed grace period ends.

This avoids forcing all `IGrabbable` objects to be weapons.

Do not implement this in the first pass. Implement it only after the base grab behavior is tested and accepted.

## Ungrab Input

After two-arm grabbing works, add explicit per-arm ungrab input:

- Press `Q` to ungrab/release the left arm.
- Press `E` to ungrab/release the right arm.

This should release only the matching arm. For example, pressing `Q` should not release the right-hand object.

This release behavior is separate from stopping arm movement. Releasing the mouse button can stop driving the arm, but the held object should stay held until the matching ungrab key is pressed, unless another system forces release.

## Proposed Implementation Order

1. Refactor `CowboyGrabController` to per-arm held state while preserving the existing `IGrabbable` interface.
2. Refactor `ArmTargetController` so no-click movement automatically selects an active arm by mouse side.
3. Add the `2` second automatic arm-switch delay for side changes.
4. Make left/right mouse buttons instantly select the matching arm and enter grab mode.
5. Make grab-mode arms continuously attempt grab while held and empty.
6. Add per-arm release input: `Q` for left arm, `E` for right arm.
7. Add per-arm speed tracking and speed-threshold attack activation.
8. Charge attack energy when speed-based attack activates.
9. Keep existing hand `AttackHitbox` as the first attack output, and only enable it for empty hands in phase 1.
10. Add active-hold energy consumption over time.
11. Add `2` second per-arm regrab lockout after `Q`/`E` release.
12. Release all physically held objects when the player faints.
13. Add optional same-object two-hand attraction if the first per-arm implementation cannot truly hold one cube with both hands.
14. Add optional held-object hitboxes only after the two-arm behavior is stable and approved in testing.

## Tests To Add Or Update

Do not add tests until requested.

When tests are requested, cover:

- Left and right arms can hold separate grabbables at the same time.
- Left and right arms can both hold the same grabbable at the same time.
- Releasing one arm leaves the other object held.
- `ReleaseAllImmediate` releases both held objects.
- `Q` releases only the left-hand object.
- `E` releases only the right-hand object.
- Releasing a mouse button does not drop a held object.
- Holding over a grabbable eventually grabs even if the button was pressed before the hand entered range.
- Attack activation follows empty-hand speed and returns to grab mode after the configured grace frames.
- Held objects do not attack in phase 1.
- Attack energy is spent when speed-based attack activates.
- Holding an active arm spends energy over time.
- Manual release starts a `2` second regrab lockout for that arm.
- Fainting releases all physically held objects.
- Consumed/attached pickups are not released by fainting.
