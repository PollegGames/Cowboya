# Documentation Cowboya

Index des documents. Les documents à la racine de `Docs/` décrivent l'état actuel ou le travail à venir.
`Docs/Archive/` contient l'historique : plans déjà réalisés, enquêtes, notes de debug. Les chemins cités
à l'intérieur des documents archivés peuvent pointer vers l'ancien emplacement (`Docs/<nom>.md`).

## Vision et feuille de route

| Document | Rôle |
|---|---|
| [../Assets/ReadMe.md](../Assets/ReadMe.md) | Vision d'origine du jeu (rogue-lite comique). Une partie (camp, alliés botaniste/shérif/mécanicien, combos) est remplacée par le laboratoire. |
| [Laboratory_Progressive_Rooms_Design.md](Laboratory_Progressive_Rooms_Design.md) | **Prochain chantier.** Laboratoire à salles débloquées une par une. |
| [Audit_Ameliorations_Cowboya_2026-10-08.md](Audit_Ameliorations_Cowboya_2026-10-08.md) | Audit qualité du 8 octobre 2026. La section sauvegarde (SAVE-xx) est en attente : rien n'est sauvegardé volontairement tant que les règles de persistance ne sont pas définies. |

## Run et génération de map

| Document | Rôle |
|---|---|
| [Run_Progression_Static_Labo_Generated_Level_Plan.md](Run_Progression_Static_Labo_Generated_Level_Plan.md) | Itinéraire du run : niveaux 1-3 statiques, laboratoire entre chaque niveau, niveaux 4-5 générés avec seed fixe, génération aléatoire à partir du niveau 6. |
| [Generated_Map_Room_Selection_Design_2026-10-08.md](Generated_Map_Room_Selection_Design_2026-10-08.md) | Slots de POI fixes 1-6, puis choix pondéré par la moralité. |
| [Generated_Map_Worker_Population.md](Generated_Map_Worker_Population.md) | Nombre de workers et worker collectors dans les niveaux générés. |
| [Static_Level_Robot_Pathfinding_Design.md](Static_Level_Robot_Pathfinding_Design.md) | Graphe de waypoints des niveaux faits à la main. |
| [Minimap_Room_Preview_Visual_Guide.md](Minimap_Room_Preview_Visual_Guide.md) | Style visuel des salles dans la minimap. |

## Robots et usine

| Document | Rôle |
|---|---|
| [Factory_Robot_Population_AI_Refactor_Plan_2026-10-11.md](Factory_Robot_Population_AI_Refactor_Plan_2026-10-11.md) | Plan de refactor : `FactoryWorkCoordinator` et `RobotPopulationManager` (pas encore commencé). |
| [Post_Refactor_Playtest_Issues_2026-10-11.md](Post_Refactor_Playtest_Issues_2026-10-11.md) | Dernier playtest de bout en bout et corrections associées. |
| [SecurityReception_Robot_Design_2026-10-08.md](SecurityReception_Robot_Design_2026-10-08.md) | Robot de réception et vol de badge. |

## Joueur et physique Master/Puppet

| Document | Rôle |
|---|---|
| [MasterPuppetLink.md](MasterPuppetLink.md) | Principe du lien Master/Puppet. |
| [PuppetBindingNotes.md](PuppetBindingNotes.md) | Notes de binding du puppet. |
| [CowboyPuppetRotationSmoothing.md](CowboyPuppetRotationSmoothing.md) | Lissage des rotations du puppet. |
| [Cowboy_TwoArmGrab_SpeedAttack_Design.md](Cowboy_TwoArmGrab_SpeedAttack_Design.md) | Grab à deux bras et attaque par vitesse du bras. |
| [Xbox_Controller_Input_And_TwoArm_Control_Design.md](Xbox_Controller_Input_And_TwoArm_Control_Design.md) | Contrôles manette. |
| [CollectorRobot_Fly_MasterPuppet_Physics_Setup.md](CollectorRobot_Fly_MasterPuppet_Physics_Setup.md) | Setup physique du robot collector volant. |

## Laboratoire (concept précédent)

| Document | Rôle |
|---|---|
| [Laboratory_System_Concept_and_Implementation_Base.md](Laboratory_System_Concept_and_Implementation_Base.md) | Concept d'août 2026 (Builder, StatsScreens, UpgradeStatsMachine). Ses règles d'état, d'ownership et de restauration restent valables. Sa structure de salles est remplacée par le document « salles progressives ». |
| [SpawnCubesCollected_Collection_and_Laboratory_Spawn_Design.md](SpawnCubesCollected_Collection_and_Laboratory_Spawn_Design.md) | Transport des cubes collectés vers le laboratoire (implémenté). |
