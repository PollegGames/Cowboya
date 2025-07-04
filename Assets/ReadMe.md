### Documentation du Jeu : Rogue-lite de Robots Comique

---

### 1. **Résumé Général du Concept du Jeu**

**Genre** : Rogue-lite en 2D  
**Thème** : Monde de robots avec une touche comique et humoristique  
**Objectifs du Joueur** :  
   - Naviguer entre le camp de base et une usine de robots pour progresser.
   - Collecter des ressources, débloquer des attaques, et gérer la moralité pour influencer le style de jeu.
   - Avancer en complétant des runs dans l'usine, en affrontant des ennemis et boss.

---

### 2. **Système de Combat et Progression du Joueur**

#### **Attaques de Base et Combos**
   - **Attaques initiales** : Coup de poing, coup de pied, tir de pistolet.
   - **Création de combos** : Les attaques peuvent être réorganisées via le botaniste pour créer des combos personnalisés, influençant la manière dont le joueur engage les ennemis.

#### **Énergie et Santé**
   - **Énergie** : Se recharge automatiquement avec le temps. Si elle tombe à zéro, le robot passe en mode ragdoll.
   - **Santé** : Peut être augmentée temporairement avec des boosters, mais la capacité maximale de santé est améliorée de façon permanente au camp de base.

#### **Boosters Temporaires et Améliorations Permanentes**
   - **Boosters Temporaires** : Des objets comme des puces ou boissons temporaires augmentent la santé et l’énergie pendant la run.
   - **Améliorations Permanentes** : Les upgrades de santé maximale, de capacité d’énergie, et de vitesse de recharge sont appliquées de façon permanente dans le camp de base.

---

### 3. **Éléments de Progression et Ressources**

#### **Engrenages et Ressources Spéciales**
   - **Engrenages** : Collectés en tuant des ennemis ou en accomplissant des objectifs. Ils sont envoyés au camp via des stations de déploiement pour un stockage permanent.
   - **Ressources Spéciales** : Objets rares utilisés pour des améliorations spécifiques. Elles fonctionnent comme les engrenages et sont envoyées au camp pour être sauvegardées de manière permanente.

#### **Système de Moralité**
   - **Jauge de Moralité** : Réinitialisée à chaque run, elle influe sur le comportement des ennemis et l’apparence des attaques (gentilles ou méchantes).
   - **Effets** : Modifie les attaques et interactions, et influence l’accès à certaines améliorations via le shérif.

---

### 4. **Structure et Fonctionnement du Camp de Base**

#### **Alliés et Leurs Rôles**
   - **Botaniste** : Permet de réorganiser les attaques pour former des combos.
   - **Shérif** : Modifie les attaques selon l’alignement moral (gentilles ou méchantes).
   - **Mécanicien** : Permet d’obtenir de nouvelles attaques ou d’en retirer de l’inventaire.

#### **Interface et Gestion de l’Inventaire**
   - **Inventaire des Attaques** : Gestion de l'ordre et amélioration des attaques via les alliés.
   - **Stations de Déploiement** : Placées dans la map, elles permettent d'envoyer les engrenages et ressources au camp de base pour les sécuriser.

---

### 5. **Ennemis et Boss**

#### **Types d’Ennemis**
   - **Robot Balayeuse** : Se déplace de manière incontrôlable vers le joueur, essayant de le faire glisser.
   - **Tondeuse de Bureau** : Charge frénétiquement vers le joueur, criant des phrases absurdes.
   - **Drone Paparazzi** : Flash le joueur, le ralentissant brièvement.
   
   *Autres idées d'ennemis avec des comportements comiques similaires.*

#### **Mini-Bosses et Boss Final**
   - **L’Instructeur de Danse** : Ennemis avec des attaques en rythme, vulnérable lors de ses pauses de danse.
   - **Directeur Farfelu (Boss Final)** : Dirige l’usine, utilise des gadgets et lance des instructions absurdes en combat.

---

### 6. **Déblocage de Nouveaux Personnages**

#### **Système de Collection de Pièces de Robots**
   - Des parties de robots peuvent être collectées durant les runs, permettant de débloquer de nouveaux personnages jouables avec des styles et attaques uniques.

#### **Personnages Supplémentaires**
   - **Exemples** : NinjaBot, avec des attaques de style furtif, ou TankBot, avec une santé élevée et des attaques puissantes.

---

### 7. **Système de Sauvegarde et Chargement**

#### **Éléments à Sauvegarder et Charger**
   - **Player Stats** : Max Health, Max Energy, Unlocked Attacks, Attack Order.
   - **Inventory** : Total Gears, Special Resources, Collected Robot Parts.
   - **CharacterProgress** : Unlocked Characters, Moral Alignment Influences.
   - **CampBaseConfig** : Purchased Upgrades, Deployment Station Capacity, Ally Interaction Levels.
   - **GameSettings** : Audio, Screen mode, Last Chosen Character.

#### **Fonctionnement du Chargement**
   - **Depuis le Menu** : Quand le joueur appuie sur "Play", la sauvegarde est chargée, et le joueur est envoyé au camp de base pour débuter une nouvelle run.

---

### Diagrammes et Schémas Logiques (à envisager pour la documentation visuelle)

1. **Diagramme de Progression de Run** : Visualiser comment le joueur collecte des ressources, envoie des engrenages, et retourne au camp pour appliquer des améliorations.
2. **Schéma des Interactions au Camp de Base** : Présenter les rôles du botaniste, shérif et mécanicien, et leurs fonctions de gestion des attaques et moralité.
3. **Organigramme des Déblocages** : Schéma montrant la progression des améliorations permanentes, des boosters et des personnages déblocables.
4. **Organigramme de Sauvegarde et Chargement** : Détailler comment chaque catégorie est sauvegardée et chargée au début d'une session.

---

### Conclusion

Ce document rassemble l’ensemble des concepts et fonctionnalités nécessaires pour ton jeu de rogue-lite. Les détails clés, comme les systèmes d’inventaire, moralité, et progression du joueur, sont documentés de manière à faciliter le développement. Tu peux maintenant utiliser cette documentation comme guide pour implémenter chaque élément et t’assurer que le jeu reste fidèle à la vision initiale. 

N’hésite pas à enrichir cette documentation avec des schémas visuels pour une vue d’ensemble encore plus complète !