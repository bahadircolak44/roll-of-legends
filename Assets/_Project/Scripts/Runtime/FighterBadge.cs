using UnityEngine;
using UnityEngine.UI;

namespace RollOfLegends
{
    /// <summary>
    /// The top-bar badge for one side: name and level, base health, hero stats with icons
    /// (health, sword attack, spear siege, defense) and any active status such as a forest penalty.
    /// </summary>
    public class FighterBadge : MonoBehaviour
    {
        private static readonly Color StatusBad = new Color(1f, 0.55f, 0.5f);
        private static readonly Color StatusGood = new Color(0.55f, 1f, 0.6f);
        private static readonly Color LevelColor = new Color(1f, 0.82f, 0.25f);

        [SerializeField] private Text _name;
        [SerializeField] private Text _base;
        [SerializeField] private Text _status;

        [Tooltip("Numbers next to the stat icons, in order: health, attack (sword), siege (spear), defense (shield).")]
        [SerializeField] private Text[] _stats = new Text[4];

        private string _lastBase;
        private float _baseFlash;
        private readonly string[] _lastStats = new string[4];
        private readonly float[] _statFlash = new float[4];

        public void Show(Fighter fighter, Color teamColor)
        {
            _name.text = $"{fighter.Name}  <color=#{ColorUtility.ToHtmlStringRGB(LevelColor)}>LV {fighter.Level}</color>";
            _name.color = teamColor;

            string baseText = $"BASE <b>{fighter.BaseHp}</b>";
            if (_lastBase != null && baseText != _lastBase) _baseFlash = 1f;
            _base.text = _lastBase = baseText;

            int[] values = { fighter.Hp, fighter.Attack, fighter.Siege, fighter.Defense };
            for (int i = 0; i < _stats.Length && i < values.Length; i++)
            {
                string text = values[i].ToString();
                if (_lastStats[i] != null && text != _lastStats[i]) _statFlash[i] = 1f;
                _stats[i].text = _lastStats[i] = text;
            }

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
            _baseFlash = Mathf.MoveTowards(_baseFlash, 0f, Time.unscaledDeltaTime * 3f);
            _base.transform.localScale = Vector3.one * (1f + 0.2f * _baseFlash);
            for (int i = 0; i < _stats.Length; i++)
            {
                _statFlash[i] = Mathf.MoveTowards(_statFlash[i], 0f, Time.unscaledDeltaTime * 3f);
                _stats[i].transform.localScale = Vector3.one * (1f + 0.35f * _statFlash[i]);
            }
        }
    }
}
