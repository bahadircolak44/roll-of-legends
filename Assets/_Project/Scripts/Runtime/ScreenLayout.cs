using UnityEngine;

namespace RollOfLegends
{
    /// <summary>
    /// Arranges the UI for the screen shape:
    /// portrait  = stats on top, history below it, cards in a row at the bottom;
    /// landscape = stats on top, history on the left, cards in a column on the right.
    /// The camera (CameraFit) then fits the board into the space that is left.
    /// </summary>
    public class ScreenLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private RectTransform _topBar;
        [SerializeField] private RectTransform _playerBadge;
        [SerializeField] private RectTransform _rivalBadge;
        [SerializeField] private RectTransform _log;
        [SerializeField] private RectTransform _prompt;
        [SerializeField] private RectTransform _hand;
        [SerializeField] private CardView[] _cards;
        [SerializeField] private EventLog _eventLog;

        private const float Margin = 20f;
        private const float TopBarHeight = 128f;

        private Vector2 _lastCanvasSize;

        public bool IsLandscape { get; private set; }

        /// <summary>UI areas the board must not be hidden behind.</summary>
        public RectTransform[] Obstacles => new[] { _topBar, _log, _prompt, _hand };

        private void Update()
        {
            if (_canvas.rect.size != _lastCanvasSize)
            {
                Apply();
            }
        }

        public void Apply()
        {
            _lastCanvasSize = _canvas.rect.size;
            IsLandscape = _lastCanvasSize.x > _lastCanvasSize.y;

            // Top bar: full width, badges in the left and right corners.
            SetRect(_topBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(Margin, -Margin - TopBarHeight), new Vector2(-Margin, -Margin));
            float badgeWidth = Mathf.Min(560f, (_lastCanvasSize.x - Margin * 3f) / 2f);
            SetRect(_playerBadge, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(badgeWidth, 0));
            SetRect(_rivalBadge, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-badgeWidth, 0), Vector2.zero);

            float belowTopBar = -Margin * 2f - TopBarHeight;
            if (IsLandscape)
            {
                const float logWidth = 430f;
                const float handWidth = 250f;
                SetRect(_log, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(Margin, Margin), new Vector2(Margin + logWidth, belowTopBar));
                SetRect(_hand, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f),
                    new Vector2(-Margin - handWidth, Margin), new Vector2(-Margin, belowTopBar));
                SetRect(_prompt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(-330f, Margin), new Vector2(330f, Margin + 64f));
                ArrangeCards(new Vector2(190f, 262f), vertical: true);
            }
            else
            {
                const float logHeight = 220f;
                const float handHeight = 360f;
                SetRect(_log, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                    new Vector2(Margin, belowTopBar - logHeight), new Vector2(-Margin, belowTopBar));
                SetRect(_hand, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
                    new Vector2(Margin, Margin), new Vector2(-Margin, Margin + handHeight));
                // Leave room above the hand for a selected card lifting up.
                const float liftRoom = 40f;
                SetRect(_prompt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
                    new Vector2(Margin, Margin * 2f + handHeight + liftRoom), new Vector2(-Margin, Margin * 2f + handHeight + liftRoom + 64f));
                ArrangeCards(new Vector2(232f, 319f), vertical: false);
            }

            _eventLog.Refresh();
        }

        private void ArrangeCards(Vector2 cardSize, bool vertical)
        {
            const float gap = 22f;
            float step = (vertical ? cardSize.y : cardSize.x) + gap;
            for (int i = 0; i < _cards.Length; i++)
            {
                float offset = (i - (_cards.Length - 1) / 2f) * step;
                // Cards hang from the middle of the hand area; the first card is on top / on the left.
                Vector2 position = vertical ? new Vector2(0f, -offset) : new Vector2(offset, 0f);
                Vector2 lift = vertical ? Vector2.left : Vector2.up;
                _cards[i].Place(position, cardSize, lift);
            }
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
