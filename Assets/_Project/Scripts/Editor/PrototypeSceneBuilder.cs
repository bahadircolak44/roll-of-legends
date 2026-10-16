using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RollOfLegends.EditorTools
{
    /// <summary>
    /// Builds Assets/_Project/Scenes/Main.unity from scratch using placeholder primitives.
    /// Menu: Roll of Legends > Rebuild Prototype Scene. This overwrites the scene, so manual edits to it are lost.
    /// Batch mode: Unity -batchmode -executeMethod RollOfLegends.EditorTools.PrototypeSceneBuilder.Build -quit
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";

        // A square with 8 tiles on each side. Corner tiles are shared, so there are 4 * 8 - 4 = 28 tiles.
        private const int TilesPerSide = 8;
        private const float TileSpacing = 1.3f;
        private const float TileSize = 1.1f;
        private const float TileThickness = 0.2f;

        private const float DiceSize = 1.2f;

        private const int HandSize = 3;
        private const string PrefabsFolder = "Assets/_Project/Prefabs/Effects";

        // Castles sit inside the loop on the diagonal toward each side's home corner (START and the opposite
        // corner), far enough in that they don't hide tiles from the tilted camera.
        private static readonly Vector2Int BaseCellPlayer = new Vector2Int(2, 2);
        private static readonly Vector2Int BaseCellRival = new Vector2Int(TilesPerSide - 3, TilesPerSide - 3);
        private static readonly Vector2 CardSize = new Vector2(200f, 260f);
        private const float CardGap = 30f;

        private static readonly Color TileColor = new Color(0.93f, 0.89f, 0.80f);
        private static readonly Color StartTileColor = new Color(0.45f, 0.80f, 0.45f);
        private static readonly Color PlayerColor = new Color(0.20f, 0.45f, 0.95f);
        private static readonly Color RivalColor = new Color(0.90f, 0.25f, 0.20f);
        private static readonly Color RivalTileColor = new Color(0.95f, 0.55f, 0.50f);
        private static readonly Color SwordColor = new Color(1.00f, 0.75f, 0.20f);
        private static readonly Color PotionColor = new Color(0.30f, 0.85f, 0.40f);
        private static readonly Color SpikesColor = new Color(0.55f, 0.55f, 0.60f);
        private static readonly Color SnareColor = new Color(0.45f, 0.28f, 0.12f);
        private static readonly Color MonsterColor = new Color(0.60f, 0.30f, 0.80f);
        private static readonly Color DiceColor = new Color(0.97f, 0.97f, 0.95f);
        private static readonly Color PipColor = new Color(0.10f, 0.10f, 0.12f);
        private static readonly Color GroundColor = new Color(0.25f, 0.42f, 0.25f);
        private static readonly Color SkyColor = new Color(0.55f, 0.75f, 0.90f);

        [MenuItem("Roll of Legends/Rebuild Prototype Scene")]
        private static void BuildFromMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Rebuild Prototype Scene",
                $"This overwrites {ScenePath}. Any manual changes to that scene will be lost.",
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
            if (!AssetDatabase.IsValidFolder(PrefabsFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Effects");
            }

            CreateLight();
            CreateGround();
            Board board = CreateBoard();
            DiceView dice = CreateDice();
            CreateCastle("PlayerBase", BaseCellPlayer, "PlayerBase", PlayerColor);
            CreateCastle("RivalBase", BaseCellRival, "RivalBase", RivalColor);
            Transform hero = CreateFighterView("Hero", "Hero", PlayerColor);
            Transform rival = CreateFighterView("Rival", "Rival", RivalColor);
            BoardEffects effects = CreateBoardEffects(board);
            CreateCamera(board);

            Canvas canvas = CreateCanvas();
            CreateHud(canvas.transform, out Text statusText, out Text messageText);
            Button playButton = CreateButton(canvas.transform, "PlayButton", "PLAY",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.20f, 0.65f, 0.30f));
            CardView[] cardSlots = CreateHand(canvas.transform, HandSize);
            CreateEventSystem();

            var controller = new GameObject("GameController").AddComponent<GameController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("_board").objectReferenceValue = board;
            serialized.FindProperty("_effects").objectReferenceValue = effects;
            serialized.FindProperty("_heroView").objectReferenceValue = hero;
            serialized.FindProperty("_rivalView").objectReferenceValue = rival;
            serialized.FindProperty("_dice").objectReferenceValue = dice;
            serialized.FindProperty("_playButton").objectReferenceValue = playButton;
            SerializedProperty slots = serialized.FindProperty("_cardSlots");
            slots.arraySize = cardSlots.Length;
            for (int i = 0; i < cardSlots.Length; i++)
            {
                slots.GetArrayElementAtIndex(i).objectReferenceValue = cardSlots[i];
            }
            serialized.FindProperty("_statusText").objectReferenceValue = statusText;
            serialized.FindProperty("_messageText").objectReferenceValue = messageText;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[PrototypeSceneBuilder] Built {ScenePath} with {board.TileCount} tiles.");
        }

        // ---------- World ----------

        private static void CreateLight()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -TileThickness, 0f);
            ground.transform.localScale = new Vector3(1.4f, 1f, 1.4f);
            SetMaterial(ground, "Ground", GroundColor);
        }

        private static Board CreateBoard()
        {
            var board = new GameObject("Board").AddComponent<Board>();
            Vector2Int[] cells = GetLoopCells();

            for (int i = 0; i < cells.Length; i++)
            {
                // The tile root sits at the tile's top surface; the visible block hangs just below it.
                var tile = new GameObject($"Tile_{i:00}");
                tile.transform.SetParent(board.transform);
                tile.transform.position = CellToWorld(cells[i]);

                GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Block";
                block.transform.SetParent(tile.transform, false);
                block.transform.localPosition = new Vector3(0f, -TileThickness / 2f, 0f);
                block.transform.localScale = new Vector3(TileSize, TileThickness, TileSize);
                bool isStart = i == 0;
                bool isRivalHome = i == cells.Length / 2; // diagonally opposite START
                if (isStart) SetMaterial(block, "StartTile", StartTileColor);
                else if (isRivalHome) SetMaterial(block, "RivalTile", RivalTileColor);
                else SetMaterial(block, "Tile", TileColor);

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

            Text labelText = CreateText(label.transform, "Text", text, text.Length > 2 ? 44 : 64, TextAnchor.MiddleCenter);
            labelText.color = new Color(0.25f, 0.25f, 0.25f);
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
            dice.transform.position = new Vector3(0f, -TileThickness + DiceSize / 2f, 0f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(dice.transform, false);
            body.transform.localScale = Vector3.one * DiceSize;
            SetMaterial(body, "Dice", DiceColor);

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

            const float spacing = 0.28f;
            foreach (Vector2 spot in PipLayouts[value])
            {
                GameObject pip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pip.name = $"Pip_{value}";
                Object.DestroyImmediate(pip.GetComponent<Collider>()); // dots don't need physics
                pip.transform.SetParent(dice, false);
                pip.transform.localScale = Vector3.one * 0.22f;
                // Sink each dot slightly into the face so only a small bump sticks out.
                pip.transform.localPosition = normal * (DiceSize / 2f - 0.06f) + (right * spot.x + up * spot.y) * spacing;
                SetMaterial(pip, "Pip", PipColor);
            }
        }

        private static Transform CreateFighterView(string name, string materialName, Color color)
        {
            // Capsule is 2 units tall at scale 1, so scale 0.5 makes a 1 unit tall hero.
            // The GameController places it on its home tile when the game starts.
            GameObject fighter = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fighter.name = name;
            fighter.transform.localScale = Vector3.one * 0.5f;
            SetMaterial(fighter, materialName, color);
            return fighter.transform;
        }

        private static void CreateCastle(string name, Vector2Int cell, string materialName, Color color)
        {
            // A small castle: a block with a tower on top.
            var castle = new GameObject(name);
            castle.transform.position = CellToWorld(cell);

            GameObject body = Part(castle, PrimitiveType.Cube, "Body", new Vector3(0f, 0.1f, 0f), new Vector3(1.1f, 0.6f, 1.1f));
            SetMaterial(body, materialName, color);
            GameObject tower = Part(castle, PrimitiveType.Cylinder, "Tower", new Vector3(0f, 0.65f, 0f), new Vector3(0.5f, 0.25f, 0.5f));
            SetMaterial(tower, materialName, color);
        }

        private static BoardEffects CreateBoardEffects(Board board)
        {
            var effects = board.gameObject.AddComponent<BoardEffects>();
            var serialized = new SerializedObject(effects);
            serialized.FindProperty("_board").objectReferenceValue = board;
            serialized.FindProperty("_swordPrefab").objectReferenceValue = SavePrefab(BuildSword());
            serialized.FindProperty("_potionPrefab").objectReferenceValue = SavePrefab(BuildPotion());
            serialized.FindProperty("_spikesPrefab").objectReferenceValue = SavePrefab(BuildSpikes());
            serialized.FindProperty("_snarePrefab").objectReferenceValue = SavePrefab(BuildSnare());
            serialized.FindProperty("_monsterPrefab").objectReferenceValue = SavePrefab(BuildMonster());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effects;
        }

        // ---------- Effect marker prefabs (placeholder art made of primitives) ----------

        private static GameObject BuildSword()
        {
            var root = new GameObject("Sword");
            SetMaterial(Part(root, PrimitiveType.Cube, "Blade", new Vector3(0f, 0.45f, 0f), new Vector3(0.1f, 0.5f, 0.03f)), "Sword", SwordColor);
            SetMaterial(Part(root, PrimitiveType.Cube, "Guard", new Vector3(0f, 0.2f, 0f), new Vector3(0.32f, 0.06f, 0.06f)), "Sword", SwordColor);
            SetMaterial(Part(root, PrimitiveType.Cube, "Grip", new Vector3(0f, 0.1f, 0f), new Vector3(0.06f, 0.16f, 0.06f)), "Snare", SnareColor);
            root.AddComponent<Spinner>();
            return root;
        }

        private static GameObject BuildPotion()
        {
            var root = new GameObject("Potion");
            SetMaterial(Part(root, PrimitiveType.Sphere, "Bottle", new Vector3(0f, 0.2f, 0f), Vector3.one * 0.36f), "Potion", PotionColor);
            SetMaterial(Part(root, PrimitiveType.Cylinder, "Neck", new Vector3(0f, 0.43f, 0f), new Vector3(0.12f, 0.08f, 0.12f)), "Potion", PotionColor);
            SetMaterial(Part(root, PrimitiveType.Cylinder, "Cork", new Vector3(0f, 0.53f, 0f), new Vector3(0.1f, 0.03f, 0.1f)), "Snare", SnareColor);
            root.AddComponent<Spinner>();
            return root;
        }

        private static GameObject BuildSpikes()
        {
            var root = new GameObject("Spikes");
            for (int i = 0; i < 5; i++)
            {
                Vector3 position = i == 0 ? Vector3.zero : Quaternion.Euler(0f, 45f + i * 90f, 0f) * new Vector3(0f, 0f, 0.2f);
                GameObject spike = Part(root, PrimitiveType.Cube, "Spike", position + new Vector3(0f, 0.15f, 0f), new Vector3(0.07f, 0.3f, 0.07f));
                spike.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                SetMaterial(spike, "Spikes", SpikesColor);
            }
            return root;
        }

        private static GameObject BuildSnare()
        {
            var root = new GameObject("Snare");
            SetMaterial(Part(root, PrimitiveType.Cylinder, "Ring", new Vector3(0f, 0.02f, 0f), new Vector3(0.65f, 0.02f, 0.65f)), "Snare", SnareColor);
            SetMaterial(Part(root, PrimitiveType.Cylinder, "Hole", new Vector3(0f, 0.03f, 0f), new Vector3(0.45f, 0.02f, 0.45f)), "Pip", PipColor);
            return root;
        }

        private static GameObject BuildMonster()
        {
            var root = new GameObject("Monster");
            SetMaterial(Part(root, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.35f, 0f), new Vector3(0.45f, 0.35f, 0.45f)), "Monster", MonsterColor);
            foreach (float x in new[] { -0.09f, 0.09f })
            {
                // Eyes look toward the camera (-z).
                SetMaterial(Part(root, PrimitiveType.Sphere, "Eye", new Vector3(x, 0.5f, -0.18f), Vector3.one * 0.12f), "Dice", DiceColor);
                SetMaterial(Part(root, PrimitiveType.Sphere, "Pupil", new Vector3(x, 0.5f, -0.235f), Vector3.one * 0.06f), "Pip", PipColor);
            }
            return root;
        }

        /// <summary>Creates a primitive as a child part, without a collider (markers don't need physics).</summary>
        private static GameObject Part(GameObject parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            return part;
        }

        /// <summary>Saves a built object as a prefab asset (overwriting the old one) and removes it from the scene.</summary>
        private static GameObject SavePrefab(GameObject root)
        {
            string path = $"{PrefabsFolder}/{root.name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateCamera(Board board)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyColor;
            camera.fieldOfView = 50f;
            cameraObject.AddComponent<AudioListener>();

            // Fixed viewing angle; CameraFit only moves the camera along this direction.
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            cameraObject.transform.position = -cameraObject.transform.forward * 20f;

            var fit = cameraObject.AddComponent<CameraFit>();
            var serialized = new SerializedObject(fit);
            serialized.FindProperty("_board").objectReferenceValue = board;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetMaterial(GameObject target, string materialName, Color color)
        {
            target.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(materialName, color);
        }

        /// <summary>Reuses an existing material so color tweaks made in the Editor survive a rebuild.</summary>
        private static Material GetOrCreateMaterial(string materialName, Color color)
        {
            string path = $"{MaterialsFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ---------- UI ----------

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            // Scale the UI with the screen so it looks the same on every phone.
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvasObject.GetComponent<Canvas>();
        }

        /// <summary>Top-left panel: both sides' stats, the latest event message and a color legend.</summary>
        private static void CreateHud(Transform canvas, out Text statusText, out Text messageText)
        {
            var panel = new GameObject("HudPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(canvas, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f); // top-left corner
            rect.anchoredPosition = new Vector2(30f, -30f);
            rect.sizeDelta = new Vector2(660f, 300f);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 14, 14);
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            statusText = CreateText(panel.transform, "StatusText", "", 34, TextAnchor.UpperLeft);
            statusText.gameObject.AddComponent<LayoutElement>().preferredHeight = 90f;

            messageText = CreateText(panel.transform, "MessageText", "", 30, TextAnchor.UpperLeft);
            messageText.color = new Color(1f, 0.92f, 0.6f);
            messageText.gameObject.AddComponent<LayoutElement>().preferredHeight = 80f;

            Text legend = CreateText(panel.transform, "Legend",
                $"<color=#{Hex(SwordColor)}>SWORD</color> +{TileRules.SwordBonus} ATK   " +
                $"<color=#{Hex(PotionColor)}>POTION</color> +{TileRules.PotionHeal} HP   " +
                $"<color=#{Hex(SpikesColor)}>SPIKES</color> -{TileRules.SpikeDamage} HP\n" +
                $"<color=#{Hex(SnareColor)}>SNARE</color> skip a turn   " +
                $"<color=#{Hex(MonsterColor)}>MONSTER</color> power {TileRules.MonsterPower}",
                24, TextAnchor.UpperLeft);
            legend.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
        }

        private static string Hex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(Color.Lerp(color, Color.white, 0.5f));
        }

        private static Button CreateButton(Transform canvas, string name, string label, Vector2 anchor, Vector2 position, Color color)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvas, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(480f, 150f);

            var image = buttonObject.GetComponent<Image>();
            image.color = color;
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText(buttonObject.transform, "Label", label, 64, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            return button;
        }

        /// <summary>The player's hand: card slots in a row along the bottom of the screen.</summary>
        private static CardView[] CreateHand(Transform canvas, int handSize)
        {
            var slots = new CardView[handSize];
            float rowWidth = handSize * CardSize.x + (handSize - 1) * CardGap;

            for (int i = 0; i < handSize; i++)
            {
                var cardObject = new GameObject($"Card_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                cardObject.transform.SetParent(canvas, false);
                var rect = cardObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f); // bottom center
                rect.sizeDelta = CardSize;
                rect.anchoredPosition = new Vector2(-rowWidth / 2f + CardSize.x / 2f + i * (CardSize.x + CardGap), 40f);

                var image = cardObject.GetComponent<Image>();
                cardObject.GetComponent<Button>().targetGraphic = image;
                Text label = CreateText(cardObject.transform, "Label", "", 40, TextAnchor.MiddleCenter);
                label.supportRichText = true;
                label.horizontalOverflow = HorizontalWrapMode.Overflow; // never split a word like "RANDOM"

                var card = cardObject.AddComponent<CardView>();
                var serialized = new SerializedObject(card);
                serialized.FindProperty("_background").objectReferenceValue = image;
                serialized.FindProperty("_label").objectReferenceValue = label;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                slots[i] = card;
            }

            return slots;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; // stretch to fill the parent
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var text = textObject.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void CreateEventSystem()
        {
            // Needed for buttons to receive clicks/taps. The project uses the new Input System,
            // so it needs the Input System UI module (it picks default UI actions automatically).
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
