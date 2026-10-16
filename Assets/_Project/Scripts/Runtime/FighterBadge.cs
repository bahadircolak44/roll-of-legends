using UnityEngine;
using UnityEngine.UI;

namespace RollOfLegends
{
    /// <summary>The top-bar badge for one side: name, health bar, attack and base health.</summary>
    public class FighterBadge : MonoBehaviour
    {
        [SerializeField] private Text _name;
        [SerializeField] private Text _hpText;
        [SerializeField] private RectTransform _hpFill;
        [SerializeField] private Text _stats;

        private float _shownFraction = 1f;
        private float _targetFraction = 1f;

        public void Show(Fighter fighter, Color teamColor)
        {
            _name.text = fighter.Name;
            _name.color = teamColor;
            _hpText.text = $"HP {fighter.Hp}/{fighter.MaxHp}";
            _stats.text = $"ATK <b>{fighter.Attack}</b>     BASE <b>{fighter.BaseHp}</b>";
            _targetFraction = fighter.MaxHp > 0 ? (float)fighter.Hp / fighter.MaxHp : 0f;
        }

        private void Update()
        {
            // Animate the health bar toward its new size.
            _shownFraction = Mathf.MoveTowards(_shownFraction, _targetFraction, Time.unscaledDeltaTime * 1.5f);
            _hpFill.anchorMax = new Vector2(_shownFraction, 1f);
        }
    }
}
