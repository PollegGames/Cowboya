# Run Progression: Static Level 1, Laboratory, and Generated Levels

This document describes the needed structure for the new run flow before implementation. It is a planning document only; it does not change runtime behavior yet.

## Goal

The game needs one clear run entry point that can start from the main menu, move through hand-built scenes, enter laboratory scenes between levels, and eventually fall back to the existing generated map system.

The new first playable level is `Assets/Scenes/Level_1.unity`, the deads/furnace/garbage scene. The laboratory sketch scene is `Assets/Scenes/Level_Laboratory.unity`. The current generated map scene is `Assets/Scenes/MapGeneration.unity`.

The structure should support both:

- Starting a full run from `MenuScene` by clicking Play.
- Opening a gameplay scene directly in the Unity Editor and pressing Play, with the scene still creating the player, camera, UI, and required managers.

## Current Situation

The current main menu flow is:

`MenuScene -> MainMenuController.OnPlayClicked -> RunProgressManager.LoadFirstLevel() -> MapGeneration`

`RunProgressManager` currently assumes that the normal run scene is `MapGeneration`. It tracks a numeric `currentLevelIndex`, gives generated map config through `CurrentConfig`, and loads the next generated level through `LoadNextLevel()`.

`MapGeneration` uses a `SceneGameSetup` object with:

- `SceneBootstrapper`
- `SceneInitiator`
- `SceneBootstrapConfigSO`

That setup currently builds a generated map, initializes the factory, spawns the player at the generated start room, initializes enemies, and wires UI/minimap/camera.

This works for the generated map, so the new design should avoid breaking that path. The safer approach is to keep the generated setup intact and add a run-flow layer above it.

## Target Run Order

The intended progression is:

1. Static Level 1: `Level_1`
2. Laboratory: `Level_Laboratory`
3. Static Level 2: not yet in project
4. Laboratory
5. Static Level 3: not yet in project
6. Laboratory
7. Static Level 4: not yet in project
8. Laboratory
9. Static Level 5: not yet in project
10. Laboratory
11. Level 6: current generated-map gameplay starts here
12. Laboratory
13. Generated Level 7
14. Laboratory
15. Generated Level 8+

For now, only `Level_1`, `Level_Laboratory`, and `MapGeneration` exist. Missing scenes should be represented in data later, and the implementation should produce a clear error instead of silently skipping unavailable scenes.

## Level 1 Requirements

`Level_1` is a hand-built scene with three rooms:

- `ROOM_Deads`
- `ROOM_Furnace`
- `ROOM_Garbage`

The first implementation step should make it runnable without needing the generated map systems.

Expected behavior:

- Player spawns in the middle of `ROOM_Deads`.
- Player starts with no energy.
- Player starts in faint mode.
- Any movement input, left/right/up/down, begins the normal recharge behavior.
- No normal enemies are needed yet.
- No normal generated-map spawning is needed yet.
- The existing right-side escape trigger can be used as the first temporary level-exit trigger.

Later behavior, not part of the first setup pass:

- A dead-robot spawn prefab should spawn enemy robots already in dead state every 10 seconds.
- Furnace/conveyor scripts should process garbage and dead robot pieces, similar to the existing cube conveyor logic.

## Laboratory Requirements

`Level_Laboratory` is currently a sketch scene with exit doors only. Its purpose is to test:

`menu -> level -> laboratory -> next level`

The laboratory should eventually be the place where run state, upgrades, and transitions are handled between levels. For the first pass, it only needs to load correctly and exit to the next itinerary step.

## Design Direction

Add a run itinerary layer to `RunProgressManager` instead of hardcoding one normal scene name for every level.

Recommended data model:

```csharp
public enum RunStepKind
{
    StaticLevel,
    Laboratory,
    GeneratedLevel
}

[System.Serializable]
public class RunStepDefinition
{
    public RunStepKind Kind;
    public string SceneName;
    public RunMapConfigSO GeneratedConfig;
}
```

`RunProgressManager` should own a serialized list of run steps. Example:

```text
0: StaticLevel    Level_1
1: Laboratory     Level_Laboratory
2: GeneratedLevel MapGeneration
```

During development this can be shorter than the final game order. Later, static Level 2-5, generated levels, and repeated laboratory steps can be inserted into the list without changing menu or trigger code.

## Responsibilities

`RunProgressManager`

- Owns the run itinerary.
- Starts a new run from the first itinerary step.
- Loads the next itinerary step.
- Tracks the current run step and current playable level number.
- Provides generated map config only when the current step is a generated level.
- Keeps run-wide player stats and save continuity.

`SceneController`

- Remains a thin scene loading service.
- Should not decide which scene comes next.

`SceneBootstrapper`

- Creates shared services needed by a scene.
- Should not assume every scene needs generated-map services.
- Should support different scene setup modes, either through separate config assets or a scene-mode field.

`SceneInitiator`

- Initializes the current scene based on the scene setup mode.
- Generated map scenes can keep the existing generated-map path.
- Static scenes should use scene-authored spawn points and room objects instead of building a map.

`LevelEndVictoryTrigger` or future exit trigger

- Should call `RunProgressManager.LoadNextStep()` or equivalent.
- Should not know whether the next scene is a lab, static level, or generated level.

## Direct Scene Play Requirement

Each gameplay scene should be runnable directly from the Unity Editor.

If `Level_1` is opened and Play is pressed:

- The scene should ensure `RunProgressManager` exists.
- If no run is active, the manager should create an editor/default run context for this scene.
- The scene should bootstrap camera, event system, UI, player spawner, and save service.
- The scene should spawn the player at its local static spawn point.

This avoids relying on `MenuScene` for setup and follows Unity-friendly scene ownership.

## Proposed Static Scene Setup

Create a static-level setup path separate from generated map initialization.

Possible components:

- `StaticLevelSceneSetup`
- `StaticLevelSpawnPoint`
- `StaticLevelExitTrigger`
- `SceneBootstrapConfigSO` extended with a setup mode, or a new config type for static scenes

`Level_1` should contain a spawn point object inside `ROOM_Deads`, for example:

`ROOM_Deads/PlayerSpawnPoint`

The static setup should resolve that transform and pass its position to `PlayerSpawner`.

## Avoiding Generated Map Breakage

The current `MapGeneration` flow should stay compatible:

- `SceneInitiator.InitializeFactory()` can keep building the generated map when the scene setup mode is generated.
- `MapManager.BuildFromConfig()` should only be required for generated scenes.
- Enemy spawning should remain disabled or skipped for static Level 1 until static enemy rules are intentionally added.
- `RunProgressManager.LoadFirstLevel()` should load the first itinerary scene instead of directly loading `MapGeneration`.

This keeps the working generated map scene available as the generated-level implementation, instead of rewriting it for static scenes.

## Decisions

- Every laboratory visit uses the same `Level_Laboratory` scene. If the player changes something there, the same laboratory state should be available on the next visit.
- The game does not need to show level numbers on screen. Internally, the first generated map is placed at Level 6 in the run order.
- Missing future scenes should produce a clear error and stop the run instead of being skipped.
- Static levels use exit triggers that are always allowed during early testing.

## Suggested Implementation Phases

1. Add run step data to `RunProgressManager`.
2. Change menu Play to start the first run step, not the hardcoded generated scene.
3. Add direct-scene-play fallback so `Level_1` and `Level_Laboratory` can bootstrap without the menu.
4. Add static-level setup support for player spawn and no generated map build.
5. Wire `Level_1` to spawn the player in `ROOM_Deads`.
6. Add temporary exit flow from `Level_1` to `Level_Laboratory`.
7. Add temporary exit flow from `Level_Laboratory` to the next available step.
8. Preserve the existing generated `MapGeneration` setup and test it after the static/lab flow.
9. Add Level 1 special starting state: zero energy, faint mode, recharge resumes on first movement.
10. Later: dead robot spawner and furnace/conveyor garbage processing.

## Done Criteria For First Pass

- Clicking Play in `MenuScene` loads `Level_1`.
- Pressing Play directly in `Level_1` creates the player and starts in the same intended state.
- Player spawns in `ROOM_Deads`.
- Exiting `Level_1` loads `Level_Laboratory`.
- Exiting `Level_Laboratory` loads the next available configured step.
- `MapGeneration` still works when reached by the itinerary or opened for testing.
- The code path for "what scene comes next" lives in one place: `RunProgressManager`.

## Implementation Notes

The first implementation route is:

`Level_1 -> Level_Laboratory -> MapGeneration`

Generated levels continue through the same run-step path, with the laboratory inserted between generated map visits.

Primary implementation classes:

- `RunProgressManager`: owns `RunStepDefinition` itinerary data and scene progression.
- `SceneBootstrapConfigSO`: exposes `SceneSetupMode`.
- `GameplaySceneAutoBootstrapper`: creates `SceneGameSetup` automatically for `Level_1` and `Level_Laboratory` when no scene setup exists.
- `SceneInitiator`: branches between generated map setup and static/laboratory setup.
- `StaticLevelSpawnPoint`: optional marker for hand-built scene player spawn points.
- `FirstMovementRechargeGate`: re-enables recharge after the first movement input in `Level_1`.
- `RunStepExitTrigger`: always-allowed trigger for static/laboratory scene exits.

Current static spawn fallback:

- `Level_1` uses `StaticLevelSpawnPoint` if present, otherwise `ROOM_Deads`.
- `Level_Laboratory` uses `StaticLevelSpawnPoint` if present, otherwise `ROOM_Laboratory`.
