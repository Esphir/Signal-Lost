# Room Authoring — Procedural Generation

The generator (`Signal.Generation`) builds a level by mating **connectors**, never by aligning room
centres. A room is any prefab with a `RoomDefinition` on its root and one or more `RoomConnector`s at
its doorways. Adding a room needs **no code** — just a prefab and a database row.

## 1. Create a new room prefab

1. Duplicate an existing room (e.g. `4 Door Room`) or build a fresh one, and drop it under
   `Assets/Prefabs/Rooms/`.
2. Build your geometry however you like (ProBuilder, meshes — the generator doesn't care).
3. At every doorway, place a **door panel** child whose name contains **`Door`** (e.g. `Blocked Door`).
   Requirements — the setup tool reads these directly:
   - It's the **wall panel that seals the opening**, **active** by default, sitting **in the opening**.
   - Its **forward (blue arrow) must be perpendicular to the wall** (i.e. the panel faces into or out
     of the room, not sideways). The tool takes the doorway's axis from this facing, so a panel turned
     the wrong way produces a wrong direction. A flat wall panel is already like this by default.
   - Each door is at its **own** opening — never leave two doors stacked at the same point.
   - Keep all door panels at the **same floor height** across rooms, so mated rooms' floors line up
     (a door at Y=0 mating a door at Y=2.4 shoves one room 2.4 units up).
4. On the prefab root, make sure there's exactly **one** `RoomDefinition`, and set its **Room Type**
   (Start / Combat / Parkour→Platforming / Treasure / Checkpoint / Transition / End) and **Difficulty
   Tier**. The prefab's contents decide what actually happens inside — the type only drives selection.

> A room with doors on more sides can branch and turn. A room with only two opposite doors can only
> extend a corridor.

## 2. Assign connectors (one click)

Run **Tools ▸ Signal Lost ▸ Rooms ▸ Setup Connectors (Selected Prefabs)** — or *(All in Rooms Folder)*.

For each `Door`/`Blocked Door` child, the tool:
- adds a `RoomConnector` under a `Connectors` child,
- finds which of the room's four **wall faces** the door is nearest, and **snaps the connector onto that
  face** — so the mating point is always on the room's boundary and rooms can never overlap when joined,
- sets **Direction** from that wall and orients the connector so **+Z points out of the room**,
- wires that door panel as the connector's **Blocking Wall** (removed when the door is used, restored
  when it isn't — so an unused door can never open into the void),
- recomputes the room's **Local Bounds** from its renderers.

Because it snaps to the wall, you don't need to place a door precisely — you just need it **on the right
wall, roughly where the hole is**. The name (`Door_N`…) only breaks ties at corners and warns you when a
door is on a different wall than its name suggests; the geometry wins, because the real hole is wherever
you actually put the door.

The tool locates a door by its **mesh** (renderer bounds), not its transform pivot — ProBuilder pivots
frequently sit far from the panel, so the pivot is meaningless. It also anchors the connector at the
door's **base**, so rooms line up floor-to-floor when they mate. Practical upshot: build the door panel
where the opening is; its pivot can be anywhere. If the log warns a door's name doesn't match its wall,
either rename it or ignore it — the connector still lands where the panel actually is.

It's re-runnable (it rebuilds the `Connectors` child each time) and logs everything to the Console.
**Verify in the Scene view:** each connector draws an arrow — green = connected, red = open — with its
cardinal letter. If an arrow points the wrong way, rotate that `Connector_*` child 90°/180° or change
its **Direction** enum. Run **Rooms ▸ Validate** to catch missing blocking walls, stacked doors, or a
duplicate `RoomDefinition`.

## 3. Add the room to the database

Run **Tools ▸ Signal Lost ▸ Rooms ▸ Populate Database**. It adds every room prefab in the folder that
isn't already listed (weight 1, no index limits). Tune per-row in `RoomDatabase.asset`:
- **Weight** — relative likelihood against other rooms of the same type.
- **Min/Max Room Index** — keep a hard room out of the opening, or a finale near the end.

That's the entire "duplicate → rename → set type → add" loop, with no code change.

## 3b. Quick variants from the 4 Door Room

**Tools ▸ Signal Lost ▸ Rooms ▸ Create 4-Door Variants** copies the 4 Door Room into three ready rooms
and registers them:

- **Combat Room** — 3 doors open, 1 sealed; a junction with an `EnemySpawnSection` (trigger + 4 spawn
  points, `DefaultEnemySpawnProfile`, 3–6 enemies) that fires when the player walks in.
- **Treasure Room** — 1 door open, 3 sealed; a reward dead-end, no enemies.
- **End Room** — 1 door open, 3 sealed, `RoomType.End`, carrying an **`EndRoomTrigger`**: entering it
  rebuilds the level with a new seed, keeping the player's health/upgrades ("next floor"). For each run
  to differ, the generation settings must have **Use Random Seed OFF**.

Sealing a door just renames its panel off the word "Door", so the connector setup skips it and the panel
stays as a solid wall. It's re-runnable and skips variants that already exist (delete a prefab to remake
it). To make more (harder combat, more treasure), duplicate a variant, retune it, and add it to the
database — no code.

## 4. How the generator chooses rooms

Per slot, in order:
1. **Checkpoint cadence** — every *Checkpoint Frequency* rooms, if the database has one.
2. **Hallway separation** — after a real room (not another hallway, not a checkpoint), a *Separator
   Type* (Transition) is dropped with probability *Hallway Separation Chance*. Two hallways never chain.
3. **Combat**, capped by *Max Consecutive Combat Rooms*.
4. Otherwise a **breather** — Platforming / Treasure / Transition, whatever the database has.

Then it picks a specific prefab of that type by weight × closeness to the run's target **difficulty
tier** × a **recency** penalty, honouring each row's index window. It chooses a doorway (see **Branch
Chance**; branches may open with **Treasure**), rotates the candidate in 90° steps so its connector
mates the opening, and rejects the placement if it overlaps any placed room — retrying up to
*Placement Attempts* times. Every random draw comes from one seeded RNG, so a seed reproduces a layout
exactly (copy it from the LevelGenerator inspector).

Unused connectors are sealed at the end (blocking wall, or an optional Dead-End Cap prefab). Enemy
spawn sections, checkpoints, loot, hazards and audio live inside the room prefabs and wire themselves
up on placement — no registration needed.
