### Game Documentation: Comedic Robot Rogue-lite

---

### 1. **Overall Game Concept**

**Genre**: 2D rogue-lite
**Theme**: A robot world with a humorous tone
**Player Goals**:
   - Travel between the base camp and the robot factory to progress.
   - Collect resources, unlock attacks and manage morality to influence your play style.
   - Advance through factory runs by fighting enemies and bosses.

---

### 2. **Combat System and Player Progression**

#### **Basic Attacks and Combos**
   - **Starting attacks**: Punch, kick and pistol shot.
   - **Combo creation**: Attacks can be rearranged via the botanist to create custom combos, affecting how you engage enemies.

#### **Energy and Health**
   - **Energy**: Automatically recharges over time. If it reaches zero the robot falls into ragdoll mode.
   - **Health**: Can be temporarily boosted with boosters, while maximum health is permanently improved at camp.

#### **Temporary Boosters and Permanent Upgrades**
   - **Temporary Boosters**: Items like chips or drinks temporarily increase health and energy during a run.
   - **Permanent Upgrades**: Increases to max health, energy capacity and recharge speed are applied permanently at camp.

---

### 3. **Progression Elements and Resources**

#### **Gears and Special Resources**
   - **Gears**: Collected by defeating enemies or completing objectives. They are sent back to camp via deployment stations for permanent storage.
   - **Special Resources**: Rare items used for specific upgrades. They behave like gears and are saved permanently when sent back to camp.

#### **Morality System**
   - **Morality Gauge**: Reset each run, affecting enemy behaviour and the look of attacks (nice or mean).
   - **Effects**: Changes attacks and interactions and influences access to some upgrades via the sheriff.

---

### 4. **Structure and Function of the Base Camp**

#### **Allies and Their Roles**
   - **Botanist**: Allows you to rearrange attacks to form combos.
   - **Sheriff**: Modifies attacks according to moral alignment (good or evil).
   - **Mechanic**: Lets you obtain new attacks or remove some from the inventory.

#### **Interface and Inventory Management**
   - **Attack Inventory**: Manage order and improvements of attacks via allies.
   - **Deployment Stations**: Placed around the map, they send gears and resources back to camp for safekeeping.

---

### 5. **Enemies and Bosses**

#### **Enemy Types**
   - **Sweeper Robot**: Moves uncontrollably toward the player, trying to sweep them up.
   - **Office Mower**: Charges frantically at the player, shouting absurd phrases.
   - **Paparazzi Drone**: Flashes the player, slowing them briefly.

   *Other enemy ideas with similarly comedic behaviours.*

#### **Mini-Bosses and Final Boss**
   - **Dance Instructor**: Enemies with rhythm-based attacks, vulnerable during dance breaks.
   - **Eccentric Director (Final Boss)**: Runs the factory, uses gadgets and shouts ridiculous instructions during battle.

---

### 6. **Unlocking New Characters**

#### **Robot Part Collection System**
   - Robot parts can be gathered during runs, allowing new playable characters with unique styles and attacks to be unlocked.

#### **Additional Characters**
   - **Examples**: NinjaBot with stealth-style attacks, or TankBot with high health and powerful blows.

---

### 7. **Save and Load System**

#### **Data to Save and Load**
   - **Player Stats**: Max Health, Max Energy, Unlocked Attacks, Attack Order.
   - **Inventory**: Total Gears, Special Resources, Collected Robot Parts.
   - **CharacterProgress**: Unlocked Characters, Morality influences.
   - **CampBaseConfig**: Purchased Upgrades, Deployment Station Capacity, Ally interaction levels.
   - **GameSettings**: Audio, Screen mode, Last chosen character.

#### **How Loading Works**
   - **From the Menu**: When the player hits "Play", the save file is loaded and the player is sent to camp to start a new run.

---

### Diagrams and Flow Charts (to consider for visual documentation)

1. **Run Progress Diagram**: Shows how the player collects resources, sends gears, and returns to camp to apply upgrades.
2. **Base Camp Interaction Diagram**: Describes the roles of botanist, sheriff and mechanic, and how they manage attacks and morality.
3. **Unlock Chart**: Shows progression of permanent upgrades, boosters and unlockable characters.
4. **Save and Load Flow**: Details how each category is saved and loaded at the start of a session.

---

### Conclusion

This document gathers all the concepts and features needed for your rogue-lite. Key details such as inventory systems, morality and player progression are documented to ease development. Use it as a guide to implement each element and keep the game true to the original vision.

Feel free to enrich it with visual diagrams for an even more complete overview!
