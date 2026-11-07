# Junk Room Mechanics Investigation and Implementation Plan (2026-07-09)

## Goal

Implement the ROOM_Junks gameplay loop:

1. `SpawnGarbage` opens its panel by shrinking/retracting one side while the opposite side stays visually fixed.
2. It spawns 2 or 3 junk objects near the middle/top of the room.
3. The panel closes by stretching back to its original position.
4. Spawned junk falls with simple physics.
5. The player can grab junk with the existing hand grab system.
6. `JunkController` moves spawned junk from the middle lane to either the left point or right point, based on the selected side.

This document started as investigation and planning, and now also tracks implementation status.

## Implementation Status

### 2026-07-09

- Added `SpawnGarbageController` for the first visual slice.
- Wired `SpawnGarbage.prefab` so `SpawnGarbage Bottom` retracts and stretches every 5 seconds.
- The controller only animates transform scale/position, so `WarpMeshXYSkew` remains unchanged.
- Added an `OnPanelOpenReady` event point for the later junk spawn step.
- Simplified visible panel tuning while keeping the working movement behavior: `openWorldUpOffset` drives the upward movement and the legacy/local offset is hidden.

## Current Observations

### Existing Assets

Observed relevant prefabs:

- `Assets/Resources/Prefabs/Map/Basic/Machines/SpawnGarbage.prefab`
- `Assets/Resources/Prefabs/Map/Basic/Machines/JunkController.prefab`
- `Assets/Resources/Prefabs/IntereableObjects/Junk_1.prefab`
- `Assets/Resources/Prefabs/Map/ROOM_Junks.prefab`

Observed new junk sprites:

- `Assets/Resources/Furnitures/Junk_1.png` through `Junk_8.png`

`JunkController.prefab` already contains child transforms named:

- `LeftPoint`
- `RightPoint`
- `MidPoint`

These map well to the desired movement rule: junk enters near `MidPoint`, then travels straight to `LeftPoint` or `RightPoint`.

### Existing Grab Pattern

Main files:

- `Assets/Scripts/Gameplay/Items/IGrabbable.cs`
- `Assets/Scripts/Gameplay/Items/GrabController.cs`
- `Assets/Scripts/Gameplay/Items/CubePickup.cs`

The player grab system works through `IGrabbable`.

`CubePickup` is the best current reference for junk:

- Requires `Rigidbody2D` and `TargetJoint2D`.
- Implements `CanBeGrabbed`, `OnGrab`, `OnAttract`, and `OnRelease`.
- Uses a `TargetJoint2D` while held so the object follows the hand with soft physics.
- Raises `OnGrabbed` and `OnReleased` events.
- Changes sprite sorting while held.

The first junk prefab already has:

- `Rigidbody2D`
- `TargetJoint2D`
- `BoxCollider2D`
- `SpriteRenderer`

But it does not currently show a script component implementing `IGrabbable`, so the grab controller will not detect it as grabbable until that is added.

### Existing Conveyor Pattern

Main file:

- `Assets/Scripts/Factory/Upgrades/CubeConveyorController.cs`

This is a useful movement reference, but junk should not reuse cube upgrade behavior directly because:

- The cube conveyor owns one current cube at a time.
- It replaces cubes at midpoint.
- It destroys cubes at the exit.
- Junk needs multiple active objects and no upgrade replacement.

The useful part to copy is the simple `Vector2.MoveTowards` guided movement pattern and the detach-on-grab behavior.

### Existing Spawner Pattern

Main files:

- `Assets/Scripts/Factory/Spawners/CubeSpawner.cs`
- `Assets/Scripts/Factory/Spawners/DeadRobotSpawner.cs`

`DeadRobotSpawner` is closest to the desired garbage spawn behavior:

- It supports a list of prefabs.
- It selects randomly or sequentially.
- It applies random spawn offsets.
- It applies spawn impulse.
- It limits active spawned bodies.

Junk spawning can use the same shape, but should remain simpler and specific to the room mechanic.

## Proposed Runtime Components

### 1. `JunkPickup`

Suggested location:

- `Assets/Scripts/Gameplay/Items/JunkPickup.cs`

Role:

- Make junk grabbable.
- Keep junk physics similar to `CubePickup`.
- Expose grabbed/released events so the lane controller can stop controlling an object when the player grabs it.

Recommended implementation:

- Start by duplicating the simple physics behavior from `CubePickup`.
- Remove cube-specific details like stolen-from-robot logic.
- Keep:
  - `Rigidbody2D`
  - `TargetJoint2D`
  - sorting-order handling
  - `OnGrabbed`
  - `OnReleased`
  - `SetFollowTarget`
  - `OnAttract`

Important behavior:

- `CanBeGrabbed` returns `true`.
- `OnGrab` enables physics, parents to the hand, enables the target joint, and fires `OnGrabbed`.
- `OnRelease` disables the target joint, unparents the junk, keeps physics dynamic, and fires `OnReleased`.

Prefab setup:

- Add `JunkPickup` to each junk prefab.
- Keep the junk prefab on the same grabbable layer used by cubes if the player `grabbableLayers` mask relies on it.
- Do not tag junk as `CubeUpgrade` unless another system needs that tag. A dedicated `Junk` tag or no special tag is cleaner.

### 2. `JunkSpawner`

Suggested location:

- `Assets/Scripts/Factory/Spawners/JunkSpawner.cs`

Role:

- Own junk prefab selection and instantiation.
- Spawn a burst of 2 or 3 junk objects.
- Spawn from a configured origin with random horizontal/vertical offset.
- Optionally apply a small impulse so each piece separates naturally.

Serialized fields:

- `List<JunkPickup> junkPrefabs`
- `Transform spawnOrigin`
- `Transform spawnedParent`
- `Vector2 randomOffset`
- `Vector2 horizontalImpulseRange`
- `Vector2 verticalImpulseRange`
- `Vector2 angularImpulseRange`
- `int minBurstCount = 2`
- `int maxBurstCount = 3`

Public methods:

- `IReadOnlyList<JunkPickup> SpawnBurst()`
- `JunkPickup SpawnOne()`

Important behavior:

- Choose a random count between 2 and 3.
- Choose a random prefab for each junk.
- Instantiate at `spawnOrigin.position + randomOffset`.
- Set `Rigidbody2D.bodyType = Dynamic`.
- Apply small force/torque if enabled.
- Return the spawned junk list so `JunkController` can take ownership.

### 3. `SpawnGarbageController`

Suggested location:

- `Assets/Scripts/Factory/Machines/SpawnGarbageController.cs`

Role:

- Sequence the visual panel opening/closing and the junk spawn burst.
- Keep the hatch visual separate from the movement controller.

Serialized fields:

- `Transform movingPanel`
- `Transform fixedEdgeAnchor`
- `Transform movingEdgeClosedAnchor`
- `Transform movingEdgeOpenAnchor`
- `JunkSpawner junkSpawner`
- `JunkController junkController`
- `float openDuration`
- `float spawnDelay`
- `float closeDuration`
- `float cycleInterval`
- `bool spawnOnStart`

Panel animation requirement:

- One side should visually stay still.
- The other side should move inward/outward.
- The object becomes thinner while opening and returns to normal while closing.

Recommended panel math:

- Store the closed panel world/local scale and position in `Awake`.
- Define the fixed edge as the edge that should not move.
- During animation:
  - interpolate panel width/scale on the moving axis.
  - offset panel position by half the removed width so the fixed edge remains in place.

For a horizontal hatch, the core idea is:

```csharp
float width = Mathf.Lerp(closedWidth, openWidth, t);
float centerOffset = (closedWidth - width) * 0.5f * fixedSideSign;
panel.localScale = new Vector3(width, closedScale.y, closedScale.z);
panel.localPosition = closedPosition + axis * centerOffset;
```

The exact axis and sign should be serialized so the prefab can be adjusted in the Inspector.

Public methods:

- `StartCycle()`
- `SpawnNow()`

Important behavior:

- Guard against starting a second cycle while already opening/spawning/closing.
- Open panel.
- Spawn burst.
- Pass spawned junk to `JunkController`.
- Close panel.
- Wait for next cycle if continuous spawning is wanted.

### 4. `JunkController`

Suggested location:

- `Assets/Scripts/Factory/Upgrades/JunkController.cs` or `Assets/Scripts/Factory/Machines/JunkController.cs`

Role:

- Move junk straight from the middle area toward the left or right point.
- Stop controlling junk once the player grabs it.
- Optionally destroy or detach junk when it reaches an exit point.

Serialized fields:

- `Transform leftPoint`
- `Transform rightPoint`
- `Transform midPoint`
- `float speed`
- `bool randomSide`
- `JunkExitSide forcedSide`
- `float reachDistance`
- `bool destroyAtExit`

Data model:

- Maintain a list of active controlled junk entries.
- Each entry stores:
  - `JunkPickup junk`
  - `Transform target`
  - `bool controlled`

Public methods:

- `void RegisterJunk(JunkPickup junk)`
- `void RegisterJunk(IEnumerable<JunkPickup> junk)`
- `void SendNextLeft()`
- `void SendNextRight()`

Movement rule:

- If junk is near the middle, move toward the selected side.
- Movement should be straight, using `Vector2.MoveTowards`.
- While controlled, use `Rigidbody2D.MovePosition` if a dynamic/kinematic rigidbody is present. This is safer with 2D physics than writing `transform.position` directly.
- When the player grabs the junk, handle `JunkPickup.OnGrabbed` and remove it from controlled movement.

Open question:

- The phrase "take on the left or right and move it straight to left or right point depend on the side chosen" could mean the player chooses the side later, or the room chooses randomly at spawn time. The first implementation can use `randomSide` with inspector overrides, then expose `SendNextLeft/Right` for future buttons or triggers.

## Suggested Implementation Phases

### Phase 1: Make Junk Grabbable

Code:

- Add `JunkPickup`.

Prefab:

- Add `JunkPickup` to `Junk_1.prefab`.
- Duplicate or create prefabs for `Junk_2` through `Junk_8` if needed.
- Confirm colliders cover the visible sprite.
- Confirm layer is included in the player grab mask.

Validation:

- Enter play mode.
- Spawn/place one junk near the player.
- Confirm each hand can grab, hold, and release it.

### Phase 2: Spawn Burst From Garbage Hatch

Code:

- Add `JunkSpawner`.
- Add `SpawnGarbageController`.

Prefab:

- Add `JunkSpawner` and `SpawnGarbageController` to `SpawnGarbage.prefab`.
- Add a child `SpawnPoint` where junk should appear.
- Assign junk prefab list.
- Assign panel transform and animation axis.

Validation:

- Trigger one cycle manually through the Inspector or a temporary start flag.
- Confirm panel opens, 2 or 3 junk spawn, then panel closes.
- Confirm junk falls into the room with physics.

### Phase 3: Controlled Left/Right Movement

Code:

- Add `JunkController`.

Prefab:

- Add script to `JunkController.prefab`.
- Assign `LeftPoint`, `RightPoint`, and `MidPoint`.
- Assign `JunkController` reference from `SpawnGarbageController`.

Validation:

- Spawned junk is registered with `JunkController`.
- Junk moves toward left/right target.
- Grabbing a moving junk stops guided movement and switches to hand physics.

### Phase 4: Room Integration

Prefab/scene:

- Place `SpawnGarbage` and `JunkController` inside `ROOM_Junks`.
- Ensure spawn point is centered above the middle drop area.
- Ensure left/right points are placed at the intended belt/room endpoints.
- Confirm sorting order puts junk in front of floor/background but behind foreground if needed.

Validation:

- Open `ROOM_Junks` scene/prefab in Unity.
- Run the loop several times.
- Confirm no junk spawns outside expected bounds.
- Confirm multiple junk pieces can coexist.

## Testing Plan

### Edit Mode Tests

Good candidates:

- `JunkPickup` initializes `TargetJoint2D` disabled.
- `JunkPickup.OnGrab` enables joint and fires `OnGrabbed`.
- `JunkPickup.OnRelease` disables joint and fires `OnReleased`.
- `JunkSpawner.SpawnBurst` respects min/max count with assigned prefabs.
- `JunkController.RegisterJunk` chooses a valid target and removes junk when grabbed.

### Manual Unity Validation

Use the project standard Edit Mode command when needed:

```bash
unity -runTests -testPlatform EditMode -projectPath "$(pwd)" -quit
```

Manual checks are still required because the panel shrink/stretch and room placement are visual/prefab-driven.

## Risks and Decisions

### Risk: Panel Shrink Axis May Be Wrong

`SpawnGarbage` uses a visual hierarchy with top, bottom, and background pieces. The controller should serialize the animation axis and fixed side sign instead of assuming X or Y.

### Risk: Direct Transform Movement Can Fight Physics

For moving active junk, prefer `Rigidbody2D.MovePosition` in `FixedUpdate`. If the object is grabbed, immediately stop guided movement.

### Risk: Junk Reusing Cube Tag Can Trigger Cube Systems

`Junk_1.prefab` currently appears tagged as `CubeUpgrade`. If this is accidental, change it before implementation. The cube collector and upgrade flow look for `CubePickup` and `CubeUpgrade`, but keeping junk out of cube tags reduces future bugs.

### Risk: Multiple Spawned Junk Need Ownership Cleanup

Unlike `CubeConveyorController`, the junk controller should track a list and remove null/grabbed/reached objects every update. Otherwise destroyed or grabbed junk can leave stale references.

## Recommended File List

New scripts:

- `Assets/Scripts/Gameplay/Items/JunkPickup.cs`
- `Assets/Scripts/Factory/Spawners/JunkSpawner.cs`
- `Assets/Scripts/Factory/Machines/SpawnGarbageController.cs`
- `Assets/Scripts/Factory/Machines/JunkController.cs`

Likely prefab edits:

- `Assets/Resources/Prefabs/IntereableObjects/Junk_1.prefab`
- any additional junk prefabs created from `Junk_2.png` through `Junk_8.png`
- `Assets/Resources/Prefabs/Map/Basic/Machines/SpawnGarbage.prefab`
- `Assets/Resources/Prefabs/Map/Basic/Machines/JunkController.prefab`
- `Assets/Resources/Prefabs/Map/ROOM_Junks.prefab`

Optional tests:

- `Assets/Editor/UnitTests/JunkPickupTests.cs`
- `Assets/Editor/UnitTests/JunkSpawnerTests.cs`
- `Assets/Editor/UnitTests/JunkControllerTests.cs`

## Minimum Viable Implementation

The smallest useful version is:

1. Add `JunkPickup` and put it on `Junk_1.prefab`.
2. Add `JunkSpawner` that spawns 2 or 3 `JunkPickup` prefabs at `SpawnPoint`.
3. Add `SpawnGarbageController` with coroutine-driven panel open/spawn/close.
4. Add `JunkController` with random left/right target selection and detach-on-grab.

This gets the full room loop working without coupling it to factory worker machines, cube upgrades, inventory pickups, or room generation.
