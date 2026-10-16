# Dev Log

> **Agents:** read this before starting work and update it before finishing (see `AGENTS.md`).
> The sections above "History" describe the **current** state — rewrite them, don't append. Add one short entry per session to History.

_Last updated: 2026-10-16_

## Current state

**Working** — a playable match against an AI rival in `Assets/_Project/Scenes/Main.unity`:
- **Look:** diamond board view (camera turned 45°, pitch 40°): START bottom-middle, RIVAL corner at the top. The board sits on an **island of grass hexagons** in a sea, with a lake behind the rival corner, forests, rocks, a blue and a red village (houses, well, windmill), mountains and hills on the horizon, drifting clouds, a gradient sky (`Art/Shaders/GradientSky.shader`) and linear fog that `CameraFit` starts just behind the board. Free CC0 3D models: KayKit Knight (you) vs Barbarian (rival) with looping idle animations, KayKit blue/red castles, KayKit sword/potion/shield/chest/spike floor, 8 random Quaternius monsters (animated). Spell book and forest trees are primitives.
- **Stats:** hero HP starts at 20 with **no upper limit**; **LV 1**; ⚔ ATK 2 (sword: damage to the other hero); 🔱 SIEGE 2 (spear: damage to the other base when passing it); DEF 0 (each DEF point lowers damage taken, min 1). Base HP starts at 10 (also no upper limit). Badge shows name + LV, BASE, and code-drawn icons (heart/sword/spear/shield, `StatIcons`).
- **Attacks:** ⚔ **Sword tile** = throw a spinning sword at the other hero from anywhere (damage = ATK − DEF); landing on the other hero = melee (same damage). 🔱 **Spear tile** = throw a spear at the other base (damage = SIEGE); passing the enemy home also throws a spear. Both tiles are used up and respawn elsewhere.
- **Getting stronger:** levels (+1 ATK +1 SIEGE each, `Fighter.LevelUp`) from monsters (small +1, medium +2, big +3), spell book "Wisdom" (+1/+2/+3), chests (+1 LV possible) and finishing a lap (passing your own home: +1 LV, +2 HP). **Skill card** tile: +1 ATK (42%), +1 SIEGE (42%) or rare both. **Chest** tile: +1 LV, +6 HP, +1 DEF or a rare DOUBLE card (25% each). Knocked out → home with 20 HP and **everything lost** (LV 1, ATK/SIEGE back to start, DEF 0, roll bonus and rare card cleared); the base keeps its health.
- **Cards:** hand of 3, select (landing preview with step count + what's on the tile, e.g. "3 · Forest") then tap again to roll. ODD, EVEN, FIXED n, RANDOM, and rare **DOUBLE** (two dice, 2–12, ~12% of draws; Twin Moons art).
- **Tile effects** (`TileRules`, counts in `BoardEffects._setups`: sword 2, spear 2, skill card 2, potion 2, elixir 1, shield 1, spell book 2, chest 2, spikes 2, snare 1, forest 2, monster 3): Sword strike, Spear throw, Skill card, Health Potion +5 HP, Repair Elixir (blue) +3 BASE, Shield +1 DEF, Spell Book (random: Fireball -4 HP to rival ignoring DEF / Earthquake -2 rival BASE / Haste next roll +2 / Wisdom +1-3 LV), Chest (loot, see above) — all used up and respawn elsewhere; Spikes -3 HP, Snare skip a turn, Forest next roll -3 (min 1 step) — these stay.
- **Monster fights** (`Monster`, `TileRules.FightMonster`, `GameController.FightMonster`): only the monster's size is shown above it. On landing it picks secret winning numbers (`Monster.Reveal`: Small 4 numbers, Medium 3, Big 1), the fighter **draws a new card**, then picks a card from the hand to roll (you tap twice — the prompt shows your % to win; `IsChoosingFightCard` pauses the turn; the rival uses `RivalAI.ChooseFightCard`). The fight card is used up and refilled after the turn. Win: Small +1 LV +3 HP, Medium +2 LV, Big +3 LV +3 HP, monster respawns elsewhere. Lose: -2/-3/-5 HP, it stays and picks new numbers next time. Tier comes from the model (blob/mushroom/bee small, ghost/golem medium, dragon/demon/yeti big).
- Hands: a played card leaves an empty slot (`_handEmpty`) until `RefillHand` (after the turn, or on landing on a monster).
- Passing the enemy home throws a spear at its base (damage = SIEGE); landing on the other hero is a sword attack (damage = ATK).
- **Combat effects** (`CombatFx`, all made in code at runtime — particles, LineRenderer ribbons, trails): sword slash arc + lunge + hit flash (white→red tint via MaterialPropertyBlock) + shake + camera shake; spear throw in an arc that sticks in the castle; fireball projectile with flames → explosion; earthquake (rune ring, rumbling castle, dust rings, debris); golden level-up beam; auras for pickups/heal/haste; monster claw strike; monster death puff; knock-out fall + smoke; castle explodes + winner cheers on game over. Heroes turn to face their target. `ModelAnimator.Play()` plays one-shot clips (Hit_A, 1H_Melee_Attack_*, Throw, Spellcast_*, Death_A, Cheer; monsters Punch/HitReact/Death) — the builder stores all model clips in `Actions`. Base 0 (also via Earthquake) → YOU WIN / YOU LOSE + PLAY AGAIN.
- HUD: badges (name, BASE, HP/ATK/DEF, status like "Next roll -3"), HISTORY log with named colored events, prompt line, floating texts, particles, bloom/vignette.
- Rival AI (standard): picks the card with the best average outcome, aware of all effects and its own roll bonus.
- **70 PlayMode tests pass** (rules, spells, levels, AI, every effect in the scene, DOUBLE, forest preview, winning, camera framing). No tests for the visual effects themselves yet.

**Not done yet**
- Difficulty levels, sound, saving, Android device build, orientation lock, walk/jump animation while hopping, tests for the new effects.

## Known issues

- Portrait phones: the diamond view is width-limited, so the board is smaller than in landscape.
- The Repair Elixir is the KayKit green bottle with a blue-tinted material (the pack has no blue bottle). Spell book and forest are primitive placeholders.
- Card art is generated on the title screen (~0.4 s per card in the Editor, spread over frames).
- Balance untested — the level system makes both sides grow faster (laps + monsters + Wisdom). Numbers are in `TileRules`, `Monster.LevelReward` and the `BoardEffects` / `GameController` Inspectors (Start Siege).
- Effects are not yet tested on a phone (particle count/perf). Glow material `Art/Materials/FxGlow.mat` is a non-HDR copy of URP ParticlesUnlit (HDR tint turned colors yellow/white without tonemapping).
- Two heroes on the same tile still overlap a bit from the camera (side offset raised to 0.38).
- Scenery not yet checked on a phone (≈400 hex tiles + trees, shadows on). If it is slow: fewer hexes (`reach`), shadows off for trees, or bake.
- Clouds jump back to the other side when they drift too far (`Drift`); mostly hidden by fog/screen edge.

## Next steps

1. User plays the effects/level build and gives feedback (look, timing, balance); then write tests for levels/effects.
2. Merge the stacked PRs in order (oldest first) — nothing has reached `main` yet.
3. Walk/Jump animation while hopping; sounds for hits, spells and level-ups.
4. Difficulty levels for the rival (easy = random card, hard = looks two turns ahead).
5. More content: more monster tiers (weak/strong), character choice (KayKit Mage/Rogue), sounds.
6. Lock orientation and do a first Android build.

## Important context

- User is a Unity beginner and speaks Turkish: keep solutions simple and explain Editor steps. Hobby project, but the repo is public — only use assets with clear free licenses (CC0) and ask the user before downloading anything (file, source, size, license).
- `Main.unity`, the prefabs in `Assets/_Project/Prefabs/{Effects,Fx}` and `Assets/Settings/GameVolumeProfile.asset` are generated by `PrototypeSceneBuilder` (`.cs` = world, `.Ui.cs` = UI; menu **Roll of Legends > Rebuild Prototype Scene**). Rebuilding overwrites manual edits to them. Materials are reused if they exist (delete a `.mat` to get the builder's color back).
- **Scenery** = `PrototypeSceneBuilder.Scenery.cs` (fixed random seed, spots given in screen directions right/far). The new KayKit Hexagon FBX files are in centimetres with a -90° X root turn: `Prop()` keeps the prefab root's rotation/scale (overwriting it made them tiny/lying down). Their `.meta` has `searchTexturesGlobally: 1` (the atlas sits one folder up). All scenery uses `Materials/Models/HexagonsGreen.mat` (atlas with green grass); scenery is batching-static. Scene file is ~2.8 MB now (≈400 hex tiles).
- **3D models** live in `Assets/_Project/Art/Models` (see `CREDITS.md`). GLB heroes need the glTFast package (`com.unity.cloud.gltfast`). The builder's `PlaceModel` turns models to face the camera, scales them to a height/width, hides unwanted parts (KayKit heroes come holding every weapon), loops an Idle clip via `ModelAnimator`, and swaps glTF materials for URP Lit (glTF's shader multiplies by missing vertex colors → black on some drivers).
- Camera angle constants: `CameraYaw`/`CameraPitch` in the builder; models use `ModelYaw = 180 + CameraYaw`; tile labels and preview labels are turned by the same yaw.
- **No image files for 2D art**: card faces (`CardArt`), glow and rounded panels (`UiSprites`) are drawn in code with `Painter`.
- Code map: `Fighter`, `TileEffect`/`TileRules`/`EffectResult` (has `SpellCast`)/`Spell`, `RivalAI`, `CombatFx`, `StatIcons`/`StatIconImage`, `Dice`/`DiceCard`, `BoardEffects` (`EffectSetup` list), `GameController` (turn flow; `TapCard`/`PlayCard`/`GiveCard`), `CardView`, `DiceView`, `MoveHighlighter`, `FxSpawner`, `EventLog`, `FighterBadge`, `ScreenLayout`, `CameraFit`, `ModelAnimator`, `IdleMotion`, `FaceCamera`.
- Pure rules have no scene dependencies — test them directly. Scene tests use `GiveCard` + `BoardEffects.SetEffect` and tap cards twice; `Time.timeScale = 6`.
- Walking order = child order of `Board`; rival home = `TileCount / 2`. UI uses legacy `UnityEngine.UI.Text`. If you add a UI panel the board must avoid, add it to `ScreenLayout.Obstacles`.

## History

### 2026-10-16 — Gameplay v3: attack tiles, skill cards, card-choice monster fights
- User clarified: sword/spear tiles are attacks (hero / base from anywhere), not stat pickups. Stats now come from levels, skill cards and chests.
- Monsters hide their numbers until someone lands on them; you get a new card and choose which card to roll.

### 2026-10-16 — Scenery around the board
- User approved downloading 42 CC0 files (~1.4 MB) from the KayKit Medieval Hexagon Pack (same pack as the castles): hex tiles, nature, village buildings.
- Hex island + lake + forests + villages + mountains + clouds, gradient sky and fog; old dark ground replaced by a sea plane.

### 2026-10-16 — Combat effects, levels, sword vs siege attack
- Added `CombatFx` (code-made slashes, spear throw, fireball, earthquake, level-up, hit flash, screen shake) and one-shot model animations.
- Attack split into ⚔ ATK (heroes) and 🔱 SIEGE (bases); new level system (monsters, Wisdom spell, laps); Spear pickup; badge icons.
- Effects checked with a temporary render-to-PNG PlayMode test (deleted). Note: `WaitForEndOfFrame` never fires in batch mode — read the camera's RenderTexture after `WaitForSeconds` instead.

### 2026-10-16 — Monster fights, harsher knock-outs
- Monsters are fought with a random fight card against their winning numbers (3 tiers); knock-outs reset all buffs.

### 2026-10-16 — CC0 models, diamond camera, gameplay v2
- User rejected their own AI-generated GLB pack; replaced with CC0 KayKit (heroes, castles, props) + Quaternius (8 monsters) after user approval (~19 MB, Git LFS). Idle animations on heroes and monsters.
- Camera: START bottom-middle diamond view, lower angle, bigger models.
- Gameplay: uncapped HP, separate base HP, DEF, health potion vs repair elixir, shield, spell book (3 spells), chest, forest, rare DOUBLE card; preview shows what's on each landing tile.
- Note: earlier PR text said "40 tests"; that count included a temporary screenshot test (real count was 39).

### 2026-10-16 — Visual polish, move preview, match history
- Themed card art drawn in code; select-then-roll with landing tile preview; bigger board/markers and closer camera; responsive portrait/landscape layout.
- HISTORY panel with named, colored events; badges with HP bars; floating texts, particles, post-processing.

### 2026-10-16 — Rival AI + tile effects
- Added an AI rival with its own base on the diagonal corner, HP/ATK/base HP, turn-based play.
- Random swords, potions, spikes, snares and monsters on tiles with primitive placeholder prefabs; win by destroying the enemy base.

### 2026-10-16 — Dice card hand
- Replaced ROLL DICE with a 3-card hand; a new random card is drawn after each move. Fixed cards carry their own number (resolves the old "Fixed" assumption).
- CameraFit now fits exact board corners between UI margins (board is much bigger on screen).

### 2026-10-16 — Project setup + board prototype
- Installed Unity Hub, Unity 6000.3.26f1 + Android modules. Created public repo `bahadircolak44/roll-of-legends` (`main` protected, squash merge).
- Built the first playable prototype (see Current state) with tests, on branch `feature/board-prototype`.
- Added `AGENTS.md`, `CLAUDE.md` and this dev log.
- User feedback: removed the base, board is now 8x8 square (28 tiles), clockwise; added spinning 3D die and hopping hero movement.
