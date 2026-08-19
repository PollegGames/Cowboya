# SpawnCubesCollected — collection persistence and laboratory spawn design

**Status:** implemented; static chute, no panel animation

**Date:** 19 August 2026

**Scope:** cubes accepted by `CubeCollecteur` in one level and physically restored by `SpawnCubesCollected` in the following laboratory visit

**Implementation:** `LaboratoryProgress` schema 3 stores typed incoming/free counts, `CubeCollector` defaults to laboratory storage, and `LaboratoryCollectedCubeSpawner` presents the free-count snapshot once per visit.

## 1. Goal

The player-facing sequence is:

```text
Previous gameplay level
    -> player puts white and/or colored cubes into CubeCollecteur
    -> the run records one unit of the exact cube type
    -> the collected physical cube disappears

Next laboratory visit
    -> SpawnCubesCollected reads the run-persistent counts
    -> recorded cubes appear rapidly, one at a time, at the existing spawn point
    -> each cube falls from above under normal 2D physics
    -> every spawned cube can be grabbed and used as a normal physics object
```

`SpawnCubesCollected` remains a static chute. No top/bottom opening, closing, or other panel animation is part of this feature.

Collecting a colored cube for this flow must **not** immediately improve the player's stats. The cube remains a resource until a laboratory machine explicitly consumes it later.

The logical counts are authoritative. Spawned GameObjects and their positions are temporary representations of those counts.

### 1.1 Relationship to the laboratory base design

This document refines the resource ownership, inter-scene transport, and free-object restoration rules in `Docs/Laboratory_System_Concept_and_Implementation_Base.md`. If the two documents differ, this focused document governs only the `CubeCollecteur -> SpawnCubesCollected` path; the base document continues to govern the wider laboratory.

This feature does not implement Builder costs, `UpgradeStatsMachine` consumption/lever behavior, balance values, or disk-based run resume. It only prepares correctly typed free resources for those later systems.

## 2. Pre-implementation baseline

The points below describe the behavior that this implementation replaced.

### 2.1 Collection and persistence

- `CubeCollector` is attached to the trigger child of `CubeCollecteur.prefab`.
- It currently accepts only an object containing both `CubePickup` and `CubeUpgrade`.
- It records only the last type in `CubeUpgradeSO.SelectedUpgrade`, immediately adds the corresponding bonus to `PlayerRunStats`, and destroys the cube.
- `CubeUpgradeSO.SelectedUpgrade` is a single value, not an inventory. It cannot represent multiple cubes or repeated cubes of the same type.
- `CubeNormal.prefab` contains `CubePickup` but no `CubeUpgrade`; therefore the current collector ignores the white cube.
- `RunProgressManager` survives scene changes and owns `LaboratoryProgress`, making `LaboratoryProgress` the correct authority for run-persistent laboratory resources.
- `LaboratoryProgress` currently persists DocBot's white-cube lifecycle, including pending, available, and laboratory-free white counts. It does not yet persist colored cube counts.

### 2.2 Cube prefabs already available

| Laboratory type | Existing prefab | Existing upgrade type |
|---|---|---|
| White | `CubeNormal.prefab` | none |
| Green / Health | `CubeMaxHealth.prefab` | `MaxHealth` |
| Blue / Energy | `CubeMaxEnergy.prefab` | `MaxEnergy` |
| Violet / Recharge | `CubeReloadEnergy.prefab` | `EnergyRecharge` |
| Red / Force | `CubeAttackDamage.prefab` | `AttackDamage` |

The display label **Force** continues to map to the technical type `AttackDamage`, as established by the laboratory base document.

### 2.3 Spawn machine baseline

- `SpawnCubesCollected.prefab` exists, is already instanced in `ROOM_Laboratory_1.prefab`, and contains a visual mesh with a nested existing `SpawnPoint`.
- It is a visual copy of `SpawnDeads.prefab` with the dead-robot spawner removed and its objects renamed.
- Its renderer still references `SpawnDeadMaterial`; the new `SpawnCubesCollectedMaterial` and furniture image are not yet used by the prefab.
- Contrary to the earlier visual description, the current `SpawnDeads.prefab` has no top/bottom transforms and no open/close animation. `DeadRobotSpawner` only instantiates bodies periodically and applies `Rigidbody2D` impulses.
- No animation should be added to `SpawnCubesCollected`. Its existing static visual and spawn point are the intended presentation.

## 3. Resource identity and collection

### 3.1 One canonical laboratory cube type

Introduce a stable resource identity that includes white:

```text
LaboratoryCubeType
    White
    MaxHealth
    MaxEnergy
    EnergyRecharge
    AttackDamage
```

The numeric values must be explicit and never reordered after data has shipped. `CubeUpgradeType` may remain for upgrade semantics, but storage and spawning use `LaboratoryCubeType` because `CubeUpgradeType` cannot represent white.

Colored identities are mapped from the existing `CubeUpgrade.UpgradeType`. A `CubePickup` without `CubeUpgrade` is accepted as `White` only when the collector's explicit `collectNormalCubesAsWhite` setting is enabled. The configured shared `CubeCollecteur.prefab` enables this policy for `CubeNormal`.

### 3.2 Explicit collector mode during migration

If any old level still needs immediate upgrades, compatibility must be an explicit serialized mode, for example:

```text
LaboratoryStorage    (target behavior for CubeCollecteur)
ImmediateUpgrade     (temporary legacy behavior only)
```

The mode must not depend on a scene name. The target `CubeCollecteur` instance or prefab for levels feeding a laboratory uses `LaboratoryStorage`.

The collector component is on the shared `CubeCollecteur.prefab`, which is used from more than one room, including `ROOM_CubeCollector` and `ROOM_Start`. Its migration must therefore audit every prefab instance; changing the shared default without that audit could alter unrelated levels.

In `LaboratoryStorage` mode:

1. Resolve one `CubePickup` root and map `CubeUpgradeType`, or the explicitly enabled normal-cube policy, to `LaboratoryCubeType`.
2. Ask `LaboratoryProgress.TryStoreIncomingCube(type, 1)` to commit the count.
3. Disable the cube's colliders and interactions immediately so multiple collider callbacks cannot commit it twice.
4. Destroy the physical cube only after the logical commit succeeds.
5. Do not call `CubeUpgradeSO.Store` and do not call `PlayerRunStats.AddCubeBonus`.

If the run manager, progress object, or identity is missing, log a clear warning and leave the physical cube intact. Silent loss is not acceptable.

## 4. Run-persistent ownership

`LaboratoryProgress`, already owned by `RunProgressManager`, remains the sole authority. It gains counts for these two ownership stages:

```text
IncomingCollectedCubes
    Cubes accepted by CubeCollecteur in the preceding gameplay level

LaboratoryFreeCubes
    Cubes available as loose resources in the current or a future laboratory
```

At successful `TryBeginVisit(visitId)`, every incoming count is atomically added to the matching laboratory-free count and then reset to zero. Repeating initialization for the same visit cannot promote the counts twice.

The existing DocBot white-cube states keep their distinct meaning:

| State | May SpawnCubesCollected emit it? | Reason |
|---|---:|---|
| `WhiteCubeCountPendingForNextVisit` | No | DocBot has not produced or presented it yet. |
| `AvailableWhiteCubeCount` | No | It still belongs to DocBot's presentation flow. |
| Laboratory-free white count | Yes | It is an ordinary free laboratory resource. |
| Incoming white count from `CubeCollecteur` | Yes, after visit promotion | It was physically delivered in the preceding level. |

When the player takes a white cube directly from DocBot, the existing physical cube already represents the newly free logical cube. `SpawnCubesCollected` must not create a second instance during that visit. If it remains unconsumed, it can be restored through the chute on a later visit.

### 4.1 Counts, not collection history

Cubes are fungible. Persist quantities per type, not GameObject identifiers, positions, velocities, or an exact collection-order list.

The spawn presentation uses a deterministic round-robin order:

```text
White -> MaxHealth -> MaxEnergy -> EnergyRecharge -> AttackDamage -> repeat
```

Types with zero remaining count are skipped. This mixes colors while producing exactly the stored quantity. Preserving the player's original collection order would require a persistent queue and is intentionally outside this feature.

## 5. Authority and physical representation

Spawning a cube is not a logical withdrawal. It creates a representation of a count already owned by `LaboratoryFreeCubes`.

```text
LaboratoryFree count = 3
    -> three physical representations are created
    -> logical count remains 3

One representation enters a compatible laboratory machine
    -> atomically transfer one unit from LaboratoryFree to MachineStorage
    -> destroy or absorb that one representation
    -> logical LaboratoryFree count becomes 2
```

Picking up, throwing, or dropping a cube does not change logical ownership. A later machine intake must perform the authoritative transfer before destroying the cube.

If a free cube falls out of the room, is destroyed unexpectedly, is held when leaving, or disappears because the scene unloads, its free count remains. It is restored on the next laboratory visit. There is no automatic same-visit replacement, because that could create unbounded duplicate physical cubes.

A future machine-intake feature may add a runtime representation/claim component before it consumes these cubes. That transfer guard is outside this spawn-only implementation.

## 6. Runtime architecture

```text
CubeCollecteur / CubeCollector
    -> LaboratoryProgress.TryStoreIncomingCube(type)
        (owned by RunProgressManager across scene changes)

LaboratoryManager.InitializeVisit()
    -> LaboratoryProgress.TryBeginVisit(visitId)
        -> promote IncomingCollectedCubes to LaboratoryFreeCubes once
    -> LaboratoryCollectedCubeSpawner.InitializeForVisit(progress, visitId)
        -> build a snapshot queue from LaboratoryFreeCubes
        -> instantiate exact cube prefabs rapidly, one at a time, at SpawnPoint
```

### 6.1 Responsibilities

`LaboratoryProgress`

- Store, query, sanitize, reset, promote, and transfer counts.
- Reject invalid types and negative quantities.
- Use saturating addition or another explicit overflow policy.
- Keep promotion and transfers atomic and idempotent.

`CubeCollector`

- Detect and identify one physical cube.
- Select the explicitly configured collection mode.
- Commit before destroying.
- Never own the persistent inventory.

`LaboratoryCollectedCubeSpawner`

- Read a snapshot of free counts after visit initialization.
- Validate exact type-to-prefab mappings.
- Build the deterministic presentation queue.
- Run at most one spawn sequence per visit.
- Instantiate free-falling cubes at the prefab's existing `SpawnPoint`.
- Never add, remove, or apply resources while presenting them.

### 6.2 Components that should not be reused directly

- Do not reuse `DeadRobotSpawner`. Its random, impulse-driven dead-body behavior does not implement the persistent-resource contract or exact cube-type mapping.
- Do not call the current `CubeSpawner.SpawnCube` for the drop. It enables the cube's target joint and makes it follow the supplied parent, which conflicts with a free fall. It also chooses colored upgrades randomly instead of honoring exact stored types.
- No panel controller or retract animator is required.

## 7. SpawnCubesCollected prefab and drop contract

Keep the prefab as a static chute and use its existing nested `SpawnPoint`. The spawn controller should expose a serialized reference to that transform and report a clear validation error if it is missing. The visual does not need to be split into top and bottom parts, and no moving-panel colliders or clearance trigger are required.

The choice between the inherited material and `SpawnCubesCollectedMaterial` is independent visual authoring and does not affect this logic.

The controller adds a serialized world-space `fallHeight` above the existing `SpawnPoint` to create the requested falling-from-a-distance effect. The marker's world X/Y are used for `Rigidbody2D` placement. The spawned Z is an explicitly configured gameplay depth, consistent with existing interactable cubes, instead of blindly copying a warped 3D mesh depth.

Spawned cubes must use a neutral laboratory or world parent. They must not inherit the warped presentation transform of the machine mesh.

The spawn sequence is:

1. `LaboratoryManager` completes `TryBeginVisit` and supplies the current `visitId` and `LaboratoryProgress`.
2. The spawner rejects a second request for the same visit.
3. It snapshots all `LaboratoryFreeCubes` counts and creates the round-robin queue.
4. If the queue is empty, it completes without spawning anything.
5. It validates that every queued type has a non-null prefab before starting the batch.
6. It instantiates one queued prefab at a time at `SpawnPoint`, optionally with a small serialized horizontal scatter.
7. Each instance starts as a normal simulated dynamic `Rigidbody2D`, with its target joint disabled, no follow target, and no inherited machine transform.
8. It waits for a short serialized interval, then emits the next cube until the queue is empty.

The interval should default to a rapid value such as `0.10 s` and remain designer-adjustable. The exact spawn-point height, horizontal scatter, world depth, and interval should be tuned in `Level_Laboratory` because the room uses a warped 3D presentation while cube physics are 2D.

## 8. Failure and interruption rules

| Situation | Required result |
|---|---|
| No collected/free cubes | No spawn. |
| Missing `RunProgressManager` during collection | Do not destroy the cube; warn. |
| Unsupported colored upgrade mapping during collection | Do not destroy the cube; warn with object name. |
| Duplicate trigger callbacks | Exactly one logical increment and one destruction. |
| Missing prefab mapping in the laboratory | Do not mutate counts; report the exact missing type; do not substitute a random cube. |
| Spawner initialized twice for one visit | No duplicate physical batch. |
| Spawner disabled during a batch | Pause its coroutine, keep logical counts unchanged, and resume the remaining snapshot when re-enabled. |
| Scene exits during the sequence | Temporary objects may disappear; free counts restore them on the next visit. |
| Cube enters a future machine twice | Representation and transfer guards allow only one authoritative transfer. |
| Directly playing the laboratory scene | Empty fallback progress produces no cubes and no fabricated state. |

If only one type lacks a prefab mapping, fail the presentation before spawning any of the batch and list all invalid mappings. This makes configuration errors obvious and avoids presenting a partial inventory as though it were complete.

## 9. Data API contract

Exact class layout can change during implementation, but the behavior should be expressible through APIs of this form:

```text
GetIncomingCubeCount(type)
GetLaboratoryFreeCubeCount(type)
GetLaboratoryFreeCubeSnapshot()

TryStoreIncomingCube(type, amount)
TryBeginVisit(visitId)              // owns incoming -> free promotion
TryTakeLaboratoryFreeCube(type, amount)
Reset()
```

Callers must not receive a mutable internal list or array. A snapshot is required so the presentation queue cannot alter authoritative state accidentally.

`TryBeginVisit` should own or call promotion; unrelated scene code must not manually copy counts. Schema versioning and deserialization sanitation must cover the new fields. Starting or restarting a run must clear both incoming and free cube counts along with the other laboratory progress.

## 10. Implemented files

Updated files:

- `Assets/Scripts/Factory/Upgrades/CubeCollector.cs`
- `Assets/Scripts/Laboratory/LaboratoryProgress.cs`
- `Assets/Scripts/Laboratory/LaboratoryManager.cs`
- `Assets/Resources/Prefabs/Map/Basic/Machines/CubeCollecteur.prefab`
- `Assets/Resources/Prefabs/Map/Basic/Machines/SpawnCubesCollected.prefab`
- `Assets/Editor/UnitTests/CubeCollectorTests.cs`
- `Assets/Editor/UnitTests/LaboratoryProgressTests.cs`

New implementation units:

- `Assets/Scripts/Laboratory/LaboratoryCubeType.cs`
- `Assets/Scripts/Laboratory/LaboratoryCollectedCubeSpawner.cs`
- `Assets/Editor/UnitTests/LaboratoryCollectedCubeSpawnerTests.cs`

Reference files that should remain behaviorally stable during this feature:

- `Assets/Scripts/Factory/Spawners/DeadRobotSpawner.cs`
- `Assets/Scripts/Factory/Machines/SpawnRobotCollectorController.cs`
- `Assets/Scripts/Factory/Spawners/CubeSpawner.cs`
- `Docs/Laboratory_System_Concept_and_Implementation_Base.md`

## 11. Confirmed implementation assumptions

1. `CubeNormal` is collectible as a white resource through the collector's explicit `collectNormalCubesAsWhite` policy.
2. Every unconsumed laboratory-free cube is restored through the static chute on each later visit, not only cubes newly gathered in the immediately preceding level.
3. Exact original collection order is not gameplay state; deterministic round-robin presentation is sufficient.
4. `LaboratoryStorage` is the desired mode for the `CubeCollecteur` that feeds the next laboratory. Any immediate-upgrade use is explicit legacy configuration.
5. The chute does not consume resources; future machines do.
6. `SpawnCubesCollected` does not animate. It emits cubes rapidly, one after another, from its existing spawn point.
