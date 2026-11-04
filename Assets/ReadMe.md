# Cowboya — Game Vision and Idea Bank

This document holds the **vision** of Cowboya and the **ideas** collected
since the first prototype. It is not a specification: the current game is
described in the root [README](../README.md) and the detailed designs live in
[`Docs/`](../Docs/README.md).

Each idea is tagged:

- **[In game]** — exists on `tourB` (possibly incomplete).
- **[Next]** — planned for the next development steps.
- **[Idea]** — kept as inspiration, not scheduled.
- **[Replaced]** — superseded by another design; kept for reference.

---

## 1. Concept

- **Genre:** comedic 2D rogue-lite, physics-driven. **[In game]**
- **World:** a robot factory run by working robots, guards and bosses. **[In game]**
- **Player:** a cowboy robot, an active ragdoll with two independent arms. **[In game]**
- **Core fantasy:** get through the factory with your hands — grab, turn,
  throw and punch robots and objects — and choose whether you free the
  workers or break them. **[In game]**
- **Humor:** absurd robot behaviors, a nervous scientist, ridiculous bosses. **[Idea]**

## 2. Run Loop

- Hand-built levels first (Level 1-3), then generated levels with fixed
  configurations, then random growing levels. **[In game]**
- A **laboratory** visited between every level, where resources brought back
  are transformed into upgrades. **[Next]**
- The old idea of a **base camp** between runs, reached by teleport, with
  allies and deployment stations. **[Replaced]** by the laboratory between
  levels; the camp could come back later as the long-term hub between runs. **[Idea]**

## 3. Player, Combat and Movement

- **Energy:** every action costs energy; at zero the robot faints (ragdoll)
  and recharges by moving. **[In game]**
- **Health:** lost from hits, restored with batteries. **[In game]**
- **Two-arm control:** each arm can rest, grab or attack independently. **[In game]**
- **Speed attack:** a hand that moves fast enough becomes the hit. **[In game]**
- **Passing robots:** in 2D you must go over or under a robot by grabbing it
  and rotating it. Key skill, must become reliable. **[Next]**
- **Starting attacks punch, kick, pistol shot.** Only arm hits exist today;
  kick and pistol remain ideas. **[Idea]**
- **Custom combos** (rearranging attack order). **[Idea]**
- **Temporary boosters** (chips, drinks) during a run. **[Idea]**

## 4. Resources and Upgrades

- **Cubes:** white cubes (basic resource) and colored cubes — Health (green),
  Energy (blue), Recharge, Force (attack damage). **[In game]**
- **CubeCollecteur -> laboratory chute:** collected cubes reappear in the lab
  as physical objects. **[In game]**
- **Upgrade Stats machine / Builder** turning cubes into permanent stats and
  new equipment. **[Next]**
- **Junk:** collected in levels, given to the scientist for white cubes. **[In game]**
- **Gears and special resources** sent home through deployment stations. **[Idea]**
- **Robot parts** to unlock new playable characters. **[Idea]**

## 5. Morality

- Saving robots raises morality, killing lowers it. **[In game]**
- Factory alarm states react to violence (Normal, Wanted, Revolt, Lockdown). **[In game]**
- **Followers** (hostile blockers) come to block a violent player. **[In game]**
- Morality weights the rooms generated in later levels. **[In game]**
- Morality changes how the laboratory scientist treats you. **[Next]**
- Morality changes the **look of attacks** (nice or mean). **[Idea]**
- A **Sheriff** who changes attacks or unlocks upgrades depending on
  alignment. **[Idea]**

## 6. Characters in the Laboratory / Camp

- **DocBot, the scientist:** works, accepts one junk per visit, becomes
  scared if attacked, refuses to work for a visit if killed. **[In game / Next]**
- **Botanist** (rearranges attacks into combos). **[Idea]**
- **Sheriff** (alignment-based attack changes). **[Idea]**
- **Mechanic** (gives or removes attacks). **[Idea]**

## 7. Robots and Enemies

In game: Worker, WorkerSpawner, SecurityGuard, SecurityReception, Follower,
Boss, flying Collector, WorkerCollector (see root README).

Comedic enemy ideas kept for later **[Idea]**:

- **Sweeper Robot:** moves uncontrollably toward the player trying to sweep them.
- **Office Mower:** charges frantically while shouting absurd phrases.
- **Paparazzi Drone:** flashes the player, slowing them briefly.
- **Dance Instructor (mini-boss):** rhythm-based attacks, vulnerable during dance breaks.
- **Eccentric Director (final boss):** runs the factory, uses gadgets and
  shouts ridiculous instructions.

## 8. New Characters **[Idea]**

- Unlock playable robots from collected parts: NinjaBot (stealth attacks),
  TankBot (high health, heavy hits).

## 9. Saving **[Later]**

Saving is deliberately postponed until the start of the game feels right.
Data to keep in mind for later: permanent stats, unlocked attacks and order,
resources, unlocked characters, morality influences, laboratory state,
settings (audio, screen mode, last character). Known technical issues are
listed in [the audit](../Docs/Audit_Ameliorations_Cowboya_2026-10-08.md).
