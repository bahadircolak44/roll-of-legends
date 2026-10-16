using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RollOfLegends
{
    /// <summary>
    /// Scatters pickups, traps, terrain and monsters randomly over the tiles and shows a marker (prefab)
    /// on each. Swap a prefab's contents to change how an effect looks.
    /// </summary>
    public class BoardEffects : MonoBehaviour
    {
        [Serializable]
        public class EffectSetup
        {
            public TileEffect Effect;
            [Tooltip("How many tiles get this effect at the start of a game.")]
            public int Count;
            [Tooltip("Looks for this effect; one is picked at random each time (e.g. different monsters).")]
            public GameObject[] Prefabs;
        }

        [SerializeField] private Board _board;
        [SerializeField] private EffectSetup[] _setups = new EffectSetup[0];

        private TileEffect[] _effects = new TileEffect[0];
        private GameObject[] _markers = new GameObject[0];
        private readonly HashSet<int> _protectedTiles = new HashSet<int>();

        public TileEffect GetEffect(int tile)
        {
            return tile < _effects.Length ? _effects[tile] : TileEffect.None;
        }

        public int Count(TileEffect effect)
        {
            int count = 0;
            foreach (TileEffect e in _effects)
            {
                if (e == effect) count++;
            }
            return count;
        }

        /// <summary>Clears the board and scatters a fresh set of effects. Protected tiles (the bases) stay empty.</summary>
        public void Setup(params int[] protectedTiles)
        {
            foreach (GameObject marker in _markers)
            {
                if (marker != null) Destroy(marker);
            }

            _effects = new TileEffect[_board.TileCount];
            _markers = new GameObject[_board.TileCount];
            _protectedTiles.Clear();
            _protectedTiles.UnionWith(protectedTiles);

            foreach (EffectSetup setup in _setups)
            {
                PlaceRandomly(setup.Effect, setup.Count);
            }
        }

        public void SetEffect(int tile, TileEffect effect)
        {
            if (_markers[tile] != null)
            {
                Destroy(_markers[tile]);
                _markers[tile] = null;
            }

            _effects[tile] = effect;
            GameObject prefab = PrefabFor(effect);
            if (prefab != null)
            {
                Transform tileTransform = _board.transform.GetChild(tile);
                _markers[tile] = Instantiate(prefab, tileTransform.position, prefab.transform.rotation, tileTransform);
            }
        }

        /// <summary>Removes the effect from this tile and puts the same effect on a random empty tile.</summary>
        public void MoveElsewhere(int tile, params int[] avoidTiles)
        {
            TileEffect effect = _effects[tile];
            SetEffect(tile, TileEffect.None);

            var avoid = new HashSet<int>(avoidTiles) { tile };
            List<int> free = FreeTiles(avoid);
            if (free.Count > 0)
            {
                SetEffect(free[Random.Range(0, free.Count)], effect);
            }
        }

        private void PlaceRandomly(TileEffect effect, int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                List<int> free = FreeTiles(null);
                if (free.Count == 0) return;
                SetEffect(free[Random.Range(0, free.Count)], effect);
            }
        }

        private List<int> FreeTiles(HashSet<int> avoid)
        {
            var free = new List<int>();
            for (int tile = 0; tile < _effects.Length; tile++)
            {
                bool blocked = _protectedTiles.Contains(tile) || (avoid != null && avoid.Contains(tile));
                if (_effects[tile] == TileEffect.None && !blocked) free.Add(tile);
            }
            return free;
        }

        private GameObject PrefabFor(TileEffect effect)
        {
            foreach (EffectSetup setup in _setups)
            {
                if (setup.Effect == effect && setup.Prefabs != null && setup.Prefabs.Length > 0)
                {
                    return setup.Prefabs[Random.Range(0, setup.Prefabs.Length)];
                }
            }
            return null;
        }
    }
}
