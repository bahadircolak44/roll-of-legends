using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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

        [Serializable]
        public class MonsterKind
        {
            public MonsterTier Tier;
            public GameObject Prefab;
        }

        [SerializeField] private Board _board;
        [SerializeField] private EffectSetup[] _setups = new EffectSetup[0];

        [Tooltip("Monster tiles pick one of these at random; the tier decides how hard the fight is.")]
        [SerializeField] private MonsterKind[] _monsterKinds = new MonsterKind[0];

        private TileEffect[] _effects = new TileEffect[0];
        private Monster[] _monsters = new Monster[0];
        private GameObject[] _markers = new GameObject[0];
        private readonly HashSet<int> _protectedTiles = new HashSet<int>();

        public TileEffect GetEffect(int tile)
        {
            return tile < _effects.Length ? _effects[tile] : TileEffect.None;
        }

        /// <summary>The object shown on a tile (a pickup, trap or monster model), or null.</summary>
        public GameObject GetMarker(int tile)
        {
            return tile < _markers.Length ? _markers[tile] : null;
        }

        /// <summary>The monster on a monster tile (null on any other tile).</summary>
        public Monster GetMonster(int tile)
        {
            return tile < _monsters.Length ? _monsters[tile] : null;
        }

        /// <summary>Puts a specific monster on a tile (handy for testing and debugging).</summary>
        public void SetMonster(int tile, Monster monster)
        {
            SetEffect(tile, TileEffect.None);
            _effects[tile] = TileEffect.Monster;
            _monsters[tile] = monster;
            SpawnMonster(tile, monster);
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
            _monsters = new Monster[_board.TileCount];
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
            _monsters[tile] = null;
            if (effect == TileEffect.Monster && _monsterKinds.Length > 0)
            {
                MonsterKind kind = _monsterKinds[Random.Range(0, _monsterKinds.Length)];
                _monsters[tile] = Monster.Create(kind.Tier);
                SpawnMonster(tile, _monsters[tile], kind.Prefab);
                return;
            }

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

        private void SpawnMonster(int tile, Monster monster, GameObject prefab = null)
        {
            if (prefab == null)
            {
                foreach (MonsterKind kind in _monsterKinds)
                {
                    if (kind.Tier == monster.Tier) prefab = kind.Prefab;
                }
            }
            if (prefab == null) return;

            Transform tileTransform = _board.transform.GetChild(tile);
            GameObject marker = Instantiate(prefab, tileTransform.position, prefab.transform.rotation, tileTransform);
            AddNumbersLabel(marker, monster);
            _markers[tile] = marker;
        }

        /// <summary>The monster's winning numbers floating above its head, colored by size.</summary>
        private static void AddNumbersLabel(GameObject marker, Monster monster)
        {
            float top = 1.2f;
            foreach (Renderer part in marker.GetComponentsInChildren<Renderer>())
            {
                top = Mathf.Max(top, part.bounds.max.y - marker.transform.position.y);
            }

            var label = new GameObject("Numbers", typeof(RectTransform), typeof(Canvas), typeof(FaceCamera));
            label.transform.SetParent(marker.transform, false);
            label.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)label.transform;
            rect.position = marker.transform.position + Vector3.up * (top + 0.25f);
            rect.localScale = Vector3.one * 0.005f / marker.transform.lossyScale.x;
            rect.sizeDelta = new Vector2(400f, 100f);

            var text = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
            text.transform.SetParent(label.transform, false);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 70;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = monster.NumbersText;
            text.color = TierColor(monster.Tier);
            text.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            text.GetComponent<Outline>().effectDistance = new Vector2(4f, -4f);
        }

        public static Color TierColor(MonsterTier tier)
        {
            switch (tier)
            {
                case MonsterTier.Small: return new Color(0.55f, 1f, 0.55f);
                case MonsterTier.Medium: return new Color(1f, 0.85f, 0.35f);
                default: return new Color(1f, 0.45f, 0.4f);
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
