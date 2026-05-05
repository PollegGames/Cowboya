# Robot Run Upgrade And Worker Task Investigation - 2026-04-30

Context from `c:\Users\B\Downloads\logs.txt`: one run with 3 workers and 1 boss. The player sent workers toward the end/star room, killed the boss, picked up a green cube health upgrade, then ended the level. Expected result: next level player stats should include normal stats plus the cube upgrade. Observed result: cube upgrades do not persist into the next level. Also observed: workers can change destination before reaching a machine after the player switches machines off, and workers do not flee away when attacked.

No code was changed for this investigation.

## 1. Cube Upgrade Does Not Carry To Next Level

### Flow in the current code

Pickup path:

1. `CubeCollector.OnTriggerEnter2D` detects `CubePickup` + `CubeUpgrade`.
2. `CubeCollector.StoreUpgrade` stores the last selected upgrade in `CubeUpgradeSO`.
3. `CubeCollector.ApplyRunBonuses` adds the bonus to `RunProgressManager.Instance.RunStats`.

Level-end path:

1. `LevelEndVictoryTrigger.OnTriggerEnter2D` calls `runStats.Capture(controller.Stats, energyBot, attack)`.
2. Then it calls `saveService.SaveGame(controller)`.
3. Then it calls `RunProgressManager.Instance.LoadNextLevel()`.

Next-level spawn path:

1. `PlayerSpawner.InitializePlayer` creates stats from `saveService.CurrentSaveData`.
2. If `runStats.HasValues`, it calls `runStats.Apply(playerRobotInfo, energyBot, attack)`.

### Why it fails

The cube pickup stores bonuses in `PlayerRunStats.MaxHealthBonus`, `MaxEnergyBonus`, `EnergyRechargeBonus`, and `AttackDamageBonus`.

But `LevelEndVictoryTrigger` calls `runStats.Capture(...)` at the end of the level. `PlayerRunStats.Capture` copies only live player stats:

- `CurrentHealth`
- `CurrentEnergy`
- `MaxHealth`
- `MaxEnergy`
- `Morality`
- `EnergyRechargeRate`
- `AttackDamage`
- `hasValues`

It does not clear the bonus fields directly, but it captures the un-upgraded live stats because the cube pickup never applies the upgrade to the current player. The upgrade exists only as a pending `RunStats` bonus.

The next level applies:

- `target.MaxHealth = MaxHealth + MaxHealthBonus`
- `target.MaxEnergy = MaxEnergy + MaxEnergyBonus`
- recharge and attack damage with bonus too

That should work if the same `RunProgressManager.RunStats` asset is used by both pickup and next spawn.

The suspicious part is saving. `PlayerSaveService.SaveGame(controller)` saves `controller.Stats.MaxHealth`, `MaxEnergy`, and `AttackEnergyCost`, but it does not save the `PlayerRunStats` bonus values. Since the player controller stats were never upgraded at pickup time, the save file remains at the old values. The startup log confirms the run begins from saved stats at 100 health and 100 energy:

```text
PlayerStats initialized with health: 100 and energy: 100 and morality: 0 and attack energy cost: 5
```

There is also no cube pickup debug line in the provided log, so the log cannot prove the pickup fired. The code path has no logging around `CubeCollector`, so this is currently invisible in logs.

### Most likely root causes

1. The cube bonus is stored as run-only pending data, not applied to the live player stats when picked up.
2. `SaveGame` saves the live player stats, not the pending run bonuses.
3. The level transition relies on `PlayerRunStats` surviving between scenes. If the `CubeCollector` and `PlayerSpawner` reference different `PlayerRunStats` instances, the bonus is added to one instance and applied from another.
4. Attack damage upgrades have an extra risk: `PlayerTemplate.InitializePlayerStats` clears `robotBehaviour.Stats.Attacks` to a new empty list, so `PlayerSpawner` often passes `attack = null` into `runStats.Apply`. In that case `AttackDamageBonus` cannot apply to any attack.

### Possible fixes

Preferred design decision first: decide whether cubes are per-run temporary upgrades or permanent saved upgrades.

If cube upgrades are per-run only:

- Keep `PlayerRunStats` as the source of truth during the run.
- Add logging in `CubeCollector` for pickup type and bonus totals.
- Ensure every cube collector uses `RunProgressManager.Instance.RunStats`, not a scene-local asset reference.
- At level end, preserve accumulated bonuses when `Capture` runs and verify `PlayerSpawner` uses the same `RunStats` instance on the next scene.
- Consider applying max-health/max-energy immediately to the live player too, so the save/capture path has consistent values.

If cube upgrades are permanent saved upgrades:

- Apply the upgrade to `controller.Stats` at pickup time.
- Save upgraded `MaxHealth`, `MaxEnergy`, recharge, and attack damage explicitly.
- Extend `SaveData` for fields that are not currently persisted, especially `EnergyRechargeRate` and attack damage/order.

## 2. Worker Changes Destination Before Arriving At Machine

### Intended behavior

For `GoToMachine`, the worker should simply travel to the selected machine. If the player switches that machine off before the worker arrives, the worker should not know until it reaches that machine or until a legitimate perception/event tells it. It should not globally replan just because a machine somewhere was turned off.

### Flow in the current code

The intended architecture is:

Memory -> Brain -> Heart -> Task logic

Actual machine-off flow:

1. Player calls `BaseMachine.PowerOffOnly`.
2. Machine calls `PowerOff`.
3. `MachineWorkerManager.HandleMachineTurnedOff` receives the event.
4. `MachineWorkerManager.NotifyWorkersMachinePoweredOff` loops through all `RobotBrainNew` objects.
5. It writes machine availability directly into each worker memory with `SetRoomWaypointAvailability(..., false)` or `SetMachineWaypointAvailability(..., false)`.
6. Every memory write triggers `MemoryNew.OnChanged`.
7. Brain immediately recalculates options and publishes a new plan.
8. Heart pushes/refreshes the new task.

Log evidence around timestamp `96.940`:

```text
event=Brain.MachineWorkerManager.NotifyWorkersMachinePoweredOff payload=machine=WorkingDesk targetInvalidated=True
eventSource=HeartNew.OnPlannedTask brainOptions=NeedMachine, MachineUnavailable plannedTask=ReturnHome heartCurrentTask=ReturnHome
```

There are also `waypointInvalidated=True` events for workers that were not targeting that exact machine.

### Why it fails

The worker is learning global machine state from `MachineWorkerManager`, not from arriving at the machine. This bypasses the desired information boundary.

There is already an attempt to defer invalidation for workers on `GoToMachine` when they are not targeting the powered-off machine:

```csharp
if (currentTask.Type == RobotTaskType.GoToMachine && !targetsPoweredOffMachine)
{
    brain.Memory.SetRoomWaypointAvailability(machineWaypoint, false);
    ...
    continue;
}
```

But this still writes the off-machine fact into memory immediately. That memory change is enough for Brain to replan. If all worker machine waypoints become unavailable, `BuildOptions` sets `MachineUnavailable`, and `BuildTaskFromOptions` returns `ReturnHome`.

For the worker that is targeting the powered-off machine, the manager explicitly changes memory and blocks the current task:

```csharp
brain.Memory.ChangeConnectionToMachine(false);
brain.Memory.SetDesiredMachineType(machine.Type);
brain.Heart.BlockCurrentTask();
```

That is also immediate remote knowledge, not arrival-based knowledge.

### Possible fixes

Use one of these policies:

1. Strict local knowledge: `MachineWorkerManager` should only notify workers currently attached to the powered-off machine. Traveling workers keep their `GoToMachine` task. On arrival, `WorkerSlot.HandlePoweredOffMachineArrival` detects `!machine.IsOn`, writes that fact into memory, blocks/replans, and the worker chooses the next task.

2. Reserved-target knowledge only: notify the worker whose selected destination is exactly the powered-off machine, but do not notify unrelated workers. This is less strict than local knowledge but prevents unrelated global replans.

3. Memory fact with no replan while traveling: allow `AllAvailableWaypoints` to update, but Brain/Heart should not replace an active `GoToMachine` task unless the current destination itself is invalidated or the task reaches its destination. This keeps knowledge and action separate.

The cleanest match to the requested behavior is option 1.

## 3. Worker Does Not Flee When Attacked

### Flow in the current code

Damage path:

1. `HealthBot.TakeDamage` logs damage.
2. If the new pipeline is active, it calls:
   - `memoryNew.RegisterAttack()`
   - `brainNew.OnDamageTaken(damage)`
3. `RobotMemoryStateNew.RegisterAttack` sets `WasRecentlyAttacked = true` and raises `TookDamage`.
4. Brain sees `WasRecentlyAttacked` and sets `BrainOption.InDanger`.
5. For `RobotRole.Worker`, `BuildTaskFromOptions` returns `RobotTaskType.Flee`.
6. Heart starts the `Flee` task.
7. `RobotTaskNew.HandleFlee` only does:
   - `context.Body?.StopMovement()`
   - schedule completion after 1 second

### What the provided log shows

The provided log does not include a worker attack case. The only new-pipeline damage traces are for the boss:

```text
memoryDelta=TookDamage robotId=BossMony...
event=Brain.OnDamageTaken payload=damage=5
```

There is no `plannedTask=Flee` or `TaskNew.Flee` trace for a worker in this log.

### Why the observed flee behavior fails

There are two separate problems:

1. If damage does reach the worker brain, Brain should plan `Flee`, but the task implementation does not move away. It only stops movement for about one second. So visually it does not flee.

2. There is no reset path visible for `WasRecentlyAttacked`. `RobotMemoryStateNew.ResetAttackMemory` exists, but `RobotTaskNew.HandleFlee` does not call it when flee completes. This can leave the worker in `InDanger`, causing repeated `Flee` planning or preventing a clean return to the interrupted `GoToMachine` intent.

Also, if the player attack hitbox damages a child object without `HealthBot`, `RobotBrainNew`, or `RobotMemoryNew` on the same object or parent, the attack may not reach the worker memory. That was not proven by this log, but it is a setup risk to verify in the prefab.

### Possible fixes

Expected behavior: worker is going to a machine, player attacks it, worker flees in the opposite direction, then resumes `GoToMachine`.

To support that behavior:

1. Memory should record a richer damage fact, not just `WasRecentlyAttacked`. It needs at least attacker position or hit direction.
2. Brain should plan `Flee` as a temporary interrupt, without destroying the underlying `GoToMachine` intent.
3. Heart should preserve the previous task under `Flee` on the stack, so after `Flee` completes the worker returns to `GoToMachine`.
4. Task logic for `Flee` should compute a destination opposite from the attacker/player and call `Body.SetDestination`, not `StopMovement`.
5. When flee finishes, memory should clear `WasRecentlyAttacked` through `ResetAttackMemory`.

## Summary

The three symptoms have different causes:

- Cube upgrades are likely lost because pickup stores pending bonuses in `PlayerRunStats`, while level-end save/capture uses live player stats that were never upgraded, and attack damage has no attack instance to update.
- Workers change destination because machine-off events write global availability facts directly into all worker memories, which causes Brain to replan before arrival.
- Workers do not visibly flee because `Flee` currently stops movement and completes after a timeout; it does not calculate or move to a flee destination, and attack memory is not cleared by the task.

The machine issue is the clearest Memory -> Brain -> Heart -> Task violation: the memory pillar receives facts the worker should not know yet. The fix should start by moving powered-off-machine discovery to arrival-time task/slot logic, then let Brain and Heart react to that local memory change.
