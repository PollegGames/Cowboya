Robot AI – Heart / Brain / Body / Memory Plan
=============================================

Scope
-----
Shared robot architecture (Workers, Guards, Followers, Boss, Spawner) with Heart (intent), Brain (orchestration), Body (execution), Memory (facts). Captures task stack rules, morality, role priorities, machine handling, collision policy.

Architecture Roles
------------------
- Heart: Stack-based intent selector; config-driven priorities/thresholds; evaluates perception, memory, morality each frame/event; precedence ladder; unique task per type; replace-on-update; depth cap; profile swap only at start room.
- Brain: Drives FSM/state + movement/interaction/attack; calls Heart every frame/event; handles completion/timeout/path fallback; subscribes to machine events; on complete/timeout/invalid → pop and re-evaluate.
- Memory: Factual store (player pos/timers, attacked flag, last visited, respawn ref); no decisions.
- BodyController: Executes move/attack/use-machine/faint/die; handles collision ignore/enable vs player based on relation; path follower can stay here; exposes arrival/stuck.

Morality
--------
- Integer per role (defaults per save): Worker -1; Guard -5; Follower -10; Boss -50; Spawner 0 (fixed).
- Player morality ±1: kill robot → -1; save robot → +1.
- Thresholds (hostility / fear):
  - Worker hostility ≤0, fear <10.
  - Guard hostility ≤-2, fear <20.
  - Follower hostility ≤-50, fear <70.
  - Boss hostility ≤-100, fear <100.
  - Spawner hostility 0 fixed, fear <1000 (never fears).
- Attack always triggers Flee (if role can flee) when playerMorality <= hostilityThreshold; else Cower if allowed.
- Cower only when not attacked and playerMorality > hostilityThreshold but robotMorality below fearThreshold.
- Profile swap only when robot reaches start room (e.g., becomes ally): swap to ally priority profile, reset stack to new default task.

Tasks and Stack
---------------
- TaskType: Idle, WorkAtMachine, GuardPost, ReactivateMachine, ChasePlayer, AttackTarget, Flee, Rest, SpawnFollowers, ReturnHome, Patrol, Investigate, Cower.
- RobotTask: {Type, Payload, ExpireAt, Urgency}; policies per type (timeouts/completions fixed defaults).
- Precedence: Flee > Hostile Attack/Chase > Reactivate/Alarm > Role Core (Work/Guard/Spawn) > Rest > Patrol/Idle/Cower.
- Stack rules: one task per type; higher-precedence removes prior and pushes; same type updates payload; depth cap (Boss depth=1 looping Idle/Defend until higher precedence).
- Expiry/completion: timeout per type; on complete/timeout → pop; on path fail → fallback intent (Reactivate→Guard/ReturnHome; Chase→Investigate; Work→Rest; Flee→Cower).
- Event coalescing: machine events coalesce to one pending Reactivate; replace payload with latest per preferred order; avoid stack spam.

Role Behaviors
--------------
- Worker: Core Work/Rest; works until replaced; rests for duration then seeks least-used work machine; hostility ≤0 → flee when attacked; fear <10 → cower when not hostile; ignores player collisions when relation positive; attacked by hostile player → flee.
- Guard: Stays at post; leaves only for machine-off/reactivation chain: SpawnMachine → SecurityMachine → WorkMachine → RestMachine → StartRoom (free) if none; hostility ≤-2 → attack/chase; fear <20 → cower only when not hostile; may ignore collisions if relation positive, but close proximity can re-enable collisions and attack.
- Follower: Goal = kill player; uses camera/last POI to chase; hostility ≤-50 → always attack; fear <70 → only cower when not hostile and high player morality; attacks shortly after entering attack zone (~0.5s delay configurable later); no machine duties.
- Boss: Stays in boss room center; follows player within room; returns to center if player leaves; if all working machines off → harmless and faints; hostility ≤-100 → attacks when player in room; fear <100 only matters when not hostile; stack depth=1; ignores collisions when friendly.
- Spawner: Only works/spawns; hostility fixed 0, fear <1000 (never fears); stays in place unless machine off → Reactivate chain per guard order; no morality change; cannot flee/cower; collision ignore when friendly.

Machine Handling
----------------
- Machines emit on/off/state changed; no direct state pushes.
- Reactivate order (Guard/Spawner/Worker fallback): SpawnMachine → SecurityMachine → WorkMachine → RestMachine → StartRoom (free).
- Worker order: WorkMachine → RestMachine → StartRoom (if none); after a machine turns off, worker will try all working rooms again after resting (attempt counter resets when working resumes).
- Spawner order: SpawnMachine → RestMachine → StartRoom (only if no spawn machine found); after resting, tries to go back to a spawn machine (same or new) before conceding to StartRoom.
- Boss: unaffected except global “all working machines off” → Faint.

Role Defaults Across Levels
---------------------------
- Each role has default hostility/fear baselines (e.g., Worker -1 hostility default), but a persistent mid-value per role is carried across scenes/levels.
- At level end, adjust the per-role default hostility by player morality delta (e.g., if player morality improved, increase all role hostilities by +1; if worsened, decrease by -1).
- Store these evolving defaults in a shared asset (ScriptableObject) so newly spawned robots in the next scene use the updated baseline before applying in-level changes.

Perception / Updates
--------------------
- Heart evaluates each frame; payloads refreshed in place (e.g., chase target pos).
- Attack zone: Guards/Followers attack after short gap (~0.5s); future randomness possible.
- Path failures: treated as completion with fallback intent (Brain asks Heart again).

Collision Policy
----------------
- BodyController toggles ignore collision with player based on relation: friendly/neutral → ignore; hostile → collide. Guards may ignore when friendly but re-enable when player too close or hostile intent. Workers ignore when friendly to avoid blocking player flow; Spawners/Boss ignore when friendly.

Implementation Notes (current refactor)
---------------------------------------
- Components per robot: Heart (intent stack), Brain (config-driven task handlers), BodyController (locomotion/path/stuck), BodyMaintenance (respawn/stuck), Memory (facts-only). Legacy controllers are being removed in favor of this set.
- Brain uses RobotTaskHandlers ScriptableObject to map RobotTaskType to handlers (MoveToPayload, ChaseTarget, Attack, Idle, etc.). Configure per-role profiles via data rather than code subclasses.
- Machine events target Brain; Brain decides if/what to push into Heart (e.g., ReactivateMachine on off, WorkAtMachine on on).
- Attack handling will plug into a simple attack controller; a handler stub exists and should be wired once the robot-specific attack controller is in place.
- Prefabs must assign the role-appropriate RobotTaskHandlers asset and Brain config (stack depth/urgency/timeouts) to honor the precedence/role rules above.

