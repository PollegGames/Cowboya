# Audit des améliorations du projet Cowboya

- Date de l'audit : 8 octobre 2026
- Branche auditée : `tourB`
- Commit de départ : `63c69cf` (`security reception and boss on level 3`)
- Version Unity : `6000.3.22f1`

## 1. Objectif et périmètre

Ce document rassemble les améliorations observables dans le dépôt Cowboya. Il couvre :

- la fiabilité du jeu et des sauvegardes ;
- le gameplay, la progression et le contenu ;
- l'interface, les contrôles et l'accessibilité ;
- l'architecture C# et la maintenabilité ;
- les performances ;
- les scènes, prefabs et autres assets ;
- les tests, le build, la CI et la documentation ;
- l'organisation Git et les dépendances tierces.

L'audit est fondé sur le code, les fichiers Unity sérialisés, les réglages du projet, les journaux et les documents présents dans le dépôt. Il ne remplace pas un playtest complet ni un profilage sur les plateformes cibles.

## 2. Résumé exécutif

Le projet possède de bonnes fondations : il compile sans erreur, dispose d'une suite Edit Mode importante, contient sept scènes de build et plusieurs systèmes récents bien testés, notamment le Worker Collector. Les priorités immédiates ne sont donc pas de tout réécrire, mais de sécuriser les données du joueur, retrouver une suite de tests entièrement verte, fiabiliser les builds et terminer les migrations déjà engagées.

Les dix actions les plus importantes sont :

1. Corriger la sauvegarde : ne plus l'effacer au clic sur Play, rendre le modèle réellement sérialisable et tester un aller-retour complet.
2. Vérifier et corriger la persistance WebGL, actuellement annoncée dans le README mais désactivée dans les `index.html` présents.
3. Relancer et corriger la suite complète : les rapports existants divergent entre 19 et 24 échecs, puis imposer zéro nouvel échec.
4. Remplacer `setup_env.sh`, qui installe Unity 2021 et d'anciennes dépendances dans un projet Unity 6.3.
5. Ajouter une CI qui compile, exécute les tests et produit un build joueur reproductible.
6. Désactiver les traces et sondes IA dans les builds de production et clarifier le statut `NewShadow`/`NewOnly`.
7. Nettoyer les artefacts générés et activer Git LFS pour les sources binaires lourdes.
8. Réduire le poids et l'ambiguïté de `Resources`, puis choisir clairement entre `Resources` et Addressables.
9. Ajouter un vrai menu Options, la reconfiguration des contrôles, la navigation manette, la localisation et les réglages d'accessibilité.
10. Transformer la vision rogue-lite décrite dans `Assets/ReadMe.md` en roadmap produit mesurable.

## 3. État mesuré

| Indicateur | Valeur observée |
|---|---:|
| Scripts C# sous `Assets/Scripts` | 256 |
| Lignes C# non vides sous `Assets/Scripts` | environ 36 800 |
| Fichiers de tests Edit Mode | 52 |
| Attributs `[Test]` / `[UnityTest]` détectés | environ 297 |
| Scènes Unity totales | 44, dont 7 scènes produit dans les Build Settings |
| Prefabs | 165 |
| Fichiers sous `Assets` | environ 2 600 |
| Taille des fichiers suivis par Git | environ 251 Mo |
| Taille des objets Git locaux compressés | environ 427 Mo |
| Fichiers sous `Assets/Resources` | 809, environ 67 Mo |
| Appels `Debug.Log*` dans le code runtime | environ 307 |
| Boucles `Update` / `FixedUpdate` / `LateUpdate` détectées | environ 61 |
| Documents Markdown sous `Docs` avant le présent audit | 31, environ 6 900 lignes |
| Copies `*.private.0` suivies | 25 |

Vérifications réalisées pendant l'audit :

- `dotnet build Cowboya.sln --no-restore --nologo` : succès, 0 erreur et 0 avertissement ;
- aucun fichier ou dossier Unity sans `.meta` et aucun `.meta` orphelin détecté ;
- aucun marqueur de conflit Git détecté dans les fichiers texte inspectés ;
- aucun `m_Script: {fileID: 0}` détecté dans les scènes, prefabs et assets YAML inspectés ;
- le rapport Edit Mode `Logs/WorkerCollectorFix/final-editmode.xml`, daté du 7 octobre 2026 à 12:58 UTC, contient 312 tests : 293 réussis et 19 échoués ;
- le rapport `baseline-editmode.xml`, horodaté une minute plus tard, contient seulement 293 tests : 269 réussis et 24 échoués ; cette divergence confirme la nécessité d'une nouvelle exécution complète ;
- les 103 tests ciblés du Worker Collector ont ensuite réussi, mais ils ne remplacent pas une nouvelle exécution de la suite complète.

La suite Unity complète n'a pas été relancée pendant cet audit, car le projet était déjà ouvert dans une autre instance de l'éditeur. La compilation .NET réussie ne remplace ni une compilation Player Unity ni les tests Edit Mode/Play Mode.

## 4. Priorités

- **P0 — Bloquant** : risque de perte de données, build non fiable ou régression critique.
- **P1 — Important** : qualité, maintenabilité, performance ou expérience joueur nettement dégradée.
- **P2 — Amélioration** : polish, industrialisation ou évolution produit.
- **P3 — Idée** : opportunité à valider par playtest ou décision de design.

## 5. Sauvegarde et progression — P0

### SAVE-01 — Le bouton Play efface la sauvegarde

`Assets/Scripts/UI/Menu/MainMenuController.cs:54-59` appelle `ResetSaveData()` avant de démarrer chaque run. Cela contredit le principe de progression permanente décrit dans le projet et rend un bouton Play potentiellement destructif.

Action recommandée :

- faire de Play un bouton Continuer ;
- créer un bouton Nouvelle partie séparé ;
- demander une confirmation avant toute suppression ;
- conserver au moins une sauvegarde de secours.

Critère de validation : fermer le jeu, le relancer, cliquer sur Continuer et retrouver les améliorations permanentes.

### SAVE-02 — Le modèle de sauvegarde n'est pas fiable avec `JsonUtility`

`Assets/Scripts/Setup/SaveData.cs` n'est pas marqué `[System.Serializable]` et contient deux `Dictionary<>` (`SpecialResources` et `MoralAlignmentInfluences`). La sérialisation Unity ne prend pas directement en charge les dictionnaires. Voir les [règles de sérialisation Unity](https://docs.unity3d.com/6000.0/Documentation/Manual/script-serialization-rules.html).

Action recommandée :

- ajouter une version explicite du schéma, par exemple `SaveVersion` ;
- utiliser un DTO `[Serializable]` composé de champs simples et de listes ;
- remplacer les dictionnaires par des listes de paires sérialisables ou utiliser un sérialiseur JSON adapté ;
- ajouter des migrations de version et des valeurs par défaut sûres.

### SAVE-03 — Seule une petite partie du modèle est réellement écrite

`PlayerSaveService.SaveGame` ne met à jour que `MaxHealth`, `MaxEnergy` et `AttackEnergyCost`. Les champs de santé/énergie courantes, recharge, attaques, ordre des attaques, gears, ressources, personnages, volume et plein écran ne sont pas persistés par ce chemin.

Action recommandée : définir clairement trois catégories :

- données permanentes entre les runs ;
- données temporaires d'un run ;
- préférences utilisateur indépendantes de la partie.

Chaque catégorie doit avoir un propriétaire, un schéma et des tests dédiés.

### SAVE-04 — Écriture non atomique et absence de récupération

`PlayerSaveService` utilise directement `File.ReadAllText` et `File.WriteAllText`, sans gestion des exceptions, fichier temporaire, checksum ni restauration d'un backup. Un arrêt pendant l'écriture ou un JSON invalide peut casser la sauvegarde.

Action recommandée : écrire dans un fichier temporaire, valider le contenu, remplacer l'ancien fichier atomiquement si la plateforme le permet et conserver la dernière version valide.

### SAVE-05 — La persistance WebGL annoncée n'est pas activée

Le README indique que `config.autoSyncPersistentDataPath = true`, mais cette ligne est commentée dans :

- `Cowboya Web/index.html:84` ;
- `Publish/index.html:84`.

Action recommandée : choisir une stratégie WebGL explicite, la tester dans un navigateur après rechargement et aligner le template, le build et le README. Ajouter un test manuel documenté : sauvegarder, fermer l'onglet, rouvrir, charger.

### SAVE-06 — Tests manquants

Ajouter au minimum :

- création d'une nouvelle sauvegarde ;
- sauvegarde puis chargement avec égalité de tous les champs ;
- migration d'une ancienne version ;
- JSON corrompu et restauration du backup ;
- séparation bonus temporaires/permanents ;
- vérification que Continuer ne réinitialise rien ;
- vérification de persistance WebGL sur une build publiée.

## 6. Tests et qualité — P0/P1

### QA-01 — Revenir à une suite complète verte

Les rapports complets disponibles ne concordent pas : `final-editmode.xml` contient 19 échecs sur 312 tests, tandis que `baseline-editmode.xml`, horodaté une minute plus tard, en contient 24 sur 293 tests. Le rapport nommé `final` correspond mieux au nombre de tests actuellement détectés, mais aucun des deux ne doit être considéré comme l'état courant sans nouvelle exécution. Les échecs du rapport `final` touchent notamment :

- pickup de batterie et attracteur de main ;
- `PositionTriggerZone` ;
- IA de garde, worker, slots et réactivation ;
- graphes `StaticLevelPath` et `WaypointService` ;
- prefab de room et traitement du lift.

Certains tests semblent aussi figer un ancien comportement. Par exemple, `KnownCurrentBehavior_GuardStationCheckReturnsFalse` ne correspond plus au code actuel de `MachineSecurityManager`. Il faut classer chaque échec en régression réelle, test obsolète ou test instable, puis atteindre un état vert documenté.

### QA-02 — Corriger l'organisation et la documentation des tests

Le README affirme que les tests sont dans `Assets/Tests/EditMode` avec leur propre assembly. En réalité, ils se trouvent dans `Assets/Editor/UnitTests` et aucun `.asmdef` de tests dédié n'existe.

Action recommandée :

- créer `Assets/Tests/EditMode` et un assembly `Game.EditModeTests` ;
- créer une zone `Assets/Tests/PlayMode` ;
- utiliser des catégories `Smoke`, `Gameplay`, `Prefab`, `Slow` ;
- retirer `Assets/Editor/TestResults_20250924_120323.xml` du dépôt.

### QA-03 — Ajouter des Play Mode tests ciblés

Les Edit Mode tests couvrent bien les contrats unitaires, mais pas suffisamment l'intégration réelle. Les scénarios prioritaires sont :

- Menu -> Level 1 -> Laboratoire -> Level 2 -> Laboratoire -> Level 3 ;
- mort, restart et retour menu ;
- sauvegarde entre deux lancements ;
- combat à deux bras, manette et clavier/souris ;
- cycle complet Worker Collector ;
- garde de réception et boss ;
- build WebGL chargée dans un navigateur.

### QA-04 — Créer une CI Unity

À chaque pull request :

1. vérifier les `.meta` et références manquantes ;
2. compiler les assemblies ;
3. exécuter Edit Mode ;
4. exécuter un petit groupe Play Mode Smoke ;
5. produire au moins un build WebGL ou Windows ;
6. conserver les résultats XML, logs et rapports de taille comme artefacts CI, pas dans `Assets`.

### QA-05 — Ajouter des budgets de non-régression

Suivre automatiquement : temps de démarrage, taille WebGL compressée, mémoire au chargement, allocations par frame, nombre de robots actifs, temps CPU de l'IA et pics de physique 2D.

## 7. Build, environnement et publication — P0/P1

### BUILD-01 — `setup_env.sh` est incompatible avec le projet

Le script configure `UNITY_VER="2021.3.8f1"`, alors que le projet utilise `6000.3.22f1`. Il force aussi d'anciennes versions de Test Framework et Addressables, et ajoute une dépendance factice `com.github.yourorg.pathfinding`.

Risque : un nouveau contributeur qui suit les instructions peut rendre le projet impossible à ouvrir ou modifier le `manifest.json` avec une dépendance invalide.

Action recommandée : remplacer ce script par un bootstrap non destructif qui lit `ProjectSettings/ProjectVersion.txt`, vérifie les prérequis et ne réécrit jamais le manifeste sans validation.

### BUILD-02 — Fournir des commandes Windows et Linux réelles

Dans cet environnement, la commande `unity` ne pointe pas vers l'éditeur Unity. Ajouter :

- `Tools/run-editmode-tests.ps1` pour Windows ;
- `Tools/run-editmode-tests.sh` pour Linux/macOS ;
- une résolution claire du chemin de l'éditeur ;
- des chemins de résultats dans `Logs/` ou un dossier d'artefacts ignoré.

### BUILD-03 — Vérifier un vrai build Player

`Assets/Scripts/Misc/Math/Core/PlainMath.cs:1` importe `UnityEditor` hors d'un `#if UNITY_EDITOR`. Le code utilisant `Handles` est protégé, mais l'import doit l'être également ou être déplacé dans un fichier Editor. La compilation des projets `.csproj` de l'éditeur ne prouve pas qu'un Player compile.

Critère de validation : une build WebGL et une build Windows propres produites depuis un clone neuf.

### BUILD-04 — Finaliser l'identité produit

`ProjectSettings/ProjectSettings.asset` et les profils de build contiennent encore :

- `companyName: DefaultCompany` ;
- `bundleVersion: 1.0` ;
- `com.DefaultCompany.2D-URP` ;
- aucune icône de plateforme configurée.

Définir l'identifiant définitif, le versionnement sémantique, les icônes, le nom de société et le canal Development/Release.

### BUILD-05 — Clarifier la publication WebGL

Le dépôt mélange trois états : `Cowboya Web/`, `Publish/` et `Publish.zip`. Les sous-dossiers `Build/` sont ignorés, ce qui rend les dossiers suivis incomplets, tandis que le ZIP contient une build complète.

Action recommandée :

- ne pas versionner les builds générées ;
- produire un artefact versionné par la CI ;
- documenter les en-têtes HTTP requis pour Brotli ;
- ajouter un test de chargement depuis l'hébergement réel ;
- conserver un seul nom de sortie.

## 8. Architecture et code C# — P1

### ARCH-01 — Réduire les classes trop volumineuses

Plusieurs fichiers dépassent 500 lignes non vides, notamment :

- `RobotTaskNew.cs` : environ 1 070 lignes ;
- `SpawnRobotCollectorController.cs` : environ 840 lignes ;
- `CollectorRobotBodyController.cs` : environ 820 lignes ;
- `WorkerCollectorBodyController.cs` : environ 810 lignes ;
- `RobotBrainNew.cs` : environ 800 lignes ;
- `EnemiesSpawner.cs` : environ 720 lignes ;
- `RobotMemoryStateNew.cs` : environ 670 lignes ;
- `ArmTargetController.cs` : environ 610 lignes.

Découper par responsabilités : décision, exécution de tâche, navigation, combat, présentation, diagnostic et adaptation Unity. Commencer par extraire les parties les mieux couvertes par les tests.

### ARCH-02 — Terminer la migration IA

`RobotNewPipelineRuntime` utilise encore `NewShadow`, mais `DriveGameplayInShadow = true`, alors que les commentaires décrivent le shadow comme non autoritaire. Les producteurs conservent aussi des doublons comme `brain`/`brainNew` et `memory`/`memoryNew`.

Action recommandée :

- écrire la matrice exacte de comportement de chaque mode ;
- valider Worker, SecurityGuard, WorkerSpawner, Follower, Boss, Collector, WorkerCollector et SecurityReception ;
- passer à un seul chemin autoritaire ;
- supprimer les références de migration et renommer les types sans suffixe `New` après stabilisation.

### ARCH-03 — Rendre les diagnostics conditionnels

Les valeurs par défaut activent `EnableTrace`, `EnableEcosystemProbe` et `EnableProbeSummaryOnSceneInit`. Avec environ 307 appels `Debug.Log*`, une build WebGL peut subir bruit, allocations et coût CPU.

Action recommandée : activer les traces uniquement en Editor/Development Build, introduire des niveaux de log et supprimer les logs par-frame en release.

### ARCH-04 — Corriger le cycle de vie des événements UI

Dans `GameUIViewModel` :

- les boutons sont abonnés sans garde après des recherches qui peuvent retourner `null` ;
- `SetPlayer` peut empiler les abonnements ;
- `OnDestroy` retire `OnMoralityChanged`, mais pas `OnEnergyChanged` ni `OnHealthChanged` ;
- l'ancien joueur n'est pas désabonné avant un nouveau `SetPlayer`.

Créer des méthodes `Bind`/`Unbind` idempotentes et ajouter des tests de destruction/rebind.

### ARCH-05 — Réduire les recherches globales

Le code contient environ 35 recherches globales ou chargements directs (`FindObjectsByType`, `FindFirstObjectByType`, `GameObject.Find`, `Resources.Load`). Elles rendent l'ordre d'initialisation et les tests plus fragiles.

Action recommandée : injecter les services lors du bootstrap, garder les fallbacks seulement pour le mode édition et échouer clairement si une dépendance requise manque.

### ARCH-06 — Encapsuler les données mutables

Plusieurs composants exposent encore des champs publics modifiables. Utiliser des champs privés `[SerializeField]`, des propriétés en lecture seule et des méthodes métier afin d'empêcher des états incohérents.

### ARCH-07 — Découper les assemblies

La majorité du runtime se trouve dans un unique assembly `Game`. Créer progressivement des assemblies pour Core, Gameplay, UI, AI et Tests réduit les temps de compilation et empêche des dépendances accidentelles vers l'éditeur.

### ARCH-08 — Fiabiliser les singletons persistants

`AudioManager`, `RunProgressManager`, `SceneController`, le pool et l'adaptateur d'événements utilisent des singletons ou `DontDestroyOnLoad`. Documenter leur propriétaire, leur ordre de création et leur politique de reset entre run, menu et tests.

## 9. Bugs et robustesse ciblés — P0/P1

### BUG-01 — `LoadingScreen` lance des exceptions

Les trois méthodes de `Assets/Scripts/Setup/LoadingScreen.cs` lèvent `NotImplementedException`, et le composant est attaché à `Assets/Resources/Prefabs/UI/LoadingScreen.prefab`.

Choisir entre implémenter le chargement asynchrone avec progression, ou supprimer le prefab et le code morts.

### BUG-02 — Fuites potentielles dans la minimap

`GameUIViewModel` instancie un prefab de minimap et crée une `Texture2D` à partir d'une RenderTexture, sans destruction explicite de ces objets. Plusieurs appels peuvent aussi créer plusieurs instances.

Action recommandée : garder une seule instance, détruire la texture et le prefab au teardown, puis profiler la mémoire lors de plusieurs changements de scène.

### BUG-03 — Projectile couplé à la souris

`BulletScript` suppose qu'une caméra `MainCamera`, un `Rigidbody2D` et `Mouse.current` existent. Il ne fonctionne pas correctement en manette/tactile et contient un `Update` vide.

Action recommandée : transmettre une direction de tir validée au spawn, utiliser `RequireComponent`, supprimer l'`Update` vide et intégrer le projectile au pool.

### BUG-04 — Gestion des références UI fragiles

Plusieurs accès `ui.Q<T>()` sont utilisés sans validation systématique. Ajouter une validation au démarrage avec un message qui donne le nom exact de l'élément manquant, puis désactiver proprement la fonctionnalité.

### BUG-05 — Sauvegarde initialisée après déréférencement

`PlayerTemplate.InitializePlayerStats` lit `saveData` avant son test `saveData != null` et convertit les valeurs `float` en `int`, ce qui peut produire une exception ou perdre de la précision.

## 10. Performance — P1/P2

### PERF-01 — Profiler les boucles par frame

Environ 61 méthodes Update/FixedUpdate/LateUpdate sont présentes. Leur nombre seul n'est pas un bug, mais plusieurs systèmes IA, triggers, IK et effets visuels s'exécutent par robot ou par room.

Créer une scène de stress reproductible et mesurer :

- CPU par système ;
- allocations GC par frame ;
- coût Physics2D ;
- coût du rendu 2D/URP ;
- impact de 10, 25 et 50 robots.

### PERF-02 — Remplacer le polling de zones lorsque possible

`PositionTriggerZone.Update` appelle `Physics2D.OverlapBox` chaque frame. Avec plusieurs zones, préférer des colliders `isTrigger`, ou au minimum des requêtes non allouantes et une fréquence adaptée.

### PERF-03 — Étendre le pooling

Le code runtime contient de nombreux `Instantiate`/`Destroy`. Un pool existe déjà, mais il n'est pas utilisé partout. Prioriser projectiles, cubes, junk, batteries, badges, effets et ennemis fréquemment recréés.

### PERF-04 — Réduire le poids de `Resources`

`Assets/Resources` contient environ 809 fichiers et 67 Mo, dont des PSD et de nombreux prefabs. Tout asset placé sous `Resources` devient difficile à analyser et peut alourdir le build.

Action recommandée :

- garder sous `Resources` uniquement les rares fallbacks indispensables ;
- référencer directement les assets statiques depuis des ScriptableObjects/prefabs ;
- déplacer le contenu chargé à la demande vers Addressables si cette technologie est conservée.

### PERF-05 — Addressables est configuré mais vide

Le groupe `Default Local Group` ne contient aucune entrée, et aucune utilisation runtime d'Addressables n'a été trouvée. Choisir :

- soit supprimer le package et sa configuration ;
- soit migrer un lot mesurable d'assets et ajouter un test de build/catalogue.

### PERF-06 — Auditer les imports lourds

Vérifier la résolution, la compression et le Read/Write des textures, les formats audio, les PSD sources et le GIF de 23 Mo. Conserver les sources de travail hors `Resources` et mesurer leur contribution réelle au Player.

## 11. Interface, contrôles et accessibilité — P1/P2

### UX-01 — Créer un vrai menu Options

Le projet contient des éléments d'interface Options/Save, mais pas de flux complet. Les réglages attendus sont :

- volume master, musique, effets et UI via un `AudioMixer` ;
- plein écran, résolution et qualité ;
- sensibilité, zones mortes et vibration ;
- reconfiguration clavier/manette ;
- taille du texte, contraste, secousses de caméra et flashs ;
- persistance séparée de la sauvegarde de partie.

### UX-02 — Terminer l'utilisation manette

L'asset Input System contient une action Pause, mais `PlayerInputReader` ne l'expose pas et le HUD est principalement mis en pause via un bouton. Vérifier entièrement :

- pause/reprise à la manette ;
- navigation et focus visibles dans tous les menus ;
- retour/annulation ;
- changement de périphérique à chaud ;
- glyphes correspondant au périphérique actif ;
- contrôle indépendant des deux bras.

### UX-03 — Supprimer l'asset d'input obsolète

Deux assets input coexistent :

- `Assets/UpdateManager/InputSystem_Actions.inputactions`, utilisé pour le wrapper généré ;
- `Assets/Resources/Cowboy.inputactions`, plus petit et apparemment inutilisé.

Conserver une source de vérité, déplacer l'asset hors du package tiers `UpdateManager` et générer le wrapper dans un dossier clairement marqué Generated.

### UX-04 — Rendre les écrans adaptatifs

`RunSetupScene.uxml` contient une racine d'environ 1898 x 1021 px et beaucoup de dimensions fixes. Le profil Web cible 960 x 600. Tester 16:9, 16:10, ultrawide, petite fenêtre et différents DPI. Utiliser flex, min/max, scroll et safe area plutôt qu'une mise en page figée.

### UX-05 — Ajouter la localisation

Les textes visibles sont codés en anglais dans les UXML et le C#. Extraire tous les libellés dans des tables de localisation, commencer par anglais/français et tester les textes plus longs.

### UX-06 — Améliorer la lisibilité et l'accessibilité

Ajouter :

- des libellés textuels en plus de la couleur ;
- des palettes daltonisme ;
- un contraste contrôlé ;
- des tailles de texte réglables ;
- des sous-titres et indications visuelles pour les sons importants ;
- une réduction des animations, flashs et tremblements ;
- des cibles de clic et focus cohérents.

### UX-07 — Adapter le bouton Quitter au WebGL

`Application.Quit()` n'apporte pas une expérience utile dans un onglet Web. Masquer ou remplacer Quitter sur WebGL par Retour au menu, Recommencer ou une indication adaptée à la plateforme.

## 12. Gameplay et contenu — P1/P3

### GAME-01 — Transformer la vision en matrice de fonctionnalités

`Assets/ReadMe.md` décrit un rogue-lite avec gears, ressources spéciales, combos réorganisables, camp, alliés, attaques déblocables et personnages. Dans le code actuel, plusieurs de ces concepts n'existent que comme champs de `SaveData` ou comme idées de documentation.

Créer une matrice :

| Fonction | État | Boucle joueur | Données persistées | UI | Tests | Priorité |
|---|---|---|---|---|---|---|
| Progression de niveaux | En cours | Oui | Partielle | Partielle | Oui | P0 |
| Cubes et upgrades de run | En cours | Oui | Partielle | Partielle | Oui | P0 |
| Gears/ressources permanentes | Concept | À définir | Non fiable | Non | Non | P1 |
| Combos d'attaques | Concept/partiel | À définir | Non | Non | Non | P2 |
| Camp et alliés | Concept | À définir | Non | Non | Non | P2 |
| Personnages déblocables | Concept | À définir | Non | Non | Non | P3 |

Cette matrice évite d'entretenir du code de sauvegarde pour des fonctionnalités qui ne sont pas encore décidées.

### GAME-02 — Donner un effet plus lisible à la moralité

La moralité est actuellement modifiée lors du sauvetage/meurtre, affichée dans le HUD et utilisée par une caméra de sécurité avec un seuil négatif. Le document de vision promet un impact plus large.

Décider si elle influence réellement : réactions ennemies, routes, récompenses, attaques, dialogues, boss et fin. Sinon, réduire la promesse et rendre son effet actuel explicitement visible.

### GAME-03 — Renforcer l'onboarding

Le jeu combine déplacement, deux bras, saisie, attaque, énergie, ragdoll, moralité, machines, badges et niveaux. Ajouter un tutoriel progressif dans Level 1, avec rappel contextuel et possibilité de revoir les commandes.

### GAME-04 — Stabiliser et exposer la progression du run

Rendre visibles : l'étape actuelle, l'objectif de sortie, les conditions de victoire, ce qui sera conservé au laboratoire et ce qui sera perdu à la mort.

### GAME-05 — Préparer l'équilibrage par données

Centraliser les valeurs de santé, dégâts, énergie, temps, probabilités et récompenses dans des ScriptableObjects versionnés. Définir des métriques de playtest : durée d'un run, taux de mort, ressources gagnées, usage des attaques et temps passé par room.

### GAME-06 — Fermer les boucles déjà présentes avant d'en ajouter

Prioriser la validation complète de :

- IA `*New` et transitions de rôles ;
- Worker Collector, garage et repos sur plusieurs cycles ;
- Collector volant en vraie scène ;
- garde de réception et boss de Level 3 ;
- laboratoire et upgrades ;
- fin de run, mort, restart et sauvegarde.

## 13. Audio et présentation — P2

### AUDIO-01 — Structurer le mixage

`AudioManager` gère principalement clic, deux musiques et pas, sans `AudioMixer` détecté. Ajouter bus Master/Music/SFX/UI/Ambience, compression/ducking si utile et préférences persistantes.

### AUDIO-02 — Enrichir le feedback

Ajouter ou homogénéiser les sons de frappe, grab/release, manque d'énergie, dégâts, machines, alarmes, portes, récompenses et transitions de niveau. Prévoir des limites de voix et variations de pitch pour éviter la répétition.

### VISUAL-01 — Établir une passe de cohérence

Définir une palette, des règles de lumière, sorting layers, particules, silhouettes, feedback de dégâts et lisibilité des objets interactifs. Les salles doivent rester identifiables sans dépendre uniquement de la minimap.

### VISUAL-02 — Tester les effets de warp et l'IK sur toutes les résolutions

Les effets visuels et physiques sont centraux à l'identité du jeu. Ajouter des captures de référence et une checklist manuelle pour éviter les régressions de rotation, d'attachement, de z-order et de déformation.

## 14. Assets, dépendances et dépôt Git — P1/P2

### REPO-01 — Mettre les gros binaires sous Git LFS

Git LFS est installé mais aucun fichier n'est suivi. Les plus gros fichiers versionnés incluent :

- `UnitOptions.db` : environ 29 Mo ;
- `Publish.zip` : environ 26 Mo ;
- `AJRO5033.GIF` : environ 23 Mo ;
- une spritesheet : environ 21 Mo ;
- deux fichiers Draw.io : environ 17 Mo au total ;
- plusieurs PSD de 4 à 5 Mo.

Utiliser LFS pour les PSD, gros PNG/WAV et sources graphiques réellement nécessaires. Ne pas placer les caches ou builds sous LFS : les supprimer du suivi.

### REPO-02 — Retirer les fichiers générés ou temporaires

Candidats à supprimer du dépôt après validation :

- `Assets/Unity.VisualScripting.Generated/VisualScripting.Flow/UnitOptions.db` ;
- `Assets/_Recovery/` ;
- `Assets/Editor/TestResults_20250924_120323.xml` ;
- les 25 fichiers `*.private.0` ;
- `Publish.zip` et dossiers de build ;
- les exemples TextMesh Pro si aucun n'est utilisé ;
- les scènes d'exemple UpdateManager si elles ne servent plus.

Toujours supprimer/déplacer les assets Unity depuis l'éditeur ou avec leur `.meta` associé.

### REPO-03 — Ajouter `.gitattributes` et `.editorconfig`

Définir les fins de ligne, le traitement LFS, UnityYAMLMerge/Smart Merge si utilisé et les conventions C# du projet. Ajouter un contrôle automatique du format sans reformater brutalement tout le dépôt d'un coup.

### REPO-04 — Nettoyer les packages

Vérifier l'utilité de :

- Addressables, actuellement vide ;
- Visual Scripting, avec un seul `using` apparemment inutile ;
- Multiplayer Center ;
- Collab Proxy ;
- Timeline ;
- modules adaptative performance/VR/terrain/vehicles non utilisés ;
- package UpdateManager, utilisé principalement par le pool via un singleton.

Retirer un package uniquement après recherche des références sérialisées et build complet.

### REPO-05 — Séparer sources et contenu runtime

Les PSD, Draw.io, captures et prototypes doivent être dans un espace `ArtSource/` ou un stockage de production, distinct des assets chargés par le jeu. Cela simplifie les imports et réduit les risques de build accidentel.

### REPO-06 — Uniformiser les noms

Le dépôt mélange `Cowboya`, `CowBoya`, noms anglais/français, espaces et fautes comme `Sanbox`, `Spawing`, `Lifs` ou `Ppuppet`. Définir une convention et renommer progressivement via Unity pour préserver les GUID.

### REPO-07 — Documenter les licences tierces

Le projet inclut Shapes2D, Leohpaz, FlatSkin, TextMesh Pro et UpdateManager, mais ne contient pas de registre central des licences. Ajouter `THIRD_PARTY_NOTICES.md`, vérifier les droits de redistribution et conserver les fichiers de licence requis.

## 15. Documentation — P1/P2

### DOC-01 — Corriger immédiatement le README

Mettre à jour :

- version Unity ;
- emplacement réel des tests ;
- commande Windows et commande Unix ;
- statut réel de la persistance WebGL ;
- séquence des scènes ;
- procédure de build ;
- contrôles clavier/manette ;
- état des fonctionnalités principales.

### DOC-02 — Distinguer plan, état et historique

Les 31 documents recensés avant le présent audit mélangent plans, investigations, guides, validations et états parfois dépassés. Introduire :

- `Docs/README.md` comme index ;
- `Docs/Architecture/` pour les décisions actives ;
- `Docs/Features/` pour les spécifications ;
- `Docs/Validation/` pour les preuves datées ;
- `Docs/Archive/` pour les plans remplacés.

Chaque document doit afficher `Statut`, `Propriétaire`, `Dernière validation` et `Remplacé par` si nécessaire.

### DOC-03 — Tenir une roadmap unique

Créer une roadmap courte avec jalons, critères d'acceptation et liens vers les documents détaillés. Éviter que plusieurs plans deviennent simultanément des sources de vérité.

### DOC-04 — Ajouter les documents projet standards

Ajouter selon le mode de diffusion : `CONTRIBUTING.md`, `CHANGELOG.md`, licence du projet, politique de versions, registre des licences tierces et checklist de release.

## 16. Roadmap recommandée

### Phase 0 — Sécurisation, 1 à 3 jours

- corriger SAVE-01 à SAVE-05 ;
- réparer `setup_env.sh` et le README ;
- corriger l'import `UnityEditor` de `PlainMath` ;
- relancer toute la suite Edit Mode et classer tous les échecs actuels ;
- produire un build WebGL propre ;
- désactiver les traces IA en release.

Livrable : une version qui ne détruit pas la progression, compile en Player et possède un rapport de tests actuel.

### Phase 1 — Fiabilité continue, 1 à 2 semaines

- CI Unity ;
- tests Save/Load et smoke Play Mode ;
- cycle de vie UI et minimap ;
- nettoyage des artefacts du dépôt ;
- Git LFS et licences ;
- clarification du pipeline IA.

Livrable : chaque changement est automatiquement compilé, testé et empaqueté.

### Phase 2 — Performance et expérience, 2 à 4 semaines

- profilage d'une scène de stress ;
- réduction de `Resources` ;
- pooling des objets fréquents ;
- options, audio mixer et rebind ;
- UI responsive, navigation manette et accessibilité ;
- localisation anglais/français.

Livrable : build stable sur la machine cible et WebGL, contrôlable sans souris et lisible sur plusieurs résolutions.

### Phase 3 — Boucle rogue-lite et contenu

- matrice de fonctionnalités ;
- économie gears/ressources ;
- progression permanente ;
- combos, camp et personnages selon priorité produit ;
- équilibrage par données ;
- onboarding et polish des niveaux.

Livrable : une vertical slice dont la boucle complète est compréhensible, rejouable et persistante.

## 17. Checklist de release minimale

- [ ] Aucun clic normal de menu ne supprime une sauvegarde.
- [ ] Sauvegarde/chargement/migration testés, y compris WebGL.
- [ ] Edit Mode : 100 % vert ou dérogations explicitement approuvées.
- [ ] Smoke Play Mode : menu, run, mort, restart, niveau suivant et retour menu.
- [ ] Build WebGL et Windows depuis un clone propre.
- [ ] Aucun log de trace massif en release.
- [ ] Aucun script/référence manquant dans les scènes de build.
- [ ] Aucun artefact `private`, Recovery, résultat de test ou build ajouté au commit.
- [ ] Version, société, identifiant, icônes et changelog à jour.
- [ ] Test clavier/souris et manette.
- [ ] Test de plusieurs résolutions et du son.
- [ ] Licences tierces vérifiées.
- [ ] Taille du build et performances comparées à la release précédente.

## 18. Points positifs à préserver

- Les assemblies C# compilent actuellement sans erreur ni avertissement.
- La couverture de contrats Edit Mode est déjà importante.
- Les tests ciblés récents du Worker Collector sont détaillés et reproductibles.
- Les GUID/meta inspectés sont cohérents.
- La progression de scènes est désormais structurée et les scènes principales sont déclarées dans les Build Settings.
- Les systèmes récents utilisent davantage d'interfaces, de services, d'événements et de ScriptableObjects.
- La documentation technique contient beaucoup de contexte utile ; elle doit être organisée, pas supprimée en bloc.

L'amélioration la plus rentable consiste à consolider ces fondations : données joueur sûres, tests verts, builds reproductibles et une source de vérité unique pour chaque système.
