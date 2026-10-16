using UnityEngine;
using UnityEngine.UI;

namespace RollOfLegends
{
    /// <summary>One card slot in the hand: a colored button showing the card's dice type.</summary>
    [RequireComponent(typeof(Button))]
    public class CardView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Text _label;

        public Button Button => GetComponent<Button>();

        public void Show(DiceCard card)
        {
            gameObject.SetActive(true);
            _background.color = ColorFor(card.Type);
            _label.text = $"<size=46><b>{TitleFor(card)}</b></size>\n{FacesFor(card)}";
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private static string TitleFor(DiceCard card)
        {
            switch (card.Type)
            {
                case DiceType.Odd: return "ODD";
                case DiceType.Even: return "EVEN";
                case DiceType.Fixed: return "FIXED";
                default: return "RANDOM";
            }
        }

        private static string FacesFor(DiceCard card)
        {
            switch (card.Type)
            {
                case DiceType.Odd: return "1 · 3 · 5";
                case DiceType.Even: return "2 · 4 · 6";
                case DiceType.Fixed: return $"<size=96><b>{card.FixedValue}</b></size>";
                default: return "1 – 6";
            }
        }

        private static Color ColorFor(DiceType type)
        {
            switch (type)
            {
                case DiceType.Odd: return new Color(0.55f, 0.35f, 0.80f);   // purple
                case DiceType.Even: return new Color(0.15f, 0.60f, 0.60f);  // teal
                case DiceType.Fixed: return new Color(0.90f, 0.50f, 0.15f); // orange
                default: return new Color(0.25f, 0.45f, 0.85f);             // blue
            }
        }
    }
}
