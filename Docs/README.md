# Cowboya Documentation Index

Start with the root [README](../README.md) (current game on `tourB`) and the
[game vision and idea bank](../Assets/ReadMe.md). This page groups the design
and investigation documents by topic. Dated documents are snapshots: when two
documents disagree, the most recent one (or the one explicitly marked as
superseding) wins.

## Run progression and levels

- [Run progression: static levels, laboratory, generated levels](Run_Progression_Static_Labo_Generated_Level_Plan.md)
- [Level 4 seeded and later generated progression (2026-10-09)](Level_4_Seeded_and_Later_Generated_Progression_Investigation_2026-10-09.md) — its "Current reconciliation 2026-10-11" section is authoritative.
- [Junk room mechanics (2026-07-09)](Junk_Room_Mechanics_Investigation_2026-07-09.md) — Level 1 garbage/junk.
- [Level 2 Worker Collector and Garage](Level_2_Cube_Worker_and_Garage_Design_Review.md) — partly superseded by the Worker Collector correction plan.
- [Static level robot pathfinding](Static_Level_Robot_Pathfinding_Design.md)
- [Post-refactor playtest issues (2026-10-11)](Post_Refactor_Playtest_Issues_2026-10-11.md) — latest end-to-end playtest.

## Generated factory

- [Generated map room selection and morality weighting (2026-10-08)](Generated_Map_Room_Selection_Design_2026-10-08.md)
- [Garage, Worker Collector and Reception in generated maps (2026-10-08)](Generated_Map_Garage_WorkerCollector_and_Reception_Security_Integration_Plan_2026-10-08.md)
- [Generated map worker population](Generated_Map_Worker_Population.md)
- [Factory room and machine logic (2026-04-15)](Factory_Room_Machine_Logic_Investigation_2026-04-15.md)
- [Minimap room preview visual guide](Minimap_Room_Preview_Visual_Guide.md)

## Laboratory (next big piece)

- [Laboratory system: concept and implementation base](Laboratory_System_Concept_and_Implementation_Base.md)
- [Collected cubes -> laboratory chute](SpawnCubesCollected_Collection_and_Laboratory_Spawn_Design.md) — implemented.

## Player (cowboy)

- [Xbox controller input and two-arm control](Xbox_Controller_Input_And_TwoArm_Control_Design.md) — final control layout.
- [Two-arm grab and speed attack](Cowboy_TwoArmGrab_SpeedAttack_Design.md)
- [Cowboy attack investigation](cowboy_attack_investigation.md)
- [Robot passing: record and replay tests](Robot_Passing_Replay_Test_Design.md) — proposal for testing the key "grab and rotate a robot to pass it" skill.

## Physics puppet (master / puppet)

- [SimplePuppetBinder (MasterPuppetLink notes)](MasterPuppetLink.md)
- [Puppet binding notes](PuppetBindingNotes.md)
- [Cowboy puppet rotation smoothing](CowboyPuppetRotationSmoothing.md)

## Robot AI (Memory -> Brain -> Heart -> Task)

- [New architecture migration guide](RobotAI_NewArchitecture_MigrationGuide.md)
- [New pipeline migration status (2026-04-08)](RobotAI_NewPipeline_MigrationStatus_2026-04-08.md)
- [Remaining work](RobotAI_SomethingNew_RemainingWork.md)
- [Robot task pseudo-code v1](RobotTask_NewPseudoCode_v1.md)
- [Factory robot population and AI refactor plan (2026-10-11)](Factory_Robot_Population_AI_Refactor_Plan_2026-10-11.md) — most recent AI plan.
- [Robot energy and health](RobotEnergyHealth.md)
- [Run upgrades and worker tasks (2026-04-30)](Robot_Run_Upgrade_And_Worker_Task_Investigation_2026-04-30.md)

### Workers

- [Worker flow migration plan](RobotAI_WorkerFlow_MigrationPlan.md)
- [Worker flow debug](RobotAI_WorkerFlow_Debug.md)
- [Worker slot simplification (2026-04-16)](Worker_Slot_Simplification_Investigation_2026-04-16.md)

### Worker Collector (moves white cubes)

- [Worker Collector logic correction plan](Worker_Collector_Logic_Correction_Plan.md) — supersedes the Level 2 review on pickup logic.
- [Worker Collector fix validation (2026-10-07)](Worker_Collector_Fix_Validation_2026-10-07.md)
- [Object-targeted arm reach test](Worker_Collector_Object_Targeted_Arm_Reach_Test_Design.md)

### Security

- [Security Reception robot (2026-10-08)](SecurityReception_Robot_Design_2026-10-08.md)
- [Security guard multi-bug triage (2026-04-30)](Debug/SecurityGuard_MultiBug_Triage_2026-04-30.md)
- [Security guard reactivation debug](Debug/SecurityGuard_Reactivation_Debug.md)

### Flying Collector (collects dead robots)

- [Master/puppet physics setup](CollectorRobot_Fly_MasterPuppet_Physics_Setup.md)
- [Flight and collection pipeline](CollectorRobot_Fly_Flight_Collection_Pipeline_Implementation.md)

## Project health

- [Improvement audit (2026-10-08)](Audit_Ameliorations_Cowboya_2026-10-08.md) — save, tests, CI, assets. Save issues are known and intentionally postponed.
