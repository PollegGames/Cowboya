# Generated Map Worker Population

## Architecture reconciliation 2026 10 11

This document describes the current generated-map population behavior. It is not the target ownership model for the factory and robot AI refactor.

The current implementation still creates normal robots in `EnemiesSpawner`, creates generated Worker Collectors through `WorkerCollectorGarageSpawnProvider`, and creates static Worker Collectors through `WorkerCollectorMissionService.SpawnStaticSceneCollector`. These paths must converge behind one `RobotPopulationManager` initialization contract. Room providers may describe spawn locations and capabilities, but they must not remain independent robot lifecycle owners.

`WorkerCollectorMissionService` also currently combines scene-scoped provider discovery, stable route assignment, cube claims, garage-slot reservations, garage binding, and static spawning through global static collections. The refactor moves stable job allocation and shared claims into a scene-scoped factory work coordinator. Brain remains responsible for choosing the next collector action from Memory.

For the staged migration and ownership map, see `Factory_Robot_Population_AI_Refactor_Plan_2026-10-11.md`.

## Scope

Levels 1, 2, and 3 are fixed scenes and do not use this generated-map population policy.

Generated Levels 4 and 5 use explicit values from their `Setup Level N` assets:

| Level | Total `workersCount` | Normal Workers | Worker Collectors | Collector resting |
| --- | ---: | ---: | ---: | --- |
| 4 | 3 | 3 | 0 | Not applicable |
| 5 | 4 | 2 | 2 | Enabled; the collector team rests after each batch |

Level 4 now uses the former Level 5 configuration: seed `867059`, a 4x2 grid, and 3 POIs. Those fixed slots are Resting, Reception, and Security. Level 5 now uses the former Level 6 configuration: seed `712226`, a 3x4 grid, and 6 POIs. Its guaranteed Resting, Garage, and Conveyor rooms support the two-collector team.

Beginning with playable Level 6, maps use cached dynamic configurations derived from Level 5. New Level 6 receives the same growth step that the former Level 7 used, and every later dynamic configuration is shifted down by one playable level. Worker counts are derived from the rooms that map generation actually created; inherited numeric worker fields are not used for dynamic population.

The Setup Sandbox/custom generator treats `workersCount` as a total population. If its generated map contains at least one Resting room and a complete Garage/Conveyor pair, half of that total becomes Worker Collectors and the remainder stays normal Workers. Normal Workers receive the extra unit for odd totals. Without all three required room capabilities, the custom population is entirely normal Workers.

## Level 6+ rules

After the map and its `RoomManager` objects exist, `GeneratedWorkerPopulation.Calculate` counts room roles and applies these rules:

| Generated room condition | Added normal Workers | Added Worker Collectors |
| --- | ---: | ---: |
| Each `UsageType.Work` room | 1 | 0 |
| Each `POIType.Resting` room | 1 | 1 |
| Each complete Garage + Conveyor pair | 0 | 1 |

A pair requires both room types. The number of pair-based collectors is `min(garage count, conveyor count)`, so an unmatched Garage or Conveyor does not create a collector.

The final formulas are:

```text
normal workers = work rooms + resting rooms
worker collectors = resting rooms + min(garage rooms, conveyor rooms)
```

Level 6+ Worker Collectors use their normal rest-after-batch cycle.

## Cooperative collector batches

Collectors assigned to the same Garage work concurrently. Each collector claims its own white cube and Garage slot, so collectors can fill the same Garage batch together without claiming the same cube.

When the Garage processor completes a batch, `WorkerCollectorDropOffProvider` notifies every collector assigned to that Garage. Each collector releases any in-progress claim or reservation, stops its current collection action, travels to the selected Resting room, and completes the rest phase. The team then begins collecting the next batch again.

## Runtime flow

```text
GridFactory generates cells and POIs
    -> RoomRenderer creates RoomManager/RoomProperties objects
    -> SceneInitiator.InitializeEnemies runs
        -> Levels 4-5: read fixed counts from RunMapConfigSO
        -> Level 6+: GeneratedWorkerPopulation counts live rooms
    -> EnemiesSpawner.CreateWorkers creates normal Workers
    -> EnemiesSpawner.SpreadEnemies assigns Work/Rest waypoints
    -> EnemiesSpawner.SpawnDedicatedRoomRobots creates Worker Collectors
    -> WorkerCollectorGarageSpawnProvider binds collectors to the Garage
    -> WorkerCollectorMissionService assigns Conveyor, Garage, and optional Rest routes
```

Spawning-room Worker Spawners are separate from the normal Worker and Worker Collector formulas.

Reception robots use the `SecurityReception` role and are not enemies. They are spawned independently, one per valid Reception work post.

Regular enemy `SecurityGuard` robots are room-derived: each generated Security room creates exactly one Security Guard. Fixed Levels 4, 5, and 6 each include a Security room and configure one enemy. The fixed setup assets mirror these expected counts, but runtime generation uses the rooms that actually exist.

## Configuration ownership

- `RunMapConfigSO.workersCount`: total fixed Worker population for Levels 4-5 and custom configurations.
- `RunMapConfigSO.workerCollectorsCount`: how many members of that total use the Worker Collector role. Normal Workers are `workersCount - workerCollectorsCount`.
- `RunMapConfigSO.workerCollectorsRestAfterBatch`: fixed-level collector rest policy.
- `SceneInitiator.ResolveWorkerPopulation`: selects fixed mode below Level 6 or room-derived mode at Level 6+.
- `GeneratedWorkerPopulation`: contains the room-count formula independently of spawning.
- `EnemiesSpawner`: creates the requested robot quantities after map generation.
- `WorkerCollectorGarageSpawnProvider`: instantiates multiple collectors and binds their Garage ownership before activation.

## Current implementation constraints

The current generated POI order places Resting at slot 1, Garage at slot 5, and Conveyor at slot 6 once the map has at least six POIs. Some spawning code relies on that ordering. This is a migration constraint, not an architectural contract: job and spawn selection must move to registered room capabilities rather than hard-coded slot identifiers.

All Worker Collectors currently use the primary generated Garage provider. This supports the current generator, which permits at most one Garage and one Conveyor. If future generation allows multiple Garage/Conveyor pairs, collector ownership should be distributed to each pair rather than only increasing the primary Garage's collector count.

Worker Collectors spawned because of Resting rooms still require the generated Garage/Conveyor pipeline to perform useful collection work. Level 6+ maps satisfy that dependency because they derive from the six-POI Level 5 layout.

## Tests

`GeneratedWorkerPopulationTests` verifies the formulas, complete-pair requirement, and defensive handling of invalid negative counts. Changes to room-derived spawning should update those tests and run the Edit Mode suite.
