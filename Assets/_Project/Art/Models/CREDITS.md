# 3D model credits

All models here are **CC0 (public domain)**: free for personal and commercial use, no attribution required.
We credit the artists anyway — thank you!

| Folder | Pack | Artist | Source |
|---|---|---|---|
| `KayKit/Adventurers` | KayKit Character Pack: Adventurers 1.0 | Kay Lousberg | https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0 |
| `KayKit/Dungeon` | KayKit Dungeon Remastered 1.0 | Kay Lousberg | https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0 |
| `KayKit/Hexagon` | KayKit Medieval Hexagon Pack 1.0 | Kay Lousberg | https://github.com/KayKit-Game-Assets/KayKit-Medieval-Hexagon-Pack-1.0 |
| `Quaternius/Monsters` | Ultimate Monsters | Quaternius | https://quaternius.com/packs/ultimatemonsters.html |

Each folder keeps the pack's original license file. Only the files the game uses were copied in;
the full packs have many more models (more heroes, skeletons, props, buildings, 50 monsters).

## What is used where

- **Heroes:** `Knight.glb` (you), `Barbarian.glb` (rival) — rigged, 76 animations each; the game loops `Idle`.
- **Bases:** `building_castle_blue.fbx` (you), `building_castle_red.fbx` (rival).
- **Tile effects:** `sword_1handed.fbx` (sword), `bottle_A_labeled_green.fbx` (potion), `floor_tile_big_spikes.fbx` (spikes),
  8 Quaternius monsters picked at random for monster tiles (each loops its idle animation).
- **Scenery** (`KayKit/Hexagon/Nature`, `Tiles`, `Buildings`, placed by `PrototypeSceneBuilder.Scenery.cs`): grass/water hex tiles,
  mountains, hills, trees, rocks, clouds, water plants, and blue/red houses, wells and windmills.
  `hexagons_medieval_green.png` is our edit of the pack's atlas (yellow-lime grass cell recolored green) used only by the scenery.
- **Not used yet (ideas):** `shield_round.fbx`, `shield_badge.fbx`, `sword_shield.fbx` (armor/defense), `chest_gold.fbx`, `coin_stack_small.fbx` (treasure).
