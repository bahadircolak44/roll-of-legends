using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RollOfLegends
{
    /// <summary>
    /// Runs the prototype: PLAY puts the hero on the START tile, ROLL DICE spins the die
    /// and then the hero hops forward one tile at a time.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Scene objects")]
        [SerializeField] private Board _board;
        [SerializeField] private Transform _hero;
        [SerializeField] private DiceView _dice;

        [Header("UI")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _rollButton;
        [SerializeField] private Text _infoText;

        [Header("Rules")]
        [Tooltip("The number a Fixed die always rolls.")]
        [SerializeField, Range(1, 6)] private int _fixedDiceValue = 3;

        [Header("Hero movement")]
        [Tooltip("How far above the tile the hero's center stands.")]
        [SerializeField] private float _heroHeight = 0.5f;

        [Tooltip("Seconds for one hop from a tile to the next.")]
        [SerializeField] private float _hopDuration = 0.3f;

        [Tooltip("How high the hero jumps on each hop.")]
        [SerializeField] private float _hopHeight = 0.6f;

        [Tooltip("Pause after the die lands, before the hero starts hopping.")]
        [SerializeField] private float _pauseAfterDice = 0.3f;

        public int HeroTileIndex { get; private set; }
        public int LastRoll { get; private set; }
        public DiceType LastDiceType { get; private set; }

        /// <summary>True while the die is spinning or the hero is still hopping.</summary>
        public bool IsRolling { get; private set; }

        private void Awake()
        {
            _playButton.onClick.AddListener(StartGame);
            _rollButton.onClick.AddListener(RollDice);
        }

        private void Start()
        {
            _hero.gameObject.SetActive(false);
            _dice.gameObject.SetActive(false);
            _rollButton.gameObject.SetActive(false);
            _playButton.gameObject.SetActive(true);
            _infoText.text = "Press PLAY to start";
        }

        public void StartGame()
        {
            HeroTileIndex = 0;
            _hero.position = HeroPositionOn(HeroTileIndex);
            _hero.gameObject.SetActive(true);
            _playButton.gameObject.SetActive(false);
            _rollButton.gameObject.SetActive(true);
            ShowInfo("-", "-");
        }

        public void RollDice()
        {
            if (IsRolling)
            {
                return; // ignore extra taps until the hero has finished moving
            }
            StartCoroutine(RollAndMove());
        }

        private IEnumerator RollAndMove()
        {
            IsRolling = true;
            _rollButton.interactable = false;

            LastDiceType = Dice.RandomType();
            LastRoll = Dice.Roll(LastDiceType, _fixedDiceValue);
            string dice = Dice.Describe(LastDiceType, _fixedDiceValue);

            ShowInfo(dice, "...");
            yield return _dice.Roll(LastRoll);
            ShowInfo(dice, LastRoll.ToString());
            yield return new WaitForSeconds(_pauseAfterDice);

            for (int step = 0; step < LastRoll; step++)
            {
                HeroTileIndex = Board.Advance(HeroTileIndex, 1, _board.TileCount);
                yield return HopTo(HeroPositionOn(HeroTileIndex));
                ShowInfo(dice, LastRoll.ToString());
            }

            _rollButton.interactable = true;
            IsRolling = false;
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

        private void ShowInfo(string dice, string rolled)
        {
            string position = HeroTileIndex == 0 ? "START" : $"Tile {HeroTileIndex}";
            _infoText.text = $"Dice: {dice}\nRolled: {rolled}\nPosition: {position}";
        }
    }
}
