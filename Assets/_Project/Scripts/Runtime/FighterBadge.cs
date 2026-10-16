using UnityEngine;
using UnityEngine.UI;

namespace RollOfLegends
{
    /// <summary>
    /// The top-bar badge for one side: name, base health, hero health/attack/defense,
    /// and any active status such as a forest penalty.
    /// </summary>
    public class FighterBadge : MonoBehaviour
    {
        private static readonly Color StatusBad = new Color(1f, 0.55f, 0.5f);
        private static readonly Color StatusGood = new Color(0.55f, 1f, 0.6f);

        [SerializeField] private Text _name;
        [SerializeField] private Text _base;
        [SerializeField] private Text _stats;
        [SerializeField] private Text _status;

        private string _lastStats;
        private string _lastBase;
        private float _statsFlash;
        private float _baseFlash;

        public void Show(Fighter fighter, Color teamColor)
        {
            _name.text = fighter.Name;
            _name.color = teamColor;

            string baseText = $"BASE <b>{fighter.BaseHp}</b>";
            string statsText = $"HP <b>{fighter.Hp}</b>     ATK <b>{fighter.Attack}</b>     DEF <b>{fighter.Defense}</b>";
            if (_lastBase != null && baseText != _lastBase) _baseFlash = 1f;
            if (_lastStats != null && statsText != _lastStats) _statsFlash = 1f;
            _base.text = _lastBase = baseText;
            _stats.text = _lastStats = statsText;

            _status.text = StatusOf(fighter);
        }

        private static string StatusOf(Fighter fighter)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (fighter.NextRollBonus < 0) parts.Add(Colored($"Next roll {fighter.NextRollBonus}", StatusBad));
            if (fighter.NextRollBonus > 0) parts.Add(Colored($"Next roll +{fighter.NextRollBonus}", StatusGood));
            if (fighter.SkipTurns > 0) parts.Add(Colored("Skips next turn", StatusBad));
            if (fighter.LuckyDraw) parts.Add(Colored("Rare card next", StatusGood));
            return string.Join("   ", parts);
        }

        private static string Colored(string text, Color color)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
        }

        private void Update()
        {
            // A quick pop when a number changes, so you notice it.
            _statsFlash = Mathf.MoveTowards(_statsFlash, 0f, Time.unscaledDeltaTime * 3f);
            _baseFlash = Mathf.MoveTowards(_baseFlash, 0f, Time.unscaledDeltaTime * 3f);
            _stats.transform.localScale = Vector3.one * (1f + 0.12f * _statsFlash);
            _base.transform.localScale = Vector3.one * (1f + 0.2f * _baseFlash);
        }
    }
}
