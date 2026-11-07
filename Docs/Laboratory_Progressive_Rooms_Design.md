# Laboratoire à salles progressives — conception

- **Date :** 7 novembre 2026
- **Branche :** `tourB`
- **Statut :** première version implémentée le 7 novembre 2026 (voir section 6)
- **Remplace :** la structure en deux salles fixes du document
  [Laboratory_System_Concept_and_Implementation_Base.md](Laboratory_System_Concept_and_Implementation_Base.md).
  Ses principes d'état autoritaire, d'ownership des objets et de restauration sans effet restent valables.

## 1. Vision

Le laboratoire est la zone safe entre deux niveaux. Il **grandit au fil du run** : au départ une seule salle, puis
de nouvelles salles se débloquent grâce aux ressources ramenées des niveaux.

| Visite | Salles disponibles | Ce que fait le joueur |
|---|---|---|
| 1 | Salle du scientifique | Rencontre le scientifique (DocBot). Des pièces détachées de la première machine traînent au sol. Donner un déchet (Junk) au scientifique lui fait produire un cube blanc à la visite suivante. |
| 2 | + Salle de la machine à cubes blancs | La machine est allumée. Son écran montre la prochaine salle à débloquer (la machine à cubes de couleur). Le joueur y dépose ses cubes blancs. |
| Quand assez de cubes blancs ont été déposés | + Salle de la machine à cubes de couleur | Collecte des cubes de couleur (Force, Énergie, Recharge, Santé), qui améliorent les stats. |
| Recette suivante de la machine à cubes blancs | + Salle des écrans | Écrans affichant les stats du joueur (force, vie, énergie, recharge…). |
| Plus tard | + Machine à améliorations, distributeur, etc. | Hors périmètre de ce document. |

La machine à cubes blancs joue le rôle du « Builder » de l'ancien concept. Ses **recettes sont des salles** :
chaque recette coûte N cubes blancs et débloque une salle.

## 2. Décision : toutes les salles dans la scène, débloquées une par une

### Les deux options

| | A. Toutes les salles placées dans `Level_Laboratory`, verrouillées | B. Les salles sont instanciées au runtime, une par une |
|---|---|---|
| Mise en page | Faite à la main dans la scène, visible et réglable dans l'éditeur | Calculée par le code (positions, raccords de portes) |
| Portes et murs | Une porte fermée ou un mur « chantier » entre une salle débloquée et une salle verrouillée | Le code doit générer les raccords et fermer les ouvertures restantes |
| Navigation des robots | Un seul graphe de waypoints statique, créé une fois. Les waypoints des salles verrouillées sont désactivés. Indispensable pour les futurs robots alliés qui suivent le joueur. | Graphe à reconstruire à chaque ajout de salle |
| Caméra et minimap | Limites calculées sur les salles actives | Idem, mais avec en plus des positions variables |
| Effet « une salle apparaît » | Possible : activer la salle avec une animation (porte qui s'ouvre, lumières qui s'allument) | Natif |
| Risque et coût | Faible. Réutilise le chemin « niveau statique » déjà en place (`SceneSetupMode.Laboratory`, `StaticLevelSpawnPoint`) | Élevé. Réimplémente une partie du générateur de map pour un seul cas |

### Recommandation : option A

Toutes les salles du laboratoire sont placées dans `Level_Laboratory`. Chaque salle reçoit un composant
`LaboratoryRoomGate` qui l'affiche ou la masque selon l'état du run. Une salle verrouillée est invisible, ou
visible mais fermée, au choix visuel (voir questions ouvertes). Sa porte d'accès reste fermée.

## 3. Ce que tu dois créer (prefabs et scène)

Oui, il faut **un prefab par salle**, comme pour les autres salles du projet (`ROOM_*`). Ça garde la minimap, les
portes et les waypoints cohérents, et chaque salle reste modifiable seule.

| Prefab | Contenu attendu | Existant réutilisable |
|---|---|---|
| `ROOM_Laboratory_Scientist` | Une seule salle (aujourd'hui répartie sur deux). `ScientistSlot`, point de spawn du DocBot, point de spawn du joueur (`StaticLevelSpawnPoint`), trappe d'apparition des cubes, pièces détachées décoratives, sortie vers le niveau suivant. | `ROOM_Laboratory_1` (contient déjà `LaboratoryManager`, le DocBot, le spawner de cubes collectés) |
| `ROOM_Laboratory_WhiteCubeMachine` | La machine à cubes blancs : zone de dépôt, écran de la recette en cours, compteur. | `MachineBuilder.prefab` (visuel seulement) |
| `ROOM_Laboratory_ColorCubeMachine` | La machine à cubes de couleur : zone de dépôt par couleur, compteurs, levier/casque si conservés. | `UpgradeStatsMachine.prefab` (visuel seulement) |
| `ROOM_Laboratory_Screens` | Les écrans de stats. | `StatsScreens.prefab` et les quatre prefabs d'écran |

Dans la scène `Level_Laboratory` :

1. Place les quatre salles selon le plan final du laboratoire.
2. Entre deux salles voisines, mets une porte. La porte vers une salle verrouillée doit rester fermée.
3. Si des robots doivent circuler (alliés, scientifique), ajoute les waypoints de chaque salle comme pour les
   niveaux 1 à 3.

Le code fera le reste : verrouillage, déverrouillage, comptage des cubes, sauvegarde dans le run, tests.

## 4. Ce que je code ensuite

| Élément | Rôle |
|---|---|
| `LaboratoryRoomId` | Identifiant stable de chaque salle (`Scientist`, `WhiteCubeMachine`, `ColorCubeMachine`, `Screens`, …). |
| `LaboratoryProgress` (existant, schéma v4) | Ajoute : salles débloquées, cubes blancs déposés pour la recette en cours, recettes déjà réalisées. |
| `LaboratoryRoomGate` (MonoBehaviour sur chaque salle) | Applique l'état au chargement : salle active ou masquée, porte ouverte ou fermée, waypoints actifs ou non. Joue l'animation d'apparition lors d'un déblocage en cours de visite. |
| `LaboratoryRoomRecipe` (ScriptableObject) | Une recette = salle débloquée + coût en cubes blancs + condition (salle prérequise, numéro de visite minimum). |
| `WhiteCubeMachine` | Accepte les cubes blancs, avance la recette en cours, met à jour l'écran, déclenche le déblocage. |
| Règle des visites | Visite 1 : salle du scientifique seulement. À partir de la visite 2 : la salle de la machine à cubes blancs se débloque automatiquement. |
| Tests Edit Mode | Déblocage, coût, restauration sans effet, pas de double déblocage, compatibilité avec le schéma v3. |

L'existant est conservé : `LaboratoryManager`, `LaboratoryProgress` (visites, scientifique, Junk → cube blanc),
`LaboratoryCollectedCubeSpawner` et le transport des cubes collectés.

## 5. Questions ouvertes (à trancher avant de coder)

1. **Apparition d'une salle :** immédiatement pendant la visite (la porte s'ouvre), ou seulement à la visite suivante ?
2. **Salle verrouillée :** invisible, ou visible derrière une vitre ou une porte fermée pour donner envie ?
3. **Coûts :** combien de cubes blancs pour la machine à cubes de couleur, puis pour la salle des écrans ?
4. **Ordre des recettes :** machine à cubes de couleur d'abord, puis écrans ? Ou le joueur choisit sur l'écran de la machine (gauche/droite) ?
5. **Cubes de couleur :** la salle des cubes de couleur applique-t-elle les améliorations tout de suite au dépôt, ou par lot avec un levier, comme dans l'ancien concept ?
6. **Pièces détachées de la première salle :** pur décor, ou objets à ramasser qui servent à quelque chose ?
7. **Persistance :** l'état du laboratoire vit pendant le run seulement (comme aujourd'hui). Faut-il garder les salles débloquées d'un run à l'autre ? Ça rejoint la décision globale sur la sauvegarde.

## 6. Décisions et implémentation (7 novembre 2026)

### Réponses retenues

| Sujet | Décision |
|---|---|
| Salles | `ROOM_Laboratory_1` cubes blancs + scientifique (spawn), `_2` cubes de couleur, `_3` stats, `_4` upgrades, plus deux `ROOM_lift` toujours accessibles. |
| Ordre et coûts | Colors 1 cube blanc, puis Stats 2, puis Upgrades 3. |
| Surplus | Les cubes en trop restent dans la machine. Une seule salle se débloque par visite ; la suivante se débloque automatiquement à la visite d'après si la banque suffit. |
| Moment du déblocage | Fin de la visite où le seuil est atteint ; la salle est visible à la visite suivante. |
| Machine à cubes blancs | Présente dès la visite 1, accepte les cubes à partir de la visite 2. |
| Salle verrouillée | Invisible ; la porte de l'ascenseur qui y mène reste fermée. |
| Sorties | Porte gauche de la salle cubes blancs dès le départ, puis la porte extérieure de chaque salle débloquée. Toutes mènent au même niveau suivant. |
| Cubes de couleur | Appliqués dès le dépôt (santé +10, énergie +10, recharge +5, force +5). |
| Cubes non déposés | Réapparaissent à la visite suivante. |

### Code

| Élément | Rôle |
|---|---|
| `LaboratoryRoomId` | Identifiant des quatre salles. |
| `LaboratoryProgress` (schéma 4) | Salles débloquées, banque de la machine à cubes blancs, dépôts blancs et colorés, déblocage en fin de visite. |
| `LaboratoryRoom` (racine de chaque prefab de salle) | Au chargement : cache la salle verrouillée et ferme la porte voisine qui y mène ; sinon crée les déclencheurs de sortie sur les portes `VictoryExit` et l'entrée de cubes de la machine. |
| `LaboratoryCubeMachine` | Entrée de machine créée sur `MachineBuilder` (cubes blancs) et `UpgradeStatsMachine` (couleur). Un cube est déposé quand le joueur le porte dans la zone. Retours par logs `[Laboratory]`. |
| `DoorController.SetExternalLock` | Garde une porte fermée et affichée bloquée. |
| `CubePickup` | Implémente `IGrabControllerDetachReceiver` pour être retiré proprement de la main. |

Pour tester une salle verrouillée en jouant directement la scène, coche `forceUnlockedInEditor` sur son `LaboratoryRoom`.
