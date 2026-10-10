# Generated Map Room Selection Design

## Status

Approved design for the generated-map room sequence, morality weighting, and removal of legacy blocked cells.

## Generation Contract

Generated maps use `RunMapConfigSO.poiCount` as their requested POI count. Start and End remain distinct structural cells. Every other generated cell begins as Work, and no cell is converted to `UsageType.Blocked`.

The old `blockedCount` configuration parameter and the Blocked Cells setup control are removed. The authored `ROOM_Spawning` POI replaces the gameplay purpose previously provided by blocked-room spawning areas.

## Fixed POI Sequence

| POI slot | Room type |
|---:|---|
| 1 | Resting |
| 2 | Reception |
| 3 | Security |
| 4 | Spawning |
| 5 | Garage |
| 6 | Conveyor |
| 7+ | Morality-weighted random room |

Reception, Garage, and Conveyor appear only in their fixed slots. A map whose effective POI count is smaller than a fixed slot simply does not contain that room; the generator does not increase `poiCount` automatically.

## Random Pool From POI 7

Slots 7 and later select from Resting, Security, Spawning, Furnace, Junks, Deads, and Cube Collector. Reception is excluded so it can spawn only once. Garage and Conveyor are excluded so their functional pair remains unique. Duplicates from the seven-room random pool are allowed.

Laboratory, Start, End, Work, Base, and Lift are structural or separately authored rooms and are never random POIs.

## Morality Weighting

The generator reads persistent `PlayerRunStats.Morality` before selecting POI room types.

- `0` is neutral and gives all seven random types equal weight.
- `+5` or higher is fully good.
- `-20` or lower is fully bad.
- Intermediate values interpolate smoothly.

Good morality strongly favors Security and Cube Collector, favors Junks, and reduces Resting, Spawning, Furnace, and Deads. Bad morality strongly favors Spawning and Furnace, favors Deads, and reduces Resting, Security, Junks, and Cube Collector.

All weights remain positive, so morality changes probability rather than forbidding a type. The same seed, configuration, and morality must reproduce the same choices.

## Spawning-Room Behavior

Every generated Spawning POI provides a worker-spawner location. Enemy initialization creates one worker-spawner robot per Spawning room. Waypoint queries target `UsageType.POI` rooms whose `POIType` is `Spawning`, replacing the old `blockedCount` and blocked-room behavior.

## Capacity and Configuration

Start and End cannot also be POIs. Effective POI count is the configured count clamped to the Work cells available after Start and End. Negative requests produce zero POIs and a warning; requests above capacity are clamped with a warning. Remaining non-path, non-POI cells stay as Work cells.

`RunMapConfigSO` no longer contains `blockedCount`. The run-setup screen no longer displays or validates a Blocked Cells field. Dynamic level growth may increase dimensions, POIs, workers, and enemies, but never creates blocked cells.

## Automated Coverage

Edit Mode tests must verify the fixed slots, unique Reception/Garage/Conveyor rooms, allowed slot-7+ pool, morality thresholds and tendencies, seeded determinism, absence of generated Blocked cells and blocked configuration, and worker-spawner routing to Spawning POIs.

## Acceptance Criteria

The fixed sequence, morality-weighted random pool, Spawning POI behavior, and blocked-cell removal must be consistent across runtime code, configuration assets, setup UI, tests, and progression documentation.
