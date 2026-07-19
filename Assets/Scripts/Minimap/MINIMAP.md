# Minimap

A Binding of Isaac-style minimap that reveals rooms as you explore and draws the generated dungeon as a
clean grid. It wires itself to the procedural generator — no manual placement, no per-room setup.

## Setup

**Tools ▸ Signal Lost ▸ Minimap ▸ Create Minimap** builds a `Minimap Canvas` (screen-space, top-right,
masked viewport) carrying a `MinimapManager`, and creates a `MinimapDatabase` pre-filled with Unity's
built-in square tinted per room type — so it reads immediately. Press Play. Swap in your own sprites via
the database whenever you like.

## Pieces (one responsibility each)

- **MinimapDatabase** (ScriptableObject) — every sprite: the four fog-state backgrounds, border, current
  indicator, connection line, and a per-`RoomType` icon list. No sprite is hardcoded anywhere else.
- **MinimapIcon** — one `RoomType → sprite (+ tint)` entry. Adding an icon is one list entry.
- **MinimapRoom** — the model for one room: grid cell, type, connections, neighbours, and the
  discovered / visited / current flags. Knows nothing about UI or world geometry.
- **MinimapRoomUI** — one tile: background, glow, icon, border images, plus fade-in and highlight. Only
  renders state; never decides it.
- **MinimapManager** — orchestration: listens for a new layout, builds the model + tiles + connections,
  and flips fog state as the player crosses room bounds. Recentres on the current room.

## How rooms register

`LevelGenerator` raises `MapGenerated` at the end of every `Generate()`. `MinimapManager` subscribes and
pulls `generator.Rooms`, building one `MinimapRoom` per room. So registration is automatic and happens
again on every regeneration (including the End room's reroll) with zero wiring. If generation already ran
before the manager woke, it rebuilds immediately on enable.

## How grid positions are assigned

The world places variable-size rooms by mating connectors, so there's no grid in world space — but the
connector *directions* are a grid. `LevelGenerator.AssignGridCoordinates()` walks the connector graph
from Start (cell 0,0): a North door steps the neighbour +1 in Y, East +1 in X, etc. Each room's cell is
stored on `RoomDefinition.GridPosition`. The minimap lays tiles out at `grid × spacing` — never world
position — so a level of any shape collapses to a tidy one-cell-per-room map. Two rooms rarely contesting
a cell is logged, not fatal.

## Adding a new room type + icon

1. Add the value to `RoomType` (e.g. `Shop`, `Secret`).
2. Add a `MinimapIcon` entry in the `MinimapDatabase` (type + sprite + tint).
3. Author rooms of that type (see `ROOM_AUTHORING.md`) and add them to the `RoomDatabase`.

No minimap code changes — the manager and tile read the type→icon mapping generically.

## Why future layouts just work

The manager holds no assumptions about size, shape, or count: it reads whatever rooms and grid cells the
generator produced, each time `MapGenerated` fires. Fog, connections, centring and icons are all derived
from that data. New room types, new prefabs, longer runs, the End-room reroll — all flow through with no
extra setup. Fullscreen map, player marker, objective markers and multiple floors are deliberately left
as data/overlay extensions on top of this same model (vertical connectors already map to no 2D cell,
reserving them for a floor axis later).
