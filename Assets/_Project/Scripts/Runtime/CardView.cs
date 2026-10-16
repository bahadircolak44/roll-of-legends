using UnityEngine;
using UnityEngine.UI;

namespace RollOfLegends
{
    /// <summary>
    /// One card slot in the hand: the themed card picture, its title and numbers.
    /// A selected card lifts up and glows. ScreenLayout decides where the slot sits on screen.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class CardView : MonoBehaviour
    {
        [SerializeField] private Image _art;
        [SerializeField] private Image _glow;
        [SerializeField] private Text _title;
        [SerializeField] private Text _value;

        [Tooltip("How far a selected card lifts out of the hand.")]
        [SerializeField] private float _liftDistance = 40f;

        private RectTransform _rect;
        private Vector2 _restPosition;
        private Vector2 _liftDirection = Vector2.up;
        private bool _isSelected;
        private float _popTimer;

        public Button Button => GetComponent<Button>();
        public bool IsSelected => _isSelected;

        private RectTransform Rect => _rect != null ? _rect : (_rect = (RectTransform)transform);

        /// <summary>Called by ScreenLayout: the slot's resting place, size, and which way "lifting" goes.</summary>
        public void Place(Vector2 restPosition, Vector2 size, Vector2 liftDirection)
        {
            _restPosition = restPosition;
            _liftDirection = liftDirection;
            Rect.sizeDelta = size;
            Rect.anchoredPosition = TargetPosition();
        }

        public void Show(DiceCard card)
        {
            gameObject.SetActive(true);
            _art.sprite = CardArt.Get(card.Type);
            _title.text = CardArt.Title(card.Type);
            _title.color = CardArt.TitleColor(card.Type);
            // Non-breaking spaces (\u00A0) keep "1 · 3 · 5" on one line; the text shrinks to fit instead.
            _value.text = card.Type switch
            {
                DiceType.Odd => "1\u00A0·\u00A03\u00A0·\u00A05",
                DiceType.Even => "2\u00A0·\u00A04\u00A0·\u00A06",
                DiceType.Fixed => card.FixedValue.ToString(),
                _ => "1\u00A0–\u00A06",
            };
            _glow.color = CardArt.AccentColor(card.Type);
            SetSelected(false);
            _popTimer = 0.25f; // a new card pops in
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (selected && _glow.sprite == null)
            {
                _glow.sprite = CardArt.Glow;
            }
            _glow.enabled = selected;
        }

        private void Update()
        {
            // Glide toward the resting or lifted position; selected cards also grow a little.
            float speed = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            Rect.anchoredPosition = Vector2.Lerp(Rect.anchoredPosition, TargetPosition(), speed);

            float scale = _isSelected ? 1.07f : 1f;
            if (_popTimer > 0f)
            {
                _popTimer -= Time.unscaledDeltaTime;
                scale *= 1f - 0.35f * Mathf.Clamp01(_popTimer / 0.25f);
            }
            Rect.localScale = Vector3.Lerp(Rect.localScale, Vector3.one * scale, speed);

            if (_isSelected)
            {
                // Gentle pulsing glow.
                Color glow = _glow.color;
                glow.a = 0.5f + 0.2f * Mathf.Sin(Time.unscaledTime * 5f);
                _glow.color = glow;
            }
        }

        private Vector2 TargetPosition()
        {
            return _restPosition + (_isSelected ? _liftDirection * _liftDistance : Vector2.zero);
        }
    }
}
