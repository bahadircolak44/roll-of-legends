using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace RollOfLegends.EditorTools
{
    /// <summary>
    /// Builds Assets/_Project/Scenes/Main.unity from scratch using placeholder primitives,
    /// plus the effect prefabs in Assets/_Project/Prefabs. This file builds the 3D world;
    /// PrototypeSceneBuilder.Ui.cs builds the user interface.
    /// Menu: Roll of Legends > Rebuild Prototype Scene. This overwrites the scene and the prefabs.
    /// Batch mode: Unity -batchmode -executeMethod RollOfLegends.EditorTools.PrototypeSceneBuilder.Build -quit
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";
        private const string PrefabsFolder = "Assets/_Project/Prefabs";
        private const string VolumeProfilePath = "Assets/Settings/GameVolumeProfile.asset";
        private const string ParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

        // A square with 8 tiles on each side. Corner tiles are shared, so there are 4 * 8 - 4 = 28 tiles.
        private const int TilesPerSide = 8;
        private const float TileSpacing = 1.3f;
        private const float TileSize = 1.22f;
        private const float BoardTop = -0.2f;       // top of the wooden board plate
        private const float CourtyardTop = -0.17f;  // top of the grass inside the loop

        private const float DiceSize = 1.3f;
        private const float MarkerScale = 1.55f;    // size of swords, potions, traps and monsters on the tiles
        private const float HeroScale = 0.62f;      // the capsule is 2 units tall at scale 1

        // Castles sit inside the loop on the diagonal toward each side's home corner (START and the opposite
        // corner), far enough in that they don't hide tiles from the tilted camera.
        private static readonly Vector2Int BaseCellPlayer = new Vector2Int(2, 2);
        private static readonly Vector2Int BaseCellRival = new Vector2Int(TilesPerSide - 3, TilesPerSide - 3);

        [MenuItem("Roll of Legends/Rebuild Prototype Scene")]
        private static void BuildFromMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Rebuild Prototype Scene",
                $"This overwrites {ScenePath} and the prefabs it uses. Any manual changes to them will be lost.",
                "Rebuild",
                "Cancel");

            if (confirmed && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Build();
            }
        }

        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder(PrefabsFolder, "Effects");
            EnsureFolder(PrefabsFolder, "Fx");

            CreateLighting();
            CreateGroundAndBoardPlate();
            Board board = CreateBoard();
            DiceView dice = CreateDice();
            Transform playerCastle = CreateCastle("PlayerBase", BaseCellPlayer, Palette.PlayerCastle, Palette.Player);
            Transform rivalCastle = CreateCastle("RivalBase", BaseCellRival, Palette.RivalCastle, Palette.Rival);
            Transform hero = CreateHero("Hero", Palette.Player, rival: false);
            Transform rival = CreateHero("Rival", Palette.Rival, rival: true);
            BoardEffects effects = CreateBoardEffects(board);
            MoveHighlighter highlighter = CreateMoveHighlighter(board);
            CreatePostProcessing();

            Ui ui = CreateUi();
            Camera camera = CreateCamera(board, ui.Layout);
            FxSpawner fx = CreateFx(ui.PopupLayer);

            var controller = new GameObject("GameController").AddComponent<GameController>();
            var serialized = new SerializedObject(controller);
            Set(serialized, "_board", board);
            Set(serialized, "_effects", effects);
            Set(serialized, "_dice", dice);
            Set(serialized, "_heroView", hero);
            Set(serialized, "_rivalView", rival);
            Set(serialized, "_playerCastle", playerCastle);
            Set(serialized, "_rivalCastle", rivalCastle);
            Set(serialized, "_highlighter", highlighter);
            Set(serialized, "_fx", fx);
            Set(serialized, "_playButton", ui.PlayButton);
            Set(serialized, "_title", ui.Title);
            SetArray(serialized, "_cardSlots", ui.Cards);
            Set(serialized, "_playerBadge", ui.PlayerBadge);
            Set(serialized, "_rivalBadge", ui.RivalBadge);
            Set(serialized, "_eventLog", ui.EventLog);
            Set(serialized, "_promptText", ui.Prompt);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[PrototypeSceneBuilder] Built {ScenePath} with {board.TileCount} tiles (camera: {camera.name}).");
        }

        // ---------- Colors ----------

        private static class Palette
        {
            public static readonly Color Sky = Hex("#1c2541");
            public static readonly Color Ground = Hex("#22392f");
            public static readonly Color Wood = Hex("#6e4a2a");
            public static readonly Color Grass = Hex("#7fae5b");
            public static readonly Color TileBase = Hex("#b5a283");
            public static readonly Color TileTop = Hex("#f3e9d2");
            public static readonly Color StartBase = Hex("#3c8a47");
            public static readonly Color StartTop = Hex("#93dd98");
            public static readonly Color RivalHomeBase = Hex("#9c3d35");
            public static readonly Color RivalHomeTop = Hex("#f4a097");
            public static readonly Color Label = Hex("#5b4a38");
            public static readonly Color Player = Hex("#3d7bff");
            public static readonly Color Rival = Hex("#ff5a4a");
            public static readonly Color PlayerCastle = Hex("#6b8fd9");
            public static readonly Color RivalCastle = Hex("#d9776b");
            public static readonly Color Stone = Hex("#8a8f99");
            public static readonly Color Gold = Hex("#ffcc33");
            public static readonly Color Steel = Hex("#dfe7f0");
            public static readonly Color Leather = Hex("#7a4a26");
            public static readonly Color Potion = Hex("#49e07a");
            public static readonly Color Spikes = Hex("#a4adb8");
            public static readonly Color DarkMetal = Hex("#4a4f57");
            public static readonly Color Snare = Hex("#6b4a2b");
            public static readonly Color Monster = Hex("#9b59d0");
            public static readonly Color Ivory = Hex("#f6ead0");
            public static readonly Color White = Hex("#f7f7f2");
            public static readonly Color Ink = Hex("#17171c");
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }

        // ---------- World ----------

        private static void CreateLighting()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            // Soft, slightly cool fill light everywhere so shadows aren't black.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.55f, 0.63f);
        }

        private static void CreateGroundAndBoardPlate()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, BoardTop - 0.16f, 0f);
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            SetMaterial(ground, "Ground", Palette.Ground, smoothness: 0.1f);

            float plateSide = TilesPerSide * TileSpacing + 0.5f;
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "BoardPlate";
            plate.transform.position = new Vector3(0f, BoardTop - 0.08f, 0f);
            plate.transform.localScale = new Vector3(plateSide, 0.16f, plateSide);
            SetMaterial(plate, "Wood", Palette.Wood, smoothness: 0.3f);

            float courtyardSide = (TilesPerSide - 2) * TileSpacing - (TileSpacing - TileSize) - 0.1f;
            GameObject courtyard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            courtyard.name = "Courtyard";
            courtyard.transform.position = new Vector3(0f, (BoardTop + CourtyardTop) / 2f, 0f);
            courtyard.transform.localScale = new Vector3(courtyardSide, CourtyardTop - BoardTop, courtyardSide);
            SetMaterial(courtyard, "Grass", Palette.Grass, smoothness: 0.1f);
        }

        private static Board CreateBoard()
        {
            var board = new GameObject("Board").AddComponent<Board>();
            Vector2Int[] cells = GetLoopCells();

            for (int i = 0; i < cells.Length; i++)
            {
                bool isStart = i == 0;
                bool isRivalHome = i == cells.Length / 2; // diagonally opposite START

                // The tile root sits at the tile's top surface; a darker block with a lighter top hangs below it.
                var tile = new GameObject($"Tile_{i:00}");
                tile.transform.SetParent(board.transform);
                tile.transform.position = CellToWorld(cells[i]);

                Color baseColor = isStart ? Palette.StartBase : isRivalHome ? Palette.RivalHomeBase : Palette.TileBase;
                Color topColor = isStart ? Palette.StartTop : isRivalHome ? Palette.RivalHomeTop : Palette.TileTop;
                string kind = isStart ? "StartTile" : isRivalHome ? "RivalTile" : "Tile";

                GameObject block = Part(tile, PrimitiveType.Cube, "Block", new Vector3(0f, -0.11f, 0f), new Vector3(TileSize, 0.18f, TileSize), keepCollider: true);
                SetMaterial(block, kind + "Base", baseColor, smoothness: 0.15f);
                GameObject top = Part(tile, PrimitiveType.Cube, "Top", new Vector3(0f, -0.02f, 0f), new Vector3(TileSize - 0.14f, 0.04f, TileSize - 0.14f));
                SetMaterial(top, kind + "Top", topColor, smoothness: 0.2f);

                CreateTileLabel(tile.transform, isStart ? "START" : isRivalHome ? "RIVAL" : i.ToString());
            }

            return board;
        }

        /// <summary>
        /// Grid cells around the edge of the square, in walking order: clockwise as seen from the camera,
        /// starting at the bottom-left corner (START).
        /// </summary>
        private static Vector2Int[] GetLoopCells()
        {
            const int last = TilesPerSide - 1;
            var cells = new System.Collections.Generic.List<Vector2Int>();
            for (int y = 0; y <= last; y++) cells.Add(new Vector2Int(0, y));        // left side, going up (from START)
            for (int x = 1; x <= last; x++) cells.Add(new Vector2Int(x, last));     // top side, going right
            for (int y = last - 1; y >= 0; y--) cells.Add(new Vector2Int(last, y)); // right side, going down
            for (int x = last - 1; x >= 1; x--) cells.Add(new Vector2Int(x, 0));    // bottom side, going left
            return cells.ToArray();
        }

        private static Vector3 CellToWorld(Vector2Int cell)
        {
            // Centre the board on the world origin.
            float offset = (TilesPerSide - 1) / 2f;
            return new Vector3((cell.x - offset) * TileSpacing, 0f, (cell.y - offset) * TileSpacing);
        }

        private static void CreateTileLabel(Transform tile, string text)
        {
            // A small world-space UI canvas lying flat along the tile's front edge, leaving the middle
            // free for effect markers. Unlike 3D Text (TextMesh), world-space UI is correctly hidden behind objects.
            var label = new GameObject("Label", typeof(RectTransform), typeof(Canvas));
            label.transform.SetParent(tile, false);
            label.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            const float unitsPerWorldUnit = 200f;
            var rect = label.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(0f, 0.01f, -TileSize * 0.33f);
            rect.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, readable from the camera
            rect.localScale = Vector3.one / unitsPerWorldUnit;
            rect.sizeDelta = new Vector2(TileSize, TileSize * 0.34f) * unitsPerWorldUnit;

            Text labelText = CreateText(label.transform, "Text", text, text.Length > 2 ? 50 : 76, TextAnchor.MiddleCenter);
            labelText.fontStyle = FontStyle.Bold;
            labelText.color = Palette.Label;
        }

        // Dot positions on a 3 x 3 grid (-1, 0, 1) for each number, like a real die. Index = number.
        private static readonly Vector2[][] PipLayouts =
        {
            null,
            new[] { new Vector2(0, 0) },
            new[] { new Vector2(-1, -1), new Vector2(1, 1) },
            new[] { new Vector2(-1, -1), new Vector2(0, 0), new Vector2(1, 1) },
            new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) },
            new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(0, 0), new Vector2(1, -1), new Vector2(1, 1) },
            new[] { new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 0), new Vector2(1, 1) },
        };

        private static DiceView CreateDice()
        {
            // A white die resting in the middle of the board. The GameController hides it until the first roll.
            var dice = new GameObject("Dice");
            dice.transform.position = new Vector3(0f, CourtyardTop + DiceSize / 2f, 0f);

            GameObject body = Part(dice, PrimitiveType.Cube, "Body", Vector3.zero, Vector3.one * DiceSize);
            SetMaterial(body, "Dice", Palette.White, smoothness: 0.6f);

            for (int value = 1; value <= 6; value++)
            {
                CreatePips(dice.transform, value);
            }

            return dice.AddComponent<DiceView>();
        }

        /// <summary>Adds the black dots for one number. DiceView.FaceNormal decides which side shows which number.</summary>
        private static void CreatePips(Transform dice, int value)
        {
            Vector3 normal = DiceView.FaceNormal(value);

            // Two directions lying flat on this face, used as the face's own "right" and "up".
            Vector3 helper = Mathf.Abs(normal.y) > 0.5f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(helper, normal);
            Vector3 up = Vector3.Cross(normal, right);

            float spacing = DiceSize * 0.23f;
            foreach (Vector2 spot in PipLayouts[value])
            {
                // Sink each dot slightly into the face so only a small bump sticks out.
                Vector3 position = normal * (DiceSize / 2f - 0.06f) + (right * spot.x + up * spot.y) * spacing;
                GameObject pip = Part(dice.gameObject, PrimitiveType.Sphere, $"Pip_{value}", position, Vector3.one * DiceSize * 0.18f);
                SetMaterial(pip, "Pip", Palette.Ink, smoothness: 0.4f);
            }
        }

        /// <summary>A cute capsule hero with big eyes. The player wears a crown, the rival has horns.</summary>
        private static Transform CreateHero(string name, Color color, bool rival)
        {
            // The GameController places it on its home tile when the game starts.
            var hero = new GameObject(name);
            SetMaterial(Part(hero, PrimitiveType.Capsule, "Body", Vector3.zero, Vector3.one * HeroScale), name, color, smoothness: 0.55f);

            float front = -HeroScale * 0.45f; // eyes look toward the camera (-z)
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 eye = new Vector3(side * 0.12f, 0.22f, front);
                SetMaterial(Part(hero, PrimitiveType.Sphere, "Eye", eye, Vector3.one * 0.16f), "Eye", Palette.White, smoothness: 0.8f);
                SetMaterial(Part(hero, PrimitiveType.Sphere, "Pupil", eye + new Vector3(0f, -0.01f, -0.06f), Vector3.one * 0.08f), "Pip", Palette.Ink, smoothness: 0.8f);
            }

            if (rival)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    GameObject horn = Part(hero, PrimitiveType.Cylinder, "Horn", new Vector3(side * 0.16f, 0.66f, 0f), new Vector3(0.09f, 0.12f, 0.09f));
                    horn.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 25f);
                    SetMaterial(horn, "Horn", Palette.Ivory, smoothness: 0.4f);
                }
            }
            else
            {
                SetMaterial(Part(hero, PrimitiveType.Cylinder, "Crown", new Vector3(0f, 0.66f, 0f), new Vector3(0.36f, 0.07f, 0.36f)), "Gold", Palette.Gold, smoothness: 0.85f);
                for (int i = 0; i < 5; i++)
                {
                    Vector3 point = Quaternion.Euler(0f, i * 72f, 0f) * new Vector3(0f, 0.76f, 0.15f);
                    SetMaterial(Part(hero, PrimitiveType.Cube, "CrownPoint", point, new Vector3(0.07f, 0.12f, 0.07f)), "Gold", Palette.Gold, smoothness: 0.85f);
                }
            }

            return hero.transform;
        }

        /// <summary>A small castle in the team's color: walls, four corner towers, a keep and a flag.</summary>
        private static Transform CreateCastle(string name, Vector2Int cell, Color wallColor, Color flagColor)
        {
            var castle = new GameObject(name);
            castle.transform.position = CellToWorld(cell) + new Vector3(0f, CourtyardTop, 0f);

            SetMaterial(Part(castle, PrimitiveType.Cube, "Walls", new Vector3(0f, 0.3f, 0f), new Vector3(1.2f, 0.6f, 1.2f)), name, wallColor, smoothness: 0.15f);
            foreach (Vector2 corner in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) })
            {
                Vector3 position = new Vector3(corner.x * 0.6f, 0.4f, corner.y * 0.6f);
                SetMaterial(Part(castle, PrimitiveType.Cylinder, "Tower", position, new Vector3(0.36f, 0.4f, 0.36f)), "Stone", Palette.Stone, smoothness: 0.15f);
            }
            SetMaterial(Part(castle, PrimitiveType.Cylinder, "Keep", new Vector3(0f, 0.85f, 0f), new Vector3(0.55f, 0.3f, 0.55f)), name, wallColor, smoothness: 0.15f);
            SetMaterial(Part(castle, PrimitiveType.Cylinder, "Pole", new Vector3(0f, 1.4f, 0f), new Vector3(0.04f, 0.28f, 0.04f)), "Stone", Palette.Stone);
            SetMaterial(Part(castle, PrimitiveType.Cube, "Flag", new Vector3(0.17f, 1.55f, 0f), new Vector3(0.32f, 0.2f, 0.03f)), name + "Flag", flagColor, smoothness: 0.3f);
            return castle.transform;
        }

        private static BoardEffects CreateBoardEffects(Board board)
        {
            var effects = board.gameObject.AddComponent<BoardEffects>();
            var serialized = new SerializedObject(effects);
            Set(serialized, "_board", board);
            Set(serialized, "_swordPrefab", SavePrefab(BuildSword(), "Effects"));
            Set(serialized, "_potionPrefab", SavePrefab(BuildPotion(), "Effects"));
            Set(serialized, "_spikesPrefab", SavePrefab(BuildSpikes(), "Effects"));
            Set(serialized, "_snarePrefab", SavePrefab(BuildSnare(), "Effects"));
            Set(serialized, "_monsterPrefab", SavePrefab(BuildMonster(), "Effects"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effects;
        }

        private static MoveHighlighter CreateMoveHighlighter(Board board)
        {
            var highlighter = new GameObject("MovePreview").AddComponent<MoveHighlighter>();
            var serialized = new SerializedObject(highlighter);
            Set(serialized, "_board", board);
            Set(serialized, "_highlightPrefab", SavePrefab(BuildTileHighlight(), "Fx"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return highlighter;
        }

        private static FxSpawner CreateFx(RectTransform popupLayer)
        {
            var fx = new GameObject("Fx").AddComponent<FxSpawner>();
            var serialized = new SerializedObject(fx);
            Set(serialized, "_popupLayer", popupLayer);
            Set(serialized, "_burstPrefab", SavePrefab(BuildBurst(), "Fx").GetComponent<ParticleSystem>());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return fx;
        }

        // ---------- Prefabs (placeholder art made of primitives) ----------

        private static GameObject BuildSword()
        {
            var root = new GameObject("Sword");
            var sword = new GameObject("Model");
            sword.transform.SetParent(root.transform, false);
            SetMaterial(Part(sword, PrimitiveType.Cube, "Blade", new Vector3(0f, 0.5f, 0f), new Vector3(0.11f, 0.5f, 0.03f)), "Steel", Palette.Steel, smoothness: 0.9f);
            GameObject tip = Part(sword, PrimitiveType.Cube, "Tip", new Vector3(0f, 0.75f, 0f), new Vector3(0.078f, 0.078f, 0.03f));
            tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            SetMaterial(tip, "Steel", Palette.Steel, smoothness: 0.9f);
            SetMaterial(Part(sword, PrimitiveType.Cube, "Guard", new Vector3(0f, 0.24f, 0f), new Vector3(0.34f, 0.06f, 0.07f)), "Gold", Palette.Gold, smoothness: 0.85f);
            SetMaterial(Part(sword, PrimitiveType.Cube, "Grip", new Vector3(0f, 0.13f, 0f), new Vector3(0.06f, 0.16f, 0.06f)), "Leather", Palette.Leather);
            SetMaterial(Part(sword, PrimitiveType.Sphere, "Pommel", new Vector3(0f, 0.04f, 0f), Vector3.one * 0.08f), "Gold", Palette.Gold, smoothness: 0.85f);
            Idle(sword, spin: 90f, bob: 0.05f);
            return Scaled(root);
        }

        private static GameObject BuildPotion()
        {
            var root = new GameObject("Potion");
            var potion = new GameObject("Model");
            potion.transform.SetParent(root.transform, false);
            SetMaterial(Part(potion, PrimitiveType.Sphere, "Bottle", new Vector3(0f, 0.2f, 0f), Vector3.one * 0.36f), "Potion", Palette.Potion, smoothness: 0.9f);
            SetMaterial(Part(potion, PrimitiveType.Sphere, "Shine", new Vector3(-0.08f, 0.27f, -0.13f), Vector3.one * 0.07f), "Eye", Palette.White, smoothness: 0.9f);
            SetMaterial(Part(potion, PrimitiveType.Cylinder, "Neck", new Vector3(0f, 0.43f, 0f), new Vector3(0.12f, 0.08f, 0.12f)), "Potion", Palette.Potion, smoothness: 0.9f);
            SetMaterial(Part(potion, PrimitiveType.Cylinder, "Cork", new Vector3(0f, 0.53f, 0f), new Vector3(0.1f, 0.03f, 0.1f)), "Leather", Palette.Leather);
            Idle(potion, spin: 60f, bob: 0.05f);
            return Scaled(root);
        }

        private static GameObject BuildSpikes()
        {
            var root = new GameObject("Spikes");
            SetMaterial(Part(root, PrimitiveType.Cylinder, "Plate", new Vector3(0f, 0.01f, 0f), new Vector3(0.58f, 0.01f, 0.58f)), "DarkMetal", Palette.DarkMetal, smoothness: 0.4f);
            for (int i = 0; i < 5; i++)
            {
                Vector3 position = i == 0 ? Vector3.zero : Quaternion.Euler(0f, 45f + i * 90f, 0f) * new Vector3(0f, 0f, 0.17f);
                GameObject spike = Part(root, PrimitiveType.Cube, "Spike", position + new Vector3(0f, 0.15f, 0f), new Vector3(0.07f, 0.3f, 0.07f));
                spike.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                SetMaterial(spike, "Spikes", Palette.Spikes, smoothness: 0.8f);
            }
            return Scaled(root);
        }

        private static GameObject BuildSnare()
        {
            var root = new GameObject("Snare");
            SetMaterial(Part(root, PrimitiveType.Cylinder, "Ring", new Vector3(0f, 0.02f, 0f), new Vector3(0.52f, 0.02f, 0.52f)), "Snare", Palette.Snare);
            SetMaterial(Part(root, PrimitiveType.Cylinder, "Hole", new Vector3(0f, 0.03f, 0f), new Vector3(0.36f, 0.02f, 0.36f)), "Pip", Palette.Ink);
            for (int i = 0; i < 6; i++)
            {
                // Little teeth around the trap.
                Vector3 position = Quaternion.Euler(0f, i * 60f, 0f) * new Vector3(0f, 0.07f, 0.22f);
                SetMaterial(Part(root, PrimitiveType.Cube, "Tooth", position, new Vector3(0.05f, 0.1f, 0.05f)), "Spikes", Palette.Spikes, smoothness: 0.8f);
            }
            return Scaled(root);
        }

        private static GameObject BuildMonster()
        {
            var root = new GameObject("Monster");
            var monster = new GameObject("Model");
            monster.transform.SetParent(root.transform, false);
            SetMaterial(Part(monster, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.35f, 0f), new Vector3(0.45f, 0.35f, 0.45f)), "Monster", Palette.Monster, smoothness: 0.5f);
            foreach (float x in new[] { -0.09f, 0.09f })
            {
                // Eyes look toward the camera (-z).
                SetMaterial(Part(monster, PrimitiveType.Sphere, "Eye", new Vector3(x, 0.5f, -0.18f), Vector3.one * 0.12f), "Eye", Palette.White, smoothness: 0.8f);
                SetMaterial(Part(monster, PrimitiveType.Sphere, "Pupil", new Vector3(x, 0.49f, -0.23f), Vector3.one * 0.06f), "Pip", Palette.Ink, smoothness: 0.8f);
                GameObject horn = Part(monster, PrimitiveType.Cube, "Horn", new Vector3(x * 1.4f, 0.72f, 0f), new Vector3(0.05f, 0.12f, 0.05f));
                horn.transform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Sign(x) * 20f);
                SetMaterial(horn, "Horn", Palette.Ivory, smoothness: 0.4f);
            }
            Idle(monster, spin: 0f, bob: 0.04f);
            return Scaled(root);
        }

        /// <summary>Glowing frame around a tile plus the step count, shown for each possible landing tile.</summary>
        private static GameObject BuildTileHighlight()
        {
            var root = new GameObject("TileHighlight");
            float size = TileSize + 0.12f;
            const float bar = 0.09f;
            Material glow = GetOrCreateMaterial("Highlight", Color.white, unlit: true);
            foreach (Vector2 side in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                bool horizontal = side.y != 0f;
                Vector3 position = new Vector3(side.x, 0f, side.y) * (size - bar) / 2f + new Vector3(0f, 0.03f, 0f);
                Vector3 scale = horizontal ? new Vector3(size, 0.05f, bar) : new Vector3(bar, 0.05f, size);
                Part(root, PrimitiveType.Cube, "Frame", position, scale).GetComponent<Renderer>().sharedMaterial = glow;
            }

            var label = new GameObject("Steps", typeof(RectTransform), typeof(Canvas), typeof(FaceCamera));
            label.transform.SetParent(root.transform, false);
            label.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = label.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(0f, 0.8f, -0.3f); // low and toward the camera, so it reads as "this tile"
            rect.localScale = Vector3.one * 0.006f;
            rect.sizeDelta = new Vector2(160f, 140f);
            Text steps = CreateText(label.transform, "Text", "3", 110, TextAnchor.MiddleCenter);
            steps.fontStyle = FontStyle.Bold;
            Outline outline = steps.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(5f, -5f);
            return root;
        }

        /// <summary>A short sparkly particle burst, recolored by FxSpawner for each event.</summary>
        private static GameObject BuildBurst()
        {
            var root = new GameObject("Burst");
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.gravityModifier = 0.9f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            root.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
            return root;
        }

        private static void Idle(GameObject target, float spin, float bob)
        {
            var idle = target.AddComponent<IdleMotion>();
            var serialized = new SerializedObject(idle);
            serialized.FindProperty("_spinDegreesPerSecond").floatValue = spin;
            serialized.FindProperty("_bobHeight").floatValue = bob;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Scaled(GameObject root)
        {
            root.transform.localScale = Vector3.one * MarkerScale;
            return root;
        }

        // ---------- Camera and post-processing ----------

        private static Camera CreateCamera(Board board, ScreenLayout layout)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Sky;
            camera.fieldOfView = 38f; // narrower lens = less distortion, so far tiles look as big as near ones
            cameraObject.AddComponent<AudioListener>();

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            // Fixed viewing angle; CameraFit only slides the camera, it never turns it.
            cameraObject.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
            cameraObject.transform.position = -cameraObject.transform.forward * 25f;

            var fit = cameraObject.AddComponent<CameraFit>();
            var serialized = new SerializedObject(fit);
            Set(serialized, "_board", board);
            Set(serialized, "_layout", layout);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return camera;
        }

        /// <summary>Soft glow on bright things, darker screen corners and a little extra color.</summary>
        private static void CreatePostProcessing()
        {
            AssetDatabase.DeleteAsset(VolumeProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.7f);
            bloom.scatter.Override(0.65f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.5f);

            var colors = profile.Add<ColorAdjustments>(true);
            colors.saturation.Override(12f);
            colors.contrast.Override(8f);

            foreach (VolumeComponent component in profile.components)
            {
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // ---------- Helpers ----------

        /// <summary>Creates a primitive as a child part. Decorations don't need physics, so colliders are removed.</summary>
        private static GameObject Part(GameObject parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale, bool keepCollider = false)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            if (!keepCollider) Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            return part;
        }

        /// <summary>Saves a built object as a prefab asset (overwriting the old one) and removes it from the scene.</summary>
        private static GameObject SavePrefab(GameObject root, string subfolder)
        {
            string path = $"{PrefabsFolder}/{subfolder}/{root.name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void SetMaterial(GameObject target, string materialName, Color color, float smoothness = 0.25f)
        {
            target.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(materialName, color, smoothness: smoothness);
        }

        /// <summary>Reuses an existing material so color tweaks made in the Editor survive a rebuild.</summary>
        private static Material GetOrCreateMaterial(string materialName, Color color, bool unlit = false, float smoothness = 0.25f)
        {
            string path = $"{MaterialsFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            if (!unlit) material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void Set(SerializedObject serialized, string property, Object value)
        {
            SerializedProperty field = serialized.FindProperty(property);
            if (field == null) throw new System.ArgumentException($"No serialized field '{property}' on {serialized.targetObject.GetType().Name}");
            field.objectReferenceValue = value;
        }

        private static void SetArray(SerializedObject serialized, string property, Object[] values)
        {
            SerializedProperty array = serialized.FindProperty(property);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
