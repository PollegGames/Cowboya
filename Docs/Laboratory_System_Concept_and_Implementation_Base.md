# Système de laboratoire — base de conception et d'implémentation

**Statut :** document de référence initial  
**Date de l'audit :** 17 août 2026  
**Portée :** gameplay, progression, état persistant et intégration Unity du laboratoire

## 1. Objectif du document

Le laboratoire est la scène récurrente entre les niveaux. Il ne s'agit pas d'un simple écran d'amélioration : c'est un espace physique dans lequel le joueur transforme des ressources, construit et installe des équipements, améliore ses statistiques et influence le comportement du scientifique.

Ce document transforme le résumé de brainstorming en base exploitable pour l'implémentation. Il distingue quatre niveaux de certitude :

- **Concept confirmé** : règle de gameplay considérée comme voulue.
- **Existant vérifié** : élément réellement présent dans le projet au jour de l'audit.
- **Décision recommandée** : précision ou correction d'architecture proposée ici.
- **À clarifier** : décision qui dépend encore du game design, d'un visuel ou d'une valeur fournie par le créateur.

Il ne faut pas considérer les exemples de noms de classes comme du code déjà implémenté.

## 2. Vision et périmètre

### 2.1 Boucle de jeu

La boucle cible est :

```text
Niveau de gameplay
    -> ressources et objets ramenés
Laboratoire
    -> transformation / construction / installation / amélioration
Niveau suivant
    -> conséquences des décisions du laboratoire
Laboratoire suivant
    -> restauration du même laboratoire dans son nouvel état
```

La scène `Level_Laboratory` est réutilisée à chaque visite. Son contenu dépend de l'état du run, jamais d'un numéro de cycle codé dans la scène.

### 2.2 Principe directeur

Le laboratoire est **piloté par l'état** (`state-driven`) :

```text
État fonctionnel autoritaire
    -> reconstruction des objets physiques
    -> reconstruction des matériaux et affichages
```

Les matériaux, positions d'objets libres, rigidbodies et animations ne sont pas des données de progression.

### 2.3 Hors périmètre de la première implémentation

Les armes, blueprints obtenus en jeu, nouvelles machines et coûts complexes doivent être supportables par l'architecture, mais n'ont pas à être réalisés dans le premier lot.

La création et la disposition des niveaux suivants, les illustrations finales, les prefabs manquants et les valeurs de balance restent des contributions du créateur du jeu.

## 3. Audit de l'existant

### 3.1 Éléments utilisables dès maintenant

| Élément | État vérifié | Conséquence |
|---|---|---|
| `Level_Laboratory` | Scène existante avec deux salles | La scène de travail est déjà disponible. |
| `ROOM_Laboratory_1` | Contient `MachineBuilder` et un `ScientistSlot` | Bon emplacement pour le Builder et le scientifique. |
| `ROOM_Laboratory_2` | Contient `UpgradeStatsMachine` et `StatsScreens` | Les deux équipements prévus ont déjà une représentation physique. |
| Progression du run | Le prefab configure `Level_1 -> Laboratory -> Level_2 -> Laboratory -> MapGeneration` | Le niveau 2 est bien intégré à l'itinéraire, contrairement à l'ancien document qui le disait absent. |
| `SceneSetupMode.Laboratory` | Existe et est résolu par `RunProgressManager` | Le laboratoire a déjà un chemin d'initialisation distinct. |
| Spawn laboratoire | `SceneInitiator` utilise `StaticLevelSpawnPoint`, sinon `ROOM_Laboratory_1` | Le joueur peut déjà être initialisé dans le laboratoire. |
| Sortie de niveau | `RunStepExitTrigger` capture les stats, sauvegarde puis charge l'étape suivante | Un hook existe, mais doit être étendu pour finaliser l'état du laboratoire. |
| Manipulation physique | `CowboyGrabController` et `IGrabbable` fonctionnent pour les objets tenus | Base réutilisable pour cubes, Junk et pièces. |
| Junk | `JunkPickup` est saisissable et possède une physique compatible | Le prefab peut être réutilisé, mais ne traverse pas encore les scènes. |
| Cubes et upgrades | Les quatre types existent : santé, énergie, recharge et dégâts | Les ressources colorées ont déjà un équivalent fonctionnel. |
| Stat « Force » | Elle existe comme `AttackDamage` / `AttackDamageBonus` | Ne pas ajouter une seconde stat `Force`; utiliser un nom d'affichage localisé. |
| Valeurs actuelles | Santé `+10`, énergie `+10`, recharge `+5`, dégâts `+5` | Ce sont des valeurs existantes, pas nécessairement la balance finale du laboratoire. |
| Pièces d'écran | Les quatre prefabs Force, Energy, Recharge et Health existent | Bonne base visuelle pour `StatsScreens`. |
| Conveyor | `CubeConveyorController` sait transporter puis relâcher un `CubePickup` | Le principe est réutilisable, mais le composant est trop spécialisé pour les pièces de construction. |

### 3.2 Éléments seulement visuels ou absents

Les prefabs `MachineBuilder`, `UpgradeStatsMachine` et `StatsScreens` ne possèdent pas encore leurs comportements de laboratoire. Leurs scripts actuels sont essentiellement des composants de rendu/warp et, pour les deux machines, un ancien bouton `ToggleButton` non câblé à la logique cible.

Les éléments suivants n'existent pas encore :

- `LaboratoryManager` et modèle `LaboratoryProgress` ;
- scientifique fonctionnel et ses états ;
- transfert du Junk entre scènes ;
- stockage logique du laboratoire et des machines ;
- catalogue et définitions de construction ;
- production générique de prefabs ;
- pièces produites identifiables et installables ;
- slots d'installation ;
- affichages numériques par matériaux ;
- persistance des constructions, compteurs et conséquences du scientifique ;
- blueprints et armes.

### 3.3 Écarts à corriger par rapport au brainstorming

#### Force

Le brainstorming indiquait que la stat Force était introuvable. Elle est aujourd'hui représentée par `PlayerRunStats.AttackDamage` et appliquée aux `Attack`/`AttackHitbox`. Le terme **Force** doit rester le libellé visible pour le joueur, tandis que l'identifiant technique recommandé est `AttackDamage`.

#### Collecte des cubes

Le `CubeCollector` actuel détruit immédiatement le cube et ajoute directement son bonus au `PlayerRunStats`. Ce comportement est incompatible avec la nouvelle boucle dans laquelle les cubes doivent être ramenés, déposés dans la machine, puis validés avec le casque et le levier.

**Décision recommandée :** conserver `CubeUpgradeType`, les prefabs et les valeurs de `CubeUpgradeSO`, mais séparer les deux usages :

- le collecteur de niveau ajoute une ressource transportée au run ;
- `UpgradeStatsMachine` consomme cette ressource et applique l'amélioration ;
- aucun bonus n'est appliqué au moment de la collecte dans un niveau utilisant la nouvelle boucle.

Une compatibilité temporaire peut être conservée pour les anciennes scènes, avec un mode explicite, mais pas avec un comportement implicite dépendant du nom de scène.

#### Première visite

Il n'existe pas actuellement de propriété `IsFirstVisit`. Elle ne doit pas être ajoutée aux stats de combat.

**Décision recommandée :** `LaboratoryProgress.HasVisitedLaboratory` est la source de vérité. La première visite correspond à `false`; la finalisation réussie de cette visite passe la valeur à `true`.

#### Sauvegarde et persistance

Deux notions sont mélangées dans le brainstorming :

- la continuité **pendant un run**, déjà portée par `RunProgressManager` et `PlayerRunStats` ;
- la reprise **après fermeture du jeu**, portée par `PlayerSaveService`/`SaveData`.

Le premier objectif doit être la continuité du run. `LaboratoryProgress` est donc possédé par `RunProgressManager`. Il ne doit être copié dans `SaveData` que si la reprise d'un run après fermeture est réellement voulue. Cette décision ne doit pas bloquer le modèle runtime.

#### Sortie du laboratoire

`RunStepExitTrigger` relâche actuellement les objets tenus, vide l'inventaire, capture les stats, sauvegarde et charge la scène suivante. Ajouter simplement un appel isolé avant `LoadNextStep()` serait fragile.

**Décision recommandée :** la transition du laboratoire doit être atomique :

```text
Bloquer une seconde demande de sortie
    -> LaboratoryManager.TryFinalizeVisit(player)
    -> résoudre le Junk transportable
    -> rendre au storage les objets libres/tenus qui restent au laboratoire
    -> finaliser scientifique, constructions et machines
    -> capturer les stats joueur
    -> sauvegarder si nécessaire
    -> charger l'étape suivante
```

Si la finalisation échoue, la scène suivante ne doit pas être chargée.

## 4. Règles fonctionnelles retenues

### 4.1 Première visite

Lors de la première visite :

- `BuilderMachine` est désactivée ;
- `StatsScreens` est désactivée/incomplète ;
- `UpgradeStatsMachine` est désactivée/incomplète ;
- le scientifique est vivant, au `ScientistSlot`, dans l'état `Work` ;
- le joueur peut donner un Junk, agresser, tenter de saisir ou tuer le scientifique ;
- aucune construction n'est disponible.

À partir de la visite suivante, le Builder est disponible. Le reste dépend uniquement de l'état enregistré.

### 4.2 Scientifique

États runtime recommandés :

```text
Work
CowardTemporary
CowardForVisit
Dead
```

Règles :

- seul `Work` accepte un Junk ;
- un seul Junk peut être accepté par visite ;
- une attaque ou tentative de saisie déclenche `CowardTemporary` pendant environ 10 secondes ;
- le scientifique conserve l'objet tenu pendant cette peur temporaire ;
- la mort lui fait lâcher physiquement l'objet tenu ;
- une mort pendant la visite N impose `CowardForVisit` pendant N+1 ;
- s'il survit à N+1, il revient à `Work` en N+2.

Résolution du Junk à la fin de visite :

```text
Junk reçu + scientifique vivant
    -> WhiteCube disponible à la visite suivante

Junk reçu + scientifique mort
    -> pas de WhiteCube
    -> Junk rendu au stockage du laboratoire
```

La mémoire du scientifique peut s'inspirer de la séparation Brain/Memory des robots, mais il ne faut pas forcer tout le pipeline de worker si ses besoins sont plus simples. Un composant comportemental dédié reste préférable tant que le scientifique n'utilise pas réellement la navigation et les tâches des workers.

### 4.3 Ownership des objets

L'état logique doit distinguer :

```text
PlayerCarry
ScientistHeld
LaboratoryFree
BuilderStorage
UpgradeMachineStorage
Installed
```

Une instance physique n'est qu'une représentation temporaire de cet état.

- Les cubes sont fongibles et se sauvegardent par quantité et propriétaire logique.
- Le Junk peut nécessiter un identifiant/type si plusieurs variantes ont un effet différent; sinon une quantité suffit.
- Les pièces uniques se sauvegardent par `PartId` et état, jamais par quantité anonyme.
- Un objet ne peut avoir qu'un seul propriétaire logique à la fois.
- Toute acceptation par une machine est un transfert d'ownership, pas seulement un `Destroy(gameObject)`.

### 4.4 Objets libres et trappe

Les cubes, Junk et pièces appartenant à `LaboratoryFree` respawnent sous la trappe à la visite suivante. Leur position, rotation et vitesse ne sont pas sauvegardées.

Un cube ordinaire tenu à la sortie ne traverse pas le niveau. Le futur Junk transportable est une exception gérée par `PlayerCarry`, pas par la conservation du GameObject entre scènes.

### 4.5 BuilderMachine

Le Builder possède trois commandes : gauche, droite et confirmer.

- gauche/droite parcourent les constructions débloquées et non exclues ;
- confirmer est actif seulement si tous les coûts sont satisfaits ;
- lorsque tous les coûts du choix courant sont remplis, la sélection est verrouillée jusqu'à confirmation ou annulation explicite ;
- une ressource acceptée appartient à `BuilderStorage`, indépendamment de la construction affichée ;
- la machine refuse un type dont aucun coût disponible n'a besoin ou dont la capacité utile est atteinte.

Le coût initial des deux constructions est `WhiteCube x4`, sous réserve d'ajustement de balance.

Une confirmation valide les coûts, les consomme, marque la construction comme produite et retire immédiatement une construction unique du catalogue.

**Précision recommandée en cas de sortie pendant l'animation :** tous les `ProducedItems` sont créés logiquement dès la confirmation. Le conveyor n'est qu'une présentation. À la visite suivante, toute pièce `Produced` non installée et sans instance active respawn sous la trappe. Ainsi, interrompre l'animation ne perd ni ne duplique une pièce.

### 4.6 Catalogue data-driven

Chaque recette est un `ConstructionDefinition` (probablement un `ScriptableObject`) contenant au minimum :

```text
Id stable
Nom/visuel d'affichage
BuildPolicy: Unique ou Repeatable
Requirements[]: ResourceType + Amount
ProducedItems[]: PartDefinition + Amount
UnlockRequirement / BlueprintId optionnel
```

Le Builder n'a aucune branche codée en dur pour `StatsScreens`, `UpgradeStatsMachine` ou une arme. Un catalogue vide est un état normal : la machine reste allumée et affiche qu'aucune construction n'est disponible.

### 4.7 Production physique

Il faut extraire le principe de `CubeConveyorController` dans un composant générique recevant un prefab arbitraire et signalant la fin d'une étape. Le composant existant ne doit pas être étendu avec de nouvelles branches spécifiques aux pièces.

Le producteur générique doit :

- instancier le prefab demandé au point d'entrée ;
- déplacer une seule pièce à la fois jusqu'à la sortie ;
- rendre la physique normale à la sortie ;
- notifier la machine afin de produire la suivante ;
- être interrompable sans affecter l'état logique produit.

### 4.8 StatsScreens

La construction produit quatre pièces uniques :

- `ScreenForce` affiche `AttackDamage` sous le libellé Force ;
- `ScreenEnergy` affiche `MaxEnergy` ;
- `ScreenRecharge` affiche `EnergyRechargeRate` ;
- `ScreenHealth` affiche `MaxHealth`.

Les quatre slots sont positionnels (`TopLeft`, `TopRight`, `BottomLeft`, `BottomRight`) mais non typés. Toute pièce compatible prend le premier slot libre. La sauvegarde conserve `PartId + SlotId`.

Chaque écran devient fonctionnel dès son installation. Les autres slots peuvent rester vides. Une pièce installée ne peut plus être retirée.

### 4.9 UpgradeStatsMachine

La machine est construite en plusieurs pièces. La liste exacte reste ouverte, mais son framework doit accepter :

- des slots génériques pour quatre écrans colorés ;
- des slots imposés pour le levier, le casque, le câble et les futures pièces uniques.

Une fois fonctionnelle, elle stocke au maximum neuf cubes par type :

| Ressource | Stat technique | Libellé joueur |
|---|---|---|
| Rouge | `AttackDamage` | Force |
| Bleu | `MaxEnergy` | Énergie |
| Violet | `EnergyRechargeRate` | Recharge |
| Vert | `MaxHealth` | Santé |

Le levier fonctionne lorsque le casque est attaché. Il applique les quantités présentes selon les valeurs de balance, remet les quatre compteurs à zéro et détache le casque. Avec quatre compteurs à zéro, il ne modifie rien mais détache quand même le casque.

La limite neuf concerne un lot avant activation, pas le nombre total d'améliorations du run.

### 4.10 Installation des pièces

Chaque pièce produite possède un `LaboratoryPart` avec un `PartId` stable. Une `InstallationZone` :

1. détecte une pièce compatible ;
2. réserve le premier slot compatible libre ;
3. demande au système de grab de céder l'objet sans lancer ;
4. désactive sa physique de transport ;
5. le place sur le transform du slot ;
6. passe son état logique de `Produced` à `Installed` ;
7. rafraîchit les visuels.

`CowboyGrabController.ReleaseAllImmediate()` est trop large pour cette opération. Il faut ajouter une API ciblée du type `TryDetachHeldObject(IGrabbable item)` qui nettoie une ou deux mains, l'inventaire éventuel et les attracteurs sans appliquer de force.

### 4.11 Affichages par matériaux

Les machines conservent le choix `MeshRenderer + Material`. L'état fonctionnel choisit le matériau; le matériau ne détermine jamais l'état.

Un `NumericMeshDisplay` générique doit :

- afficher zéro ou plusieurs chiffres ;
- supporter au minimum les valeurs `0..999` pour les stats, à confirmer ;
- masquer les positions inutilisées ;
- permettre à un `ResourceCostDisplay` de composer valeur actuelle, séparateur, coût requis, icône et validation ;
- supporter jusqu'à cinq lignes de coût sans imposer cinq ressources à chaque recette.

## 5. Architecture cible

```text
RunProgressManager
    owns LaboratoryProgress (état du run)
            |
SceneInitiator
    initializes LaboratoryManager with the current progress
            |
LaboratoryManager (orchestration et transaction de visite)
    |-- ScientistController
    |-- LaboratoryObjectSpawner / StoragePresenter
    |-- BuilderMachine
    |       |-- ConstructionCatalog
    |       |-- ProductionConveyor
    |       `-- ResourceCostDisplay[]
    |-- StatsScreensMachine
    |       `-- InstallationSlot[]
    `-- UpgradeStatsMachine
            |-- InstallationSlot[]
            |-- MachineResourceStorage
            |-- HelmetConnector
            `-- LeverController
```

`LaboratoryManager` orchestre l'entrée, la restauration et la sortie. Il ne déplace pas les pièces, ne choisit pas les matériaux chiffre par chiffre et n'implémente pas l'acceptation de chaque couleur.

Une base `LaboratoryMachine` ne doit être créée que si plusieurs machines partagent réellement un cycle de vie commun. Les nouvelles machines ne doivent pas dériver de `BaseMachine`/`FactoryMachine`, dont les responsabilités worker, waypoint et alarmes sont spécifiques à la Factory.

## 6. Modèle de progression recommandé

Le modèle ci-dessous décrit les responsabilités; ses conteneurs exacts pourront évoluer pendant l'implémentation.

```text
LaboratoryProgress
    SchemaVersion
    HasVisitedLaboratory

    ScientistProgress
        NextVisitDisposition: Work | CowardForVisit
        PendingWhiteCubeCount

    FreeResources: ResourceAmount[]
    FreeJunkCount

    BuilderProgress
        IsUnlocked
        SelectedConstructionId (confort, non critique)
        InternalResources: ResourceAmount[]

    KnownBlueprintIds[]

    ConstructionProgress[]
        ConstructionId
        ProducedCount
        Parts[]
            PartId
            State: Produced | Installed
            InstalledMachineId
            InstalledSlotId

    UpgradeMachineProgress
        Construction/parts state
        InternalResources: ResourceAmount[]
```

Les états purement temporaires — peur de 10 secondes, objet actuellement animé sur le conveyor, position d'un cube, casque en mouvement — ne sont pas persistés.

### Invariants obligatoires

- Aucun compte ne devient négatif.
- Un coût est validé puis consommé dans une seule opération.
- Une construction unique ne peut être confirmée qu'une fois.
- Une pièce unique existe dans un seul état logique.
- Un slot ne contient qu'une pièce et une pièce n'occupe qu'un slot.
- Restaurer un état ne déclenche ni coût, ni upgrade, ni effet sonore/gameplay de nouvelle installation.
- Une même demande de sortie ne peut être finalisée qu'une fois.

## 7. Cycle d'une visite

### 7.1 Entrée

```text
SceneInitiator initialise le joueur
    -> récupère LaboratoryProgress depuis RunProgressManager
    -> LaboratoryManager.Initialize(progress, player context)
    -> configure le scientifique
    -> restaure les machines et slots
    -> dérive tous les visuels
    -> instancie les objets libres sous la trappe
    -> active les interactions
```

La restauration doit disposer d'un mode explicite (`Restore` ou méthodes dédiées) afin de ne pas passer par les commandes de gameplay normales.

### 7.2 Pendant la visite

Chaque interaction met immédiatement à jour l'état runtime autoritaire : transfert de ressource, Junk accepté, construction confirmée, pièce installée ou upgrade appliqué. L'écriture disque n'est pas nécessaire après chaque action.

### 7.3 Sortie

La finalisation résout les faits dépendant de la fin de visite, réconcilie les représentations physiques et l'état logique, capture le joueur, puis autorise le chargement de la scène suivante.

## 8. Plan d'implémentation conseillé

### Phase 0 — contrats et tests de données

- Créer les identifiants, enums et `LaboratoryProgress`.
- Ajouter le progress au `RunProgressManager` et le réinitialiser au début d'un run.
- Tester les invariants, coûts multi-ressources, constructions uniques et restauration sans effets.

### Phase 1 — squelette de visite

- Créer `LaboratoryManager` et son initialisation par la scène.
- Restaurer première visite/visites suivantes.
- Ajouter la transaction de sortie et le hook dans le trigger.
- Ajouter un point de spawn sous la trappe explicitement placé par le créateur.

### Phase 2 — storage et transport inter-scènes

- Créer les types de ressources et transferts d'ownership.
- Adapter le collecteur de niveau pour stocker les cubes au lieu de les appliquer immédiatement.
- Créer le transport logique du Junk.
- Spawn/restauration des objets libres.

### Phase 3 — scientifique

- Créer son prefab/comportement minimal.
- Implémenter Work, peur temporaire, peur pour une visite, mort et objet tenu.
- Résoudre Junk vers WhiteCube à la finalisation.

### Phase 4 — Builder vertical slice

- Créer `ConstructionDefinition`, catalogue et storage interne.
- Implémenter les trois boutons et un coût `WhiteCube x4`.
- Produire une première recette de test avec un conveyor générique.

### Phase 5 — StatsScreens

- Rendre les quatre pièces saisissables et identifiables.
- Créer les quatre slots et l'API de détachement ciblé du grab.
- Restaurer `PartId + SlotId`.
- Afficher les stats actuelles du joueur.

### Phase 6 — UpgradeStatsMachine

- Finaliser la liste des pièces et slots.
- Construire/installer la machine.
- Ajouter compteurs, casque, levier, application et remise à zéro.
- Remplacer définitivement l'ancien flux d'upgrade immédiat dans les niveaux concernés.

### Phase 7 — extensibilité

- Ajouter blueprints, constructions repeatable et coûts multi-ressources réels.
- Ajouter armes ou nouvelles machines sans modifier la logique centrale du Builder.

## 9. Répartition des contributions

### À implémenter côté code

- modèles de progression et ownership ;
- orchestration de visite et transaction de sortie ;
- comportements du scientifique et des machines ;
- catalogue, conveyor générique et slots ;
- intégration grab, stats et sauvegarde ;
- tests Edit Mode des règles déterministes.

### À fournir/configurer par le créateur

- disposition finale des futures salles/niveaux ;
- transforms de spawn, trappe, conveyor et slots dans les prefabs ;
- prefab/visuels/animations du scientifique ;
- matériaux chiffres, icônes, validation et états ON/OFF ;
- liste finale des pièces de l'UpgradeStatsMachine ;
- valeurs de balance et limites d'affichage ;
- nouveaux blueprints, armes et recettes.

Le code doit exposer ces dépendances en données sérialisées avec validation claire, sans inventer silencieusement des positions ou assets manquants.

## 10. Décisions encore ouvertes

### À décider avant le vertical slice complet

1. **WhiteCube du scientifique :** apparaît-il dans sa main à la visite suivante ou directement sous la trappe ?  
   Recommandation : dans sa main si cela crée une interaction lisible; sous la trappe si le scientifique n'a pas encore d'animation/prise fiable.

2. **Déclenchement d'une installation :** uniquement lorsque le joueur tient la pièce, ou aussi lorsqu'elle est lancée/tombe dans la zone ?  
   Recommandation : accepter toute pièce compatible entrant dans la zone, avec une courte stabilité/réservation, car le lancer fait partie du système physique du jeu.

3. **Persistance après fermeture :** faut-il pouvoir reprendre un run en cours ?  
   Recommandation : commencer par la continuité entre scènes, puis sérialiser le même `LaboratoryProgress` si la reprise est confirmée.

4. **Migration du CubeCollector :** quelles scènes doivent conserver temporairement l'amélioration immédiate ?

### Peut être décidé plus tard

- pièces et slots exacts de l'UpgradeStatsMachine ;
- valeur finale de chaque cube ;
- règle de santé/énergie courante après augmentation du maximum ; l'existant ajoute actuellement le gain au courant ;
- format et nombre maximal de chiffres pour chaque stat ;
- durée exacte de peur temporaire ;
- identité ou simple quantité pour les différentes variantes de Junk ;
- ordre et conditions de déblocage des futurs blueprints.

## 11. Critères d'acceptation du premier système complet

- La première visite désactive les machines et permet l'interaction avec le scientifique.
- Tuer le scientifique produit exactement la conséquence prévue à la visite suivante.
- Le Junk reçu est transformé ou rendu sans duplication ni perte.
- Les ressources ramenées d'un niveau apparaissent dans le laboratoire sans avoir déjà amélioré le joueur.
- Le Builder conserve ses ressources lors d'un changement de recette et entre deux visites.
- Une recette unique disparaît dès sa confirmation.
- Quitter pendant une production ne perd ni ne duplique ses pièces.
- Toute pièce produite non installée revient sous la trappe à la visite suivante.
- Les écrans restaurent leur identité et leur slot et affichent les stats vivantes du joueur.
- La machine d'upgrade refuse uniquement la couleur arrivée à neuf et accepte les autres.
- Le levier applique les upgrades une fois, remet les compteurs à zéro et détache le casque.
- Les objets ordinaires ne traversent pas la sortie; le Junk autorisé traverse par état logique.
- La restauration n'émet aucun coût ni effet de gameplay supplémentaire.
- La progression `Level_1 -> Laboratory -> Level_2 -> Laboratory` continue de fonctionner.
- Les tests Edit Mode couvrent les transitions d'état et les invariants sans dépendre de la physique de scène.

## 12. Fichiers existants de référence

- `Assets/Scenes/Level_Laboratory.unity`
- `Assets/Resources/Prefabs/Map/ROOM_Laboratory_1.prefab`
- `Assets/Resources/Prefabs/Map/ROOM_Laboratory_2.prefab`
- `Assets/Resources/Prefabs/Map/Basic/Machines/MachineBuilder.prefab`
- `Assets/Resources/Prefabs/Map/Basic/Machines/UpgradeStatsMachine.prefab`
- `Assets/Resources/Prefabs/Map/Basic/Machines/StatsScreens.prefab`
- `Assets/Scripts/Managers/RunProgressManager.cs`
- `Assets/Scripts/Setup/SceneInitiator.cs`
- `Assets/Scripts/Misc/Interaction/RunStepExitTrigger.cs`
- `Assets/Scripts/Player/PlayerRunStats.cs`
- `Assets/Scripts/Player/PlayerSaveService.cs`
- `Assets/Scripts/Gameplay/Items/GrabController.cs`
- `Assets/Scripts/Gameplay/Items/JunkPickup.cs`
- `Assets/Scripts/Factory/Upgrades/CubeCollector.cs`
- `Assets/Scripts/Factory/Upgrades/CubeConveyorController.cs`
- `Docs/Run_Progression_Static_Labo_Generated_Level_Plan.md`

## 13. Conclusion

Le concept général du brainstorming est cohérent et suffisamment solide pour commencer l'architecture. Les règles les plus importantes — état autoritaire, ownership logique, constructions data-driven, pièces `Produced` jusqu'à installation et restauration sans effets — doivent être conservées.

Les principales corrections concernent l'existant réel : le niveau 2 est déjà présent dans l'itinéraire, Force correspond à `AttackDamage`, les machines sont encore des maquettes visuelles, et le système actuel de cubes applique les upgrades trop tôt. La première implémentation doit donc commencer par le modèle de progression et la transaction de visite, avant de brancher les visuels et la physique des machines.
