using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RollOfLegends
{
    /// <summary>
    /// Runs the prototype: PLAY deals a hand of dice cards and puts the hero on START.
    /// Tapping a card rolls the die with that card, the hero hops forward, and a new card
    /// is drawn into the empty slot.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Scene objects")]
        [SerializeField] private Board _board;
        [SerializeField] private Transform _hero;
        [SerializeField] private DiceView _dice;

        [Header("UI")]
        [SerializeField] private Button _playButton;
        [SerializeField] private CardView[] _cardSlots;
        [SerializeField] private Text _infoText;

        [Header("Hero movement")]
        [Tooltip("How far above the tile the hero's center stands.")]
        [SerializeField] private float _heroHeight = 0.5f;

        [Tooltip("Seconds for one hop from a tile to the next.")]
        [SerializeField] private float _hopDuration = 0.3f;

        [Tooltip("How high the hero jumps on each hop.")]
        [SerializeField] private float _hopHeight = 0.6f;

        [Tooltip("Pause after the die lands, before the hero starts hopping.")]
        [SerializeField] private float _pauseAfterDice = 0.3f;

        private DiceCard[] _hand;

        public int HeroTileIndex { get; private set; }
        public int LastRoll { get; private set; }
        public DiceCard LastCard { get; private set; }
        public IReadOnlyList<DiceCard> Hand => _hand;

        /// <summary>True while the die is spinning or the hero is still hopping.</summary>
        public bool IsRolling { get; private set; }

        private void Awake()
        {
            _hand = new DiceCard[_cardSlots.Length];
            _playButton.onClick.AddListener(StartGame);

            for (int i = 0; i < _cardSlots.Length; i++)
            {
                int slot = i; // copy for the click handler below
                _cardSlots[i].Button.onClick.AddListener(() => PlayCard(slot));
            }
        }

        private void Start()
        {
            _hero.gameObject.SetActive(false);
            _dice.gameObject.SetActive(false);
            foreach (CardView card in _cardSlots)
            {
                card.Hide();
            }
            _playButton.gameObject.SetActive(true);
            _infoText.text = "Press PLAY to start";
        }

        public void StartGame()
        {
            HeroTileIndex = 0;
            _hero.position = HeroPositionOn(HeroTileIndex);
            _hero.gameObject.SetActive(true);
            _playButton.gameObject.SetActive(false);

            for (int slot = 0; slot < _hand.Length; slot++)
            {
                DrawInto(slot);
            }
            ShowInfo("-", "-");
        }

        public void PlayCard(int slot)
        {
            if (IsRolling)
            {
                return; // ignore taps until the hero has finished moving
            }
            StartCoroutine(PlayCardAndMove(slot));
        }

        private IEnumerator PlayCardAndMove(int slot)
        {
            IsRolling = true;
            SetCardsInteractable(false);

            LastCard = _hand[slot];
            LastRoll = LastCard.Roll();
            _cardSlots[slot].Hide(); // the card leaves the hand

            ShowInfo(LastCard.Describe(), "...");
            yield return _dice.Roll(LastRoll);
            ShowInfo(LastCard.Describe(), LastRoll.ToString());
            yield return new WaitForSeconds(_pauseAfterDice);

            for (int step = 0; step < LastRoll; step++)
            {
                HeroTileIndex = Board.Advance(HeroTileIndex, 1, _board.TileCount);
                yield return HopTo(HeroPositionOn(HeroTileIndex));
                ShowInfo(LastCard.Describe(), LastRoll.ToString());
            }

            DrawInto(slot); // a fresh random card fills the empty slot
            SetCardsInteractable(true);
            IsRolling = false;
        }

        private void DrawInto(int slot)
        {
            _hand[slot] = DiceCard.Draw();
            _cardSlots[slot].Show(_hand[slot]);
        }

        private void SetCardsInteractable(bool interactable)
        {
            foreach (CardView card in _cardSlots)
            {
                card.Button.interactable = interactable;
            }
        }

        private IEnumerator HopTo(Vector3 target)
        {
            Vector3 start = _hero.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / _hopDuration)
            {
                Vector3 position = Vector3.Lerp(start, target, t);
                position.y += Mathf.Sin(t * Mathf.PI) * _hopHeight; // goes up, then back down
                _hero.position = position;
                yield return null;
            }
            _hero.position = target;
        }

        private Vector3 HeroPositionOn(int tileIndex)
        {
            return _board.GetTilePosition(tileIndex) + Vector3.up * _heroHeight;
        }

        private void ShowInfo(string card, string rolled)
        {
            string position = HeroTileIndex == 0 ? "START" : $"Tile {HeroTileIndex}";
            _infoText.text = $"Card: {card}\nRolled: {rolled}\nPosition: {position}";
        }
    }
}
