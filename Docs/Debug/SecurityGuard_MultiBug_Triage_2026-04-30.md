# Security Guard Multi-Bug Triage - 2026-04-30

Source log: `c:\Users\B\Downloads\logs.txt`

This document separates the current failures so they can be investigated and fixed one at a time. The bugs look related in play, but they touch different systems: guard machine dispatch, worker machine availability, combat perception/attack, and badge spawning/attachment.

## Track 1 - ALREADY FIXED - Guard misses machine-off event while already moving to security post

### Player-visible symptom
- A security guard is already moving to a security machine.
- The player switches off a work/rest machine during that movement.
- The guard continues to the security machine and does not reactivate the newly powered-off machine.

### Evidence from logs
- A factory machine power-off dispatch is emitted:
  - `MachineSecurityManager.DispatchGuard`
  - `MachineSecurityManager.DispatchGuardToReactivateMachine`
  - `TaskNew.ReactivateMachine.MoveToTarget`
- Later, when another rest machine is switched off, both guards are considered not stationed:
  - `stationed=False currentTask=ReactivateMachine:WorkingDesk... securityPost=none`
  - `stationed=False currentTask=Rest:Rest... securityPost=none`
  - `[MachineSecurityManager] No stationed guard available for resting machine=RestingDesk`
- Security slot also rejects a guard passing through while its current task is `ReactivateMachine` targeting `WorkingDesk`, which confirms the guard is not treated as available for security-post duty:
  - `Slot.SecuritySlot.rejected_reactivate_mismatch`

### Suspected cause
- `MachineSecurityManager` currently dispatches only guards considered "stationed at security machine".
- A guard that is in transit to a security post is not eligible, even if it is the correct guard to retarget.
- There is no queue/pending-off-machine list in `MachineSecurityManager`, so a machine-off event can be lost if no guard is eligible at that exact frame.

### Files to inspect first
- `Assets/Scripts/Factory/Machines/MachineSecurityManager.cs`
- `Assets/Scripts/Factory/Machines/SecurityMachine.cs`
- `Assets/Scripts/Factory/Slots/SecuritySlot.cs`
- `Assets/Scripts/Robots/RobotHeartNew.cs`
- `Assets/Scripts/Robots/Tasks/RobotTaskNew.cs`

### Proposed investigation
1. Decide desired policy: should a guard moving to a security post be interruptible for machine reactivation?
2. Add pending machine tracking in `MachineSecurityManager` or broaden eligibility to include guards whose current task is `GoToMachine` targeting a security waypoint.
3. Add a log when a guard arrives at a security machine and immediately checks pending powered-off machines.
4. Validate with: switch off machine while guard is moving to security post.

## Track 2 - ALREADY FIXED- Workers do not retarget a machine after the guard turns it back on

### Player-visible symptom
- A machine is turned off.
- A security guard turns it back on.
- Workers no longer go back to that machine.

### Evidence from logs
- Worker slot rejections continue after reactivation scenarios:
  - `Slot.WorkerSlot.rejected_task payload=machine=WorkingDesk task=GoToMachine`
  - repeated `Slot.WorkerSlot.rejected_task payload=machine=WorkingDesk task=WorkAtMachine`
- Earlier machine-off handling invalidates worker waypoint availability:
  - `MachineWorkerManager.NotifyWorkersMachinePoweredOff`
  - `waypointInvalidated=True`
  - `targetInvalidated=True`
- The reactivation dispatch log sometimes reports `waypoint=none` for `WorkingDesk`, which suggests the reactivation path may not restore the exact waypoint entry workers use.

### Suspected cause
- Worker memories mark the powered-off machine waypoint unavailable when the machine is switched off.
- When the guard powers the machine back on, availability may only be updated for the guard memory, not for all workers.
- If `ResolveMachineWaypoint(machine)` returns null or the wrong waypoint, the worker memory dictionary keeps the old `Work` waypoint as unavailable.

### Files to inspect first
- `Assets/Scripts/Factory/Machines/MachineWorkerManager.cs`
- `Assets/Scripts/Factory/Machines/MachineSecurityManager.cs`
- `Assets/Scripts/Robots/RobotBrainNew.cs`
- `Assets/Scripts/Robots/RobotMemoryNew.cs`
- `Assets/Scripts/Robots/RobotMemoryStateNew.cs`
- `Assets/Scripts/Factory/Slots/WorkerSlot.cs`

### Proposed investigation
1. Trace machine power-on event after guard reactivation.
2. Verify whether all worker brains receive `SetRoomWaypointAvailability(waypoint, true)`.
3. Fix waypoint resolution for machines whose `RoomWaypoint` is not on the same object.
4. Add logs on machine power-on that show each worker memory receiving restored availability.
5. Validate with: turn off `WorkingDesk`, wait for guard reactivation, confirm at least one worker plans `GoToMachine` to that exact `Work` waypoint.

## Track 3 - Security guard does not attack player when nearby

### Player-visible symptom
- Player stands near a security guard.
- Guard does not attack.

### Evidence from logs
- Logs show player attack hitbox events hitting robots:
  - `AttackHitbox:OnTriggerEnter2D`
  - `RobotMemoryStateNew:RegisterAttack`
- In the searched log excerpts, there is no clear security-guard transition to:
  - `Brain.OnPerceptionChanged detect=True attack=True`
  - `plannedTask=AttackTarget`
  - `Heart.OnCurrentTaskChanged current=AttackTarget`
- This suggests the issue may be before `RobotBrainNew.BuildTaskFromOptions`, likely perception trigger wiring or player reference payload.

### Suspected cause
- `FollowPlayerTriggerHandler` may not be firing new-pipeline perception events for the security guard collider setup.
- Or `RobotTaskNew.HandleAttackTarget` expects `Payload is Transform`, but `RobotBrainNew.BuildPlayerPayload` currently returns a `Vector3` when memory has only a last known position. In that case `TryStartAttack(target)` never runs.

### Files to inspect first
- `Assets/Scripts/Player/FollowPlayerTriggerHandler.cs`
- `Assets/Scripts/Robots/RobotBrainNew.cs`
- `Assets/Scripts/Robots/Tasks/RobotTaskNew.cs`
- `Assets/Scripts/Robots/Body/RobotAttackController.cs`
- Security guard prefab collider/trigger setup.

### Proposed investigation
1. Add probe logs around `FollowPlayerTriggerHandler` enter/stay/exit for security guards.
2. Confirm `RobotBrainNew.OnPerceptionChanged` is called with `attack=True`.
3. Confirm `AttackTarget` payload type is valid for `RobotAttackController.TryStartAttack`.
4. Fix either perception event delivery or payload handling, not both at once.

## Track 4 - ALREADY FIXED - Security badge stays on floor instead of attached to guard

### Player-visible symptom
- Security badge is spawned but remains on the floor.
- It is not visually/physically attached to the security guard.

### Current code path
- `EnemiesSpawner.InitializeRobot(...)` spawns badge for `RobotRole.SecurityGuard`.
- It resolves an anchor from `securityBadgeAnchor`, then calls:
  - `securityBadgeSpawner.SpawnBadge(anchor)`
  - `inventory.SetItem(PickupType.SecurityBadge, badge)`
  - `badge.AssignInventory(inventory)`

### Suspected cause
- The badge prefab likely has physics/joint behavior that is not configured to attach to the guard anchor on spawn.
- `SecurityBadgeSpawner.SpawnBadge(parent)` may instantiate under the parent but leave Rigidbody2D/TargetJoint2D in a world/free state.
- The security guard prefab may have a missing or wrong `securityBadgeAnchor`.

### Files/prefabs to inspect first
- `Assets/Scripts/AI/EnemiesSpawner.cs`
- `Assets/Scripts/Factory/Spawners/SecurityBadgeSpawner.cs`
- `Assets/Scripts/Gameplay/Items/SecurityBadgePickup.cs`
- Security guard prefab.
- Security badge prefab.

### Proposed investigation
1. Inspect `SecurityBadgeSpawner.SpawnBadge` and confirm transform parent/local position handling.
2. Inspect `SecurityBadgePickup` initialization to see whether it detaches itself or leaves physics enabled.
3. Inspect security guard prefab for a valid badge anchor.
4. Add one spawn log with badge parent, local position, world position, Rigidbody2D body type, and joint target.
5. Fix attachment separately from inventory registration.

## Recommended order

1. Track 1: Guard dispatch/pending machine-off handling.
2. Track 2: Worker availability restoration after machine power-on.
3. Track 3: Guard perception/attack.
4. Track 4: Badge attachment.

Reason: Tracks 1 and 2 are coupled through machine power state and worker availability. Attack and badge are independent and should not be mixed into the machine-cycle fix.

## Next concrete task

Start with Track 1. The smallest useful test is:
1. Spawn one guard moving toward a security machine.
2. Switch off a work machine during that movement.
3. Expect `MachineSecurityManager` to either retarget that moving guard or store the machine as pending.
4. On guard arrival at security machine, expect an immediate pending-machine check and `ReactivateMachine` dispatch.
