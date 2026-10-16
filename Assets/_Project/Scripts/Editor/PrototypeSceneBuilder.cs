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
        private const float MarkerScale = 1.55f;    // size of the primitive-made snare marker
        private const float HeroCenterHeight = 0.62f; // must match GameController's Hero Height

        // 3D models: free CC0 packs by KayKit (kaylousberg.com) and Quaternius (quaternius.com).
        // See Assets/_Project/Art/Models/CREDITS.md. Models face +Z; the camera looks toward +Z, so they are
        // turned around to face the player.
        private const string ModelsFolder = "Assets/_Project/Art/Models";

        // The camera looks at the board from the START corner (turned 45 degrees), so START is at the bottom
        // middle of the screen and the board looks like a diamond. Lower pitch = more from the side.
        private const float CameraYaw = 45f;
        private const float CameraPitch = 40f;
        private const float ModelYaw = 180f + CameraYaw; // models face the camera
        // Monster models by size: small ones are easy fights, big ones hard (see Monster).
        private static readonly (string model, MonsterTier tier)[] MonsterModels =
        {
            ("GreenBlob", MonsterTier.Small), ("Mushnub", MonsterTier.Small), ("Armabee", MonsterTier.Small),
            ("Ghost", MonsterTier.Medium), ("Goleling", MonsterTier.Medium),
            ("Dragon", MonsterTier.Big), ("Demon", MonsterTier.Big), ("Yeti", MonsterTier.Big),
        };

        // Animation clips to loop while standing still, in order of preference.
        private static readonly string[] IdleClipNames =
        {
            "Idle", "CharacterArmature|Idle", "CharacterArmature|Flying_Idle",
        };

        // Castles sit inside the loop on the diagonal toward each side's home corner (START and the opposite
        // corner), close enough to the middle that they don't hide the far tiles from the low camera.
        private static readonly Vector2 BaseCellPlayer = new Vector2(2.2f, 2.2f);
        private static readonly Vector2 BaseCellRival = new Vector2(TilesPerSide - 3.2f, TilesPerSide - 3.2f);

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
            EnsureFolder(MaterialsFolder, "Models");

            CreateLighting();
            CreateGroundAndBoardPlate();
            CreateScenery();
            Board board = CreateBoard();
            DiceView dice = CreateDice();
            Transform playerCastle = CreateCastle("PlayerBase", BaseCellPlayer, "KayKit/Hexagon/building_castle_blue.fbx");
            Transform rivalCastle = CreateCastle("RivalBase", BaseCellRival, "KayKit/Hexagon/building_castle_red.fbx");
            // KayKit heroes come holding every weapon of their kit at once; keep one weapon and one shield.
            Transform hero = CreateHero("Hero", "KayKit/Adventurers/Knight.glb",
                "1H_Sword_Offhand", "2H_Sword", "Rectangle_Shield", "Round_Shield", "Spike_Shield");
            Transform rival = CreateHero("Rival", "KayKit/Adventurers/Barbarian.glb",
                "1H_Axe_Offhand", "2H_Axe", "Mug");
            BoardEffects effects = CreateBoardEffects(board);
            MoveHighlighter highlighter = CreateMoveHighlighter(board);
            CreatePostProcessing();

            Ui ui = CreateUi();
            Camera camera = CreateCamera(board, ui.Layout);
            FxSpawner fx = CreateFx(ui.PopupLayer);
            CombatFx combat = CreateCombatFx();

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
            Set(serialized, "_combat", combat);
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
            public static readonly Color Wood = Hex("#6e4a2a");
            public static readonly Color Grass = Hex("#7fae5b");
            public static readonly Color TileBase = Hex("#b5a283");
            public static readonly Color TileTop = Hex("#f3e9d2");
            public static readonly Color StartBase = Hex("#3c8a47");
            public static readonly Color StartTop = Hex("#93dd98");
            public static readonly Color RivalHomeBase = Hex("#9c3d35");
            public static readonly Color RivalHomeTop = Hex("#f4a097");
            public static readonly Color Label = Hex("#5b4a38");
            public static readonly Color Spikes = Hex("#a4adb8");
            public static readonly Color Snare = Hex("#6b4a2b");
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
            ground.transform.position = new Vector3(0f, BoardTop - 0.7f, 0f); // below the grass hexagons; only seen past them
            ground.transform.localScale = new Vector3(12f, 1f, 12f); // big enough to reach the horizon
            SetMaterial(ground, "Sea", SeaColor, smoothness: 0.75f); // the hexagon island floats in a calm sea (see Scenery)

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
            return CellToWorld((Vector2)cell);
        }

        private static Vector3 CellToWorld(Vector2 cell)
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
            // Lie flat on the edge nearest the camera, turned so the text reads upright from the camera.
            rect.localPosition = Quaternion.Euler(0f, CameraYaw, 0f) * new Vector3(0f, 0.01f, -TileSize * 0.33f);
            rect.localRotation = Quaternion.Euler(90f, CameraYaw, 0f);
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

        /// <summary>
        /// An animated hero model standing on its tile. The root sits at the hero's middle (where
        /// GameController places it); the model's feet are moved down to the tile surface.
        /// </summary>
        private static Transform CreateHero(string name, string model, params string[] hiddenParts)
        {
            // The GameController places it on its home tile when the game starts.
            var hero = new GameObject(name);
            GameObject body = PlaceModel(hero, model, height: 1.85f, hiddenParts: hiddenParts);
            body.transform.localPosition = new Vector3(0f, -HeroCenterHeight, 0f);
            return hero.transform;
        }

        private static Transform CreateCastle(string name, Vector2 cell, string model)
        {
            var castle = new GameObject(name);
            castle.transform.position = CellToWorld(cell) + new Vector3(0f, CourtyardTop, 0f);
            PlaceModel(castle, model, width: 1.6f, height: 2.2f);
            return castle.transform;
        }

        /// <summary>
        /// Adds a model (FBX or GLB, path relative to the Models folder) as a child, turned to face the
        /// camera and scaled to fit the given height and/or footprint width. If the model has an idle
        /// animation it is set to loop. The model stays linked to its file, so updated models flow in.
        /// </summary>
        private static GameObject PlaceModel(GameObject parent, string model, float height = 0f, float width = 0f, string[] hiddenParts = null)
        {
            string path = $"{ModelsFolder}/{model}";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new System.IO.FileNotFoundException($"Model not found (GLB files need glTFast): {path}");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.SetParent(parent.transform, false);
            instance.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f);

            foreach (Transform part in instance.GetComponentsInChildren<Transform>(true))
            {
                if (hiddenParts != null && System.Array.IndexOf(hiddenParts, part.name) >= 0) part.gameObject.SetActive(false);
            }

            // Pose the model in its idle animation first, so it is measured the way it will look.
            AnimationClip idle = FindIdleClip(path, out AnimationClip[] allClips);
            if (idle != null)
            {
                idle.SampleAnimation(instance, 0f);
                var animator = instance.AddComponent<ModelAnimator>();
                animator.Clip = idle;
                animator.Actions = allClips; // attacks, hit reactions, cheering... played once by the game
            }

            Bounds bounds = ModelBounds(instance);
            float scale = float.MaxValue;
            if (height > 0f) scale = Mathf.Min(scale, height / bounds.size.y);
            if (width > 0f) scale = Mathf.Min(scale, width / Mathf.Max(bounds.size.x, bounds.size.z));
            instance.transform.localScale = Vector3.one * scale;

            UseProjectMaterials(instance);
            return instance;
        }

        private static AnimationClip FindIdleClip(string modelPath, out AnimationClip[] allClips)
        {
            var clips = new System.Collections.Generic.Dictionary<string, AnimationClip>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) clips[clip.name] = clip;
            }
            allClips = new AnimationClip[clips.Count];
            clips.Values.CopyTo(allClips, 0);
            foreach (string name in IdleClipNames)
            {
                if (clips.TryGetValue(name, out AnimationClip clip)) return clip;
            }
            return null;
        }

        /// <summary>
        /// Swaps glTF materials (from .glb models) for this project's URP Lit materials with the same
        /// texture and color. The glTF shader multiplies by vertex colors, which these models don't have;
        /// on some graphics drivers that turns everything black. FBX models already use URP Lit.
        /// </summary>
        private static void UseProjectMaterials(GameObject model)
        {
            foreach (Renderer part in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = part.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || !source.shader.name.Contains("glTF")) continue;

                    Color color = source.GetColor("baseColorFactor");
                    var texture = source.GetTexture("baseColorTexture") as Texture2D;
                    string name = texture != null ? $"Models/{texture.name}" : $"Models/Color_{ColorUtility.ToHtmlStringRGB(color)}";
                    Material material = GetOrCreateMaterial(name, color, smoothness: 0.2f);
                    if (texture != null && material.GetTexture("_BaseMap") == null)
                    {
                        material.SetTexture("_BaseMap", texture);
                        EditorUtility.SetDirty(material);
                    }
                    materials[i] = material;
                }
                part.sharedMaterials = materials;
            }
        }

        private static Bounds ModelBounds(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer part in renderers) bounds.Encapsulate(part.bounds);
            return bounds;
        }

        /// <summary>Every tile effect, how many of it a game starts with, and the prefab(s) that show it.</summary>
        private static BoardEffects CreateBoardEffects(Board board)
        {

            var setups = new (TileEffect effect, int count, GameObject[] prefabs)[]
            {
                (TileEffect.Sword, 2, new[] { SavePrefab(BuildSword(), "Effects") }),
                (TileEffect.Spear, 2, new[] { SavePrefab(BuildSpearPickup(), "Effects") }),
                (TileEffect.HealthPotion, 2, new[] { SavePrefab(BuildHealthPotion(), "Effects") }),
                (TileEffect.RepairElixir, 2, new[] { SavePrefab(BuildRepairElixir(), "Effects") }),
                (TileEffect.Shield, 1, new[] { SavePrefab(BuildShield(), "Effects") }),
                (TileEffect.SpellBook, 2, new[] { SavePrefab(BuildSpellBook(), "Effects") }),
                (TileEffect.Chest, 1, new[] { SavePrefab(BuildChest(), "Effects") }),
                (TileEffect.Spikes, 2, new[] { SavePrefab(BuildSpikes(), "Effects") }),
                (TileEffect.Snare, 1, new[] { SavePrefab(BuildSnare(), "Effects") }),
                (TileEffect.Forest, 2, new[] { SavePrefab(BuildForest(), "Effects") }),
                (TileEffect.Monster, 3, new GameObject[0]), // prefabs come from the monster kinds below
            };

            var effects = board.gameObject.AddComponent<BoardEffects>();
            var serialized = new SerializedObject(effects);
            Set(serialized, "_board", board);
            SerializedProperty list = serialized.FindProperty("_setups");
            list.arraySize = setups.Length;
            for (int i = 0; i < setups.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Effect").intValue = (int)setups[i].effect;
                entry.FindPropertyRelative("Count").intValue = setups[i].count;
                SerializedProperty prefabs = entry.FindPropertyRelative("Prefabs");
                prefabs.arraySize = setups[i].prefabs.Length;
                for (int j = 0; j < setups[i].prefabs.Length; j++)
                {
                    prefabs.GetArrayElementAtIndex(j).objectReferenceValue = setups[i].prefabs[j];
                }
            }
            SerializedProperty kinds = serialized.FindProperty("_monsterKinds");
            kinds.arraySize = MonsterModels.Length;
            for (int i = 0; i < MonsterModels.Length; i++)
            {
                SerializedProperty kind = kinds.GetArrayElementAtIndex(i);
                kind.FindPropertyRelative("Tier").intValue = (int)MonsterModels[i].tier;
                kind.FindPropertyRelative("Prefab").objectReferenceValue =
                    SavePrefab(BuildMonster(MonsterModels[i].model, MonsterModels[i].tier), "Effects");
            }
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

        /// <summary>
        /// Battle effects (slashes, fireballs, quakes...). They are made in code at runtime; the builder only
        /// hands over a glowing particle material, an unlit material and the spear to throw.
        /// </summary>
        private static CombatFx CreateCombatFx()
        {
            var combat = new GameObject("CombatFx").AddComponent<CombatFx>();
            var serialized = new SerializedObject(combat);
            Set(serialized, "_glowMaterial", GetOrCreateGlowMaterial());
            Set(serialized, "_solidMaterial", GetOrCreateMaterial("Highlight", Color.white, unlit: true));

            var projectile = new GameObject("SpearProjectile");
            BuildSpearModel(projectile).transform.localScale = Vector3.one * 1.15f;
            Set(serialized, "_spearPrefab", SavePrefab(projectile, "Fx"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return combat;
        }

        /// <summary>
        /// URP's soft round particle material, for every glowing effect (colors come from the effect).
        /// Reused if it exists, so tweaks made in the Editor survive a rebuild.
        /// </summary>
        private static Material GetOrCreateGlowMaterial()
        {
            string path = $"{MaterialsFolder}/FxGlow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ---------- Prefabs (placeholder art made of primitives) ----------

        private static GameObject BuildSword()
        {
            var root = new GameObject("Sword");
            GameObject sword = PlaceModel(root, "KayKit/Adventurers/sword_1handed.fbx", height: 1.25f);
            sword.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            Idle(sword, spin: 90f, bob: 0.06f);
            return root;
        }

        /// <summary>A floating spear (made from primitives): +1 siege, the damage you deal to bases.</summary>
        private static GameObject BuildSpearPickup()
        {
            var root = new GameObject("Spear");
            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            GameObject spear = BuildSpearModel(pivot);
            spear.transform.localRotation = Quaternion.Euler(-60f, 0f, 0f); // tip up
            Idle(pivot, spin: 80f, bob: 0.06f);
            return root;
        }

        /// <summary>A spear lying along +Z (tip forward): wooden shaft, red binding and a leaf-shaped steel head.</summary>
        private static GameObject BuildSpearModel(GameObject parent)
        {
            var spear = new GameObject("SpearModel");
            spear.transform.SetParent(parent.transform, false);

            GameObject shaft = Part(spear, PrimitiveType.Cylinder, "Shaft", new Vector3(0f, 0f, -0.05f), new Vector3(0.06f, 0.55f, 0.06f));
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SetMaterial(shaft, "Bark", Hex("#7a4a26"));

            GameObject binding = Part(spear, PrimitiveType.Cylinder, "Binding", new Vector3(0f, 0f, 0.47f), new Vector3(0.09f, 0.05f, 0.09f));
            binding.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SetMaterial(binding, "SpearCloth", Hex("#d23c3c"), smoothness: 0.2f);

            // A cube turned 45 degrees inside a flattened, stretched parent makes a diamond-shaped blade.
            var head = new GameObject("Head");
            head.transform.SetParent(spear.transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, 0.66f);
            head.transform.localScale = new Vector3(0.17f, 0.05f, 0.32f);
            GameObject blade = Part(head, PrimitiveType.Cube, "Blade", Vector3.zero, Vector3.one);
            blade.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            SetMaterial(blade, "Spikes", Palette.Spikes, smoothness: 0.8f);
            return spear;
        }

        private static GameObject BuildHealthPotion()
        {
            var root = new GameObject("HealthPotion");
            GameObject potion = PlaceModel(root, "KayKit/Dungeon/bottle_A_labeled_green.fbx", height: 1.05f);
            Idle(potion, spin: 60f, bob: 0.05f);
            return root;
        }

        /// <summary>The same bottle tinted blue, so it reads clearly as a different potion (repairs your base).</summary>
        private static GameObject BuildRepairElixir()
        {
            var root = new GameObject("RepairElixir");
            GameObject bottle = PlaceModel(root, "KayKit/Dungeon/bottle_A_labeled_green.fbx", height: 1.05f);
            Renderer renderer = bottle.GetComponentInChildren<Renderer>();
            Material blue = GetOrCreateMaterial("RepairElixir", new Color(0.55f, 0.8f, 2.2f), smoothness: 0.5f);
            blue.SetTexture("_BaseMap", renderer.sharedMaterial.GetTexture("_BaseMap"));
            EditorUtility.SetDirty(blue);
            renderer.sharedMaterial = blue;
            Idle(bottle, spin: -60f, bob: 0.05f);
            return root;
        }

        private static GameObject BuildShield()
        {
            var root = new GameObject("Shield");
            GameObject shield = PlaceModel(root, "KayKit/Adventurers/shield_round.fbx", height: 0.9f);
            shield.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            Idle(shield, spin: 70f, bob: 0.06f);
            return root;
        }

        private static GameObject BuildChest()
        {
            var root = new GameObject("Chest");
            PlaceModel(root, "KayKit/Dungeon/chest_gold.fbx", width: 0.95f);
            return root;
        }

        /// <summary>A floating purple spell book with golden corners (made from primitives).</summary>
        private static GameObject BuildSpellBook()
        {
            var root = new GameObject("SpellBook");
            var book = new GameObject("Book");
            book.transform.SetParent(root.transform, false);
            book.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            book.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            SetMaterial(Part(book, PrimitiveType.Cube, "Cover", Vector3.zero, new Vector3(0.62f, 0.14f, 0.48f)), "BookCover", Hex("#6a3fb8"), smoothness: 0.4f);
            SetMaterial(Part(book, PrimitiveType.Cube, "Pages", new Vector3(0.03f, 0f, 0f), new Vector3(0.6f, 0.1f, 0.44f)), "BookPages", Hex("#f6ead0"));
            foreach (Vector2 corner in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) })
            {
                Vector3 position = new Vector3(corner.x * 0.27f, 0.075f, corner.y * 0.2f);
                SetMaterial(Part(book, PrimitiveType.Cube, "Corner", position, new Vector3(0.09f, 0.02f, 0.09f)), "Gold", Hex("#ffcc33"), smoothness: 0.85f);
            }
            SetMaterial(Part(book, PrimitiveType.Sphere, "Gem", new Vector3(0f, 0.08f, 0f), new Vector3(0.12f, 0.04f, 0.12f)), "Gem", Hex("#7fe7ff"), smoothness: 0.95f);
            Idle(book, spin: 45f, bob: 0.07f);
            return root;
        }

        /// <summary>A little cluster of round trees (made from primitives): rolls are lower after landing here.</summary>
        private static GameObject BuildForest()
        {
            var root = new GameObject("Forest");
            var trees = new[] { (new Vector3(-0.25f, 0f, 0.15f), 1f), (new Vector3(0.22f, 0f, 0.2f), 0.85f), (new Vector3(0.02f, 0f, -0.22f), 0.75f) };
            foreach ((Vector3 position, float size) in trees)
            {
                SetMaterial(Part(root, PrimitiveType.Cylinder, "Trunk", position + new Vector3(0f, 0.2f * size, 0f), new Vector3(0.12f, 0.2f, 0.12f) * size), "Bark", Hex("#7a4a26"));
                SetMaterial(Part(root, PrimitiveType.Sphere, "Leaves", position + new Vector3(0f, 0.55f * size, 0f), Vector3.one * 0.5f * size), "Leaves", Hex("#3f9b45"), smoothness: 0.15f);
                SetMaterial(Part(root, PrimitiveType.Sphere, "LeavesTop", position + new Vector3(0f, 0.85f * size, 0f), Vector3.one * 0.34f * size), "LeavesLight", Hex("#6cc957"), smoothness: 0.15f);
            }
            return root;
        }

        private static GameObject BuildSpikes()
        {
            var root = new GameObject("Spikes");
            PlaceModel(root, "KayKit/Dungeon/floor_tile_big_spikes.fbx", width: 1.0f);
            return root;
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

        /// <summary>One prefab per monster model; BoardEffects picks one at random for each monster tile.</summary>
        private static GameObject BuildMonster(string model, MonsterTier tier)
        {
            var root = new GameObject($"Monster_{model}");
            // Bigger tiers look bigger. Winged monsters are wide; letting them spread a bit past their tile
            // keeps them from looking tiny.
            float height = tier == MonsterTier.Small ? 1.15f : tier == MonsterTier.Medium ? 1.45f : 1.85f;
            PlaceModel(root, $"Quaternius/Monsters/{model}.fbx", height: height, width: height * 1.35f);
            return root;
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
            // Low and toward the camera, so it reads as "this tile".
            rect.localPosition = Quaternion.Euler(0f, CameraYaw, 0f) * new Vector3(0f, 0.9f, -0.3f);
            rect.localScale = Vector3.one * 0.006f;
            rect.sizeDelta = new Vector2(420f, 240f);
            Text steps = CreateText(label.transform, "Text", "3", 110, TextAnchor.MiddleCenter);
            steps.fontStyle = FontStyle.Bold;
            steps.horizontalOverflow = HorizontalWrapMode.Overflow; // never split a word like "Monster"
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
            camera.clearFlags = CameraClearFlags.Skybox; // the gradient sky (see Scenery)
            camera.backgroundColor = Palette.Sky;
            camera.fieldOfView = 38f; // narrower lens = less distortion, so far tiles look as big as near ones
            cameraObject.AddComponent<AudioListener>();

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            // Fixed viewing angle; CameraFit only slides the camera, it never turns it.
            cameraObject.transform.rotation = Quaternion.Euler(CameraPitch, CameraYaw, 0f);
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
