using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RollOfLegends
{
    /// <summary>
    /// The match history panel: who played what, who found or lost what. Newest line at the bottom;
    /// shows as many recent lines as fit in the panel.
    /// </summary>
    public class EventLog : MonoBehaviour
    {
        [SerializeField] private Text _text;
        [SerializeField] private int _maxEntries = 40;

        private readonly List<string> _entries = new List<string>();

        public IReadOnlyList<string> Entries => _entries;

        public void Clear()
        {
            _entries.Clear();
            Refresh();
        }

        public void Add(string richText)
        {
            _entries.Add(richText);
            if (_entries.Count > _maxEntries)
            {
                _entries.RemoveAt(0);
            }
            Refresh();
        }

        /// <summary>Call after the panel changes size (e.g. the phone rotates).</summary>
        public void Refresh()
        {
            // Take entries from the newest backwards until the panel is full.
            float available = _text.rectTransform.rect.height;
            int first = _entries.Count;
            while (first > 0)
            {
                _text.text = Join(first - 1);
                if (_text.preferredHeight > available && first < _entries.Count) break;
                first--;
            }
            _text.text = Join(first);
        }

        private string Join(int from)
        {
            var lines = new List<string>();
            for (int i = from; i < _entries.Count; i++)
            {
                bool newest = i == _entries.Count - 1;
                // Older lines are a bit dimmer so the newest one stands out.
                lines.Add(newest ? _entries[i] : $"<color=#ffffffb0>{_entries[i]}</color>");
            }
            return string.Join("\n", lines);
        }
    }
}
