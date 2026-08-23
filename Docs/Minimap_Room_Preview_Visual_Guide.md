# Cowboya — Minimap Room Preview Visual Guide

**Status:** New visual specification
**Scope:** Room minimap backgrounds and room-purpose icons
**Supersedes:** `Room minimap icon changes` document

---

## 1. Purpose

Every room visible on the Cowboya minimap must communicate two different pieces of information:

1. **What kind of room is this?**
2. **What specifically happens inside it?**

These two meanings must not be carried by the same visual element.

The system is therefore divided into two layers:

```text
ROOM BACKGROUND COLOR
    ↓
Communicates the room family / gameplay role

ICON
    ↓
Communicates the exact purpose of that room
```

Example:

```text
Blue background
    = progression / upgrade-related room

Laboratory icon
    = this specific blue room is the Laboratory

Colored cubes icon
    = this specific blue room is the Cube Collector
```

The player should be able to understand the broad importance of a room from its color before recognizing the icon.

---

# 2. Main Visual Principle

## 2.1 Background = gameplay family

Background colors are not decorative.

Each color represents a consistent gameplay concept.

Rooms with similar functions should deliberately use similar colors.

The goal is **not** to give every room a unique color.

The goal is to create recognizable families.

For example:

* Laboratory and Cube Collector both relate to upgrades → both belong to the **blue family**.
* Work, Garage and Conveyor are industrial/machine spaces → they belong to the **brown family**.
* Security and Spawn represent hostile pressure → they belong to the **red family**.

The icon then differentiates individual rooms inside the same family.

---

## 2.2 Icon = exact room purpose

The icon explains what the room actually contains or does.

Examples:

```text
Brown + gears
    = Work

Brown + garage door
    = Garage

Brown + conveyor belt
    = Conveyor
```

The shared brown background communicates:

> active industrial / machine room

The icon communicates:

> which machine room

This is preferable to assigning unrelated colors to each room.

---

# 3. Color Families

## 3.1 Neutral Grey — low-interest / passive rooms

### Meaning

Grey represents rooms that are:

* neutral;
* passive;
* secondary;
* mostly environmental;
* not an important destination by themselves.

Grey should visually say:

> "This room exists, but nothing especially important is being highlighted here."

### Current rooms

* `Junks`
* `Deads`

Other future rooms can use grey if they do not contain an important interaction, machine, upgrade, objective, recovery point or threat.

### Important rule

Do not use grey simply because a room has no special color yet.

Grey has a deliberate gameplay meaning.

---

# 4. Brown Family — industrial / active machine rooms

## Meaning

Brown indicates:

> "There is an active factory function, machine or useful industrial point here."

These rooms are not necessarily dangerous.

They are places where something happens mechanically or where the player may have a reason to interact.

Brown should feel:

* industrial;
* mechanical;
* warm;
* functional;
* factory-like.

Different brown tones may be used while remaining clearly part of the same family.

---

## 4.1 Work

**Background:** light industrial brown / beige-brown
**Icon:** interlocking gears

Meaning:

> Active working machinery / production room.

---

## 4.2 Garage

**Background:** light industrial brown / beige-brown
**Icon:** industrial roll-up garage door

Meaning:

> Functional mechanical/industrial area.

Work and Garage should intentionally look related.

Their icons provide the distinction.

---

## 4.3 Conveyor

**Background:** light industrial brown / beige-brown
**Icon:** side-view conveyor belt

Meaning:

> Active factory transport machinery.

It should use the same general brown family as Work and Garage.

---

## 4.4 Furnace

**Background:** rust brown / burnt orange-brown
**Icon:** furnace / incinerator

Furnace should **not use hostile red**.

Although fire naturally suggests red, this room is not inherently an enemy or security threat.

Its purpose is simply to collect and burn junk.

The warmer brown communicates:

* heat;
* industry;
* fire;
* machinery;

without falsely communicating:

> danger / hostile room.

The Furnace may therefore use the warmest and darkest brown in the industrial family.

---

# 5. Blue Family — upgrades / technological progression

## Meaning

Blue represents rooms connected to:

* upgrades;
* progression resources;
* advanced technology;
* laboratory systems.

It should communicate:

> "This room is relevant to improving or developing the player."

Blue rooms may use slightly different tones while remaining visually related.

---

## 5.1 Laboratory

**Background:** strong blue
**Icon:** laboratory flask + cube / wrench

Meaning:

> Research, construction and player progression.

---

## 5.2 Cube Collector

**Background:** blue / lighter blue / blue-cyan
**Icon:** five colored cubes/squares

Required cube colors:

* white;
* red;
* green;
* blue;
* violet.

Meaning:

> Upgrade-resource collection.

The Cube Collector belongs in the same visual family as Laboratory because the cubes ultimately exist for player upgrades.

It does not need the exact same shade of blue.

A slightly lighter or more cyan blue can distinguish it while preserving the relationship.

---

# 6. Red Family — hostile / security-controlled rooms

## Meaning

Red is reserved for rooms directly connected to:

* enemies;
* hostile systems;
* security;
* active threats.

Red should have a strong and predictable meaning:

> "This room is related to hostile pressure."

Because of this, red should not be used simply for heat, machinery or visual attractiveness.

---

## 6.1 Security

**Background:** red
**Icon:** security terminal / computer

Meaning:

> Factory security system.

---

## 6.2 Spawn

**Background:** red
**Icon:** spawn / lock-related symbol

Meaning:

> Followers can spawn here.

This is directly connected to hostile pressure and therefore belongs clearly in the red family.

---

# 7. Teal Family — energy / recovery

## Meaning

Teal / turquoise represents:

* recharge;
* energy recovery;
* batteries;
* restoration.

It sits visually between blue and green:

* blue suggests technology;
* green suggests safety/recovery;
* teal combines both without being confused with either.

---

## 7.1 Rest

**Background:** teal / turquoise
**Icon:** charging battery

Meaning:

> Energy recovery / battery-related room.

Rest should not use Laboratory blue because it is not an upgrade room.

It should also avoid the same green as Start because the meanings are different.

Teal provides a distinct identity.

---

# 8. Green — player origin / safe start

## 8.1 Start

**Background:** green
**Icon:** Cowboy / player robot symbol

Meaning:

> Beginning / player origin / safe starting point.

Green should remain strongly associated with the Start room.

Because the minimap currently does not display the player's live position, the Start room must remain immediately recognizable.

This is another reason not to reuse the exact same green for Rest.

---

# 9. Orange / Amber Family — movement and controlled passage

Orange indicates a place related to:

* passage;
* access;
* transport;
* controlled movement.

It should feel important without communicating the same threat level as red.

---

## 9.1 Lift

**Background:** amber / orange
**Icon:** elevator / vertical arrows

Meaning:

> Movement between areas/floors.

The Lift is useful enough that it should not disappear into neutral grey.

Its orange background makes it easy to locate quickly.

---

## 9.2 Reception

**Background:** warning orange / amber
**Icon:** reception desk + service bell

Meaning:

> Controlled passage / guarded checkpoint.

Reception is not simply decorative.

It is likely to contain a guard or prevent the player from passing freely.

For that reason it should not use neutral grey.

However, Reception itself is not necessarily equivalent to an enemy Spawn or Security system.

Orange provides a useful intermediate meaning:

```text
Orange
    = access / checkpoint / controlled movement

Red
    = hostile / security threat
```

Reception and Lift may therefore share the orange family while using somewhat different tones.

Their icons make their exact purposes clear.

---

# 10. Gold — objective / completion

## 10.1 End

**Background:** light gold
**Icon:** trophy

Meaning:

> Goal / completion / exit objective.

Gold should remain rare.

It should immediately tell the player:

> "This is an objective."

Do not reuse gold for ordinary machine or utility rooms.

---

# 11. Current Room Color Map

| Room      | Family      | Background meaning              | Icon                           |
| --------- | ----------- | ------------------------------- | ------------------------------ |
| Labor     | Blue        | Upgrade / progression           | Laboratory flask + cube/wrench |
| Collect   | Blue        | Upgrade resources               | Five colored cubes             |
| Work      | Light Brown | Active industrial machine       | Gears                          |
| Garage    | Light Brown | Active industrial machine       | Garage shutter                 |
| Conveyor  | Light Brown | Active industrial machine       | Conveyor belt                  |
| Furnace   | Rust Brown  | Industrial machine / heat       | Furnace                        |
| Security  | Red         | Hostile / security              | Security terminal              |
| Spawn     | Red         | Hostile / enemy spawning        | Spawn/lock symbol              |
| Rest      | Teal        | Recharge / energy recovery      | Charging battery               |
| Start     | Green       | Player origin / safe start      | Cowboy robot                   |
| Lift      | Orange      | Transport / movement            | Elevator arrows                |
| Reception | Orange      | Controlled passage / checkpoint | Reception desk + bell          |
| End       | Gold        | Objective / completion          | Trophy                         |
| Junks     | Grey        | Passive / secondary             | Trash bin                      |
| Deads     | Grey        | Passive / secondary             | Dead robots / graves           |

---

# 12. Icon Tile

Each room preview contains:

```text
Room background
    ↓
Icon tile
    ↓
Icon artwork
```

The icon tile is intentional.

It should no longer look like an accidental black rectangle created by seeing the back side of a mesh.

---

## 12.1 Tile color

The icon tile should use a **darker version of the room's background family**.

Examples:

```text
Light brown room
    → darker brown tile

Blue room
    → darker blue tile

Red room
    → darker red tile

Teal room
    → darker teal tile

Green room
    → darker green tile

Orange room
    → darker amber/brown tile

Grey room
    → darker grey tile

Gold room
    → darker ochre/gold tile
```

Avoid pure black unless absolutely necessary.

The tile should feel visually integrated with the room.

---

# 13. Icon Artwork Style

All icons should look as though they were designed as part of one set.

They should not look like icons taken from several unrelated libraries.

---

## 13.1 General style

Icons should use:

* simple shapes;
* strong silhouettes;
* consistent line weight;
* limited detail;
* similar visual scale;
* similar padding;
* similar perspective;
* clean industrial styling.

Avoid:

* realistic rendering;
* gradients;
* shadows;
* unnecessary 3D effects;
* tiny details;
* highly different artistic styles;
* emoji-like artwork;
* photographic elements.

---

## 13.2 Main icon color

The default icon artwork should be:

**off-white / very light neutral**

rather than pure black.

The light symbol contrasts naturally with the darker icon tile.

---

## 13.3 Outline

Icons should have a visible dark contrast outline.

However, the outline does not need to be pure black.

Preferred approach:

```text
Grey family
    → dark charcoal outline

Blue family
    → dark navy outline

Brown family
    → dark brown outline

Red family
    → dark burgundy outline

Teal family
    → dark teal outline

Green family
    → dark green outline

Orange family
    → dark brown/orange outline

Gold family
    → dark ochre/brown outline
```

The goal is readability without making every icon look like it has a harsh black border.

---

# 14. Allowed Icon Color Exceptions

Most icons should remain monochrome.

Color inside an icon should only be used when the color itself communicates gameplay information.

---

## 14.1 Cube Collector

The five cubes must remain colored because their colors correspond to actual upgrade resources.

Required:

* white;
* red;
* green;
* blue;
* violet.

Use exactly five clearly separated shapes.

They should remain visible individually at minimap size.

---

## 14.2 Rest

The charging indicator may use green or another strong energy cue.

For example:

* white battery;
* green lightning bolt.

This reinforces the meaning of charging.

---

# 15. Icon Readability Rule

The minimap is small.

An icon that looks attractive at `1024 x 1024` but becomes unclear in gameplay is a failed icon.

Every icon must remain recognizable at approximately:

```text
30–40 px visual size
```

and should also be reviewed around:

```text
128 x 128
```

before final approval.

If a detail disappears at minimap scale, remove it.

---

# 16. Image Creation Rules

When creating a new icon:

1. Start with a square canvas.
2. Prefer `1024 x 1024`.
3. Keep genuine transparency outside the artwork.
4. Center the symbol visually.
5. Leave comfortable padding.
6. Avoid touching canvas edges.
7. Use one obvious silhouette.
8. Keep internal details large.
9. Match the visual weight of existing approved icons.
10. Test the result on its real minimap background.

The PNG itself should contain the icon artwork only.

The room background and icon tile should be handled separately by Unity materials.

Do not bake the room background directly into each PNG.

---

# 17. Adapting Existing Icons

When updating an old icon, do not automatically redesign it.

First determine whether the problem is:

```text
wrong symbol
wrong style
wrong line weight
wrong color
wrong tile/background
too much detail
poor minimap readability
```

If the symbol itself already communicates the room correctly, preserve the concept and adapt only its visual treatment.

Examples:

* Trophy remains a trophy.
* Security terminal remains a terminal.
* Cowboy remains the Start symbol.
* Furnace remains a furnace.

The goal is coherence, not unnecessary redesign.

---

# 18. Icon-Specific Requirements

## Work

At least three interlocking gears of different sizes.

Avoid excessive gear teeth or tiny mechanical details.

---

## Garage

Front-facing industrial roll-up door.

Use:

* strong outer frame;
* visible horizontal slats;
* clear side rails.

It must not resemble a generic normal door.

---

## Conveyor

Side-view industrial conveyor.

Use:

* belt;
* clearly visible rollers;
* support structure.

Avoid loose cubes on top so it cannot be mistaken for Cube Collector.

---

## Furnace

Industrial furnace / incinerator.

It should communicate:

* heat;
* burning;
* industrial processing.

It should not look like a hostile enemy symbol.

---

## Laboratory

Scientific flask combined with a technological/mechanical cue such as:

* cube;
* wrench.

It should communicate experimentation and construction.

---

## Cube Collector

Exactly five separated colored cubes/squares:

* white;
* green;
* red;
* blue;
* violet.

Slight rotation and different heights may be used to suggest falling.

Avoid unnecessary 3D perspective.

---

## Security

Security computer/terminal.

It should feel like a system controlling security, not a generic personal laptop.

---

## Spawn

Symbol should communicate locked/hostile spawning functionality.

Keep it simple and clearly different from Security.

---

## Rest

Battery actively charging.

Use:

* battery body;
* charging bars or terminal;
* strong charging cue such as lightning bolt.

It must not resemble a generic collectible battery.

---

## Start

Cowboy robot/player symbol.

It should remain one of the most recognizable icons in the minimap.

---

## Lift

Elevator / vertical movement.

Up/down arrows are preferred because they remain extremely readable at small scale.

---

## Reception

Reception counter viewed from the front.

Use:

* desk/counter;
* service bell or small reception cue.

Do not include a human figure.

It must read as a checkpoint/reception area rather than a generic table.

---

## End

Trophy.

Keep the silhouette extremely simple.

This should remain immediately recognizable as the objective.

---

## Junks

Trash bin / junk container.

Simple and low visual complexity.

---

## Deads

Dead robots / grave markers.

It should communicate a dead-storage/dead-robot room without using visually aggressive danger imagery.

---

# 19. Visual Priority

The minimap should communicate information in this order:

```text
1. BACKGROUND COLOR
   "What family of room is this?"

2. ICON SILHOUETTE
   "Which room specifically?"

3. SMALL ICON DETAILS
   Additional confirmation only
```

The system must never depend on small details being visible before the player understands the room.

---

# 20. Special Importance of Minimap Clarity

The minimap currently does not provide a live indication of the player's location.

Because of this, room recognition must be especially strong.

The player must be able to quickly recognize landmarks such as:

* Start;
* Lift;
* Laboratory;
* Cube Collector;
* Security;
* Spawn;
* Reception;
* End.

These rooms help the player mentally understand the factory layout.

Distinct color families and strong icons are therefore not just decoration; they are part of navigation.

---

# 21. Unity Preview Structure

The existing minimap architecture should remain separated:

```text
Room..._PreviewMiniMap
    Background
        background material

    Icon
        icon tile / rendering surface
        transparent icon texture
```

Background colors should preferably use reusable shared materials.

Examples:

```text
MinimapGrey
MinimapIndustrialBrown
MinimapRustBrown
MinimapBlue
MinimapCollectBlue
MinimapRed
MinimapTeal
MinimapGreen
MinimapOrange
MinimapGold
```

Exact material count can be reduced when rooms intentionally share the same tone.

Avoid creating a unique material for every room unless its color is genuinely different.

---

# 22. Consistency Rules

When reviewing the complete minimap, verify that:

* all brown rooms clearly look related;
* all blue rooms clearly look related;
* red is reserved for hostile/security-related rooms;
* teal is clearly distinguishable from both blue and green;
* orange is clearly distinguishable from Furnace brown;
* grey looks deliberately neutral;
* gold remains unique enough to represent the objective;
* icon tiles always belong visually to their room background;
* icon sizes are visually balanced;
* no icon looks imported from a completely different art style.

---

# 23. Acceptance Criteria

The visual system is successful when a player can look at the minimap and quickly infer:

### From color alone

* grey → secondary / neutral;
* brown → industrial / active machine;
* blue → upgrades / progression;
* red → hostile / security;
* teal → recharge / energy;
* green → start / origin;
* orange → transport / checkpoint;
* gold → objective.

### From the icon

The player can then identify the exact room.

Examples:

```text
Brown + gears
    → Work

Brown + conveyor
    → Conveyor

Blue + cubes
    → Cube Collector

Orange + arrows
    → Lift

Orange + desk
    → Reception

Red + terminal
    → Security
```

---

# 24. Final Design Principle

The minimap should not be a collection of individually attractive icons.

It should behave as one visual language.

The rule is:

> **Color tells the player what kind of place it is.
> Icon tells the player exactly what place it is.**

Similar rooms should look related.

Different gameplay roles should be clearly distinguishable.

Every icon must remain simple enough to be recognized instantly at normal minimap size.
