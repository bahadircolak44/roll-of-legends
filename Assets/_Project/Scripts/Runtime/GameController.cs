using UnityEngine;
using UnityEngine.UI;

namespace RollOfLegends
{
    /// <summary>
    /// Runs the prototype: PLAY puts the hero on the START tile, ROLL DICE moves it along the board.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Scene objects")]
        [SerializeField] private Board _board;
        [SerializeField] private Transform _hero;

        [Header("UI")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _rollButton;
        [SerializeField] private Text _infoText;

        [Header("Rules")]
        [Tooltip("The number a Fixed die always rolls.")]
        [SerializeField, Range(1, 6)] private int _fixedDiceValue = 3;

        [Tooltip("How far above the tile the hero's center stands.")]
        [SerializeField] private float _heroHeight = 0.5f;

        public int HeroTileIndex { get; private set; }
        public int LastRoll { get; private set; }
        public DiceType LastDiceType { get; private set; }

        private void Awake()
        {
            _playButton.onClick.AddListener(StartGame);
            _rollButton.onClick.AddListener(RollDice);
        }

        private void Start()
        {
            _hero.gameObject.SetActive(false);
            _rollButton.gameObject.SetActive(false);
            _playButton.gameObject.SetActive(true);
            _infoText.text = "Press PLAY to start";
        }

        public void StartGame()
        {
            HeroTileIndex = 0;
            PlaceHero();
            _hero.gameObject.SetActive(true);
            _playButton.gameObject.SetActive(false);
            _rollButton.gameObject.SetActive(true);
            ShowInfo("-", "-");
        }

        public void RollDice()
        {
            LastDiceType = Dice.RandomType();
            LastRoll = Dice.Roll(LastDiceType, _fixedDiceValue);
            HeroTileIndex = Board.Advance(HeroTileIndex, LastRoll, _board.TileCount);
            PlaceHero();
            ShowInfo(Dice.Describe(LastDiceType, _fixedDiceValue), LastRoll.ToString());
        }

        private void PlaceHero()
        {
            _hero.position = _board.GetTilePosition(HeroTileIndex) + Vector3.up * _heroHeight;
        }

        private void ShowInfo(string dice, string rolled)
        {
            string position = HeroTileIndex == 0 ? "START" : $"Tile {HeroTileIndex}";
            _infoText.text = $"Dice: {dice}\nRolled: {rolled}\nPosition: {position}";
        }
    }
}
