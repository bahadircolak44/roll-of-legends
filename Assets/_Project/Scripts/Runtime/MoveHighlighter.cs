using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RollOfLegends
{
    /// <summary>
    /// Marks the tiles the hero could land on with the selected card: a glowing frame on each tile
    /// and the number of steps (plus what is on that tile) floating above it.
    /// </summary>
    public class MoveHighlighter : MonoBehaviour
    {
        [SerializeField] private Board _board;
        [SerializeField] private GameObject _highlightPrefab;

        private readonly List<GameObject> _pool = new List<GameObject>();
        private readonly List<int> _activeTiles = new List<int>();
        private MaterialPropertyBlock _colorBlock; // created on first use (Unity forbids it in field initializers)

        /// <summary>Tiles currently highlighted (useful for tests).</summary>
        public IReadOnlyList<int> ActiveTiles => _activeTiles;

        /// <param name="notes">Optional text under each step count, e.g. "Potion".</param>
        public void Show(int fromTile, int[] steps, Color color, string[] notes = null)
        {
            Hide();
            if (_colorBlock == null) _colorBlock = new MaterialPropertyBlock();
            for (int i = 0; i < steps.Length; i++)
            {
                if (i == _pool.Count)
                {
                    _pool.Add(Instantiate(_highlightPrefab, transform));
                }

                int tile = Board.Advance(fromTile, steps[i], _board.TileCount);
                GameObject highlight = _pool[i];
                highlight.transform.position = _board.GetTilePosition(tile);
                highlight.SetActive(true);
                Text label = highlight.GetComponentInChildren<Text>();
                string note = notes != null && i < notes.Length ? notes[i] : null;
                label.text = string.IsNullOrEmpty(note) ? steps[i].ToString() : $"{steps[i]}\n<size=56>{note}</size>";
                label.color = color;

                // Brighter than white so the glow (bloom) effect picks it up.
                _colorBlock.SetColor("_BaseColor", color * 1.8f);
                foreach (Renderer frame in highlight.GetComponentsInChildren<Renderer>())
                {
                    frame.SetPropertyBlock(_colorBlock);
                }
                _activeTiles.Add(tile);
            }
        }

        public void Hide()
        {
            foreach (GameObject highlight in _pool)
            {
                highlight.SetActive(false);
            }
            _activeTiles.Clear();
        }

        private void Update()
        {
            // Pulse the active frames.
            float pulse = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 6f);
            foreach (GameObject highlight in _pool)
            {
                if (highlight.activeSelf) highlight.transform.localScale = new Vector3(pulse, 1f, pulse);
            }
        }
    }
}
