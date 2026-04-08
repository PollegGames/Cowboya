# Robot AI Debug: Empty Stack + RestingMachine Loop

This document summarizes the issue, expected behavior, observed logs, root causes, and the fixes that make the behavior stable.

## What You Asked For
- Workers should **rest when the factory machine is OFF**, then return to work when possible.
- The **task stack should not become empty**; it should always contain a primary task for each role.
- Logs should show useful **stack + intent** transitions instead of noisy per-machine logs.

## Scenario Reproduced
- Map with 6 rooms (start, end, work, rest, lift, lift).
- 2 workers + 1 boss.
- Player toggles a factory machine OFF.
- Game paused for a few seconds after toggle.

## Expected Behavior (from your description)
1. Worker assigned to the machine goes to rest.
2. Resting machine tries to send the worker back to work. If no machine is available, the worker returns to rest.
3. Second worker arrives at rest; if the rest machine is already occupied, the worker should go to the start room.
4. No player interaction after the toggle other than the initial OFF switch.

## Observed Logs (key points)
The logs show a loop like this, repeatedly:
- `RestingSlot.OnTriggerEnter2D` -> `RestingMachine.AttachRobot`
- `RestingMachine.SendWorkerToRest` -> `RobotBrain.OnMachineStateChanged(...)`
- `RobotHeart.CompleteCurrentTask()` -> `stack=[]`
- `RobotHeart.TryPushTask(Rest)` -> `stack=[Rest]`

And for some robots:
- `RobotHeart.Update()` removes expired tasks -> `stack=[]`

## Root Causes
### 1) Stack becomes empty after completion/expiry
The Heart now seeds **primary tasks** only during `ResetIntentStack()`.
But tasks are removed later by:
- `CompleteCurrentTask()`
- `RemoveExpired()` inside `RobotHeart.Update()`

When that happens, nothing re-seeds the stack, so it stays empty and logs show:
`intent=None current=None stack=[]`.

### 2) RestingMachine re-attaches the same worker repeatedly
`RestingSlot.OnTriggerEnter2D` triggers `RestingMachine.AttachRobot` multiple times for the same worker.
There is no early guard when the worker is already assigned and the machine is ON.
This causes repeated `SendWorkerToRest` calls and a task-flap loop.

## Solution (Proposed Fixes)
### Fix A: Re-seed primary task when the stack becomes empty
When a task is completed or expired and the stack is empty, insert the role’s primary task.

Where to apply:
- `RobotHeart.CompleteCurrentTask()`
- `RobotHeart.Update()` after `RemoveExpired()`

Effect:
The stack never stays empty and always falls back to a role-specific baseline intent.

### Fix B: Prevent duplicate RestingMachine attachment
Ignore `AttachRobot` when the machine is ON and the same worker is already assigned.

Where to apply:
- `RestingMachine.AttachRobot(...)`

Effect:
Stops the repeated rest re-attach loop, which eliminates the rapid task-flipping.

## How to Verify the Fixes
1. Enable `logStackChanges` in all robot prefabs.
2. Re-run the exact scenario.
3. Expect:
   - No `stack=[]` loops after completion/expiry.
   - No repeated RestingMachine attach spam.
   - Stable transitions:
     - Work -> Rest (when machine OFF)
     - Rest -> Work (when machine ON)
     - If no work is available, Rest remains as the top stack task.

## Notes About Primary Intent
Primary intent is `Move`, `Stay`, or `Attack`. Tasks (handlers) express *how* the robot does that.
- `ChasePlayer` = **Move** (move to player, then attack if close).
- `ReactivateMachine` = **Move** (move to machine + play interaction).

## If the Issue Persists
Next checks:
1. Robot has multiple colliders that can trigger `OnTriggerEnter2D` repeatedly.
2. RestingSlot collider is too large, causing multiple enter events.
3. Worker prefab has multiple child colliders without filtering, causing re-attach.

