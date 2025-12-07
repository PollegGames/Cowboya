Robot Role Combat Rules
=======================

Purpose
-------
Make explicit when each robot role should attack, flee, or cower, and how the proximity triggers tie into the new Heart/Brain/Body stack and the AttackZone.

Shared Signals
--------------
- `FollowPlayerTriggerHandler` drives detection: detect zone records player position into `RobotMemory`; attack zone raises `OnPlayerDetectInAttackZoneChanged` used by `EnemyPunchAttack`.
- `RobotMemory.WasRecentlyAttacked` is set by damage events (not wired here) and should override fear/hostility checks.
- `FactoryAlarmStatus` state **Wanted** makes all hostile-capable roles attack regardless of player morality.
- Heart precedence already sets `Flee > Attack/Chase > Reactivate > Core role > Rest > Idle`.

Per-Role Intent
---------------
- **Worker**
  - Primary loop: Work ↔ Rest.
  - When player hostility threshold met (playerMorality <= 0) or attacked: **Flee** (no attacking).
  - Otherwise can **Cower** if fear threshold hit (playerMorality high but robot fear low).
- **Security Guard**
  - Primary loop: GuardPost (at security machine) or ReactivateMachine when any machine goes off.
  - Attack rules:
    - Attack player while in AttackZone if playerMorality <= -2 (hostility threshold).
    - Always attack if alarm is **Wanted**.
    - Always attack if the guard was recently attacked (`Memory.WasRecentlyAttacked`).
  - Cower only when not hostile and fear threshold crossed; otherwise patrol/hold post.
- **Follower**
  - Goal = kill player. Attack as soon as player in AttackZone regardless of morality or alarm.
  - Chase player using last seen position; never cower while hostile.
- **Boss**
  - Attack when player is in boss room (or AttackZone) unless all work machines are off (then faint).
  - Ignores fear/cower when hostile.
- **Spawner**
  - No combat; ignores AttackZone. Only Reactivate/Work loop.

Timeout Expectations
--------------------
- Rest: ~3s then seek least-used work machine (workers).
- GuardPost: ~5m dwell unless replaced or responding to a machine off.
- Work: stays until replaced or machine off; fallback Rest.
- Reactivate: short (~30s) then fallback Guard/ReturnHome.
