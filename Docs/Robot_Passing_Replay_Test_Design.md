# Passage d'un robot : tests par enregistrement et rejeu

**Statut :** proposition de conception, rien n'est implémenté
**Date :** 4 novembre 2026
**Branche :** `tourB`

## 1. Problème

Le monde est en 2D : pour dépasser un robot, le cowboy doit passer
**physiquement au-dessus ou en dessous** de lui. Pour ça, il saisit le robot
(`EnemyGrabbable`) et le fait tourner avec ses bras.

- Le créateur du jeu réussit ce geste facilement à la main, mais il **ne marche
  pas à tous les coups**.
- C'est l'élément physique clé du jeu. Sans lui, impossible de jouer un niveau
  de bout en bout.
- Il est très difficile à simuler. Écrire un « bot » qui reproduit le geste
  serait long et fragile.

## 2. Idée : enregistrer le vrai geste, puis le rejouer

Tout l'input du joueur passe déjà par l'interface `IPlayerInput`
(`Assets/Scripts/Player/IPlayerInput.cs`) : `Movement`, `Aim`, `JumpDown`,
`CrouchHeld`, `Left/RightGrabHeld`, `Left/RightAttackHeld`, et les compteurs
de pression. `ArmTargetController` et `ArcTargetFollower` lisent cette
interface, et non la manette directement.

On peut donc :

1. **Enregistrer** les inputs pendant que le créateur réussit un passage.
2. **Rejouer** ces inputs avec une autre implémentation de `IPlayerInput`.
3. **Mesurer** si le passage réussit, et à quel taux.

## 3. Composants proposés

### 3.1 `InputRecorder`
- Il enveloppe le `PlayerInputReader` actif et écrit **l'état complet de
  `IPlayerInput` à chaque pas de physique** (`Fixed Timestep` = 0,02 s).
- **Point important :** la visée doit être enregistrée **par rapport au corps
  du cowboy**, pas en coordonnées écran. `AimIsScreenPosition` vaut `true` à la
  souris, et un décalage de caméra fausserait alors le rejeu. Il faut
  convertir la position écran en position monde au moment de l'enregistrement.
- Le fichier enregistré (JSON ou binaire) contient :
  - les métadonnées : type de robot, position relative du robot, sens du
    passage (dessus ou dessous), version du prefab ;
  - la liste des frames d'input.
- L'enregistrement se déclenche avec une touche de debug. On ne garde un
  enregistrement que si le passage a réussi.

### 3.2 `ReplayPlayerInput : IPlayerInput`
- Il lit un enregistrement et renvoie les valeurs frame par frame.
- Il remplace `PlayerInputReader` sur le prefab du joueur pendant le test.

### 3.3 Scène d'arène `Test_RobotPassing`
- Un sol plat, le cowboy et **un seul robot** posé à une distance connue.
- L'IA du robot est en pause (cas statique) ou active (cas réel, Follower qui bloque).
- **Critère de réussite :** le centre du cowboy a franchi le centre du robot
  plus une marge, en moins de N secondes, et le cowboy n'est ni mort ni
  évanoui à cause de l'énergie.

### 3.4 Test Play Mode à variations
Chaque enregistrement est rejoué avec de petites variations :

| Variation | Exemple |
|---|---|
| Position du robot | ±0,1 / ±0,2 |
| Décalage temporel | de 0 à 3 pas de physique |
| Masse ou paramètres de grab | `maximumGrabAcceleration`, `frequency`, `dampingRatio` de `EnemyGrabbable` |
| Type de robot | Worker, SecurityGuard, Follower, Boss |

Le résultat est un **taux de réussite** par cas, par exemple « par-dessus le
Worker : 85 % ».

### 3.5 Déterminisme et vitesse
- On utilise `Physics2D.simulationMode = SimulationMode2D.Script` et on appelle
  `Physics2D.Simulate(0.02f)` à la main. Les rejeux deviennent déterministes
  sur une même machine et peuvent tourner plus vite que le temps réel.
- Les centaines de rejeux peuvent passer en batch mode, via les scripts de
  `Tools/`.

## 4. Mesures à enregistrer pour comprendre les échecs

Pour chaque essai, on enregistre :

- l'angle maximal du robot ;
- la hauteur maximale du cowboy ;
- la distance horizontale finale ;
- la force et la distance du `TargetJoint2D` pendant le grab ;
- le moment où le grab se relâche, et pourquoi (bouton relâché, détachement
  forcé, mort) ;
- l'énergie restante.

Ces mesures permettent de savoir quel paramètre fait échouer le passage, au
lieu de seulement constater l'échec.

## 5. Utilisation

1. Le créateur enregistre 10 à 20 passages réussis par type de robot et par
   sens (dessus ou dessous).
2. Les enregistrements sont commités dans un dossier de test, par exemple
   `Assets/Tests/PlayMode/RobotPassing/Recordings/`.
3. Le test Play Mode devient un **test de non-régression** : si un commit fait
   chuter un taux (par exemple de 85 % à 40 %), on le voit immédiatement.
4. Plus tard, les mêmes outils pourront servir de « joueur fantôme » pour
   rejouer un niveau complet (Level 1, puis Level 2, puis le Labo).

## 6. Limites

- Un rejeu ne vaut que pour une situation proche de l'enregistrement. C'est
  pour ça qu'on commence par une arène contrôlée, pas par un niveau complet.
- Si on modifie fortement le prefab du cowboy (masses, longueur des bras,
  IK), il faudra réenregistrer les passages.
- Le déterminisme n'est garanti que sur une même machine, avec la même
  version d'Unity.

## 7. Étapes proposées

1. `InputRecorder` et `ReplayPlayerInput`, avec un test qui rejoue un
   enregistrement et vérifie que les inputs sont identiques frame par frame.
2. Scène d'arène et critère de réussite pour un seul cas (Worker, par-dessus).
3. Variations et rapport de taux de réussite.
4. Enregistrement des mesures et réglage des paramètres de grab.
5. Extension à tous les types de robots et aux deux sens de passage.
