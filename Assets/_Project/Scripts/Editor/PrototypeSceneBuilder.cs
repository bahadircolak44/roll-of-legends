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

        private static readonly Color TileColor = new Color(0.93f, 0.89f, 0.80f);
        private static readonly Color StartTileColor = new Color(0.45f, 0.80f, 0.45f);
        private static readonly Color HeroColor = new Color(0.20f, 0.45f, 0.95f);
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

            CreateLight();
            CreateGround();
            Board board = CreateBoard();
            DiceView dice = CreateDice();
            Transform hero = CreateHero(board);
            CreateCamera(board);

            Canvas canvas = CreateCanvas();
            Text infoText = CreateInfoPanel(canvas.transform);
            Button playButton = CreateButton(canvas.transform, "PlayButton", "PLAY",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.20f, 0.65f, 0.30f));
            // Bottom-right corner: stays clear of the board in both portrait and landscape.
            Button rollButton = CreateButton(canvas.transform, "RollDiceButton", "ROLL DICE",
                new Vector2(1f, 0f), new Vector2(-40f, 40f), new Color(0.95f, 0.55f, 0.10f));
            CreateEventSystem();

            var controller = new GameObject("GameController").AddComponent<GameController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("_board").objectReferenceValue = board;
            serialized.FindProperty("_hero").objectReferenceValue = hero;
            serialized.FindProperty("_dice").objectReferenceValue = dice;
            serialized.FindProperty("_playButton").objectReferenceValue = playButton;
            serialized.FindProperty("_rollButton").objectReferenceValue = rollButton;
            serialized.FindProperty("_infoText").objectReferenceValue = infoText;
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
                SetMaterial(block, isStart ? "StartTile" : "Tile", isStart ? StartTileColor : TileColor);

                CreateTileLabel(tile.transform, isStart ? "START" : i.ToString());
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
            // A small world-space UI canvas lying flat on the tile. Unlike 3D Text (TextMesh),
            // world-space UI is correctly hidden behind the hero and the base.
            var label = new GameObject("Label", typeof(RectTransform), typeof(Canvas));
            label.transform.SetParent(tile, false);
            label.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            const float unitsPerWorldUnit = 200f;
            var rect = label.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(0f, 0.01f, 0f);
            rect.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, readable from the camera
            rect.localScale = Vector3.one / unitsPerWorldUnit;
            rect.sizeDelta = Vector2.one * TileSize * unitsPerWorldUnit;

            Text labelText = CreateText(label.transform, "Text", text, text.Length > 2 ? 56 : 100, TextAnchor.MiddleCenter);
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

        private static Transform CreateHero(Board board)
        {
            // Capsule is 2 units tall at scale 1, so scale 0.5 makes a 1 unit tall hero.
            GameObject hero = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hero.name = "Hero";
            hero.transform.localScale = Vector3.one * 0.5f;
            hero.transform.position = board.GetTilePosition(0) + Vector3.up * 0.5f;
            SetMaterial(hero, "Hero", HeroColor);
            return hero.transform;
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

        private static Text CreateInfoPanel(Transform canvas)
        {
            var panel = new GameObject("InfoPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f); // top-left corner
            rect.anchoredPosition = new Vector2(30f, -30f);
            rect.sizeDelta = new Vector2(640f, 220f);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            Text text = CreateText(panel.transform, "InfoText", "", 44, TextAnchor.UpperLeft);
            var textRect = text.rectTransform;
            textRect.offsetMin = new Vector2(24f, 16f);
            textRect.offsetMax = new Vector2(-24f, -16f);
            return text;
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
