# Follower Detect/Attack Debug (New Thread)

Date: 2026-02-05

Goal
- Track the new strange behavior after switching attacks to trigger on detect zone.
- Capture symptoms, expected behavior, and reproduction steps for a fresh chat.

Observed Behavior (from screenshots)
- Follower pathing/attack behavior looks inconsistent after detect-zone change.
- Visuals show path line and robots in same room; behavior appears off compared to expectation.

Expected Behavior
- On entering detect zone, aggressive enemies (Follower/SecurityGuard/Boss) stop movement and attack.
- Attack should continue while inside detect zone, even if attack zone is not entered.
- When leaving detect zone, attack ends and chasing resumes.

Repro Steps
1. Start sandbox run.
2. Trigger camera alarm to spawn Follower.
3. Bring player into follower detect zone.
4. Observe movement stop + attack timing.

Recent Code Changes (Detect-Zone Attack)
- File: Assets/Scripts/Player/FollowPlayerTriggerHandler.cs
  - Detect zone now sets PlayerInAttackZone and triggers attack for aggressive roles.
  - Attack zone exit does not cancel attack if still in detect zone.
  - Detect exit ends attack and clears PlayerInAttackZone.
- Cleaned debug logs for follower pathing/spawn/chase.

Key Questions to Answer
- Is detect zone size/position correct for follower?
- Is the follower attack animation / hitbox aligned with detect zone?
- Is movement properly stopped on detect enter and resumed on detect exit?
- Are multiple triggers firing (detect + attack) in unexpected order?

Suggested Data to Capture
- Short log snippet around:
  - Detect zone enter/exit
  - Attack zone enter/exit
  - Task changes (AttackTarget / ChasePlayer)
- Screenshot showing follower + detect/attack gizmos in same frame.

Notes
- Screenshots attached in prior chat show mismatch between expected attack stop point and actual behavior.

---

## Follower Chase Simplification (2026-02-05)

Goal
- Use waypoint pathing for chase, but always append the real player position as the last point.
- When the follower reaches that last point, it should request a new path (via refresh/recalc).
- When the player is inside attack range, reuse the same attack logic as the boss (attack zone + attack task).

Intended Behavior (Simple Version)
1. Follow path to last known player position:
   - Build waypoint path to a waypoint target.
   - Append the exact player world position as the last point in the path.
2. Reach last point -> refresh path:
   - When path completes, refresh chase to the latest known player position.
3. When player is in attack zone:
   - Use existing attack-zone mechanics (same as boss) to keep attacking.

Changes Already Implemented
- File: `Assets/Scripts/World/Waypoints/WaypointPathFollower.cs`
  - Added optional final world position appended to the waypoint path.
  - Recovery reuses the same final target so path ends at the player position.
- File: `Assets/Scripts/Robots/Body/RobotBodyController.cs`
  - `SetDestination(Vector3)` now passes the player position as the final path point.
- File: `Assets/Scripts/Robots/RobotBrain.cs`
  - Simplified follower chase: follower always paths to last known player position or waypoint.
  - Removed direct-chase behavior for followers; all chase uses waypoint path with final player position.
  - Logs are now consolidated via a single `LogChase(...)` method.

Current Open Checks
- Verify follower continues moving from last waypoint to the exact player position.
- Confirm attack zone logic keeps attacking while player remains in range (boss-like behavior).
- If still stopping short, capture a short log snippet around:
  - `ChasePlayer` decisions
  - `FollowPlayerTriggerHandler` detect/attack enter/exit
  - `WaypointPathFollower` Path set / Path cleared
