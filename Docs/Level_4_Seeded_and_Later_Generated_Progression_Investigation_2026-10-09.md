# Level 4 Seeded Map and Later Generated Progression Investigation

## Status

Planning document only. No runtime code, scene, prefab, or map configuration has been changed.

## Requested Run Structure

The requested direction is:

```text
Level 1 -> Laboratory
Level 2 -> Laboratory
Level 3 -> Laboratory
Level 4 (seed 282899) -> Laboratory
Level 5 (fixed seed to be supplied) -> Laboratory
Level 6 (fixed seed to be supplied) -> Laboratory
Level 7 (probably a fixed seed) -> Laboratory
Level 8+ (random, progressively larger) -> Laboratory -> ...
```

The same `Level_Laboratory` scene should be reused between every playable level. Laboratory content is not required for this progression change; its transition and persistent run-state role must still be preserved.

## Level 4 Configuration Captured From the Reference Image

The seed alone does not fully describe a generated map. Reproducing the reference also requires all generation inputs shown in the image:

| Setting | Level 4 value |
|---|---:|
| Grid width | 4 |
| Grid height | 2 |
| POI cells | 2 |
| Blocked cells | 0 |
| Seed | `282899` |
| Randomize seed | Off |
| Workers | 2 |
| Enemies / security guards | 1 |

This should be stored as a dedicated `RunMapConfigSO` asset, tentatively named `Setup Level 4`, rather than changing `Setup Level 1` or `Setup Level 2`.

With the current room-selection rules, two POIs mean POI 1 is Resting and POI 2 is Security. This agrees with the compact map intent documented for small maps.

## What Already Exists

The main architecture needed for the requested flow is already present:

- `RunProgressManager` owns a serialized list of `RunStepDefinition` entries.
- A run step can be a `StaticLevel`, `Laboratory`, or `GeneratedLevel`.
- Generated steps can reference a specific `RunMapConfigSO` through `GeneratedConfig`.
- When configured steps end, `RunProgressManager` alternates `MapGeneration` and `Level_Laboratory` dynamically.
- `RunStepExitTrigger` and `LevelEndVictoryTrigger` call `LoadNextStep()` and do not need to know which kind of scene follows.
- `Level_Laboratory` is already in Build Settings and is represented repeatedly in the run itinerary.
- `LaboratoryProgress` lives on the persistent `RunProgressManager`, so reloading the same laboratory scene does not inherently erase run-wide laboratory state.
- `MapManager.ApplyConfig()` initializes Unity's random state from the configured seed before building the generated map.

The current configured prefab itinerary is:

```text
Level_1 -> Laboratory -> Level_2 -> Laboratory -> Level_3 -> Laboratory
-> MapGeneration (Setup Level 2)
```

After that list, generated maps and laboratory visits alternate automatically.

## Problems Found

### 1. The first generated map is labeled Level 6, not Level 4

`ResolvePlayableLevelIndex()` currently returns:

```csharp
6 + GetGeneratedStepOrdinal(stepIndex)
```

Therefore the generated map after static Level 3 becomes internal Level 6 even though it is the fourth playable level. Laboratory systems also receive this level number as their visit identity. This conflicts with the requested Level 4 seed assignment and should be corrected before adding the new itinerary.

The generated-level base should not be hardcoded to 6. It should either:

- count all playable steps up to the current step; or
- store an explicit playable level number in each step.

Counting playable steps is the simpler and less error-prone choice for the present linear run.

### 2. Dynamic configuration selection repeats an existing config

The prefab currently contains two fallback map configs: `Setup Level 1` and `Setup Level 2`. The first generated itinerary entry explicitly references `Setup Level 2`.

For the following generated step, `GetGeneratedStepOrdinal()` returns 1 and `CurrentConfig` selects `mapConfigs[1]`, which is `Setup Level 2` again. Dynamic creation only starts on a later generated step. This makes the relation between playable level number and config index difficult to reason about.

Fixed Levels 4-7 should reference their assets directly from their `RunStepDefinition`. The random generator should have a separate, explicit base config instead of relying on the ordinal position of legacy fallback assets.

### 3. Later random growth is implicit and not tied clearly to Level 8

`CreateDynamicConfig()` grows from the last item in `mapConfigs`, increasing one grid dimension and some counts, then appending the result. This does create cumulative growth, but:

- its `levelIndex` argument is a generated-config ordinal, not the playable level number;
- it depends on mutable list contents and legacy fallback ordering;
- it uses the global Unity random stream for growth choices;
- its seed comes from `DateTime.Now.GetHashCode()`, which is not an inspectable run seed policy;
- it can increase `blockedCount`, which can make a larger grid feel more obstructed rather than simply broader;
- capacity is clamped only by global maxima, not by the number of cells available after Start and End.

The start of procedural growth should be explicit: fixed configs through Level 7, then dynamic generation beginning at Level 8.

### 4. Direct play support is limited to Level 1 and Laboratory

`GameplaySceneAutoBootstrapper` only auto-creates setup for `Level_1` and `Level_Laboratory`. This does not block the normal itinerary because all generated levels reuse `MapGeneration`, but it is important not to create separate `Level_4`, `Level_5`, etc. scenes merely to hold seeds. These levels should remain data configurations for the existing `MapGeneration` scene.

### 5. The seed must remain a string for current compatibility

`RunMapConfigSO.seed` is a string and `MapManager` calls `cfg.seed.GetHashCode()`. The new Level 4 asset should therefore store `"282899"`, not introduce a numeric seed field as part of this small progression change.

A later determinism hardening pass may replace `string.GetHashCode()` with a stable, explicitly implemented hash. That should be treated as a migration because it can change every existing generated layout.

## Recommended Data Model

Keep one scene for all generated levels and make Levels 4-7 explicit data entries:

| Playable level | Step kind | Scene | Configuration |
|---:|---|---|---|
| 1 | StaticLevel | `Level_1` | Scene-authored |
| - | Laboratory | `Level_Laboratory` | Persistent run state |
| 2 | StaticLevel | `Level_2` | Scene-authored |
| - | Laboratory | `Level_Laboratory` | Persistent run state |
| 3 | StaticLevel | `Level_3` | Scene-authored |
| - | Laboratory | `Level_Laboratory` | Persistent run state |
| 4 | GeneratedLevel | `MapGeneration` | `Setup Level 4`, seed `282899` |
| - | Laboratory | `Level_Laboratory` | Persistent run state |
| 5 | GeneratedLevel | `MapGeneration` | `Setup Level 5`, seed pending |
| - | Laboratory | `Level_Laboratory` | Persistent run state |
| 6 | GeneratedLevel | `MapGeneration` | `Setup Level 6`, seed pending |
| - | Laboratory | `Level_Laboratory` | Persistent run state |
| 7 | GeneratedLevel | `MapGeneration` | `Setup Level 7` if a seed is selected |
| - | Laboratory | `Level_Laboratory` | Persistent run state |
| 8+ | GeneratedLevel | `MapGeneration` | Dynamically generated and growing |

No new Unity scene is required for Levels 4-7.

## Recommended Growth Policy for Level 8+

The first dynamic map should derive from the Level 7 config, or from an explicit `dynamicBaseConfig` if Level 7 remains undecided. Each later generated level should derive from the preceding generated config so growth is monotonic.

Recommended rules:

1. Generate and store one random seed when the level config is created.
2. Increase width or height by one, alternating or using a deterministic choice, until the current maximum of 10 by 10.
3. Increase POIs gradually, never beyond `gridWidth * gridHeight - 2`.
4. Increase workers and enemies gradually within their existing caps.
5. Increase blocked cells more slowly than total area so navigable space still grows.
6. Cache the generated config for the active run so restarting the same current level does not silently produce a different map.
7. Recreate random configs on a new run unless future save requirements say they must persist across runs.

Suggested initial tuning, subject to playtesting:

```text
Every level:  +1 to either width or height
Every level:  +1 POI when capacity permits
Every level:  +1 worker
Every 2 levels: +1 enemy
Every 3 levels: +1 blocked cell
```

This policy makes maps visibly larger while keeping difficulty changes understandable. The existing hard caps can remain as safety limits.

## Laboratory Transition Contract

Every playable level, whether static, fixed-seed generated, or random generated, must be followed by a Laboratory step.

The transition contract should remain:

```text
Playable exit
-> finalize any active laboratory state if applicable
-> capture player run stats and save
-> LoadNextStep()
-> Level_Laboratory
-> laboratory exit performs the same save/transition contract
-> next playable step
```

The laboratory should remain a scene step, not a generated POI room. It is already excluded from generated-room selection, which is correct.

Although laboratory gameplay is not required now, keeping the step is important because current and planned systems attach persistent consequences to each visit. The itinerary must not optimize away an "empty" laboratory visit.

## Implementation Plan After Seed Selection

1. Create `Setup Level 4.asset` using the exact values from the reference image.
2. Add explicit generated steps for Level 4 and its following Laboratory visit to the `RunProgressManager` prefab.
3. Replace the hardcoded generated-level base of 6 with playable-step counting and add tests covering Levels 1-8.
4. Create `Setup Level 5.asset` and `Setup Level 6.asset` after their seeds and complete grid/count settings are supplied.
5. If selected, create `Setup Level 7.asset`; otherwise agree whether Level 7 is the first random level or whether it needs a temporary fixed config.
6. Insert a Laboratory step after each fixed generated level.
7. Separate dynamic generation from the legacy `mapConfigs` indexing, preferably with an explicit `dynamicBaseConfig` and first dynamic playable level of 8.
8. Add validation for POI and blocked-cell capacity before generating a map.
9. Preserve the existing `MapGeneration` scene and all generated-room prefab mappings.
10. Run Edit Mode tests and perform a play-through of the complete itinerary.

## Test Coverage Needed

### Edit Mode

- The playable level number counts only `StaticLevel` and `GeneratedLevel` steps.
- Laboratory after Level 4 retains playable level number 4 for visit tracking.
- Level 4 resolves to `Setup Level 4` and seed `282899`.
- Levels 5-7 resolve to their explicit config assets.
- Level 8 is the first dynamic config.
- Every resolved playable step is followed by Laboratory.
- Re-requesting the same dynamic level during a run returns the cached config and seed.
- Dynamic dimensions and counts never exceed caps or cell capacity.
- A new run clears old dynamic configs and produces a new random sequence.

### Play Mode / Manual

- Complete `Level_3 -> Laboratory -> Level 4` and verify the reference layout.
- Complete `Level 4 -> Laboratory -> Level 5` and verify no laboratory is skipped.
- Continue through Level 8 and verify the generated map grows.
- Verify player stats and laboratory progress survive every scene transition.
- Verify replay/restart behavior does not unexpectedly change the current fixed-seed map.

## Information Still Needed

For Levels 5 and 6, and probably Level 7, capture all fields shown in the generator UI, not only the seed:

- width;
- height;
- POI cells;
- blocked cells;
- workers;
- enemies;
- seed;
- confirmation that Randomize is off.

Until those values are available, the implementation can safely add Level 4, but placeholders for Levels 5-7 should not guess map sizes or population counts.

## Acceptance Criteria

- Level 4 uses a dedicated config with seed `282899` and the reference image's full settings.
- The internal playable level number is 4, not 6, when that map is active.
- The run always enters `Level_Laboratory` between playable levels.
- Levels 5-7 can receive fixed configs without creating new scenes or changing transition code.
- Level 8 and later use cached random configs that become progressively larger within safe limits.
- Static Levels 1-3 and the existing generated-room composition remain unchanged.

