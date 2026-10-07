# Static Level Robot Pathfinding Design

Status: implementation-ready design. No runtime, scene, or prefab changes are included in this document.

Date: 2026-08-27.

## 1. Purpose and scope

The authored/static levels need robot navigation, while generated maps already build navigation automatically. Static levels are intentionally simpler: their valid routes are known while authoring the scene and do not change during a run.

The static solution must:

- reuse the existing `WaypointService`, `WaypointPathFinder`, `RoomWaypoint`, and robot movement interfaces;
- leave generated-map navigation behavior unchanged;
- allow level designers to author a main start-to-end route;
- allow additional routes branching toward POI rooms;
- support bidirectional and one-way routes;
- draw the authored graph in the Scene view;
- remain independent of robot role, mission, machine, or destination logic.

The static path system only defines and supplies navigation topology. Workers, guards, bosses, collectors, and future robot types all consume the same graph.

## 2. Confirmed current behavior and failure

The existing navigation system is a waypoint graph, not a Unity NavMesh:

- `RobotBodyController` delegates destination requests to `WaypointPathFollower`.
- `WaypointPathFollower` asks `IWaypointQueries.FindWorldPath` for a route.
- `WaypointPathFinder` performs breadth-first search over `RoomWaypoint.Neighbors`.
- `WaypointService` owns registration and exposes pathfinding to robots.

Static levels currently do not supply that graph:

1. `SceneBootstrapper` instantiates the configured `WaypointService` only for `GeneratedMap` setup.
2. `FactoryManager.InitializeStatic` explicitly stores a null waypoint service.
3. `RoomManager.InitializeStatic` therefore cannot register its waypoints with a service.
4. No explicit static connections are applied to `RoomWaypoint.Neighbors`.
5. Static robots do not receive the shared navigation service through a general static initialization step.

There is one additional integration constraint. `WaypointPathFollower` can call `BuildAllNeighbors(true)` when a destination includes unavailable waypoints. The call does not first check whether unavailable waypoints actually exist. The current generated builder clears all neighbors before applying generated axis and same-room connections. Consequently, a static graph applied only once could later be erased and replaced by generated-map connections.

Static levels do not need availability-driven topology changes. Their authored route remains fixed for the entire run. Nevertheless, the shared `BuildAllNeighbors` API must behave safely when called in a static level.

## 3. Topology mode

Add an explicit graph-building mode:

```csharp
public enum WaypointGraphMode
{
    Generated,
    StaticExplicit
}
```

The mode belongs to the waypoint graph/service setup, not to individual robots.

### 3.1 Generated mode

`Generated` preserves the current behavior:

- clear existing neighbors;
- apply horizontal axis connections;
- apply directional lift connections;
- connect waypoints using the existing generated-room rules;
- honor waypoint availability as it does today.

No generated-map behavior should change as part of the static implementation.

### 3.2 Static explicit mode

`StaticExplicit` uses only the routes authored in the static scene:

- clear existing neighbors;
- connect consecutive waypoints from every configured static route;
- apply the configured direction for each route;
- never infer links from position, shared axes, or room membership;
- include every valid authored waypoint and connection.

Because static topology is immutable during a run, `includeUnavailable` does not alter the static graph. Calling either `BuildAllNeighbors(false)` or `BuildAllNeighbors(true)` rebuilds the same complete authored topology.

This keeps `BuildAllNeighbors` safe for all existing callers without adding static-level conditions to robot code.

## 4. Static route authoring

### 4.1 Scene component

Each static level that needs navigation contains one `StaticLevelPath` component. Its serialized data is a collection of named ordered routes:

```text
StaticLevelPath
    Routes
        Main
            Direction: Bidirectional
            Waypoints: A, B, C, D, E

        WorkshopPOI
            Direction: Bidirectional
            Waypoints: C, F, G

        ExitLift
            Direction: ForwardOnly
            Waypoints: E, H, I
```

This produces one combined graph:

```text
A <-> B <-> C <-> D <-> E -> H -> I
          |
          F
          |
          G
```

Routes may share waypoints. Shared points form graph junctions. Duplicate links are ignored.

### 4.2 Route data

A serialized route contains only:

- a designer-facing name;
- an ordered list of `RoomWaypoint` references;
- a direction: `Bidirectional` or `ForwardOnly`.

For a bidirectional route `A, B, C`, the builder creates:

```text
A -> B
B -> A
B -> C
C -> B
```

For a forward-only route `A, B, C`, it creates:

```text
A -> B
B -> C
```

If one segment needs a different direction, it is authored as a separate short route. This avoids per-segment configuration while still supporting lifts or other directional transitions.

### 4.3 Waypoint placement

Every configured point is a normal `RoomWaypoint`. Place points:

- at the beginning and end of the navigable level route;
- at room entrances and exits;
- at corridor turns;
- before and after obstacles that prevent a safe straight segment;
- at POI branch junctions;
- at lift entry and exit positions;
- anywhere a direct segment between consecutive nodes would be unsafe.

Do not build a dense grid. Robots move in straight segments between consecutive waypoints, so the authored graph only needs enough points to describe safe movement.

Every waypoint must:

- be non-null;
- belong to a `RoomManager`;
- have a unique effective identity;
- be included in at least one route if it is part of the static graph.

No connection exists unless it is explicitly represented by consecutive points in an authored route.

## 5. Runtime setup

Static setup must occur in this order:

1. Find the enabled `StaticLevelPath` in the scene.
2. Instantiate exactly one existing `WaypointService` prefab.
3. Configure the service/pathfinder with `WaypointGraphMode.StaticExplicit` and the authored routes.
4. Give the waypoint service to `FactoryManager.InitializeStatic` instead of storing null.
5. Collect every distinct waypoint referenced by the routes.
6. Assign or validate each waypoint's `parentRoom`.
7. Register the referenced waypoints with the service, grouped by their owning room.
8. Call `BuildAllNeighbors` to create the explicit graph.
9. Initialize static-scene robots through one general navigation initialization seam.
10. Mark static navigation ready.

The robot initialization step is general. It supplies the same navigation dependencies to every applicable robot role and contains no Worker Collector, guard, boss, or mission-specific behavior.

Static levels without an enabled `StaticLevelPath` do not instantiate or configure static navigation. Generated maps do not load `StaticLevelPath` and continue through their existing initialization path.

## 6. Component responsibilities

### 6.1 `StaticLevelPath`

Responsible for:

- storing the named ordered routes;
- exposing the distinct configured waypoints;
- producing explicit directed connections;
- deduplicating shared connections;
- validating authoring errors;
- drawing the configured topology with Gizmos.

Not responsible for:

- selecting robot destinations;
- spawning robots;
- initializing individual robot roles;
- controlling robot movement;
- machine, POI, mission, or level objective behavior;
- changing the graph during play.

### 6.2 `WaypointService` and `WaypointPathFinder`

Responsible for:

- retaining the selected `WaypointGraphMode`;
- registering static waypoints;
- rebuilding the correct topology for the selected mode;
- answering existing waypoint and path queries without robot-side mode checks.

### 6.3 Static scene bootstrap

Responsible for:

- detecting the opt-in `StaticLevelPath`;
- creating and configuring the service;
- ordering registration and graph construction;
- providing the service through the existing factory/room setup;
- initializing all applicable static-scene robots through a shared mechanism.

## 7. Validation and diagnostics

### 7.1 Authoring validation

Report a clear error when:

- a route contains fewer than two waypoints;
- a route contains a null waypoint;
- a waypoint has no owning `RoomManager`;
- two distinct waypoints have the same effective identity;
- a consecutive pair contains the same waypoint twice;
- static mode has no valid route;
- more than one enabled `StaticLevelPath` exists in the scene.

Report a warning when:

- an authored route repeats the same connection;
- a waypoint is listed repeatedly without adding useful topology;
- a route name is empty.

### 7.2 Scene visualization

When debug drawing is enabled, draw:

- waypoint nodes as spheres;
- bidirectional connections as lines with a bidirectional indication;
- forward-only connections as arrows;
- route junctions in a distinct color;
- invalid or missing route entries in red when possible.

The visualization represents the authored graph, not a particular robot's current destination or route.

### 7.3 Runtime validation

Before static navigation becomes ready, verify:

- exactly one waypoint service exists;
- the service is in `StaticExplicit` mode;
- all configured waypoints were registered;
- every authored connection was built in the correct direction;
- at least one route exists between the configured level start and end validation points, when those optional validation references are assigned.

## 8. Automated tests

Add focused Edit Mode tests for:

1. A bidirectional ordered route creates both neighbor directions.
2. A forward-only ordered route creates only forward neighbors.
3. Multiple routes combine into one graph through a shared junction.
4. Duplicate connections are not added twice.
5. `BuildAllNeighbors(false)` and `BuildAllNeighbors(true)` produce the same graph in `StaticExplicit` mode.
6. Static mode never creates an unconfigured same-room or same-axis connection.
7. Generated mode retains the existing automatic graph behavior.
8. Static setup creates and retains exactly one waypoint service.
9. All distinct authored waypoints are registered under their owning rooms.
10. Null waypoints, invalid routes, and duplicate waypoint identities produce clear diagnostics.

## 9. Manual scene checkpoint

For each static level:

1. Add one `StaticLevelPath`.
2. Author the main start-to-end route.
3. Add a separate ordered route for every POI branch or directional transition.
4. Confirm the Scene-view graph contains only the intended connections.
5. Enter Play Mode and confirm a path query can traverse the main route in every allowed direction.
6. Confirm each POI branch can be entered and exited when bidirectional.
7. Confirm a forward-only route cannot be traversed backward.
8. Trigger another `BuildAllNeighbors` call and confirm the authored graph remains unchanged.

## 10. Implementation sequence

1. Add the `WaypointGraphMode` enum.
2. Add serializable static route data and the `StaticLevelPath` component.
3. Add explicit-static graph rebuilding to `WaypointPathFinder`/`WaypointService`.
4. Add validation and Scene-view Gizmos.
5. Allow static factory initialization to retain a waypoint service.
6. Add the static bootstrap registration and initialization order.
7. Add one general static robot navigation initializer without role-specific logic.
8. Add Edit Mode tests for graph construction and generated-mode regression coverage.
9. Configure and verify one static level as the first vertical slice.
10. Configure the remaining static levels after the shared implementation is proven.

The first implementation pass should build and test the generic static graph system before changing all static scenes. The first configured scene provides the proof that registration, graph rebuilding, visualization, and shared navigation consumption work together.
