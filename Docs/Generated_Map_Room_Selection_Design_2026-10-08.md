# Generated Map Room Selection Design - 2026-10-08

## Status

Implementation-ready design. This document defines the intended generated-room selection behavior but does not change runtime code or prefab configuration.

## Goal

Preserve the current compact composition of small generated maps while allowing larger maps, which have more POI slots, to include the wider set of authored room prefabs.

Small maps must remain focused on Reception and Security. Additional room purposes become available only from the sixth POI onward. This makes the extra rooms additional destinations on maps that have space for them instead of mandatory rooms that crowd small layouts.

The number of POIs continues to come from `RunMapConfigSO.poiCount`. Room-selection logic must not silently raise a small map's configured POI count.

## Terminology

- **Grid cell:** One position in the generated rectangular map.
- **Work cell:** The default purpose assigned when the grid is created.
- **POI:** A cell selected as a point of interest and connected to Start by the path solver.
- **POI slot:** The one-based selection position within the configured POIs. For example, POI 1 is the first selected POI.
- **Room type:** The authored purpose/prefab rendered in a POI cell, such as Reception, Security, or Furnace.
- **Unused room type:** An eligible room type that is not yet present among the selected POIs in the current map.

## Existing Generation Order to Preserve

The high-level grid-generation order remains:

1. Create every grid cell as `UsageType.Work`.
2. Place Start and End in opposite corners.
3. Select up to the configured number of eligible Work cells as POIs.
4. Assign a room type to every selected POI according to the rules in this document.
5. Generate an independent path from Start to every POI and to End.
6. Convert remaining eligible Work cells into blocked cells up to `blockedCount`.
7. Run the existing room processors and render the selected prefabs.

Start, End, path, blocked, and remaining Work cells are not part of the POI room-type randomization.

## POI Room Selection Rules

Room selection uses the POI's one-based slot:

| POI slot | Selection rule |
|---|---|
| 1 | Always Reception |
| 2 | Always Security |
| 3-5 | Randomly select Reception or Security |
| 6+ | Randomly select from the complete eligible generated-room pool, prioritizing room types not yet present on the map |

These rules imply:

- A map with at least one POI always contains Reception.
- A map with at least two POIs always contains Security.
- POI 2 is the Security guarantee; a second special Security insertion is not required.
- Larger maps may contain additional Reception and Security rooms because both remain members of the POI 6+ pool.
- The first five POIs preserve the compact-map identity even when the map contains more than five POIs.

## Eligible Generated-Room Pool

For POI 6 and later, the complete pool is:

- Reception
- Security
- Resting
- Spawning
- Garage
- Conveyor
- Furnace
- Junks
- Deads
- Cube Collector

The corresponding authored prefabs currently are:

- `ROOM_Reception.prefab`
- `ROOM_security.prefab`
- `ROOM_resting.prefab`
- `ROOM_Spawning.prefab`
- `ROOM_Garage.prefab`
- `ROOM_Conveyor.prefab`
- `ROOM_Furnace.prefab`
- `ROOM_Junks.prefab`
- `ROOM_Deads.prefab`
- `ROOM_CubeCollector.prefab`

Laboratory is explicitly excluded from procedural room selection. Neither `ROOM_Laboratory_1.prefab` nor `ROOM_Laboratory_2.prefab` may be selected by this system.

The following are also not members of the randomized POI pool because they have structural roles rather than optional POI purposes:

- Start
- End
- Work
- Base
- Lift
- Laboratory

## Prefer Variety Before Duplicates

POIs 6 and later must prefer a room type that is not already present among the map's POIs.

Selection procedure for each POI slot from 6 onward:

1. Build the complete eligible generated-room pool.
2. Determine which types from that pool are not present in the current map.
3. If at least one unused type exists, randomly choose among the unused types only.
4. If every eligible type is already present, randomly choose from the complete pool and allow duplicates.
5. Record the chosen type before selecting the next slot.

This is priority-based uniqueness, not an absolute uniqueness constraint. A duplicate is valid once no unused eligible type remains.

Reception and Security are already present when a map reaches POI 6, so the first POI 6+ selections will naturally prefer the other room types. Reception and Security become possible again after every eligible type has appeared.

### Example

For a map with eight POIs:

```text
POI 1: Reception
POI 2: Security
POI 3: Reception or Security
POI 4: Reception or Security
POI 5: Reception or Security
POI 6: An eligible type not already present
POI 7: Another eligible type not already present
POI 8: Another eligible type not already present
```

POIs 6-8 should therefore introduce new room purposes rather than add another Reception or Security, assuming unused eligible types remain.

## Minimum Counts and Graceful Small-Map Behavior

There is no global minimum that automatically changes `poiCount`. The configured count determines which rules can be reached:

| Configured `poiCount` | Result |
|---:|---|
| 0 | No POIs |
| 1 | Reception only |
| 2 | Reception, then Security |
| 3 | Reception, Security, then one random Reception/Security |
| 4 | Reception, Security, then two random Reception/Security rooms |
| 5 | Reception, Security, then three random Reception/Security rooms |
| 6+ | The first five rules above, followed by the complete room pool with variety preferred |

The Security guarantee applies when the requested and available POI count is at least two. A one-POI map remains valid and does not need to create an extra cell or replace Reception with Security.

The current configuration intent is therefore preserved:

- `Setup Tutorial`: one POI, Reception only.
- `Setup Level 1`: two POIs, Reception and Security.
- `Setup Level 2`: three POIs, Reception, Security, and one random Reception/Security.
- `Setup StressTest`: five POIs, using only Reception and Security.

To exercise the expanded room pool, a configuration must request at least six POIs and have enough eligible cells to create them.

## Capacity and Invalid Configuration Rules

Start and End cannot also be POIs. The generator must calculate the number of eligible Work cells after assigning distinct endpoints.

```text
maximumPOICount = number of eligible Work cells after Start and End
effectivePOICount = clamp(configured poiCount, 0, maximumPOICount)
```

Required behavior:

- A negative `poiCount` is invalid and should be treated as zero with a clear warning or rejected during configuration validation.
- If `poiCount` exceeds available Work cells, create only the available number and issue a clear warning containing the requested and effective counts.
- Do not overwrite Start or End to satisfy a POI request.
- Do not increase the grid dimensions automatically.
- Do not increase `poiCount` automatically to force Reception or Security.
- The grid must have enough cells to assign distinct Start and End positions. A grid that cannot do so is an invalid map configuration and should fail clearly rather than generate ambiguous endpoints.
- `blockedCount` continues to consume only Work cells remaining after POI selection and path generation. If fewer eligible Work cells remain than requested, the existing effective blocked count may be lower than `blockedCount`.

The selection rule is based on the effective number of successfully selected POI positions. For example, if six POIs are requested but capacity allows only four, the result uses rules for POIs 1-4 and does not attempt a POI 6 room.

## Randomness and Seed Behavior

Room choice must use the same seeded Unity random sequence as the rest of map generation so the same configuration and seed reproduce the same map.

Implementation must avoid unordered collection enumeration as an input to random selection. Eligible room types should be held in a stable serialized or code-defined order before choosing a random index. This keeps results reproducible across runs and reduces accidental seed changes caused by dictionary or set ordering.

Exact generated layouts for existing seeds may change when this feature is introduced because room selection will consume additional random values. Reproducibility is required after the new algorithm is established; preserving every historical seeded layout is not a requirement unless separately requested.

## Proposed Data Model

The current `POIType` contains only `None`, `Reception`, and `Security`. It must be extended so every eligible generated room has an explicit identity.

Recommended enum direction:

```csharp
public enum POIType
{
    None,
    Reception,
    Security,
    Resting,
    Spawning,
    Garage,
    Conveyor,
    Furnace,
    Junks,
    Deads,
    CubeCollector
}
```

Append new values after existing values. Do not reorder `None`, `Reception`, or `Security`, because Unity serializes enum values numerically in scenes and prefabs.

`MapManager` needs a prefab mapping for every eligible `POIType`. The mapping must be validated before generation so a selected type cannot silently fall back to an unrelated prefab.

The first implementation can keep the selection policy in code because the slot rules are explicit. If designers later need weights, per-level pools, or unlock conditions, the room catalogue can move to serialized configuration without changing the core selection contract.

## Recommended Responsibility Split

### `GridFactory`

- Select eligible POI positions.
- Assign POI room types in slot order.
- Enforce Reception/Security thresholds.
- Prefer unused types for POIs 6+.
- Store the selected `POIType` on each cell before path solving.

### `MapManager`

- Expose prefab references for every supported POI type.
- Build and validate the complete `POIType -> GameObject` mapping.
- Pass the mapping to the renderer.

### `GridRenderer`

- Render the exact prefab mapped to the cell's `POIType`.
- Report a clear error for a missing selected-type mapping rather than substituting an incorrect room silently.

### `RunMapConfigSO`

- Continue to provide grid dimensions, `poiCount`, `blockedCount`, enemy counts, and seed.
- Does not need a new minimum-POI field for this design.

## Compatibility Considerations

Every newly generated room prefab must remain compatible with the generated-room lifecycle:

- contain the expected `RoomManager` and `RoomProperties` components;
- support grid-assigned doors and lifts;
- expose appropriate room waypoints;
- tolerate rotation/placement at any eligible grid coordinate;
- register its machines and waypoints through existing factory initialization;
- use `UsageType.POI` with the selected `POIType` after `GridManager.AssignRoomProperties`;
- have a valid minimap presentation;
- avoid dependencies that only exist in hand-authored static scenes.

Prefab compatibility must be verified separately from selection logic. Being present in the room pool does not by itself guarantee that the prefab functions correctly in a generated map.

## Implementation Sequence

1. Append the new explicit values to `POIType` without renumbering existing values.
2. Extract or add a deterministic POI room-type selector implementing the slot rules.
3. Update `GridFactory.AssignPOICells` to use that selector.
4. Add serialized prefab references and complete the mapping in `MapManager`.
5. Validate missing mappings with actionable error messages.
6. Inspect every eligible prefab for generated-room compatibility and correct any prefab-specific setup deliberately.
7. Add focused Edit Mode tests.
8. Run the complete Edit Mode suite.
9. Generate representative maps with POI counts 0, 1, 2, 5, 6, and more than the pool size, then inspect them in Unity.

## Automated Test Plan

Add focused Edit Mode tests covering:

1. **Zero POIs**
   - No cell is assigned `UsageType.POI`.

2. **One POI**
   - Exactly one POI exists.
   - Its type is Reception.

3. **Two POIs**
   - POI 1 is Reception.
   - POI 2 is Security.
   - Exactly one Security is guaranteed.

4. **POIs 3-5**
   - Every type in those slots is Reception or Security.
   - No expanded-pool type appears in the first five slots.

5. **Sixth POI unlock**
   - Slot 6 uses the complete eligible pool.
   - Because Reception and Security already exist, it prefers a room type not yet present.
   - Laboratory is never selected.

6. **Variety before duplicates**
   - Slots 6+ do not repeat an existing type while another unused eligible type remains.
   - Duplicates become possible only after the complete eligible pool is represented.

7. **Security guarantee**
   - Every generated assignment with an effective POI count of at least two contains Security.
   - An effective count of one remains Reception only.

8. **Capacity limits**
   - Start and End are never replaced by POIs.
   - A request larger than available capacity produces the effective maximum without throwing.
   - Selection rules use the effective slot count.

9. **Determinism**
   - The same valid seed and configuration produce the same POI positions and types.

10. **Prefab mapping completeness**
    - Every eligible generated `POIType` has a non-null prefab mapping.
    - Neither Laboratory prefab is in the generated mapping.

11. **Path compatibility**
    - Every selected POI remains a path target.
    - End remains a path target.
    - Room-type selection does not alter the independent Start-to-target path contract.

Run all Edit Mode tests using the repository test script:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/run-editmode-tests.ps1
```

## Manual Acceptance Test

1. Generate a map with zero POIs and confirm no POI room is present.
2. Generate maps with one and two POIs and confirm the exact Reception/Security thresholds.
3. Generate several five-POI maps with different seeds and confirm only Reception and Security appear.
4. Generate several six-or-more-POI maps and confirm new room types begin at slot 6.
5. Generate a map large enough to select many extra rooms and confirm new types appear before duplicates.
6. Confirm no generated map contains either Laboratory prefab.
7. Confirm every POI and End are reachable from Start.
8. Enter every generated room type and verify doors, lifts, minimap visuals, waypoints, machines, and room initialization.
9. Regenerate twice with the same seed and configuration and confirm the POI layout and types match.

## Acceptance Criteria

The feature is complete when:

- small-map POI counts retain the documented Reception/Security composition;
- Security is guaranteed whenever at least two POIs can be created;
- expanded room types appear only from POI 6 onward;
- all eligible types except Laboratory participate in the large-map pool;
- unused eligible types are selected before duplicates;
- duplicates are allowed after every eligible type is already represented;
- requested POI counts are bounded safely by grid capacity without changing Start or End;
- the same seed and configuration produce the same result;
- all selected room prefabs initialize and function in generated maps;
- automated Edit Mode tests and the manual acceptance pass succeed.
