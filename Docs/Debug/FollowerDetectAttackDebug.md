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
