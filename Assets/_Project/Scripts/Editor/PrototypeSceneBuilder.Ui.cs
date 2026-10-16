using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RollOfLegends.EditorTools
{
    /// <summary>The user interface part of the scene builder. Positions are set at runtime by ScreenLayout.</summary>
    public static partial class PrototypeSceneBuilder
    {
        private const int HandSize = 3;

        private static readonly Color PanelColor = new Color(0.06f, 0.08f, 0.15f, 0.82f);
        private static readonly Color PromptColor = new Color(1f, 0.88f, 0.55f);

        /// <summary>Everything the GameController and camera need from the UI.</summary>
        private class Ui
        {
            public ScreenLayout Layout;
            public RectTransform PopupLayer;
            public GameObject Title;
            public Button PlayButton;
            public CardView[] Cards;
            public FighterBadge PlayerBadge;
            public FighterBadge RivalBadge;
            public EventLog EventLog;
            public Text Prompt;
        }

        private static Ui CreateUi()
        {
            var ui = new Ui();
            Canvas canvas = CreateCanvas();
            Transform root = canvas.transform;

            // Floating texts sit behind every panel, so they never cover the HUD.
            ui.PopupLayer = Stretch(new GameObject("PopupLayer", typeof(RectTransform)), root);

            RectTransform topBar = Stretch(new GameObject("TopBar", typeof(RectTransform)), root);
            ui.PlayerBadge = CreateBadge(topBar, "PlayerBadge");
            ui.RivalBadge = CreateBadge(topBar, "RivalBadge");

            ui.EventLog = CreateEventLog(root, out RectTransform log);
            ui.Prompt = CreatePrompt(root, out RectTransform prompt);
            RectTransform hand = Stretch(new GameObject("Hand", typeof(RectTransform)), root);
            ui.Cards = new CardView[HandSize];
            for (int i = 0; i < HandSize; i++)
            {
                ui.Cards[i] = CreateCard(hand, i);
            }

            ui.Title = CreateTitle(root);
            ui.PlayButton = CreatePlayButton(root);
            CreateEventSystem();

            ui.Layout = canvas.gameObject.AddComponent<ScreenLayout>();
            var serialized = new SerializedObject(ui.Layout);
            Set(serialized, "_canvas", canvas.GetComponent<RectTransform>());
            Set(serialized, "_topBar", topBar);
            Set(serialized, "_playerBadge", ui.PlayerBadge.GetComponent<RectTransform>());
            Set(serialized, "_rivalBadge", ui.RivalBadge.GetComponent<RectTransform>());
            Set(serialized, "_log", log);
            Set(serialized, "_prompt", prompt);
            Set(serialized, "_hand", hand);
            SetArray(serialized, "_cards", ui.Cards);
            Set(serialized, "_eventLog", ui.EventLog);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return ui;
        }

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

        private static GameObject CreateTitle(Transform canvas)
        {
            // A dark see-through backdrop over the whole screen makes the title readable over the board.
            var title = new GameObject("Title", typeof(RectTransform), typeof(Image));
            Stretch(title, canvas);
            title.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.08f, 0.6f);
            title.GetComponent<Image>().raycastTarget = false;

            var heading = new GameObject("Heading", typeof(RectTransform));
            RectTransform rect = heading.GetComponent<RectTransform>();
            Place(rect, new Vector2(0f, 0.55f), new Vector2(1f, 0.8f), Vector2.zero, Vector2.zero, title.transform);

            Text name = CreateText(rect, "Name", "ROLL OF LEGENDS", 110, TextAnchor.MiddleCenter);
            name.fontStyle = FontStyle.Bold;
            name.color = new Color(1f, 0.83f, 0.35f);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 40;
            name.resizeTextMaxSize = 110;
            name.rectTransform.anchorMin = new Vector2(0.05f, 0.35f);
            name.rectTransform.anchorMax = new Vector2(0.95f, 1f);
            AddOutline(name, 6f);

            Text subtitle = CreateText(rect, "Subtitle", "A dice & card adventure", 40, TextAnchor.MiddleCenter);
            subtitle.color = new Color(0.85f, 0.9f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(1f, 0.35f);
            AddOutline(subtitle, 3f);
            return title;
        }

        private static Button CreatePlayButton(Transform canvas)
        {
            var buttonObject = new GameObject("PlayButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(RoundedImage));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(canvas, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.42f);
            rect.sizeDelta = new Vector2(480f, 150f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.75f, 0.44f);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText(rect, "Label", "PLAY", 70, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            AddOutline(text, 3f);
            return button;
        }

        /// <summary>Name, health bar, attack and base health of one side.</summary>
        private static FighterBadge CreateBadge(Transform topBar, string name)
        {
            var badgeObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(RoundedImage));
            var rect = badgeObject.GetComponent<RectTransform>();
            rect.SetParent(topBar, false);
            badgeObject.GetComponent<Image>().color = PanelColor;
            badgeObject.GetComponent<Image>().raycastTarget = false;

            Text nameText = CreateText(rect, "Name", "", 34, TextAnchor.UpperLeft);
            nameText.fontStyle = FontStyle.Bold;
            Place(nameText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -52), new Vector2(-20, -12));

            var barBack = new GameObject("HpBar", typeof(RectTransform), typeof(Image), typeof(RoundedImage));
            var barRect = barBack.GetComponent<RectTransform>();
            barRect.SetParent(rect, false);
            barBack.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            barBack.GetComponent<Image>().pixelsPerUnitMultiplier = 2f; // smaller rounded corners
            Place(barRect, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -88), new Vector2(-20, -58));

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image), typeof(RoundedImage));
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.SetParent(barRect, false);
            fill.GetComponent<Image>().color = new Color(0.3f, 0.85f, 0.45f);
            fill.GetComponent<Image>().pixelsPerUnitMultiplier = 2f;
            Place(fillRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Text hpText = CreateText(barRect, "HpText", "", 22, TextAnchor.MiddleCenter);
            hpText.fontStyle = FontStyle.Bold;
            AddOutline(hpText, 2f);

            Text stats = CreateText(rect, "Stats", "", 26, TextAnchor.UpperLeft);
            stats.color = new Color(0.9f, 0.92f, 1f);
            Place(stats.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -124), new Vector2(-20, -94));

            var badge = badgeObject.AddComponent<FighterBadge>();
            var serialized = new SerializedObject(badge);
            Set(serialized, "_name", nameText);
            Set(serialized, "_hpText", hpText);
            Set(serialized, "_hpFill", fillRect);
            Set(serialized, "_stats", stats);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return badge;
        }

        /// <summary>The match history panel with a small "HISTORY" header.</summary>
        private static EventLog CreateEventLog(Transform canvas, out RectTransform panel)
        {
            var logObject = new GameObject("HistoryPanel", typeof(RectTransform), typeof(Image), typeof(RoundedImage));
            panel = logObject.GetComponent<RectTransform>();
            panel.SetParent(canvas, false);
            logObject.GetComponent<Image>().color = PanelColor;
            logObject.GetComponent<Image>().raycastTarget = false;

            Text header = CreateText(panel, "Header", "HISTORY", 22, TextAnchor.UpperLeft);
            header.fontStyle = FontStyle.Bold;
            header.color = PromptColor;
            Place(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -42), new Vector2(-20, -12));

            Text lines = CreateText(panel, "Lines", "", 26, TextAnchor.LowerLeft);
            lines.lineSpacing = 1.1f;
            Place(lines.rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 14), new Vector2(-16, -46));

            var log = logObject.AddComponent<EventLog>();
            var serialized = new SerializedObject(log);
            Set(serialized, "_text", lines);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return log;
        }

        /// <summary>The short instruction line ("Your turn: pick a card").</summary>
        private static Text CreatePrompt(Transform canvas, out RectTransform panel)
        {
            var promptObject = new GameObject("Prompt", typeof(RectTransform), typeof(Image), typeof(RoundedImage));
            panel = promptObject.GetComponent<RectTransform>();
            panel.SetParent(canvas, false);
            promptObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            promptObject.GetComponent<Image>().raycastTarget = false;

            Text text = CreateText(panel, "Text", "", 32, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            text.color = PromptColor;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = 32;
            Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(18, 6), new Vector2(-18, -6));
            return text;
        }

        /// <summary>
        /// One card slot: a glow (shown when selected), the card picture, and title/number text placed
        /// on the picture's ribbon and plaque.
        /// </summary>
        private static CardView CreateCard(Transform hand, int index)
        {
            var cardObject = new GameObject($"Card_{index}", typeof(RectTransform), typeof(Button));
            var rect = cardObject.GetComponent<RectTransform>();
            rect.SetParent(hand, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

            // The glow sprite is bigger than the card by a fixed margin on every side.
            var glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            var glowRect = glow.GetComponent<RectTransform>();
            glowRect.SetParent(rect, false);
            const float marginX = 28f / CardArt.Width;
            const float marginY = 28f / CardArt.Height;
            Place(glowRect, new Vector2(-marginX, -marginY), new Vector2(1 + marginX, 1 + marginY), Vector2.zero, Vector2.zero);
            glow.GetComponent<Image>().raycastTarget = false;
            glow.GetComponent<Image>().enabled = false;

            var art = new GameObject("Art", typeof(RectTransform), typeof(Image));
            Place(art.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, rect);
            Image artImage = art.GetComponent<Image>();

            Text title = CreateText(rect, "Title", "", 44, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 14;
            title.resizeTextMaxSize = 44;
            title.raycastTarget = false;
            Place(title.rectTransform, CardArt.TitleArea.min, CardArt.TitleArea.max, Vector2.zero, Vector2.zero);

            Text value = CreateText(rect, "Value", "", 60, TextAnchor.MiddleCenter);
            value.fontStyle = FontStyle.Bold;
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 16;
            value.resizeTextMaxSize = 60;
            value.raycastTarget = false;
            Place(value.rectTransform, CardArt.ValueArea.min, CardArt.ValueArea.max, Vector2.zero, Vector2.zero);

            var button = cardObject.GetComponent<Button>();
            button.targetGraphic = artImage;
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.65f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            button.colors = colors;

            var card = cardObject.AddComponent<CardView>();
            var serialized = new SerializedObject(card);
            Set(serialized, "_art", artImage);
            Set(serialized, "_glow", glow.GetComponent<Image>());
            Set(serialized, "_title", title);
            Set(serialized, "_value", value);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            Place(textObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, parent);

            var text = textObject.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void AddOutline(Text text, float size)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(size, -size);
        }

        /// <summary>Anchors a rect inside its parent with pixel offsets from those anchors.</summary>
        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Transform parent = null)
        {
            if (parent != null) rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        /// <summary>Adds a rect to the canvas filling the whole screen (ScreenLayout resizes it later).</summary>
        private static RectTransform Stretch(GameObject gameObject, Transform canvas)
        {
            var rect = gameObject.GetComponent<RectTransform>();
            Place(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, canvas);
            return rect;
        }

        private static void CreateEventSystem()
        {
            // Needed for buttons to receive clicks/taps. The project uses the new Input System,
            // so it needs the Input System UI module (it picks default UI actions automatically).
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
